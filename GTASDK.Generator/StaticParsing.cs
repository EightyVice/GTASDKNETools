using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GTASDK.Generator
{
    /// <summary>
    /// Parses static fields into <see cref="StaticMember"/> instances.
    /// </summary>
    internal sealed class StaticParsing
    {
        private readonly TypeCache _typeCache;

        public StaticParsing(TypeCache typeCache)
        {
            _typeCache = typeCache;
        }

        /// <summary>
        /// Parses a <see cref="StaticMember"/> from a YAML field definition.
        /// </summary>
        /// <param name="list">A tuple of [C++ type, field name, field address]</param>
        /// <returns>Parsed <see cref="StaticMember"/> instance</returns>
        public StaticMember ParseDefinition((string type, string name, uint address) list)
        {
            return new StaticMember(_typeCache, list.type, list.name, list.address);
        }
    }

    public sealed class StaticMember : IFixedEmittableMember
    {
        private readonly TypeCache _typeCache;
        public string Type { get; }
        public string Name { get; }
        public uint Address { get; }
        private ParserType ParserType => _typeCache[Type];
        public Visibility Visibility => Visibility.@public;

        public StaticMember(TypeCache typeCache, string type, string name, uint address)
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
                    {FieldParsing.PropModifiers} get => {ParserType.Template.Get($"0x{Address:X}")};
                    {FieldParsing.PropModifiers} set => {ParserType.Template.Set($"0x{Address:X}")};
                }}
            ";
        }
    }
}
