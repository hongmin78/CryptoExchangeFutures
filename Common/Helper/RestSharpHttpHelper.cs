using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Helper
{
    public class RestSharpHttpHelper
    {
        #region 暴露执行方法
        /// <summary>
        /// 组装Client，Request，并执行Http请求
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="baseUrl">基地址</param>
        /// <param name="relativeUrl">相对地址</param>
        /// <param name="method">请求类型</param>
        /// <param name="lstParam">Get/Put/Delete/Post等参数</param>
        /// <param name="obj">post请求体</param>
        /// <returns></returns>
        public static async Task<ResponseMessage<T>> RestAction<T>(string baseUrl, string relativeUrl, Method method = Method.Get, List<RestParam> lstParam = null)
        {
            var client = new RestClient(baseUrl);
            return await RestMethod<T>(client, InstallRequest(relativeUrl, method, lstParam));
        }
        public static async Task<ResponseMessage<T>> RestAction<T>(string baseUrl, string relativeUrl, IDictionary<string, object> parameters, Method method = Method.Get)
        {
            var lstParam = parameters.Select(e => new RestParam() { ParamType = EmParType.Param, Key = e.Key, Value = e.Value });
            var client = new RestClient(baseUrl);
            return await RestMethod<T>(client, InstallRequest(relativeUrl, method, lstParam));
        }
        /// <summary>
        /// 异步请求无返回值
        /// </summary>
        /// <param name="baseUrl"></param>
        /// <param name="relativeUrl"></param>
        /// <param name="method"></param>
        /// <param name="lstParam"></param>
        /// <param name="obj"></param>
        public static async Task<string> RestAction(string baseUrl, string relativeUrl, Method method = Method.Get, List<RestParam> lstParam = null)
        {
            var client = new RestClient(baseUrl);
            return await RestMethod(client, InstallRequest(relativeUrl, method, lstParam));
        }
        public static async Task<string> RestAction(string baseUrl, string relativeUrl, IDictionary<string, object> parameters, Method method = Method.Get)
        {
            var lstParam = parameters.Select(e => new RestParam() { ParamType = EmParType.Param, Key = e.Key, Value = e.Value });
            var client = new RestClient(baseUrl);
            return await RestMethod(client, InstallRequest(relativeUrl, method, lstParam));
        }
        #endregion

        #region 底层调用，并不暴露方法
        /// <summary>
        /// Http请求
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="client"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        static async Task<ResponseMessage<T>> RestMethod<T>(RestClient client, RestRequest request)
        {
            RestResponse restResponse = await client.ExecuteAsync(request);
            try
            {
                if (restResponse.StatusCode == System.Net.HttpStatusCode.InternalServerError) 
                    throw new Exception(restResponse.Content); 

                var response = new ResponseMessage<T>() { success = true };
                if (!string.IsNullOrWhiteSpace(restResponse.Content))
                    response.data = JsonConvert.DeserializeObject<T>(restResponse.Content);
                return response;
                //return restResponse == null ? new ResponseMessage<T>() :
                //    string.IsNullOrWhiteSpace(restResponse.Content) ? new ResponseMessage<T>() :
                //    JsonConvert.DeserializeObject<ResponseMessage<T>>(restResponse.Content);
            }
            catch (Exception ex)
            {
                return new ResponseMessage<T>() { success = false, error = ex.Message  };
            }
        }

        /// <summary>
        /// 无返回值异步调用
        /// </summary>
        /// <param name="client"></param>
        /// <param name="request"></param>
        static async Task<string> RestMethod(RestClient client, RestRequest request)
        {
            //ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, errors) =>
            //{
            //    return true;
            //    //return errors == SslPolicyErrors.None;
            //};
            //client.RemoteCertificateValidationCallback = new RemoteCertificateValidationCallback(OnRemoteCertificateValidationCallback);
            var response = await client.ExecuteAsync(request);
            return response.Content;
        } 
        static bool OnRemoteCertificateValidationCallback( Object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true; 
        }
        /// <summary>
        /// 组装Request
        /// </summary>
        /// <param name="relativeUrl"></param>
        /// <param name="method"></param>
        /// <param name="lstParam"></param>
        /// <returns></returns>
        static RestRequest InstallRequest(string relativeUrl, Method method = Method.Get, IEnumerable<RestParam> lstParam = null)
        {
            var request = string.IsNullOrEmpty(relativeUrl) ? new RestRequest("/", method) : new RestRequest(relativeUrl, method);
            if (lstParam != null)
            {
                foreach (RestParam p in lstParam)
                {
                    switch (p.ParamType)
                    {
                        case EmParType.UrlSegment:
                            //request.AddUrlSegment(Parameter.CreateParameter(p.Key, p.Value, ParameterType.UrlSegment));
                            request.AddParameter(Parameter.CreateParameter(p.Key, p.Value, ParameterType.UrlSegment));
                            break;
                        case EmParType.Param:
                            request.AddParameter(Parameter.CreateParameter(p.Key, p.Value, ParameterType.QueryString));
                            break;
                        case EmParType.Body:
                            request.AddJsonBody(p.Value);
                            break;
                        case EmParType.GetOrPost:
                            request.AddParameter(Parameter.CreateParameter(p.Key, p.Value, ParameterType.GetOrPost));
                            break;
                        default:
                            break;
                    }
                }
            }
            return request;
        }
        #endregion

        #region async

        #region 暴露执行方法
        /// <summary>
        /// 组装Client，Request，并执行Http请求
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="baseUrl">基地址</param>
        /// <param name="relativeUrl">相对地址</param>
        /// <param name="method">请求类型</param>
        /// <param name="lstParam">Get/Put/Delete/Post等参数</param>
        /// <param name="obj">post请求体</param>
        /// <returns></returns>
        public static async Task<ResponseMessage<T>> RestActionAsync<T>(string baseUrl, string relativeUrl, Method method = Method.Get, List<RestParam> lstParam = null)
        {
            var client = new RestClient(baseUrl);
            return await RestMethodAsync<T>(client, InstallRequest(relativeUrl, method, lstParam));
        }
        public static async Task<ResponseMessage<T>> RestActionAsync<T>(string baseUrl, string relativeUrl, IDictionary<string, object> parameters, Method method = Method.Get)
        {
            var lstParam = parameters.Select(e => new RestParam() { ParamType = EmParType.Param, Key = e.Key, Value = e.Value });
            var client = new RestClient(baseUrl);
            return await RestMethodAsync<T>(client, InstallRequest(relativeUrl, method, lstParam));
        }

        /// <summary>
        /// post json请求
        /// </summary>
        /// <param name="baseUrl"></param>
        /// <param name="json"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        public static async Task<RestResponse> RestPostActionAsync(string baseUrl, string json, Method method = Method.Post)
        {
            return await new RestClient(baseUrl).ExecuteAsync(new RestRequest("/", method).AddStringBody(json, DataFormat.Json));
        }
        public static async Task<RestResponse> RestPostActionAsync<T>(string baseUrl, string relativeUrl, Dictionary<string, string> headers, T data, Method method = Method.Post) where T : class
        {
            var request = string.IsNullOrEmpty(relativeUrl) ? new RestRequest("/", method) : new RestRequest(relativeUrl, method);         
            var client = new RestClient(baseUrl);
            client.AddDefaultHeaders(headers);
            return await client.ExecuteAsync(request.AddJsonBody(data));
        }
        /// <summary>
        /// 异步请求无返回值
        /// </summary>
        /// <param name="baseUrl"></param>
        /// <param name="relativeUrl"></param>
        /// <param name="method"></param>
        /// <param name="lstParam"></param>
        /// <param name="obj"></param>
        public async static Task<string> RestActionAsync(string baseUrl, string relativeUrl, Method method = Method.Get, List<RestParam> lstParam = null)
        {
            var client = new RestClient(baseUrl);
            return await RestMethodAsync(client, InstallRequest(relativeUrl, method, lstParam));
        }
        public static async Task<string> RestActionAsync(string baseUrl, string relativeUrl, IDictionary<string, object> parameters, Method method = Method.Get)
        {
            var lstParam = parameters.Select(e => new RestParam() { ParamType = EmParType.Param, Key = e.Key, Value = e.Value });
            var client = new RestClient(baseUrl);
            return await RestMethodAsync(client, InstallRequest(relativeUrl, method, lstParam));
        }
        public static async Task<string> RestActionAsync(string baseUrl, string relativeUrl, Dictionary<string, string> headers, IDictionary<string, object> parameters, Method method = Method.Get)
        {
            var lstParam = parameters.Select(e => new RestParam() { ParamType = EmParType.Param, Key = e.Key, Value = e.Value });
            var client = new RestClient(baseUrl);
            client.AddDefaultHeaders(headers);
            return await RestMethodAsync(client, InstallRequest(relativeUrl, method, lstParam));
        }

        public static async Task<string> RestActionAsync(string baseUrl, string relativeUrl, Dictionary<string, string> headers, List<RestParam> lstParam, Method method = Method.Get)
        {
            var client = new RestClient(baseUrl);
            client.AddDefaultHeaders(headers);
            var request = InstallRequest(relativeUrl, method, lstParam);
            return await RestMethodAsync(client, request);
        }
        #endregion

        #region 底层调用，并不暴露方法
        /// <summary>
        /// Http请求
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="client"></param>
        /// <param name="request"></param>
        /// <returns></returns>
        static async Task<ResponseMessage<T>> RestMethodAsync<T>(RestClient client, RestRequest request)
        {
            var restResponse = await client.ExecuteAsync(request);
            try
            {
                if (restResponse.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                    throw new Exception(restResponse.Content);

                var response = new ResponseMessage<T>() { success = true };
                if (!string.IsNullOrWhiteSpace(restResponse.Content))
                    response.data = JsonConvert.DeserializeObject<T>(restResponse.Content);
                return response;
                //return restResponse == null ? new ResponseMessage<T>() :
                //    string.IsNullOrWhiteSpace(restResponse.Content) ? new ResponseMessage<T>() :
                //    JsonConvert.DeserializeObject<ResponseMessage<T>>(restResponse.Content);
            }
            catch (Exception ex)
            {
                return new ResponseMessage<T>() { success = false, error = ex.Message };
            }
        }

        /// <summary>
        /// 无返回值异步调用
        /// </summary>
        /// <param name="client"></param>
        /// <param name="request"></param>
        static async Task<string> RestMethodAsync(RestClient client, RestRequest request)
        {
            var response = await client.ExecuteAsync(request);
            return response.Content;
        }

        #endregion 
        #endregion
    }

    public enum EmParType { UrlSegment, Param, Body, GetOrPost }

    public class RestParam
    {
        public string Key { set; get; }
        public object Value { set; get; }
        public EmParType ParamType { set; get; }
    }

    public class ResponseMessage<T>
    {
        public bool success { set; get; }

        public T data { set; get; }

        public string error { set; get; }
    }
}

