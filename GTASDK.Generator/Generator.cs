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
            public List<object> InstanceMethodDefinitions { get; set; }
        }

        public TypeGraph GetTypeGraph(string typeName, string input)
        {
            Debug.WriteLine($"Processing type {typeName} in module {Path.GetFileName(_rootDirectory)}");
            var structure = Serializer.Deserialize<TypeGraphModel>(input);

            var statics = new List<StaticMember>();
            if (structure.StaticDefinitions != null)
            {
                foreach (var entry in structure.StaticDefinitions)
                {
                    statics.Add(_staticParsing.ParseDefinition(entry));
                }
            }

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

            var size = offset;
            if (size != structure.PresetSize)
            {
                Debug.WriteLine($"Size of {typeName} is 0x{size:X}, expected 0x{structure.PresetSize:X}");
            }

            return new TypeGraph(structure.Namespace, typeName, size, statics, fields);
        }
    }

    public class GetSetTemplate
    {
        public Template Get => parameters => string.Format(_get, parameters);
        public Template Set => parameters => string.Format(_set, parameters);

        public string Getter
        {
            set => _get = value;
        }

        public string Setter
        {
            set => _set = value;
        }

        private string _get;
        private string _set;
    }

    public delegate string Template(params object[] parameters);

    public class TypeGraph
    {
        public string Namespace { get; }
        public string Name { get; }
        public uint Size { get; }
        public IList<StaticMember> Statics { get; }
        public IList<Field> Fields { get; }

        public TypeGraph(string typeNamespace, string name, uint size, IList<StaticMember> statics, IList<Field> fields)
        {
            Namespace = typeNamespace;
            Name = name;
            Size = size;
            Statics = statics;
            Fields = fields;
        }

        public string GraphToString()
        {
            return $@"using System;
using System.Runtime.CompilerServices;
using EasyHook;

namespace {Namespace}
{{
    public partial class {Name}
    {{
        /// <summary>Size of this type in native code, in bytes.</summary>
        public const uint _Size = 0x{Size:X}U;

{StaticsToString(4, 2)}

{FieldsToString(4, 2)}
    }}
}}";
        }

        private string StaticsToString(int indentation, int indentLevel = 0)
        {
            var staticsEmitted = Statics.Select(staticMember => staticMember.Emit());

            return RedoIndentation(indentation, indentLevel, staticsEmitted);
        }

        private string FieldsToString(int indentation, int indentLevel = 0)
        {
            var fieldsEmitted = new List<string>();
            uint offset = 0;

            foreach (var field in Fields)
            {
                fieldsEmitted.Add(field.Emit(offset));
                offset += field.Size;
            }

            return RedoIndentation(indentation, indentLevel, fieldsEmitted);
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