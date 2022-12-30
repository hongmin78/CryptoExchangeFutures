using CEF.Common.Converters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace CEF.Common.Extentions
{
    public static partial class Extention
    {
        /// <summary>
        /// Add a parameter
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void AddParameter(this IDictionary<string, object> parameters, string key, string value)
        {
            parameters.Add(key, value);
        }

        /// <summary>
        /// Add a parameter
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void AddParameter(this IDictionary<string, object> parameters, string key, object value)
        {
            parameters.Add(key, value);
        }

        /// <summary>
        /// Add an optional parameter. Not added if value is null
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void AddOptionalParameter(this IDictionary<string, object> parameters, string key, object value)
        {
            if (value != null)
                parameters.Add(key, value);
        }

        /// <summary>
        /// Add an optional parameter. Not added if value is null
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void AddOptionalParameter(this IDictionary<string, string> parameters, string key, string value)
        {
            if (value != null)
                parameters.Add(key, value);
        }
        /// <summary>
        /// Convert a dictionary to formdata string
        /// </summary>
        /// <param name="dict"></param>
        /// <returns></returns>
        public static string ToFormData(this IDictionary<string, object> dict)
        {
            var parameters = new SortedDictionary<string, object>(dict);
            var formData = HttpUtility.ParseQueryString(string.Empty);
            foreach (var kvp in parameters)
            {
                if (kvp.Value.GetType().IsArray)
                {
                    var array = (Array)kvp.Value;
                    foreach (var value in array)
                        formData.Add(kvp.Key, value.ToString());
                }
                else if (kvp.Value.GetType().IsSimple())
                    formData.Add(kvp.Key, kvp.Value.ToString());
                else
                    formData.Add(kvp.Key, JsonConvert.SerializeObject(kvp.Value, new DecimalJsonConverter()));
            }
            return formData.ToString();
        }
        /// <summary>
        /// Create a query string of the specified parameters
        /// </summary>
        /// <param name="parameters">The parameters to use</param>
        /// <param name="urlEncodeValues">Whether or not the values should be url encoded</param>
        /// <param name="serializationType">How to serialize array parameters</param>
        /// <returns></returns>
        public static string CreateParamString(this IDictionary<string, object> parameters, bool urlEncodeValues, ArrayParametersSerialization serializationType)
        {
            //var uriString = "";
            //var arraysParameters = parameters.Where(p => p.Value.GetType().IsArray).ToList();
            //foreach (var arrayEntry in arraysParameters)
            //{
            //    if (serializationType == ArrayParametersSerialization.Array)
            //        uriString += $"{string.Join("&", ((object[])(urlEncodeValues ? WebUtility.UrlEncode(arrayEntry.Value.ToString()) : arrayEntry.Value)).Select(v => $"{arrayEntry.Key}[]={v}"))}&";
            //    else
            //    {
            //        var array = (Array)arrayEntry.Value;
            //        uriString += string.Join("&", array.OfType<object>().Select(a => $"{arrayEntry.Key}={WebUtility.UrlEncode(a.ToString())}"));
            //        uriString += "&";
            //    }
            //}

            //uriString += $"{string.Join("&", parameters.Where(p => !p.Value.GetType().IsArray).Select(s => $"{s.Key}={(urlEncodeValues ? WebUtility.UrlEncode(s.Value.ToString()) : s.Value)}"))}";
            //uriString = uriString.TrimEnd('&');
            //return uriString;
            var uriString = "";
            uriString += $"{string.Join("&", parameters.Select(s => ParamString(s, urlEncodeValues, serializationType)))}";
            uriString = uriString.TrimEnd('&');
            return uriString;
        }

        static string ParamString(KeyValuePair<string, object> kvp, bool urlEncodeValues, ArrayParametersSerialization serializationType)
        {
            if (!kvp.Value.GetType().IsArray && kvp.Value.GetType() != typeof(Dictionary<string, bool>))
            {
                return $"{kvp.Key}={(urlEncodeValues ? WebUtility.UrlEncode(kvp.Value.ToString()) : (kvp.Value.GetType() == typeof(bool) ? (kvp.Value.ToString().ToLower()) : kvp.Value))}";
            }
            else if (serializationType == ArrayParametersSerialization.Array)
                return $"{string.Join("&", ((object[])(urlEncodeValues ? WebUtility.UrlEncode(kvp.Value.ToString()) : kvp.Value)).Select(v => $"{kvp.Key}[]={v.ToJson()}"))}&";
            else
            {
                if (kvp.Value.GetType() == typeof(Dictionary<string, bool>))
                {
                    var array = (Dictionary<string, bool>)kvp.Value;
                    return string.Join("&", array.OfType<KeyValuePair<string, bool>>().Select(a => $"{kvp.Key}={WebUtility.UrlEncode(a.Key + "," + a.Value.ToString().ToLower())}"));
                }
                else
                {
                    var array = (Array)kvp.Value;
                    return string.Join("&", array.OfType<object>().Select(a => $"{kvp.Key}={WebUtility.UrlEncode(a.ToString())}"));
                }
            }
        }

        public static string HMACSHA256Sign(this IDictionary<string, object> signParameters, string securityKey, bool isBody = false)
        {
            signParameters = signParameters.OrderBy(kv => kv.Key).ToDictionary(k => k.Key, k => k.Value);
            var paramString = string.Empty;
            if (isBody)
                paramString = signParameters.ToFormData();
            else
                paramString = signParameters.CreateParamString(false, ArrayParametersSerialization.Array);
            var hmacSha = new HMACSHA256(Encoding.UTF8.GetBytes(securityKey));
            var hash = hmacSha.ComputeHash(Encoding.UTF8.GetBytes(paramString)).ToArray();
            var sign = Convert.ToBase64String(hash);
            Debug.WriteLine(paramString);
            Debug.WriteLine(sign);
            return sign;
        }

        public static string Sign(this IDictionary<string, object> signParameters, string securityKey)
        {
            signParameters = signParameters.Where(kv => kv.Key.ToLower() != "mac").OrderBy(kv => kv.Key).ToDictionary(k => k.Key, k => k.Value);
            signParameters.Add("k", securityKey);
            var paramString = signParameters.CreateParamString(false, ArrayParametersSerialization.MultipleValues);
            var sign = Md5Hex(paramString).ToLower();
            return sign;
        }

        private static string Md5Hex(string data)
        {
            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
            byte[] dataHash = md5.ComputeHash(Encoding.UTF8.GetBytes(data));
            StringBuilder sb = new StringBuilder();
            foreach (byte b in dataHash)
            {
                sb.Append(b.ToString("x2").ToLower());
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Define how array parameters should be send
    /// </summary>
    public enum ArrayParametersSerialization
    {
        /// <summary>
        /// Send multiple key=value for each entry
        /// </summary>
        MultipleValues,
        /// <summary>
        /// Create an []=value array
        /// </summary>
        Array
    }
}
