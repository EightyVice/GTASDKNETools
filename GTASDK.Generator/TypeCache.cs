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
                Template = new GetSetTemplate("(IntPtr)({0})", "Memory.WriteUInt32({0}, (int)value)")
            },
            // Size 1
            ["<byte>"] = new BuiltinType
            {
                TypeMapsTo = "byte",
                Size = sizeof(byte),
                Template = new GetSetTemplate("Memory.ReadByte({0})", "Memory.WriteByte({0}, value)"),
                BitsTemplate = new GetSetTemplate(@"Memory.ReadBitsInt8({0}, {1}, {2})", @"throw new InvalidOperationException(""NOT DONE YET"")")
            },
            ["<sbyte>"] = new BuiltinType
            {
                TypeMapsTo = "sbyte",
                Size = sizeof(sbyte),
                Template = new GetSetTemplate("Memory.ReadSByte({0})", "Memory.WriteSByte({0}, value)")
            },

            ["bool"] = Builtin["<byte>"],
            ["char"] = Builtin["<byte>"],
            ["char8_t"] = Builtin["<byte>"],
            ["__int8"] = Builtin["<byte>"],
            ["unsigned char"] = Builtin["<byte>"],
            ["signed char"] = Builtin["<sbyte>"],

            // Size 2
            ["<short>"] = new BuiltinType
            {
                TypeMapsTo = "short",
                Size = sizeof(short),
                Template = new GetSetTemplate("Memory.ReadInt16({0})", "Memory.WriteInt16({0}, value)")
            },
            ["<ushort>"] = new BuiltinType
            {
                TypeMapsTo = "ushort",
                Size = sizeof(ushort),
                Template = new GetSetTemplate("Memory.ReadUInt16({0})", "Memory.WriteUInt16({0}, value)")
            },

            ["short"] = Builtin["<short>"],
            ["__int16"] = Builtin["<short>"],
            ["short int"] = Builtin["<short>"],
            ["unsigned short"] = Builtin["<ushort>"],
            ["unsigned __int16"] = Builtin["<ushort>"],
            ["unsigned short int"] = Builtin["<ushort>"],

            // Size 4
            ["<int>"] = new BuiltinType
            {
                TypeMapsTo = "int",
                Size = sizeof(int),
                Template = new GetSetTemplate("Memory.ReadInt32({0})", "Memory.WriteInt32({0}, value)")
            },
            ["<uint>"] = new BuiltinType
            {
                TypeMapsTo = "uint",
                Size = sizeof(uint),
                Template = new GetSetTemplate("Memory.ReadUInt32({0})", "Memory.WriteUInt32({0}, value)")
            },

            ["__int32"] = Builtin["<int>"],
            ["signed"] = Builtin["<int>"],
            ["signed int"] = Builtin["<int>"],
            ["int"] = Builtin["<int>"],
            ["long"] = Builtin["<int>"],
            ["long int"] = Builtin["<int>"],
            ["signed long int"] = Builtin["<int>"],
            ["unsigned __int32"] = Builtin["<uint>"],
            ["unsigned"] = Builtin["<uint>"],
            ["unsigned int"] = Builtin["<uint>"],
            ["unsigned long"] = Builtin["<uint>"],
            ["unsigned long int"] = Builtin["<uint>"],

            // Size 8
            ["<long>"] = new BuiltinType
            {
                TypeMapsTo = "long",
                Size = sizeof(long),
                Template = new GetSetTemplate("Memory.ReadInt64({0})", "Memory.WriteInt64({0}, value)")
            },
            ["<ulong>"] = new BuiltinType
            {
                TypeMapsTo = "ulong",
                Size = sizeof(ulong),
                Template = new GetSetTemplate("Memory.ReadUInt64({0})", "Memory.WriteUInt64({0}, value)")
            },

            ["__int64"] = Builtin["<long>"],
            ["long long"] = Builtin["<long>"],
            ["signed long long"] = Builtin["<long>"],
            ["unsigned __int64"] = Builtin["<ulong>"],
            ["unsigned long long"] = Builtin["<ulong>"],

            // Floating-point
            ["float"] = new BuiltinType
            {
                TypeMapsTo = "float",
                Size = sizeof(float),
                Template = new GetSetTemplate("Memory.ReadFloat({0})", "Memory.WriteFloat({0}, value)")
            },
            ["double"] = new BuiltinType
            {
                TypeMapsTo = "double",
                Size = sizeof(double),
                Template = new GetSetTemplate("Memory.ReadDouble({0})", "Memory.WriteDouble({0}, value)")
            },
            ["long double"] = Builtin["double"],

            // Special/placeholder
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