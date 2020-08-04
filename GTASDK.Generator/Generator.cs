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
        private readonly FieldParsing _fieldParsing;
        private readonly StaticParsing _staticParsing;

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

        public TypeGraph GetTypeGraph(string typeName, string input)
        {
            Debug.WriteLine($"Processing type {typeName} in module {Path.GetFileName(_rootDirectory)}");
            var serializer = new Serializer();
            var structure = serializer.Deserialize<IDictionary<string, object>>(input);

            var typeNamespace = (string) structure["namespace"];
            var fieldDefinitions = structure.TryGetValue("fields", out var outFields) ? (List<object>) outFields : null;
            var staticDefinitions = structure.TryGetValue("static", out var outStaticDefinitions) ? (List<object>) outStaticDefinitions : null;

            var presetSize = structure.TryGetValue("size", out var outPresetSize) ? (int?) outPresetSize : null;

            var statics = new List<StaticMember>();
            if (staticDefinitions != null)
            {
                foreach (var entry in staticDefinitions)
                {
                    switch (entry)
                    {
                        case List<object> list:
                            statics.Add(_staticParsing.ParseDefinition(list));
                            break;
                        default:
                            throw new ArgumentException($"Unrecognized entry type {entry}");
                    }
                }
            }

            var fields = new List<Field>();
            uint offset = 0;
            if (fieldDefinitions != null)
            {
                foreach (var entry in fieldDefinitions)
                {
                    Field entryField;

                    switch (entry)
                    {
                        case string str:
                            entryField = _fieldParsing.ParseStringDescriptor(str);
                            break;
                        case List<object> list:
                            entryField = _fieldParsing.ParseRegularField(list);
                            break;
                        case Dictionary<object, object> dict:
                            entryField = _fieldParsing.ParseComplexField(dict);
                            break;
                        default:
                            throw new ArgumentException($"Unrecognized entry type {entry}");
                    }

                    fields.Add(entryField);
                    offset += entryField.Size;
                }
            }

            var size = offset;
            if (size != presetSize)
            {
                Debug.WriteLine($"Size of {typeName} is 0x{size:X}, expected 0x{presetSize:X}");
            }

            return new TypeGraph(typeNamespace, typeName, size, statics, fields);
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