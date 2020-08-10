using System;
using System.Collections.Generic;
using System.IO;

namespace GTASDK.Generator
{
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