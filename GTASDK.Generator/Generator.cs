using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.Server;
using SharpYaml.Serialization;

namespace GTASDK.Generator
{
    public class Generator
    {
        private readonly string _rootDirectory;

        private readonly IDictionary<string, TypeGraph> _typeGraphCache = new Dictionary<string, TypeGraph>();
        private readonly TypeCache _typeCache;
        private readonly Parsing _parsing;

        public Generator(string rootDirectory)
        {
            _rootDirectory = rootDirectory;
            _typeCache = new TypeCache(this);
            _parsing = new Parsing(_typeCache);
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

        public TypeGraph GetTypeGraph(string typeName, string input)
        {
            Debug.WriteLine($"Processing type {typeName} in module {Path.GetFileName(_rootDirectory)}");
            var serializer = new Serializer();
            var structure = serializer.Deserialize<IDictionary<string, object>>(input);

            var typeNamespace = (string) structure["namespace"];
            var fieldDefinitions = (List<object>) structure["fields"];

            var presetSize = structure["size"] as int?;

            var fields = new List<Field>();

            uint offset = 0;
            foreach (var entry in fieldDefinitions)
            {
                Field entryField;

                switch (entry)
                {
                    case string str:
                        entryField = _parsing.ParseStringDescriptor(str);
                        break;
                    case List<object> list:
                        entryField = _parsing.ParseRegularField(list);
                        break;
                    case Dictionary<object, object> dict:
                        entryField = _parsing.ParseComplexField(dict);
                        break;
                    default:
                        throw new ArgumentException($"Unrecognized entry type {entry}");
                }

                fields.Add(entryField);
                offset += entryField.Size;
            }

            var size = offset;
            if (size != presetSize)
            {
                Debug.WriteLine($"Size of {typeName} is 0x{size:X}, expected 0x{presetSize:X}");
            }

            return new TypeGraph(typeNamespace, typeName, size, fields);
        }
    }

    public class GetSetTemplate
    {
        public Template Get => parameters => string.Format(_get, parameters);
        public Template Set => parameters => string.Format(_set, parameters);

        private readonly string _get;
        private readonly string _set;

        public GetSetTemplate(string get, string set)
        {
            _get = get;
            _set = set;
        }
    }

    public delegate string Template(params object[] parameters);

    public class TypeGraph
    {
        public string Namespace { get; }
        public string Name { get; }
        public uint Size { get; }
        public IList<Field> Fields { get; }

        public TypeGraph(string typeNamespace, string name, uint size, IList<Field> fields)
        {
            Namespace = typeNamespace;
            Name = name;
            Size = size;
            Fields = fields;
        }

        public string GraphToString()
        {
            return $@"using System;

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

        private string FieldsToString(int indentation, int indentLevel = 0)
        {
            var fieldsEmitted = new List<string>();
            uint offset = 0;

            foreach (var field in Fields)
            {
                fieldsEmitted.Add(field.Emit(offset));
                offset += field.Size;
            }

            var output = new StringBuilder();
            foreach (var s in fieldsEmitted)
            {
                var lines = s.Split('\n').Select(e => e.Trim()).Where(e => !string.IsNullOrEmpty(e));
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