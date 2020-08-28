using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using SharpYaml.Model;
using SharpYaml.Serialization;
using Path = System.IO.Path;
using YamlNode = SharpYaml.Model.YamlNode;

namespace GTASDK.Generator
{
    public class Generator
    {
        private readonly string _rootDirectory;

        private readonly IDictionary<string, TypeGraph> _typeGraphCache = new Dictionary<string, TypeGraph>();
        private readonly TypeCache _typeCache;
        private readonly StaticFieldParsing _staticFieldParsing;
        private readonly StaticMethodParsing _staticMethodParsing;
        private readonly InstanceFieldParsing _instanceFieldParsing;
        private readonly InstanceMethodParsing _instanceMethodParsing;

        internal static SerializerSettings SerializerSettings { get; } = new SerializerSettings();
        internal static Serializer Serializer { get; }

        static Generator()
        {
            SerializerSettings.RegisterSerializerFactory(new ValueTupleSerializer());
            //SerializerSettings.RegisterSerializerFactory(new NullableSerializer());
            SerializerSettings.RegisterSerializerFactory(new YamlNodeSerializer());
            SerializerSettings.RegisterSerializerFactory(new PrimitiveSerializer());
            Serializer = new Serializer(SerializerSettings);
        }

        public Generator(string rootDirectory)
        {
            _rootDirectory = rootDirectory;
            _typeCache = new TypeCache(this);
            _instanceFieldParsing = new InstanceFieldParsing(_typeCache);
            _staticFieldParsing = new StaticFieldParsing(_typeCache);
            _instanceMethodParsing = new InstanceMethodParsing(_typeCache);
            _staticMethodParsing = new StaticMethodParsing(_typeCache);
        }

        public TypeGraph GetCachedTypeGraph(string name)
        {
            if (_typeGraphCache.TryGetValue(name, out var cachedTypeGraph))
            {
                return cachedTypeGraph;
            }

            var targetFile = Path.Combine(_rootDirectory, name + ".yml");
            if (!File.Exists(targetFile))
            {
                throw new FileNotFoundException("Did not find .yml template for type graph", targetFile);
            }

            var input = File.ReadAllText(targetFile);
            return _typeGraphCache[name] = GetTypeGraph(name, input);
        }

        public class TypeGraphModel
        {
            [YamlMember("namespace")]
            public string Namespace { get; set; }

            [YamlMember("size")]
            [DefaultValue(null)]
            public int PresetSize { get; set; }

            [YamlMember("enum_type")]
            [DefaultValue(null)]
            public string EnumType { get; set; }

            [YamlMember("enum")]
            [DefaultValue(null)]
            public List<(string name, long value)> EnumDefinitions { get; set; }

            [YamlMember("static")]
            [DefaultValue(null)]
            public List<(string type, string name, uint address)> StaticFieldDefinitions { get; set; }

            [YamlMember("static_methods")]
            [DefaultValue(null)]
            public List<(string returnType, string name, string[] arguments, uint offset)> StaticMethodDefinitions { get; set; }

            [YamlMember("fields")]
            [DefaultValue(null)]
            public List<YamlNode> InstanceFieldDefinitions { get; set; }

            [YamlMember("methods")]
            [DefaultValue(null)]
            public List<YamlSequence> InstanceMethodDefinitions { get; set; }

        }

        public TypeGraph GetTypeGraph(string typeName, string input)
        {
            Debug.WriteLine($"Processing type {typeName} in module {Path.GetFileName(_rootDirectory)}");
            var structure = Serializer.Deserialize<TypeGraphModel>(input);

            if (structure.EnumType != null)
            {
                return new EnumTypeGraph(structure.Namespace, typeName, new CompositeType(_typeCache, structure.EnumType), structure.EnumDefinitions);
            }

            var staticFields = new List<StaticField>();
            if (structure.StaticFieldDefinitions != null)
            {
                foreach (var entry in structure.StaticFieldDefinitions)
                {
                    staticFields.Add(_staticFieldParsing.ParseDefinition(entry));
                }
            }

            var staticMethods = new List<StaticMethod>();
            if (structure.StaticMethodDefinitions != null)
            {
                foreach (var entry in structure.StaticMethodDefinitions)
                {
                    staticMethods.Add(_staticMethodParsing.ParseMethod(typeName, entry));
                }
            }

            var instanceFields = new List<Field>();
            uint offset = 0;
            if (structure.InstanceFieldDefinitions != null)
            {
                foreach (var entry in structure.InstanceFieldDefinitions)
                {
                    Field entryField;

                    switch (entry)
                    {
                        case YamlValue val:
                            entryField = _instanceFieldParsing.ParseStringDescriptor(val.Value);
                            break;
                        case YamlSequence seq:
                            entryField = _instanceFieldParsing.ParseRegularField(seq);
                            break;
                        case YamlMapping mapping:
                            entryField = _instanceFieldParsing.ParseComplexField(mapping.ToObjectX<Dictionary<ComplexFieldType, YamlSequence>>());
                            break;
                        default:
                            throw new ArgumentException($"Unrecognized entry type {entry}");
                    }

                    instanceFields.Add(entryField);
                    offset += entryField.Size;
                }
            }

            var instanceMethods = new List<Method>();
            if (structure.InstanceMethodDefinitions != null)
            {
                foreach (var entry in structure.InstanceMethodDefinitions)
                {
                    instanceMethods.Add(_instanceMethodParsing.ParseMethod(typeName, entry));
                }
            }

            var size = offset;
            if (size != structure.PresetSize)
            {
                Debug.WriteLine($"Size of {typeName} is 0x{size:X}, expected 0x{structure.PresetSize:X}");
            }

            return new TypeGraph(structure.Namespace, typeName, size, staticFields, staticMethods, instanceFields, instanceMethods);
        }
    }

    public sealed class GetSetTemplate
    {
        public delegate string Template(params object[] parameters);
        public Template Get => parameters => string.Format(_get, parameters);
        public Template Set => parameters => string.Format(_set, parameters);

        public string AsGet
        {
            set => _get = value;
        }

        public string AsSet
        {
            set => _set = value;
        }

        private string _get;
        private string _set;
    }

    public sealed class CallTemplate
    {
        public delegate string Template(params object[] parameters);
        public Template Argument => parameters => string.Format(_arg, parameters);
        public Template Call => parameters => string.Format(_call, parameters);

        public string AsArgument
        {
            set => _arg = value;
        }

        public string AsCall
        {
            set => _call = value;
        }

        private string _arg;
        private string _call;
    }

    public class TypeGraph
    {
        public string Namespace { get; }
        public string Name { get; }
        public uint Size { get; }
        public IList<StaticField> StaticFields { get; }
        public IList<StaticMethod> StaticMethods { get; }
        public IList<Field> InstanceFields { get; }
        public IList<Method> InstanceMethods { get; }

        public TypeGraph(string typeNamespace, string name, uint size, IList<StaticField> staticFields, IList<StaticMethod> staticMethods, IList<Field> instanceFields, IList<Method> instanceMethods)
        {
            Namespace = typeNamespace;
            Name = name;
            Size = size;
            StaticFields = staticFields;
            StaticMethods = staticMethods;
            InstanceFields = instanceFields;
            InstanceMethods = instanceMethods;
        }

        public virtual IReadOnlyDictionary<string, string> GraphToString()
        {
            return new Dictionary<string, string>
            {
                [$"{Name}.Methods.cs"] = MethodsToString(),
                [$"{Name}.Fields.cs"] = FieldsToString(),
            };
        }

        private string MethodsToString()
        {
            return $@"using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using EasyHook;

namespace {Namespace}
{{
    public partial class {Name}
    {{
        public static class Hook
        {{
{HooksToString(4, 3)}
        }}

{MethodsToString(4, 2)}
    }}
}}";
        }

        private string FieldsToString()
        {
            return $@"using System;
using System.Runtime.CompilerServices;

namespace {Namespace}
{{
    public partial class {Name}
    {{
        /// <summary>Size of this type in native code, in bytes.</summary>
        public const uint _Size = 0x{Size:X}U;

{FieldsToString(4, 2)}
    }}
}}";
        }

        private IEnumerable<string> InstanceFieldStrings()
        {
            var fieldsEmitted = new List<string>();
            uint offset = 0;

            foreach (var field in InstanceFields)
            {
                fieldsEmitted.Add(field.Emit(offset));
                offset += field.Size;
            }

            return fieldsEmitted;
        }

        private IEnumerable<string> StaticFieldStrings()
        {
            return StaticFields.Select(staticMember => staticMember.Emit());
        }

        private string FieldsToString(int indentation, int indentLevel = 0)
        {
            return RedoIndentation(indentation, indentLevel, InstanceFieldStrings().Concat(StaticFieldStrings()));
        }

        private string HooksToString(int indentation, int indentLevel = 0)
        {
            var methodsEmitted = InstanceMethods.Select(method => method?.EmitHook() ?? "");

            return RedoIndentation(indentation, indentLevel, methodsEmitted);
        }

        private string MethodsToString(int indentation, int indentLevel = 0)
        {
            var methodsEmitted = InstanceMethods.Select(method => method?.Emit() ?? "");

            return RedoIndentation(indentation, indentLevel, methodsEmitted);
        }

        protected static string RedoIndentation(int indentation, int indentLevel, IEnumerable<string> stringComponents)
        {
            var output = new StringBuilder();
            foreach (var str in stringComponents)
            {
                if (string.IsNullOrWhiteSpace(str))
                {
                    continue;
                }

                var lines = str.Split('\n').Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e));
                foreach (var line in lines)
                {
                    if (line.EndsWith("}"))
                    {
                        indentLevel--;
                    }

                    output.Append(new string(' ', indentation * indentLevel)).AppendLine(line);

                    if (line.EndsWith("{"))
                    {
                        indentLevel++;
                    }
                }

                output.AppendLine();
            }

            return output.ToString().TrimEnd();
        }
    }

    public class EnumTypeGraph : TypeGraph
    {
        public CompositeType EnumType { get; }
        public IEnumerable<(string name, long value)> EnumElements { get; }

        public EnumTypeGraph(string typeNamespace, string name, CompositeType enumType, IEnumerable<(string name, long value)> enumElements)
            : base(typeNamespace, name, enumType.BackingType.Size, Array.Empty<StaticField>(), Array.Empty<StaticMethod>(), Array.Empty<Field>(), Array.Empty<Method>())
        {
            EnumType = enumType;
            EnumElements = enumElements;
        }

        public override IReadOnlyDictionary<string, string> GraphToString()
        {
            return new Dictionary<string, string>
            {
                [$"{Name}.cs"] = $@"namespace {Namespace}
{{
    public enum {Name} : {EnumType.CsharpName}
    {{
{EnumMembersToString(4, 2)}
    }}
}}",
            };
        }

        private string EnumMembersToString(int indentation, int indentLevel)
        {
            var enumMembersEmitted = EnumElements.Select(el => $"{el.name} = {el.value}");

            return RedoIndentation(indentation, indentLevel, enumMembersEmitted);
        }
    }
}