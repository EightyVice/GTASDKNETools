using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                Template =
                {
                    Getter = "Memory.ReadByte({0})",
                    Setter = "Memory.WriteByte({0}, value)"
                },
                BitsTemplate =
                {
                    Getter = "Memory.ReadBitsInt8({0}, {1}, {2})",
                    Setter = @"throw new InvalidOperationException(""NOT DONE YET"")"
                }
            };

            public static readonly BuiltinType SByte = new BuiltinType
            {
                TypeMapsTo = "sbyte",
                Size = sizeof(sbyte),
                Template =
                {
                    Getter = "Memory.ReadSByte({0})",
                    Setter = "Memory.WriteSByte({0}, value)"
                }
            };

            public static readonly BuiltinType Short = new BuiltinType
            {
                TypeMapsTo = "short",
                Size = sizeof(short),
                Template =
                {
                    Getter = "Memory.ReadInt16({0})",
                    Setter = "Memory.WriteInt16({0}, value)"
                }
            };

            public static readonly BuiltinType UShort = new BuiltinType
            {
                TypeMapsTo = "ushort",
                Size = sizeof(ushort),
                Template =
                {
                    Getter = "Memory.ReadUInt16({0})",
                    Setter = "Memory.WriteUInt16({0}, value)"
                }
            };

            public static readonly BuiltinType Int = new BuiltinType
            {
                TypeMapsTo = "int",
                Size = sizeof(int),
                Template =
                {
                    Getter = "Memory.ReadInt32({0})",
                    Setter = "Memory.WriteInt32({0}, value)"
                }
            };

            public static readonly BuiltinType UInt = new BuiltinType
            {
                TypeMapsTo = "uint",
                Size = sizeof(uint),
                Template =
                {
                    Getter = "Memory.ReadUInt32({0})",
                    Setter = "Memory.WriteUInt32({0}, value)"
                }
            };

            public static readonly BuiltinType Long = new BuiltinType
            {
                TypeMapsTo = "long",
                Size = sizeof(long),
                Template =
                {
                    Getter = "Memory.ReadInt64({0})",
                    Setter = "Memory.WriteInt64({0}, value)"
                }
            };

            public static readonly BuiltinType ULong = new BuiltinType
            {
                TypeMapsTo = "ulong",
                Size = sizeof(ulong),
                Template =
                {
                    Getter = "Memory.ReadUInt64({0})",
                    Setter = "Memory.WriteUInt64({0}, value)"
                }
            };

            public static readonly BuiltinType Float = new BuiltinType
            {
                TypeMapsTo = "float",
                Size = sizeof(float),
                Template =
                {
                    Getter = "Memory.ReadFloat({0})",
                    Setter = "Memory.WriteFloat({0}, value)"
                }
            };

            public static readonly BuiltinType Double = new BuiltinType
            {
                TypeMapsTo = "double",
                Size = sizeof(double),
                Template =
                {
                    Getter = "Memory.ReadDouble({0})",
                    Setter = "Memory.WriteDouble({0}, value)"
                }
            };
        }

        public static BuiltinType Pointer { get; } = new BuiltinType
        {
            Size = 0x4,
            Template =
            {
                Getter = "(IntPtr)({0})",
                Setter = "Memory.WriteInt32({0}, value.ToInt32())"
            }
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
                Template =
                {
                    Getter = "new CPlaceable((IntPtr){0})",
                    Setter = "Memory.CopyRegion({0}, value.BaseAddress, 0x48)"
                }
            }
        };
    }

    public abstract class ParserType
    {
        public string TypeMapsTo { get; set; } = null;
        public virtual uint Size { get; set; }
        public GetSetTemplate Template { get; set; } = new GetSetTemplate();
        public GetSetTemplate BitsTemplate { get; set; } = new GetSetTemplate();
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
            Template = new GetSetTemplate
            {
                Getter = $"new {_typeGraph.Name}({{0}})",
                Setter = $"Memory.CopyRegion({{0}}, value.BaseAddress, {_typeGraph.Name}._Size)"
            };
        }
    }

    public sealed class CompositeType
    {
        private readonly TypeCache _typeCache;
        public ParserType BackingType => _typeCache[OriginalName];
        public string OriginalName { get; }
        public bool IsPointer { get; }
        public string CsharpName => BackingType.TypeMapsTo ?? OriginalName;

        public CompositeType(TypeCache typeCache, string typeName)
        {
            _typeCache = typeCache;
            if (typeName.EndsWith("*"))
            {
                typeName = typeName.Substring(0, typeName.Length - 1);
                IsPointer = true;
            }

            OriginalName = typeName;
        }

        public bool TryGet(out ParserType type) => _typeCache.TryGetValue(OriginalName, out type);

        public override string ToString()
        {
            return OriginalName;
        }
    }
}
