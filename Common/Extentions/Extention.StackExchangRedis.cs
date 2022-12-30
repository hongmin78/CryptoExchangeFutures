using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Extentions
{
    public static partial class Extention
    {
        public static async Task<T> GetAsync<T>(this IDatabase database, string key)
        {
            var redisValue = await database.StringGetAsync(key);
            return Deserialize<T>(redisValue);
        }

        public static async Task<List<T>> GetAsync<T>(this IDatabase database, List<string> keys)
        {
            var batch = database.CreateBatch();
            List<Task<RedisValue>> taskResult = new List<Task<RedisValue>>();
            foreach (var item in keys)
            {
                var task = batch.StringGetAsync(item);
                taskResult.Add(task);
            }
            batch.Execute();
            List<T> result = new List<T>();
            foreach (var task in taskResult)
            {
                var taskData = await task;
                if (taskData == RedisValue.Null)
                {
                    continue;
                }
                var data = Deserialize<T>(taskData);
                if (data != null)
                {
                    result.Add(data);
                }
            }
            return result;
        }

        public static void Add<T>(this IDatabase database, string key, T data, int expiredMin = 0)
        {
            var jsonData = Serialize(data);
            TimeSpan? timeSpan = null;
            if (expiredMin > 0)
            {
                timeSpan = new TimeSpan(0, expiredMin, 0);
            }
            database.StringSet(key, jsonData, timeSpan);
        }

        public static async void AddAsync<T>(this IDatabase database, string key, T data, TimeSpan timeSpan)
        {
            var jsonData = Serialize(data);
            await database.StringSetAsync(key, jsonData, timeSpan);
        }
        public static void AddAsync<T>(this IDatabase database, string key, T data, int expiredMin = 0)
        {
            var jsonData = Serialize(data);
            TimeSpan? timeSpan = null;
            if (expiredMin > 0)
            {
                timeSpan = new TimeSpan(0, expiredMin, 0);
            }
            database.StringSetAsync(key, jsonData, timeSpan);
        }
        /// <summary>
        /// 设置Key
        /// </summary>
        /// <param name="database"></param>
        /// <param name="dataList"></param>
        /// <param name="expiredMin"></param>
        public static void Add(this IDatabase database, List<KeyValuePair<string, object>> dataList, int expiredMin = 0)
        {
            Dictionary<string, bool> result = new Dictionary<string, bool>();
            var batch = database.CreateBatch();
            TimeSpan? timeSpan = null;
            if (expiredMin > 0)
            {
                timeSpan = new TimeSpan(0, expiredMin, 0);
            }
            foreach (var item in dataList)
            {
                var data = Serialize(item.Value);
              batch.StringSetAsync(item.Key, data, timeSpan);
            }
             batch.Execute();
        }

        /// <summary>
        /// 删除Key
        /// </summary>
        /// <param name="database"></param>
        /// <param name="keys"></param>
        public static void Remove(this IDatabase database, params string[] keys)
        {
            var batch = database.CreateBatch();
            foreach (var item in keys)
            {
                batch.KeyDeleteAsync(item);
            }
            batch.Execute();
        }
        public static async Task<bool> Remove(this IDatabase database, string key)
        {
            return await database.KeyDeleteAsync(key);
        }


        #region byte[] 序列化反序列化
        /// <summary>
        /// 系列化对象
        /// </summary>
        /// <param name="data">数据信息</param>
        /// <returns></returns>
        private static byte[] Serialize(object data)
        {
            if (data == null)
            {
                return null;
            }
            var jsonData = JsonConvert.SerializeObject(data);
            return Encoding.UTF8.GetBytes(jsonData);

        }
        /// <summary>
        /// 反系列化
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="stream">byte数据</param>
        /// <returns></returns>
        private static T Deserialize<T>(byte[] stream)
        {
            if (stream == null)
            {
                return default(T);
            }
            var jsonData = Encoding.UTF8.GetString(stream);
            return JsonConvert.DeserializeObject<T>(jsonData);

        }
        #endregion
    }
}
