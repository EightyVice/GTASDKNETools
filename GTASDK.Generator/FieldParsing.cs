using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using SharpYaml.Model;
using SharpYaml.Serialization;
using YamlNode = SharpYaml.Model.YamlNode;

namespace GTASDK.Generator
{
    public sealed class FieldParsing
    {
        private readonly TypeCache _typeCache;
        internal const string PropModifiers = @"[MethodImpl(MethodImplOptions.AggressiveInlining)]";

        public FieldParsing(TypeCache typeCache)
        {
            _typeCache = typeCache;
        }

        public Field ParseComplexField(IReadOnlyDictionary<ComplexFieldType, YamlSequence> dict)
        {
            var entryKvp = dict.Single();
            var instruction = entryKvp.Key;
            var data = entryKvp.Value;
            switch (instruction)
            {
                case ComplexFieldType.Union:
                    return ParseUnion(data.ToObjectX<List<(string type, string name)>>());
                case ComplexFieldType.Bitfield:
                    return ParseBitfield(data.ToObjectX<List<YamlNode>>());
                default:
                    throw new ArgumentException($"Invalid instruction {instruction}, must be one of [union, bitfield]");
            }
        }

        public Field ParseBitfield(IReadOnlyList<YamlNode> data)
        {
            // Type of the bitfield members
            string type = null;
            var bitfieldBits = new List<(string name, uint length)>();

            foreach (var dataEntry in data)
            {
                switch (dataEntry)
                {
                    case YamlMapping dict1:
                        var descriptor = dict1.ToObjectX<BitfieldDescriptor>();
                        if (descriptor.Type != null)
                        {
                            type = descriptor.Type;
                        }

                        break;
                    case YamlSequence list1:
                        bitfieldBits.Add(list1.ToObjectX<(string name, uint length)>());
                        break;
                    default:
                        throw new ArgumentException($"Unrecognized bitfield entry type {dataEntry}");
                }
            }

            if (type == null)
            {
                throw new ArgumentNullException(nameof(type), "A bitfield must have a type specified");
            }

            return new BitfieldField(_typeCache, type, bitfieldBits);
        }

        public Field ParseUnion(IReadOnlyList<(string type, string name)> data)
        {
            var unionElements = new List<AlignedField>();
            foreach (var (type, name) in data)
            {
                unionElements.Add(new AlignedField(_typeCache, type, name));
            }

            return new UnionField(unionElements);
        }

        public Field ParseRegularField(YamlSequence list)
        {
            if (list.Count == 3)
            {
                var (visibility, type, name) = list.ToObjectX<(Visibility visibility, string type, string name)>();

                return new AlignedField(_typeCache, type, name, visibility);
            }
            else
            {
                var (type, name) = list.ToObjectX<(string type, string name)>();

                return new AlignedField(_typeCache, type, name);
            }
        }

        public Field ParseStringDescriptor(string str)
        {
            if (str == "vtable")
            {
                return new VTableField();
            }

            throw new ArgumentException($"Unrecognized string {str}");
        }
    }

    public abstract class Field
    {
        public abstract uint Size { get; }
        public virtual string Name { get; protected set; }
        public virtual Visibility Visibility { get; protected set; } = Visibility.@public;

        public abstract string Emit(uint offset);
    }

    public sealed class VTableField : Field
    {
        public override uint Size { get; } = Types.Pointer.Size;
        public override string Name => "_vtable";
        public override Visibility Visibility => Visibility.@internal;
        public override string Emit(uint offset)
        {
            return $@"
                {Visibility} IntPtr {Name}
                {{
                    {FieldParsing.PropModifiers} get => {Types.Pointer.Template.Get($"BaseAddress + 0x{offset:X}")};
                    {FieldParsing.PropModifiers} set => {Types.Pointer.Template.Set($"BaseAddress + 0x{offset:X}")};
                }}
            ";
        }
    }

    public abstract class ComplexTypedField : Field
    {
        protected TypeCache TypeCache { get; }
        public CompositeType Type { get; }
        public uint? InlineArrayLength { get; }

        protected ParserType ParserType => Type.BackingType;

        protected ComplexTypedField(TypeCache typeCache, string type)
        {
            if (type.EndsWith("]"))
            {
                var sizeStartingIndex = type.IndexOf('[') + 1;
                var sizeEndIndex = type.Length - 1;
                InlineArrayLength = uint.Parse(type.Substring(sizeStartingIndex, sizeEndIndex - sizeStartingIndex));
                type = type.Substring(0, sizeStartingIndex - 1);
            }

            TypeCache = typeCache;
            Type = new CompositeType(TypeCache, type);
        }
    }

    public sealed class AlignedField : ComplexTypedField
    {
        public override uint Size => BaseSize * (InlineArrayLength ?? 1);
        private uint BaseSize => Type.IsPointer ? Types.Pointer.Size : ParserType.Size;

        public AlignedField(TypeCache typeCache, string type, string name, Visibility visibility = Visibility.@public) : base(typeCache, type)
        {
            Name = name;
            Visibility = visibility;
        }

        public override string Emit(uint offset)
        {
            if (InlineArrayLength.HasValue)
            {
                // TODO: do we need to support both InlineArrayLength and IsPointer at the same time?

                return $@"
                    {Visibility} Span<{Type.CsharpName}> {Name}
                    {{
                        {FieldParsing.PropModifiers} get => Memory.GetSpan<{Type.CsharpName}>(BaseAddress + 0x{offset:X}, {InlineArrayLength.Value});
                        {FieldParsing.PropModifiers} set => Memory.WriteSpan<{Type.CsharpName}>(BaseAddress + 0x{offset:X}, {InlineArrayLength.Value}, value);
                    }}
                ";
            }

            if (Type.IsPointer)
            {
                if (!Type.TryGet(out var type))
                {
                    return $@"
                        // PLACEHOLDER: Expose raw IntPtr
                        // {Type} at offset 0x{offset:X}
                        {Visibility} IntPtr {Name}
                        {{
                            {FieldParsing.PropModifiers} get => {Types.Pointer.Template.Get($"BaseAddress + 0x{offset:X}")};
                            {FieldParsing.PropModifiers} set => {Types.Pointer.Template.Set($"BaseAddress + 0x{offset:X}")};
                        }}
                    ";
                }

                return $@"
                    // {Type} at offset 0x{offset:X}
                    {Visibility} {Type.CsharpName} {Name}
                    {{
                        {FieldParsing.PropModifiers} get => {type.Template.Get(Types.Pointer.Template.Get($"BaseAddress + 0x{offset:X}"))};
                        {FieldParsing.PropModifiers} set => throw new InvalidOperationException(""NOT DONE YET"");
                    }}
                ";
            }

            return $@"
                // {Type} at offset 0x{offset:X}
                {Visibility} {Type.CsharpName} {Name}
                {{
                    {FieldParsing.PropModifiers} get => {ParserType.Template.Get($"BaseAddress + 0x{offset:X}")};
                    {FieldParsing.PropModifiers} set => {ParserType.Template.Set($"BaseAddress + 0x{offset:X}")};
                }}
            ";
        }
    }

    public class UnionField : Field
    {
        public IReadOnlyList<Field> Elements { get; }
        public override uint Size { get; }

        public UnionField(IEnumerable<Field> elements)
        {
            Elements = elements.ToArray();
            Size = Elements.Max(e => e.Size);
        }

        public override string Emit(uint offset)
        {
            var sb = new StringBuilder($"// Beginning of union of [{string.Join(", ", Elements.Select(e => e.Name))}]\n");
            foreach (var element in Elements)
            {
                sb.Append(element.Emit(offset)); // Emit all elements at the same offset
            }
            sb.Append("\n// End of union\n");
            return sb.ToString();
        }
    }

    public sealed class BitfieldField : Field
    {
        private TypeCache TypeCache { get; }
        public string Type { get; }
        public IReadOnlyList<(string name, uint length)> BitfieldElements { get; }
        public override uint Size { get; }

        private ParserType ParserType => TypeCache[Type];

        public BitfieldField(TypeCache typeCache, string type, IReadOnlyList<(string name, uint length)> bitfieldElements)
        {
            TypeCache = typeCache;
            Name = null;
            Type = type;
            BitfieldElements = bitfieldElements;

            var bitCount = bitfieldElements.Aggregate(0U, (acc, next) => acc + next.length); // Count the amount of bits
            bitCount += bitCount % 8; // Round up to the nearest byte
            var byteCount = bitCount / 8; // Convert to bytes
            byteCount += byteCount % ParserType.Size; // round up to align with bitfield size

            Size = Math.Max(byteCount, ParserType.Size); // ensure that the size is at least the size of any element
        }

        public override string Emit(uint offset)
        {
            var sb = new StringBuilder($"// Beginning of bitfield {Type} {Name} Size: {Size} \n");
            var bitOffset = 0U;
            foreach (var (name, length) in BitfieldElements)
            {
                if (length == 1)
                {
                    sb.Append($@"
                        {Visibility} bool {name}
                        {{
                            {FieldParsing.PropModifiers} get => Memory.ReadBit(BaseAddress + 0x{offset:X}, {bitOffset});
                            {FieldParsing.PropModifiers} set => Memory.WriteBit(BaseAddress + 0x{offset:X}, {bitOffset}, value);
                        }}
                    ");
                }
                else
                {
                    sb.Append($@"
                        {Visibility} {ParserType.TypeMapsTo ?? Type} {name}
                        {{
                            {FieldParsing.PropModifiers} get => {ParserType.BitsTemplate.Get($"BaseAddress + 0x{offset:X}", bitOffset, length)};
                            {FieldParsing.PropModifiers} set => {ParserType.BitsTemplate.Set($"BaseAddress + 0x{offset:X}", bitOffset, length)};
                        }}
                    ");
                }

                bitOffset += length;
                if (bitOffset >= 8)
                {
                    offset += bitOffset / 8;
                    bitOffset %= 8;
                }
            }
            sb.Append("\n// End of bitfield\n");
            return sb.ToString();
        }
    }

    public enum Visibility
    {
        [YamlMember("private")]
        @private,
        [YamlMember("internal")]
        @internal,
        [YamlMember("public")]
        @public
    }
    public enum ComplexFieldType
    {
        [YamlMember("union")]
        Union,
        [YamlMember("bitfield")]
        Bitfield
    }

    public class BitfieldDescriptor
    {
        [YamlMember("type")]
        [DefaultValue(null)]
        public string Type { get; set; }
    }
}
