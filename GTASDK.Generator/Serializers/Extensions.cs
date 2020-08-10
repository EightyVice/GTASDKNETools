using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpYaml.Serialization;
using YamlNode = SharpYaml.Model.YamlNode;

namespace GTASDK.Generator
{
    internal static class Extensions
    {
        public static T ToObjectX<T>(this YamlNode node)
        {
            return node.ToObject<T>(Generator.SerializerSettings);
        }
    }
}
