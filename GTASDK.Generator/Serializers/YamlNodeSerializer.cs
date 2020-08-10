using System;
using SharpYaml;
using SharpYaml.Events;
using SharpYaml.Model;
using SharpYaml.Serialization;
using SharpYaml.Serialization.Descriptors;
using YamlNode = SharpYaml.Model.YamlNode;

namespace GTASDK.Generator
{
    /// <summary>
    /// Deserializes YamlNode, YamlMapping, YamlSequence and YamlValue from any arbitrary object tree. No serialization support yet.
    /// </summary>
    internal class YamlNodeSerializer : IYamlSerializable, IYamlSerializableFactory
    {
        public IYamlSerializable TryCreate(SerializerContext context, ITypeDescriptor typeDescriptor)
        {
            if (typeDescriptor is ObjectDescriptor objectDescriptor && typeof(YamlNode).IsAssignableFrom(objectDescriptor.Type))
                return this;

            return null;
        }

        public object ReadYaml(ref ObjectContext objectContext)
        {
            if (objectContext.Descriptor.Type == typeof(YamlNode))
            {
                if (objectContext.Reader.Accept<MappingStart>())
                {
                    return YamlMapping.Load(objectContext.Reader, new YamlNodeTracker());
                }
                if (objectContext.Reader.Accept<SequenceStart>())
                {
                    return YamlSequence.Load(objectContext.Reader, new YamlNodeTracker());
                }
                if (objectContext.Reader.Accept<Scalar>())
                {
                    return YamlValue.Load(objectContext.Reader, new YamlNodeTracker());
                }
            }
            if (objectContext.Descriptor.Type == typeof(YamlMapping))
            {
                if (objectContext.Reader.Accept<MappingStart>())
                {
                    return YamlMapping.Load(objectContext.Reader, new YamlNodeTracker());
                }
                throw new YamlException($"Expected {nameof(MappingStart)} but did not find it");
            }
            if (objectContext.Descriptor.Type == typeof(YamlSequence))
            {
                if (objectContext.Reader.Accept<SequenceStart>())
                {
                    return YamlSequence.Load(objectContext.Reader, new YamlNodeTracker());
                }
                throw new YamlException($"Expected {nameof(SequenceStart)} but did not find it");
            }
            if (objectContext.Descriptor.Type == typeof(YamlValue))
            {
                if (objectContext.Reader.Accept<Scalar>())
                {
                    return YamlValue.Load(objectContext.Reader, new YamlNodeTracker());
                }
                throw new YamlException($"Expected {nameof(Scalar)} but did not find it");
            }

            throw new YamlException($"{objectContext.Descriptor.Type} is not a supported {nameof(YamlNode)} type");
        }

        public void WriteYaml(ref ObjectContext objectContext)
        {
            throw new NotImplementedException("Not done yet");
        }
    }
}
