using System;
using System.Collections.Generic;
using System.IO;

namespace GTASDK.Generator
{
    public static class Types
    {
        private static class PresetTypes
        {
            public static readonly BuiltinType Byte = new BuiltinType
            {
                TypeMapsTo = "byte",
                Size = sizeof(byte),
                Template = new GetSetTemplate("Memory.ReadByte({0})", "Memory.WriteByte({0}, value)"),
                BitsTemplate = new GetSetTemplate(@"Memory.ReadBitsInt8({0}, {1}, {2})", @"throw new InvalidOperationException(""NOT DONE YET"")")
            };

            public static readonly BuiltinType SByte = new BuiltinType
            {
                TypeMapsTo = "sbyte",
                Size = sizeof(sbyte),
                Template = new GetSetTemplate("Memory.ReadSByte({0})", "Memory.WriteSByte({0}, value)")
            };

            public static readonly BuiltinType Short = new BuiltinType
            {
                TypeMapsTo = "short",
                Size = sizeof(short),
                Template = new GetSetTemplate("Memory.ReadInt16({0})", "Memory.WriteInt16({0}, value)")
            };

            public static readonly BuiltinType UShort = new BuiltinType
            {
                TypeMapsTo = "ushort",
                Size = sizeof(ushort),
                Template = new GetSetTemplate("Memory.ReadUInt16({0})", "Memory.WriteUInt16({0}, value)")
            };

            public static readonly BuiltinType Int = new BuiltinType
            {
                TypeMapsTo = "int",
                Size = sizeof(int),
                Template = new GetSetTemplate("Memory.ReadInt32({0})", "Memory.WriteInt32({0}, value)")
            };
            
            public static readonly BuiltinType UInt = new BuiltinType
            {
                TypeMapsTo = "uint",
                Size = sizeof(uint),
                Template = new GetSetTemplate("Memory.ReadUInt32({0})", "Memory.WriteUInt32({0}, value)")
            };

            public static readonly BuiltinType Long = new BuiltinType
            {
                TypeMapsTo = "long",
                Size = sizeof(long),
                Template = new GetSetTemplate("Memory.ReadInt64({0})", "Memory.WriteInt64({0}, value)")
            };

            public static readonly BuiltinType ULong = new BuiltinType
            {
                TypeMapsTo = "ulong",
                Size = sizeof(ulong),
                Template = new GetSetTemplate("Memory.ReadUInt64({0})", "Memory.WriteUInt64({0}, value)")
            };

            public static readonly BuiltinType Float = new BuiltinType
            {
                TypeMapsTo = "float",
                Size = sizeof(float),
                Template = new GetSetTemplate("Memory.ReadFloat({0})", "Memory.WriteFloat({0}, value)")
            };

            public static readonly BuiltinType Double = new BuiltinType
            {
                TypeMapsTo = "double",
                Size = sizeof(double),
                Template = new GetSetTemplate("Memory.ReadDouble({0})", "Memory.WriteDouble({0}, value)")
            };
        }

        public static BuiltinType Pointer { get; } = new BuiltinType
        {
            Size = 0x4,
            Template = new GetSetTemplate("(IntPtr)({0})", "Memory.WriteUInt32({0}, (uint)value)")
        };

        public static readonly IReadOnlyDictionary<string, BuiltinType> Builtin = new Dictionary<string, BuiltinType>
        {
            // Size 1
            ["<byte>"] = PresetTypes.Byte,
            ["<sbyte>"] = PresetTypes.SByte,

            ["bool"] = PresetTypes.Byte,
            ["char"] = PresetTypes.Byte,
            ["char8_t"] = PresetTypes.Byte,
            ["__int8"] = PresetTypes.Byte,
            ["unsigned char"] = PresetTypes.Byte,
            ["signed char"] = PresetTypes.SByte,

            // Size 2
            ["<short>"] = PresetTypes.Short,
            ["<ushort>"] = PresetTypes.UShort,

            ["short"] = PresetTypes.Short,
            ["__int16"] = PresetTypes.Short,
            ["short int"] = PresetTypes.Short,
            ["unsigned short"] = PresetTypes.UShort,
            ["unsigned __int16"] = PresetTypes.UShort,
            ["unsigned short int"] = PresetTypes.UShort,

            // Size 4
            ["<int>"] = PresetTypes.Int,
            ["<uint>"] = PresetTypes.UInt,

            ["__int32"] = PresetTypes.Int,
            ["signed"] = PresetTypes.Int,
            ["signed int"] = PresetTypes.Int,
            ["int"] = PresetTypes.Int,
            ["long"] = PresetTypes.Int,
            ["long int"] = PresetTypes.Int,
            ["signed long int"] = PresetTypes.Int,
            ["unsigned __int32"] = PresetTypes.UInt,
            ["unsigned"] = PresetTypes.UInt,
            ["unsigned int"] = PresetTypes.UInt,
            ["unsigned long"] = PresetTypes.UInt,
            ["unsigned long int"] = PresetTypes.UInt,

            // Size 8
            ["<long>"] = PresetTypes.Long,
            ["<ulong>"] = PresetTypes.ULong,

            ["__int64"] = PresetTypes.Long,
            ["long long"] = PresetTypes.Long,
            ["signed long long"] = PresetTypes.Long,
            ["unsigned __int64"] = PresetTypes.ULong,
            ["unsigned long long"] = PresetTypes.ULong,

            // Floating-point
            ["float"] = PresetTypes.Float,
            ["double"] = PresetTypes.Double,
            ["long double"] = PresetTypes.Double,

            // Special/placeholder
            ["CPlaceable"] = new BuiltinType
            {
                Size = 0x48,
                Template = new GetSetTemplate("new CPlaceable((IntPtr){0})", @"throw new InvalidOperationException(""NOT DONE YET"")")
            }
        };
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