using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        public Field ParseComplexField(Dictionary<object, object> dict)
        {
            var entryKvp = dict.Single();
            var instruction = (string)entryKvp.Key;
            var data = (List<object>)entryKvp.Value;
            switch (instruction)
            {
                case "union":
                    return ParseUnion(data);
                case "bitfield":
                    return ParseBitfield(data);
                default:
                    throw new ArgumentException($"Invalid instruction {instruction}, must be one of [union, bitfield]");
            }
        }

        public Field ParseBitfield(List<object> data)
        {
            // Type of the bitfield members
            string type = null;
            var bitfieldBits = new List<(string name, uint length)>();

            foreach (var dataEntry in data)
            {
                switch (dataEntry)
                {
                    case Dictionary<object, object> dict1:
                        foreach (var kvp1 in dict1)
                        {
                            switch ((string)kvp1.Key)
                            {
                                case "type":
                                    type = (string)kvp1.Value;
                                    break;
                                default:
                                    throw new ArgumentException(
                                        $"Unsupported bitfield parameter {kvp1.Key}, must be one of [name, type]");
                            }
                        }

                        break;
                    case List<object> list1:
                        if (list1.Count != 2)
                        {
                            throw new ArgumentException(
                                $"Bitfield entry must be a tuple of [name, bitLength], but had too many or too few elements: {string.Join(",", list1)}");
                        }

                        bitfieldBits.Add(((string)list1[0], (uint)(int)list1[1]));
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

        public Field ParseUnion(IEnumerable<object> data)
        {
            var unionElements = new List<AlignedField>();
            foreach (List<object> unionElement in data)
            {
                if (unionElement.Count != 2)
                {
                    throw new ArgumentException(
                        $"Union must be a tuple of [type, name], but had too many or too few elements: {string.Join(",", unionElement)}");
                }

                unionElements.Add(new AlignedField(_typeCache, (string)unionElement[0], (string)unionElement[1]));
            }

            return new UnionField(unionElements);
        }

        public Field ParseRegularField(IReadOnlyList<object> list)
        {
            return list.Count == 3
                ? new AlignedField(_typeCache, (string)list[1], (string)list[2], (string)list[0] == "private" ? Visibility.@private : Visibility.@public)
                : new AlignedField(_typeCache, (string)list[0], (string)list[1]);
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
        public string Type { get; }
        public bool IsPointer { get; }
        public uint? InlineArrayLength { get; }

        protected ParserType ParserType => TypeCache[Type];

        protected ComplexTypedField(TypeCache typeCache, string type)
        {
            if (type.EndsWith("*"))
            {
                type = type.Substring(0, type.Length - 1);
                IsPointer = true;
            }

            if (type.EndsWith("]"))
            {
                var sizeStartingIndex = type.IndexOf('[') + 1;
                var sizeEndIndex = type.Length - 1;
                InlineArrayLength = uint.Parse(type.Substring(sizeStartingIndex, sizeEndIndex - sizeStartingIndex));
                type = type.Substring(0, sizeStartingIndex - 1);
            }

            TypeCache = typeCache;
            Type = type;
        }
    }

    public sealed class AlignedField : ComplexTypedField
    {
        public override uint Size => BaseSize * (InlineArrayLength ?? 1);
        private uint BaseSize => IsPointer ? Types.Pointer.Size : TypeCache[Type].Size;

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
                    {Visibility} Span<{ParserType.TypeMapsTo ?? Type}> {Name}
                    {{
                        {FieldParsing.PropModifiers} get => Memory.GetSpan<{ParserType.TypeMapsTo ?? Type}>(BaseAddress + 0x{offset:X}, {InlineArrayLength.Value});
                        {FieldParsing.PropModifiers} set => Memory.WriteSpan<{ParserType.TypeMapsTo ?? Type}>(BaseAddress + 0x{offset:X}, {InlineArrayLength.Value}, value);
                    }}
                ";
            }

            if (IsPointer)
            {
                if (!TypeCache.TryGetValue(Type, out var type))
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
                    {Visibility} {type.TypeMapsTo ?? Type} {Name}
                    {{
                        {FieldParsing.PropModifiers} get => {type.Template.Get(Types.Pointer.Template.Get($"BaseAddress + 0x{offset:X}"))};
                        {FieldParsing.PropModifiers} set => throw new InvalidOperationException(""NOT DONE YET"");
                    }}
                ";
            }

            return $@"
                // {Type} at offset 0x{offset:X}
                {Visibility} {ParserType.TypeMapsTo ?? Type} {Name}
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
            bitCount += (bitCount % 8); // Round up to the nearest byte
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
        @private, @internal, @public
    }
}
