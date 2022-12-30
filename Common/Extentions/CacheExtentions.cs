using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Extentions
{
    /// <summary>
    /// 缓存拓展
    /// </summary>
    public static class CacheExtentions
    {
        /// <summary>
        /// 获取缓存，若不存在则设置缓存
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="distributedCache">分布式缓存对象</param>
        /// <param name="cacheKey">缓存键值</param>
        /// <param name="getFromDb">从数据持久层获取数据</param>
        /// <param name="options">缓存参数</param>
        /// <returns></returns>
        public static async Task<T> GetOrSetObjectAsync<T>(this IDistributedCache distributedCache, string cacheKey, Func<Task<T>> getFromDb, DistributedCacheEntryOptions options = null)
        {
            options ??= new DistributedCacheEntryOptions();

            T resObj;

            var body = await distributedCache.GetStringAsync(cacheKey);
            if (string.IsNullOrEmpty(body))
            {
                resObj = await getFromDb();
                var value = SerializeObject(resObj);
                if (!string.IsNullOrEmpty(value))
                    await distributedCache.SetStringAsync(cacheKey, value, options);
            }
            else
            {
                resObj = DeserializeObject<T>(body);
            }
            return resObj;
        }
        /// <summary>
        /// 获取缓存，若不存在则设置缓存
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="distributedCache">分布式缓存对象</param>
        /// <param name="cacheKey">缓存键值</param>
        /// <param name="getFromDb">从数据持久层获取数据</param>
        /// <param name="options">缓存参数</param>
        /// <returns></returns>
        public static T GetOrSetObject<T>(this IDistributedCache distributedCache, string cacheKey, Func<T> getFromDb, DistributedCacheEntryOptions options = null)
        {
            options ??= new DistributedCacheEntryOptions();

            T resObj;

            var body = distributedCache.GetString(cacheKey);
            if (string.IsNullOrEmpty(body))
            {
                resObj = getFromDb();
                var value = SerializeObject(resObj);
                if (!string.IsNullOrEmpty(value))
                    distributedCache.SetString(cacheKey, value, options);
            }
            else
            {
                try
                {
                    resObj = DeserializeObject<T>(body);
                }
                catch
                {
                    //不是json类型的直接放行,转换会报错
                    return (T)Convert.ChangeType(body, typeof(T));
                }
            }
            return resObj;
        }

        /// <summary>
        /// 获取缓存，若不存在则设置缓存
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="cache">分布式缓存对象</param>
        /// <param name="cacheKey">缓存键值</param>
        /// <param name="getFromDb">从数据持久层获取数据</param>
        /// <param name="options">缓存参数</param>
        /// <returns></returns>
        //public static async Task<T> GetOrSetObjectAsync<T>(this IMemoryCache cache, string cacheKey, Func<Task<T>> getFromDb, MemoryCacheEntryOptions options = null)
        //{
        //    options ??= new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15) };
        //    T resObj = cache.Get<T>(cacheKey);
        //    if (resObj == null)
        //    {
        //        resObj = await getFromDb();
        //        if (!EqualityComparer<T>.Default.Equals(resObj, default(T)))
        //            cache.Set(cacheKey, resObj, options);
        //    }
        //    return resObj;
        //}
        /// <summary>
        /// 获取缓存，若不存在则设置缓存
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="cache">分布式缓存对象</param>
        /// <param name="cacheKey">缓存键值</param>
        /// <param name="getFromDb">从数据持久层获取数据</param>
        /// <param name="options">缓存参数</param>
        /// <returns></returns>
        public static T GetOrSetObject<T>(this IMemoryCache cache, string cacheKey, Func<T> getFromDb, MemoryCacheEntryOptions options = null)
        {
            options ??= new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15) };
            if (cache.TryGetValue<T>(cacheKey, out var resObj))
                return resObj;
            resObj = getFromDb();
            var value = SerializeObject(resObj);
            if (!string.IsNullOrEmpty(value))
                cache.Set(cacheKey, resObj, options);
            return resObj;
        }
        //public static IEnumerable<T> GetOrSetList<T>(this IMemoryCache cache, string cacheKey, Func<IEnumerable<T>> getFromDb, MemoryCacheEntryOptions options = null)
        //{
        //    options ??= new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15) };
        //    if (cache.TryGetValue<IEnumerable<T>>(cacheKey, out var resObj))
        //        return resObj;
        //    resObj = getFromDb();
        //    var value = SerializeObject(resObj);
        //    if (!string.IsNullOrEmpty(value))
        //        cache.Set(cacheKey, resObj, options);
        //    return resObj;
        //}

        public static async Task<IEnumerable<T>> GetOrSetListAsync<T>(this IMemoryCache cache, string cacheKey, Func<Task<IEnumerable<T>>> getFromDb, MemoryCacheEntryOptions options = null)
        {
            options ??= new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15) };
            if (cache.TryGetValue<IEnumerable<T>>(cacheKey, out var resObj))
                return resObj;
            resObj = await getFromDb();
            var value = SerializeObject(resObj);
            if (!string.IsNullOrEmpty(value))
                cache.Set(cacheKey, resObj, options);
            return resObj;
        }
        /// <summary>
        /// 获取缓存，若不存在则设置缓存
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="cache">分布式缓存对象</param>
        /// <param name="cacheKey">缓存键值</param>
        /// <param name="getFromDb">从数据持久层获取数据</param>
        /// <param name="options">缓存参数</param>
        /// <returns></returns>
        public static async Task<T> GetOrSetObjectAsync<T>(this IMemoryCache cache, string cacheKey, Func<Task<T>> getFromDb, MemoryCacheEntryOptions options = null)
        {
            options ??= new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15) };
            if (cache.TryGetValue<T>(cacheKey, out var resObj))
                return resObj;
            resObj = await getFromDb();
            var value = SerializeObject(resObj);
            if (!string.IsNullOrEmpty(value))
                cache.Set(cacheKey, resObj, options);
            return resObj;
        }
        /// <summary>
        /// 获取缓存
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="distributedCache">分布式缓存对象</param>
        /// <param name="cacheKey">缓存键值</param>
        /// <returns></returns>
        public static async Task<T> GetAsync<T>(this IDistributedCache distributedCache, string cacheKey)
        {
            var body = await distributedCache.GetStringAsync(cacheKey);
            if (string.IsNullOrEmpty(body))
            {
                return default;
            }
            else
            {
                return DeserializeObject<T>(body);
            }
        }

        /// <summary>
        /// 获取缓存
        /// </summary>
        /// <typeparam name="T">缓存对象类型</typeparam>
        /// <param name="distributedCache">分布式缓存对象</param>
        /// <param name="cacheKey">缓存键值</param>
        /// <returns></returns>
        public static T Get<T>(this IDistributedCache distributedCache, string cacheKey)
        {
            var body = distributedCache.GetString(cacheKey);
            if (string.IsNullOrEmpty(body))
            {
                return default;
            }
            else
            {
                return DeserializeObject<T>(body);
            }
        }

        /// <summary>
        /// 设置缓存
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="distributedCache"></param>
        /// <param name="cacheKey"></param>
        /// <param name="value"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static async Task SetObjectAsync<T>(this IDistributedCache distributedCache, string cacheKey, T value, DistributedCacheEntryOptions options = null)
        {
            options ??= new DistributedCacheEntryOptions();

            await distributedCache.SetStringAsync(cacheKey, SerializeObject(value), options);
        }

        public static string SerializeObject(object obj)
        {
            if (obj == null)
            {
                return null;
            }

            var type = obj.GetType();
            if (type.IsEnum)
            {
                return Convert.ChangeType(obj, typeof(int)).ToString();
            }
            else if (obj.GetType().IsSimple())
            {
                if (type == typeof(DateTime) || type == typeof(DateTime?))
                {
                    return ((DateTime)obj).ToString("o");
                }
                else if (type == typeof(DateTimeOffset) || type == typeof(DateTimeOffset?))
                {
                    return ((DateTimeOffset)obj).ToString("o");
                }
                return obj?.ToString();
            }
            else
            {
                return obj.ToJson(false);
            }
        }

        public static T DeserializeObject<T>(string json)
        {
            if (typeof(T).IsEnum)
            {
                return (T)Enum.ToObject(typeof(T), json.ToInt());
                //return (T)Convert.ChangeType(json.ToInt(), typeof(T));
            }
            else if (typeof(T).IsSimple())
            {
                if (typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTime?))
                {
                    return (T)(object)DateTime.Parse(json);
                }
                else if (typeof(T) == typeof(DateTimeOffset) || typeof(T) == typeof(DateTimeOffset?))
                {
                    return (T)(object)DateTimeOffset.Parse(json);
                }
                else if (typeof(T) == typeof(Guid) || typeof(T) == typeof(Guid?))
                {
                    return (T)(object)Guid.Parse(json);
                }
                var t = typeof(T);
                if (t.IsGenericType && t.GetGenericTypeDefinition().Equals(typeof(Nullable<>)))
                    t = Nullable.GetUnderlyingType(t);
                return (T)Convert.ChangeType(json, t);
            }
            else
            {
                return json.ToObject<T>(false);
            }
        }
    }
}
