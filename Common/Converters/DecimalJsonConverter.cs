using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text; 
using System.Threading.Tasks;

namespace CEF.Common.Converters
{
    public class DecimalJsonConverter : JsonConverter
    {
        public DecimalJsonConverter()
        {
        }

        public override bool CanRead
        {
            get
            {
                return false;
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(decimal) || objectType == typeof(float) || objectType == typeof(double));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (DecimalJsonConverter.IsWholeValue(value))
            {
                writer.WriteRawValue(JsonConvert.ToString(Convert.ToInt64(value)));
            }
            else if (value is decimal)
            {
                var buffer = ((decimal)value).ToString("#0.####################################");
                writer.WriteRawValue(JsonConvert.ToString(Convert.ToDecimal(buffer)));
            }
            else
            {
                writer.WriteRawValue(JsonConvert.ToString(value));
            }
        }

        public static bool IsWholeValue(object value)
        {
            if (value is decimal)
            {
                decimal decimalValue = (decimal)value;
                if (decimalValue - Convert.ToInt64(decimalValue) == 0m)
                    return true;
                int precision = (Decimal.GetBits(decimalValue)[3] >> 16) & 0x000000FF;
                return precision == 0;
            }
            else if (value is float || value is double)
            {
                double doubleValue = (double)value;
                return doubleValue == Math.Truncate(doubleValue);
            }
            return false;
        }
    }


    public class DecimalMsJsonConverter : System.Text.Json.Serialization.JsonConverter<decimal>
    {
        public override decimal Read(
             ref System.Text.Json.Utf8JsonReader reader,
             Type typeToConvert,
             System.Text.Json.JsonSerializerOptions options)
        { 
            if(reader.TryGetDecimal(out var value))
                return value;
            return default(decimal);
        }

        public override void Write(
            System.Text.Json.Utf8JsonWriter writer,
            decimal value,
            System.Text.Json.JsonSerializerOptions options)
        {
            if (DecimalJsonConverter.IsWholeValue(value))
            {
                writer.WriteRawValue(JsonConvert.ToString(Convert.ToInt64(value)));
            }
            else 
            {
                var buffer = ((decimal)value).ToString("#0.####################################");
                writer.WriteRawValue(JsonConvert.ToString(Convert.ToDecimal(buffer)));
            } 
        }
    }
}