using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GTASDK.Generator
{
    internal sealed class StaticParsing
    {
        private readonly TypeCache _typeCache;

        public StaticParsing(TypeCache typeCache)
        {
            _typeCache = typeCache;
        }

        public StaticMember ParseDefinition(List<object> list)
        {
            if (list.Count != 3)
            {
                throw new ArgumentException("There were either too many or too few items in list, must be exactly [type, name, address]", nameof(list));
            }

            return new StaticMember(_typeCache, (string) list[0], (string) list[1], (uint) (int) list[2]);
        }
    }

    public sealed class StaticMember
    {
        private readonly TypeCache _typeCache;
        public string Type { get; }
        public string Name { get; }
        public uint Address { get; }
        private ParserType ParserType => _typeCache[Type];

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
                    ${FieldParsing.PropModifiers} get => {ParserType.Template.Get($"0x{Address:X}")};
                    ${FieldParsing.PropModifiers} set => {ParserType.Template.Set($"0x{Address:X}")};
                }}
            ";
        }
    }
}
