using CEF.Common.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System;

namespace CEF.Common.Extentions
{
    /// <summary>
    /// 拓展类
    /// </summary>
    public static class JsonExtention
    {
        static JsonExtention()
        {
            JsonConvert.DefaultSettings = () => DefaultJsonSetting;
        }
        public static JsonSerializerSettings DefaultJsonSetting = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver(),
            DateFormatHandling = DateFormatHandling.MicrosoftDateFormat,
            DateFormatString = "yyyy-MM-dd HH:mm:ss.fff"
        };

        /// <summary>
        /// 将对象序列化成Json字符串
        /// </summary>
        /// <param name="obj">需要序列化的对象</param>
        /// <returns></returns>
        public static string ToJson(this object obj)
        {
            var jsonSerializerSettings = new Newtonsoft.Json.JsonSerializerSettings() { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            jsonSerializerSettings.Converters.Add(new DecimalJsonConverter());
            return JsonConvert.SerializeObject(obj, jsonSerializerSettings);
        }

        /// <summary>
        /// 将Json字符串反序列化为对象
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="jsonStr">Json字符串</param>
        /// <returns></returns>
        public static T ToObject<T>(this string jsonStr)
        {
            if (jsonStr.IsNullOrEmpty()) return default(T);
            return JsonConvert.DeserializeObject<T>(jsonStr);
        }

        /// <summary>
        /// 将Json字符串反序列化为对象
        /// </summary>
        /// <param name="jsonStr">json字符串</param>
        /// <param name="type">对象类型</param>
        /// <returns></returns>
        public static object ToObject(this string jsonStr, Type type)
        {
            return JsonConvert.DeserializeObject(jsonStr, type);
        }

        /// <summary>
        /// Iso配置，时间格式为ISO-8601
        /// </summary>
        /// <remarks>系统内部需要可读性时采用此序列化，例如缓存、日志打印等</remarks>
        public static readonly JsonSerializerSettings IsoSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore, 
            FloatParseHandling = FloatParseHandling.Decimal,
            ContractResolver = new DefaultContractResolver(),
            Converters = new JsonConverter[] { new IsoDateTimeConverter(), new DecimalJsonConverter() }
        }; 
        /// <summary>
        /// 时间戳配置
        /// </summary>
        /// <remarks>接口数据传输的时候使用此序列化</remarks>
        public static readonly JsonSerializerSettings TimestampSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            FloatParseHandling = FloatParseHandling.Decimal,
            ContractResolver = new DefaultContractResolver(),
            Converters = new JsonConverter[] { new TimestampConverter(), new DecimalJsonConverter() }
        };

        /// <summary>
        /// 序列化
        /// </summary>
        /// <param name="obj">对象</param>
        /// <param name="timestamp">是否序列化成时间戳，默认true</param>
        /// <param name="settings">自定义配置</param>
        /// <returns></returns>
        public static string ToJson(this object obj, bool timestamp = true, JsonSerializerSettings settings = null)
        {
            settings = settings ?? (timestamp ? TimestampSettings : IsoSettings);
            return JsonConvert.SerializeObject(obj, settings);
        }

        /// <summary>
        /// 反序列化
        /// </summary>
        /// <param name="json">json</param>
        /// <param name="timestamp">是否序列化成时间戳，默认true</param>
        /// <param name="settings">自定义配置</param>
        /// <returns></returns>
        public static T ToObject<T>(this string json, bool timestamp = true, JsonSerializerSettings settings = null)
        {
            settings = settings ?? (timestamp ? TimestampSettings : IsoSettings);
            return JsonConvert.DeserializeObject<T>(json, settings);
        }
    }
}
