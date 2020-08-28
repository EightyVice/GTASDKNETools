using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GTASDK.Generator
{
    /// <summary>
    /// Parses static fields into <see cref="StaticField"/> instances.
    /// </summary>
    internal sealed class StaticFieldParsing
    {
        private readonly TypeCache _typeCache;

        public StaticFieldParsing(TypeCache typeCache)
        {
            _typeCache = typeCache;
        }

        /// <summary>
        /// Parses a <see cref="StaticField"/> from a YAML field definition.
        /// </summary>
        /// <param name="signature">A tuple of [C++ type, field name, field address]</param>
        /// <returns>Parsed <see cref="StaticField"/> instance</returns>
        public StaticField ParseDefinition((string type, string name, uint address) signature)
        {
            return new StaticField(_typeCache, signature.type, signature.name, signature.address);
        }
    }

    public sealed class StaticField : IFixedEmittableMember
    {
        private readonly TypeCache _typeCache;
        public string Type { get; }
        public string Name { get; }
        public uint Address { get; }
        private ParserType ParserType => _typeCache[Type];
        public Visibility Visibility => Visibility.@public;

        public StaticField(TypeCache typeCache, string type, string name, uint address)
        {
            Type = type;
            Name = name;
            Address = address;
            _typeCache = typeCache;
        }

        public string Emit()
        {
            return $@"
                // static {Type} at 0x{Address:X}
                public static {ParserType.TypeMapsTo ?? Type} {Name}
                {{
                    {InstanceFieldParsing.PropModifiers} get => {ParserType.Template.Get($"0x{Address:X}")};
                    {InstanceFieldParsing.PropModifiers} set => {ParserType.Template.Set($"0x{Address:X}")};
                }}
            ";
        }
    }
}
