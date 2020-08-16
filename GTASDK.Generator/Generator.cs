using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.Server;
using SharpYaml;
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
        private readonly FieldParsing _fieldParsing;
        private readonly StaticParsing _staticParsing;
        private readonly MethodParsing _methodParsing;

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
            _fieldParsing = new FieldParsing(_typeCache);
            _staticParsing = new StaticParsing(_typeCache);
            _methodParsing = new MethodParsing(_typeCache);
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

            [YamlMember("fields")]
            [DefaultValue(null)]
            public List<YamlNode> FieldDefinitions { get; set; }

            [YamlMember("static")]
            [DefaultValue(null)]
            public List<(string type, string name, uint address)> StaticDefinitions { get; set; }

            [YamlMember("methods")]
            [DefaultValue(null)]
            public List<YamlSequence> InstanceMethodDefinitions { get; set; }
        }

        public TypeGraph GetTypeGraph(string typeName, string input)
        {
            Debug.WriteLine($"Processing type {typeName} in module {Path.GetFileName(_rootDirectory)}");
            var structure = Serializer.Deserialize<TypeGraphModel>(input);

            var fields = new List<Field>();
            uint offset = 0;
            if (structure.FieldDefinitions != null)
            {
                foreach (var entry in structure.FieldDefinitions)
                {
                    Field entryField;

                    switch (entry)
                    {
                        case YamlValue val:
                            entryField = _fieldParsing.ParseStringDescriptor(val.Value);
                            break;
                        case YamlSequence seq:
                            entryField = _fieldParsing.ParseRegularField(seq);
                            break;
                        case YamlMapping mapping:
                            entryField = _fieldParsing.ParseComplexField(mapping.ToObjectX<Dictionary<ComplexFieldType, YamlSequence>>());
                            break;
                        default:
                            throw new ArgumentException($"Unrecognized entry type {entry}");
                    }

                    fields.Add(entryField);
                    offset += entryField.Size;
                }
            }

            var statics = new List<StaticMember>();
            if (structure.StaticDefinitions != null)
            {
                foreach (var entry in structure.StaticDefinitions)
                {
                    statics.Add(_staticParsing.ParseDefinition(entry));
                }
            }

            var methods = new List<Method>();
            if (structure.InstanceMethodDefinitions != null)
            {
                foreach (var entry in structure.InstanceMethodDefinitions)
                {
                    methods.Add(_methodParsing.ParseMethod(typeName, entry));
                }
            }

            var size = offset;
            if (size != structure.PresetSize)
            {
                Debug.WriteLine($"Size of {typeName} is 0x{size:X}, expected 0x{structure.PresetSize:X}");
            }

            return new TypeGraph(structure.Namespace, typeName, size, statics, fields, methods);
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

    public sealed class TypeGraph
    {
        public string Namespace { get; }
        public string Name { get; }
        public uint Size { get; }
        public IList<StaticMember> Statics { get; }
        public IList<Field> Fields { get; }
        public IList<Method> Methods { get; }

        public TypeGraph(string typeNamespace, string name, uint size, IList<StaticMember> statics, IList<Field> fields, IList<Method> methods)
        {
            Namespace = typeNamespace;
            Name = name;
            Size = size;
            Statics = statics;
            Fields = fields;
            Methods = methods;
        }

        public IReadOnlyDictionary<string, string> GraphToString()
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
{HooksToString(4, 2)}
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

            foreach (var field in Fields)
            {
                fieldsEmitted.Add(field.Emit(offset));
                offset += field.Size;
            }

            return fieldsEmitted;
        }

        private IEnumerable<string> StaticFieldStrings()
        {
            return Statics.Select(staticMember => staticMember.Emit());
        }

        private string FieldsToString(int indentation, int indentLevel = 0)
        {
            return RedoIndentation(indentation, indentLevel, InstanceFieldStrings().Concat(StaticFieldStrings()));
        }

        private string HooksToString(int indentation, int indentLevel = 0)
        {
            var methodsEmitted = Methods.Select(method => method?.EmitHook() ?? "");

            return RedoIndentation(indentation, indentLevel, methodsEmitted);
        }

        private string MethodsToString(int indentation, int indentLevel = 0)
        {
            var methodsEmitted = Methods.Select(method => method?.Emit() ?? "");

            return RedoIndentation(indentation, indentLevel, methodsEmitted);
        }

        private static string RedoIndentation(int indentation, int indentLevel, IEnumerable<string> stringComponents)
        {
            var output = new StringBuilder();
            foreach (var lines in stringComponents.Select(s => s.Split('\n').Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e))))
            {
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
}