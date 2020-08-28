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
                    AsGet = "Memory.ReadByte({0})",
                    AsSet = "Memory.WriteByte({0}, value)"
                },
                BitsTemplate =
                {
                    AsGet = "Memory.ReadBitsInt8({0}, {1}, {2})",
                    AsSet = "Memory.WriteBitsInt8({0}, {1}, {2}, value)"
                }
            };

            public static readonly BuiltinType SByte = new BuiltinType
            {
                TypeMapsTo = "sbyte",
                Size = sizeof(sbyte),
                Template =
                {
                    AsGet = "Memory.ReadSByte({0})",
                    AsSet = "Memory.WriteSByte({0}, value)"
                }
            };

            public static readonly BuiltinType Short = new BuiltinType
            {
                TypeMapsTo = "short",
                Size = sizeof(short),
                Template =
                {
                    AsGet = "Memory.ReadInt16({0})",
                    AsSet = "Memory.WriteInt16({0}, value)"
                }
            };

            public static readonly BuiltinType UShort = new BuiltinType
            {
                TypeMapsTo = "ushort",
                Size = sizeof(ushort),
                Template =
                {
                    AsGet = "Memory.ReadUInt16({0})",
                    AsSet = "Memory.WriteUInt16({0}, value)"
                }
            };

            public static readonly BuiltinType Int = new BuiltinType
            {
                TypeMapsTo = "int",
                Size = sizeof(int),
                Template =
                {
                    AsGet = "Memory.ReadInt32({0})",
                    AsSet = "Memory.WriteInt32({0}, value)"
                }
            };

            public static readonly BuiltinType UInt = new BuiltinType
            {
                TypeMapsTo = "uint",
                Size = sizeof(uint),
                Template =
                {
                    AsGet = "Memory.ReadUInt32({0})",
                    AsSet = "Memory.WriteUInt32({0}, value)"
                }
            };

            public static readonly BuiltinType Long = new BuiltinType
            {
                TypeMapsTo = "long",
                Size = sizeof(long),
                Template =
                {
                    AsGet = "Memory.ReadInt64({0})",
                    AsSet = "Memory.WriteInt64({0}, value)"
                }
            };

            public static readonly BuiltinType ULong = new BuiltinType
            {
                TypeMapsTo = "ulong",
                Size = sizeof(ulong),
                Template =
                {
                    AsGet = "Memory.ReadUInt64({0})",
                    AsSet = "Memory.WriteUInt64({0}, value)"
                }
            };

            public static readonly BuiltinType Float = new BuiltinType
            {
                TypeMapsTo = "float",
                Size = sizeof(float),
                Template =
                {
                    AsGet = "Memory.ReadFloat({0})",
                    AsSet = "Memory.WriteFloat({0}, value)"
                }
            };

            public static readonly BuiltinType Double = new BuiltinType
            {
                TypeMapsTo = "double",
                Size = sizeof(double),
                Template =
                {
                    AsGet = "Memory.ReadDouble({0})",
                    AsSet = "Memory.WriteDouble({0}, value)"
                }
            };

            public static readonly BuiltinType Void = new BuiltinType
            {
                TypeMapsTo = "void",
                Size = 0, // Not to be used in properties
                Template =
                {
                    AsGet = @"throw new InvalidOperationException(""Not supported on this type"")",
                    AsSet = @"throw new InvalidOperationException(""Not supported on this type"")"
                }
            };
        }

        public static BuiltinType Pointer { get; } = new BuiltinType
        {
            Size = 0x4,
            Template =
            {
                AsGet = "(IntPtr)({0})",
                AsSet = "Memory.WriteInt32({0}, value.ToInt32())"
            },
            ArgumentTemplate =
            {
                AsArgument = "IntPtr {1}",
                AsCall = "{1}"
            }
        };

        public static readonly IReadOnlyDictionary<string, BuiltinType> Builtin = new Dictionary<string, BuiltinType>
        {
            // Size 1
            ["<byte>"] = PresetTypes.Byte,
            ["<sbyte>"] = PresetTypes.SByte,

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

            ["bool"] = PresetTypes.Int,
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
            ["void"] = PresetTypes.Void,
            ["CPlaceable"] = new BuiltinType
            {
                Size = 0x48,
                Template =
                {
                    AsGet = "new CPlaceable((IntPtr){0})",
                    AsSet = "Memory.CopyRegion({0}, value.BaseAddress, 0x48)"
                }
            },
            ["CVector"] = new BuiltinType
            {
                Size = sizeof(float) * 3,
                Template =
                {
                    AsGet = "Memory.ReadVector({0})",
                    AsSet = "Memory.WriteVector({0}, value)"
                }
            },
            ["CVector2D"] = new BuiltinType
            {
                Size = sizeof(float) * 2,
                Template =
                {
                    AsGet = "Memory.ReadVector2D({0})",
                    AsSet = "Memory.WriteVector2D({0}, value)"
                }
            },
            ["CRect"] = new BuiltinType
            {
                Size = sizeof(float) * 4,
                Template =
                {
                    AsGet = "Memory.ReadRect({0})",
                    AsSet = "Memory.WriteRect({0}, value)"
                }
            }
        };
    }

    public abstract class ParserType
    {
        public string TypeMapsTo { get; set; }
        public virtual uint Size { get; set; }
        public GetSetTemplate Template { get; set; } = new GetSetTemplate();
        public GetSetTemplate BitsTemplate { get; set; } = new GetSetTemplate();

        // Identity mapping
        public CallTemplate ArgumentTemplate { get; set; } = new CallTemplate
        {
            AsArgument = "{0} {1}",
            AsCall = "{1}"
        };
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
                AsGet = $"new {_typeGraph.Name}({{0}})",
                AsSet = $"Memory.CopyRegion({{0}}, value.BaseAddress, {_typeGraph.Name}._Size)"
            };
            ArgumentTemplate = typeGraph is EnumTypeGraph
                ? new CallTemplate
                {
                    AsArgument = "{0} {1}",
                    AsCall = "{1}"
                }
                : new CallTemplate
                {
                    AsArgument = "{0} {1}",
                    AsCall = "(IntPtr){1}.BaseAddress"
                };
        }
    }

    public sealed class CompositeType
    {
        private readonly TypeCache _typeCache;
        public ParserType BackingType => _typeCache[CppName];
        public string OriginalName { get; }
        public string CppName { get; }
        public bool IsPointer { get; }
        public bool IsRef { get; set; }
        public string CsharpName => BackingType.TypeMapsTo ?? CppName;

        public CompositeType(TypeCache typeCache, string typeName)
        {
            _typeCache = typeCache;

            OriginalName = typeName;

            if (typeName.EndsWith("*"))
            {
                typeName = typeName.Substring(0, typeName.Length - 1);
                IsPointer = true;
            }

            if (typeName.EndsWith("&"))
            {
                typeName = typeName.Substring(0, typeName.Length - 1);
                IsRef = true;
            }

            CppName = typeName;
        }

        public bool TryGet(out ParserType type) => _typeCache.TryGetValue(CppName, out type);

        public override string ToString()
        {
            return CppName;
        }
    }
}
