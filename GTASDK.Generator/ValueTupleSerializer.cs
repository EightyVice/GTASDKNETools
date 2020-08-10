using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using SharpYaml;
using SharpYaml.Events;
using SharpYaml.Serialization;
using SharpYaml.Serialization.Descriptors;

namespace GTASDK.Generator
{
    // https://gist.github.com/PathogenDavid/434ff4cbb7512576c2b128b74268fb09
    internal class ValueTupleSerializer : IYamlSerializable, IYamlSerializableFactory
    {
        public IYamlSerializable TryCreate(SerializerContext context, ITypeDescriptor typeDescriptor)
        {
            if (typeDescriptor is ObjectDescriptor objectDescriptor && GetFieldCount(objectDescriptor.Type) >= 0)
                return this;

            return null;
        }

        /// <summary>
        /// Returns the number of fields in the given ValueTuple variant, or -1 if the specified type is not a ValueTuple.
        /// </summary>
        private static int GetFieldCount(Type tupleType)
        {
            if (!tupleType.IsGenericType)
                return -1;

            if (!tupleType.IsGenericTypeDefinition)
                tupleType = tupleType.GetGenericTypeDefinition();

            if (tupleType == typeof(ValueTuple))
                return 0;
            if (tupleType == typeof(ValueTuple<>))
                return 1;
            if (tupleType == typeof(ValueTuple<,>))
                return 2;
            if (tupleType == typeof(ValueTuple<,,>))
                return 3;
            if (tupleType == typeof(ValueTuple<,,,>))
                return 4;
            if (tupleType == typeof(ValueTuple<,,,,>))
                return 5;
            if (tupleType == typeof(ValueTuple<,,,,,>))
                return 6;
            if (tupleType == typeof(ValueTuple<,,,,,,>))
                return 7;
            if (tupleType == typeof(ValueTuple<,,,,,,,>))
                return 8;
            return -1;
        }

        private static IEnumerable<FieldInfo> GetFieldAccessors(Type tupleType)
        {
            var fieldCount = GetFieldCount(tupleType);

            if (fieldCount < 0)
                throw new YamlException($"[{tupleType}] is not a valid tuple type.");

            for (var i = 0; i < fieldCount; i++)
            {
                var field = tupleType.GetField(i == 7 ? "Rest" : $"Item{i + 1}");

                /*//TODO: Add support for oversized tuples. (The compiler will make these automatically if you make a tuple over 8 elements long.)
                if (GetFieldCount(field.FieldType) >= 0)
                    throw new NotSupportedException("Nested tuples are not yet supported.");*/

                yield return field;
            }
        }

        public virtual object ReadYaml(ref ObjectContext objectContext)
        {
            var ret = objectContext.Instance;
            var objectDescriptor = (ObjectDescriptor)objectContext.Descriptor;

            // Create an empty ValueTuple if we don't have one yet
            if (ret == null)
                ret = FormatterServices.GetUninitializedObject(objectDescriptor.Type);

            // Read in the tuple
            objectContext.Reader.Expect<SequenceStart>();

            foreach (var field in GetFieldAccessors(objectDescriptor.Type))
            {
                var fieldValue = objectContext.SerializerContext.ReadYaml(null, field.FieldType);
                field.SetValue(ret, fieldValue);
            }

            objectContext.Reader.Expect<SequenceEnd>();

            return ret;
        }

        public virtual void WriteYaml(ref ObjectContext objectContext)
        {
            var value = objectContext.Instance;
            var valueType = value.GetType();
            var objectDescriptor = (ObjectDescriptor)objectContext.Descriptor;

            objectContext.Writer.Emit(new SequenceStartEventInfo(value, valueType)
            {
                Tag = objectContext.Tag,
                //TODO: Would it be better to serialize as block only if a tuple contains objects for auto mode?
                Style = objectContext.Style == YamlStyle.Any ? YamlStyle.Flow : objectContext.Style
            });

            foreach (var field in GetFieldAccessors(objectDescriptor.Type))
            {
                var fieldValue = field.GetValue(value);
                objectContext.SerializerContext.WriteYaml(fieldValue, field.FieldType);
            }

            objectContext.Writer.Emit(new SequenceEndEventInfo(value, valueType));
        }
    }
}
