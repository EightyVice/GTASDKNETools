using System;
using System.Collections.Generic;
using System.IO;

namespace GTASDK.Generator
{
    public static class Types
    {
        public static readonly IReadOnlyDictionary<string, BuiltinType> Builtin = new Dictionary<string, BuiltinType>
        {
            ["<pointer>"] = new BuiltinType
            {
                Size = 0x4,
                Template = new GetSetTemplate("(IntPtr)({0})", "Memory.WriteInt32({0}, (int)value)")
            },
            // Size 1
            ["bool"] = new BuiltinType
            {
                TypeMapsTo = "byte",
                Size = sizeof(byte),
                Template = new GetSetTemplate("Memory.ReadByte({0})", "Memory.WriteByte({0}, value)")
            },
            ["char"] = new BuiltinType
            {
                TypeMapsTo = "byte",
                Size = sizeof(sbyte),
                Template = new GetSetTemplate("Memory.ReadByte({0})", "Memory.WriteByte({0}, value)")
            },
            ["char8_t"] = new BuiltinType
            {
                TypeMapsTo = "byte",
                Size = sizeof(byte),
                Template = new GetSetTemplate("Memory.ReadByte({0})", "Memory.WriteByte({0}, value)")
            },
            ["signed char"] = new BuiltinType
            {
                TypeMapsTo = "sbyte",
                Size = sizeof(byte),
                Template = new GetSetTemplate("Memory.ReadByte({0})", "Memory.WriteByte({0}, value)")
            },
            ["unsigned char"] = new BuiltinType
            {
                TypeMapsTo = "byte",
                Size = sizeof(byte),
                Template = new GetSetTemplate("Memory.ReadByte({0})", "Memory.WriteByte({0}, value)"),
                BitsTemplate = new GetSetTemplate(@"Memory.ReadBitsInt8({0}, {1}, {2})", @"throw new InvalidOperationException(""NOT DONE YET"")")
            },
            ["__int8"] = new BuiltinType
            {
                TypeMapsTo = "byte",
                Size = sizeof(byte),
                Template = new GetSetTemplate("Memory.ReadByte({0})", "Memory.WriteByte({0}, value)")
            },
            // Size 2
            ["short"] = new BuiltinType
            {
                Size = sizeof(short),
                Template = new GetSetTemplate("Memory.ReadInt16({0})", "Memory.WriteInt16({0}, value)")
            },
            ["CPlaceable"] = new BuiltinType
            {
                Size = 0x48,
                Template = new GetSetTemplate("new CPlaceable({0})", @"throw new InvalidOperationException(""NOT DONE YET"")")
            }
        };

        public static BuiltinType Pointer => Builtin["<pointer>"];
    }

    public abstract class ParserType
    {
        public string TypeMapsTo { get; set; } = null;
        public virtual uint Size { get; set; }
        public GetSetTemplate Template { get; set; }
        public GetSetTemplate BitsTemplate { get; set; }
    }

    public class BuiltinType : ParserType
    {
    }

    public class CustomType : ParserType
    {
        private readonly TypeGraph _typeGraph;
        public override uint Size => _typeGraph.Size;

        public CustomType(TypeGraph typeGraph)
        {
            _typeGraph = typeGraph;
            Template = new GetSetTemplate($"new ${_typeGraph.Name}({{0}})", @"throw new InvalidOperationException(""NOT DONE YET"")");
        }
    }

    public class TypeCache
    {
        private readonly Generator _generator;
        private readonly IDictionary<string, CustomType> _ownTypeCache = new Dictionary<string, CustomType>();

        public TypeCache(Generator generator)
        {
            _generator = generator;
        }

        public bool TryGetValue(string typeName, out ParserType outType)
        {
            if (Types.Builtin.TryGetValue(typeName, out var builtinType))
            {
                outType = builtinType;
                return true;
            }

            if (_ownTypeCache.TryGetValue(typeName, out var ownCachedType))
            {
                outType = ownCachedType;
                return true;
            }

            outType = default;
            return false;
        }

        public ParserType this[string typeName]
        {
            get
            {
                if (TryGetValue(typeName, out var existingType))
                {
                    return existingType;
                }

                try
                {
                    return _ownTypeCache[typeName] = new CustomType(_generator.GetCachedTypeGraph(typeName));
                }
                catch (FileNotFoundException ex)
                {
                    throw new ArgumentException("Did not find any matching type: " + typeName, nameof(typeName), ex);
                }
            }
        }
    }
}