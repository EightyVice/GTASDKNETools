using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SharpYaml;
using SharpYaml.Events;
using SharpYaml.Serialization;
using SharpYaml.Serialization.Descriptors;
using SharpYaml.Serialization.Serializers;

namespace GTASDK.Generator
{
    // PrimitiveSerializer fork from SharpYaml but with addex hex specifier support.
    internal class PrimitiveSerializer : ScalarSerializerBase, IYamlSerializableFactory
    {
        public IYamlSerializable TryCreate(SerializerContext context, ITypeDescriptor typeDescriptor)
        {
            return typeDescriptor is PrimitiveDescriptor ? this : null;
        }

        public override object ConvertFrom(ref ObjectContext context, Scalar scalar)
        {
            var primitiveType = (PrimitiveDescriptor)context.Descriptor;
            var type = primitiveType.Type;
            var text = scalar.Value;

            // Return null if expected type is an object and scalar is null
            if (text == null)
            {
                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.Object:
                    case TypeCode.Empty:
                    case TypeCode.String:
                        return null;
                    default:
                        // TODO check this
                        throw new YamlException(scalar.Start, scalar.End, "Unexpected null scalar value");
                }
            }

            // If type is an enum, try to parse it
            if (type.GetTypeInfo().IsEnum)
            {
                var result = primitiveType.ParseEnum(text, out var enumRemapped);
                if (enumRemapped)
                {
                    typeof(SerializerContext).GetField("HasRemapOccurred", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(context.SerializerContext, true);
                    //context.SerializerContext.HasRemapOccurred = true;
                }
                return result;
            }

            // Parse default types 
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                    context.SerializerContext.Schema.TryParse(scalar, type, out var value);
                    return value;
                case TypeCode.DateTime:
                    return DateTime.Parse(text, CultureInfo.InvariantCulture);
                case TypeCode.String:
                    return text;
            }

            if (type == typeof(TimeSpan))
            {
#if NET35
                return TimeSpan.Parse(text);
#else
                return TimeSpan.Parse(text, CultureInfo.InvariantCulture);
#endif
            }
            else if (type == typeof(DateTimeOffset))
            {
#if NET35
                return DateTimeOffset.Parse(text);
#else
                return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);
#endif
            }
            else if (type == typeof(Guid))
            {
                return new Guid(text);
            }

            // Remove _ character from numeric values
            text = text.Replace("_", string.Empty);

            // Parse default types 
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Char:
                    if (text.Length != 1)
                    {
                        throw new YamlException(scalar.Start, scalar.End, $"Unable to decode char from [{text}]. Expecting a string of length == 1");
                    }
                    return text.ToCharArray()[0];
                case TypeCode.Byte when text.StartsWith("0x"):
                    return byte.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                case TypeCode.SByte when text.StartsWith("0x"):
                    return sbyte.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                case TypeCode.Int16 when text.StartsWith("0x"):
                    return short.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                case TypeCode.UInt16 when text.StartsWith("0x"):
                    return ushort.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                case TypeCode.Int32 when text.StartsWith("0x"):
                    return int.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                case TypeCode.UInt32 when text.StartsWith("0x"):
                    return uint.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                case TypeCode.Int64 when text.StartsWith("0x"):
                    return long.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                case TypeCode.UInt64 when text.StartsWith("0x"):
                    return ulong.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                case TypeCode.Single when text.StartsWith("0x"):
                    return float.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                case TypeCode.Double when text.StartsWith("0x"):
                    return double.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                case TypeCode.Decimal when text.StartsWith("0x"):
                    return decimal.Parse(text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                case TypeCode.Byte:
                    return byte.Parse(text, CultureInfo.InvariantCulture);
                case TypeCode.SByte:
                    return sbyte.Parse(text, CultureInfo.InvariantCulture);
                case TypeCode.Int16:
                    return short.Parse(text, CultureInfo.InvariantCulture);
                case TypeCode.UInt16:
                    return ushort.Parse(text, CultureInfo.InvariantCulture);
                case TypeCode.Int32:
                    return int.Parse(text, CultureInfo.InvariantCulture);
                case TypeCode.UInt32:
                    return uint.Parse(text, CultureInfo.InvariantCulture);
                case TypeCode.Int64:
                    return long.Parse(text, CultureInfo.InvariantCulture);
                case TypeCode.UInt64:
                    return ulong.Parse(text, CultureInfo.InvariantCulture);
                case TypeCode.Single:
                    return float.Parse(text, CultureInfo.InvariantCulture);
                case TypeCode.Double:
                    return double.Parse(text, CultureInfo.InvariantCulture);
                case TypeCode.Decimal:
                    return decimal.Parse(text, CultureInfo.InvariantCulture);
            }

            // If we are expecting a type object, return directly the string
            if (type == typeof(object))
            {
                // Try to parse the scalar directly
                if (context.SerializerContext.Schema.TryParse(scalar, true, out var defaultTag, out var scalarValue))
                {
                    return scalarValue;
                }

                return text;
            }

            throw new YamlException(scalar.Start, scalar.End, $"Unable to decode scalar [{scalar}] not supported by current schema");
        }

        /// <summary>
        /// Appends decimal point to arg if it does not exist
        /// </summary>
        /// <param name="text"></param>
        /// <param name="hasNaN">True if the floating point type supports NaN or Infinity.</param>
        /// <returns></returns>
        private static string AppendDecimalPoint(string text, bool hasNaN)
        {
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                // Do not append a decimal point if floating point type value
                // - is in exponential form, or
                // - already has a decimal point
                if (c == 'e' || c == 'E' || c == '.')
                {
                    return text;
                }
            }
            // Special cases for floating point type supporting NaN and Infinity
            if (hasNaN && (string.Equals(text, "NaN") || text.Contains("Infinity")))
                return text;

            return text + ".0";
        }

        public override string ConvertTo(ref ObjectContext objectContext)
        {
            return ConvertValue(objectContext.Instance);
        }

        public static string ConvertValue(object value)
        {
            var text = string.Empty;

            // Return null if expected type is an object and scalar is null
            if (value == null)
            {
                return text;
            }

            var valueType = value.GetType();

            // Handle string
            if (valueType.GetTypeInfo().IsEnum)
            {
                text = ((Enum)Enum.ToObject(valueType, value)).ToString("G");
            }
            else
            {
                // Parse default types 
                switch (Type.GetTypeCode(valueType))
                {
                    case TypeCode.String:
                    case TypeCode.Char:
                        text = value.ToString();
                        break;
                    case TypeCode.Boolean:
                        text = (bool)value ? "true" : "false";
                        break;
                    case TypeCode.Byte:
                        text = ((byte)value).ToString("G", CultureInfo.InvariantCulture);
                        break;
                    case TypeCode.SByte:
                        text = ((sbyte)value).ToString("G", CultureInfo.InvariantCulture);
                        break;
                    case TypeCode.Int16:
                        text = ((short)value).ToString("G", CultureInfo.InvariantCulture);
                        break;
                    case TypeCode.UInt16:
                        text = ((ushort)value).ToString("G", CultureInfo.InvariantCulture);
                        break;
                    case TypeCode.Int32:
                        text = ((int)value).ToString("G", CultureInfo.InvariantCulture);
                        break;
                    case TypeCode.UInt32:
                        text = ((uint)value).ToString("G", CultureInfo.InvariantCulture);
                        break;
                    case TypeCode.Int64:
                        text = ((long)value).ToString("G", CultureInfo.InvariantCulture);
                        break;
                    case TypeCode.UInt64:
                        text = ((ulong)value).ToString("G", CultureInfo.InvariantCulture);
                        break;
                    case TypeCode.Single:
                        // Append decimal point to floating point type values 
                        // because type changes in round trip conversion if ( value * 10.0 ) % 10.0 == 0
                        //
                        // G9 is used instead of R as per the following documentation:
                        // https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#the-round-trip-r-format-specifier
                        // R can cause issues on x64 systems, see https://github.com/dotnet/coreclr/issues/13106 for details.
                        text = AppendDecimalPoint(((float)value).ToString("G9", CultureInfo.InvariantCulture), true);
                        break;
                    case TypeCode.Double:
                        // G17 is used instead of R due to issues on x64 systems. See documentation on TypeCode.Single case above.
                        text = AppendDecimalPoint(((double)value).ToString("G17", CultureInfo.InvariantCulture), true);
                        break;
                    case TypeCode.Decimal:
                        text = AppendDecimalPoint(((decimal)value).ToString("G", CultureInfo.InvariantCulture), false);
                        break;
                    case TypeCode.DateTime:
                        text = ((DateTime)value).ToString("o", CultureInfo.InvariantCulture);
                        break;
                    default:
                        if (valueType == typeof(TimeSpan))
                        {
#if NET35
                            text = string.Format("{0:G}",((TimeSpan) value));
#else
                            text = ((TimeSpan)value).ToString("G", CultureInfo.InvariantCulture);
#endif
                        }
                        else if (valueType == typeof(DateTimeOffset))
                        {
#if NET35
                            text = string.Format("{0:o}", ((DateTimeOffset) value));
#else
                            text = ((DateTimeOffset)value).ToString("o", CultureInfo.InvariantCulture);
#endif
                        }
                        else if (valueType == typeof(Guid))
                        {
                            text = ((Guid)value).ToString();
                        }
                        break;
                }
            }

            if (text == null)
            {
                throw new YamlException($"Unable to serialize scalar [{value}] not supported");
            }

            return text;
        }
    }
}
