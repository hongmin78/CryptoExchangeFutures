using CEF.Common.Converters;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Helper
{
    public class StackRedisHelper : IDisposable
    {
        #region 配置属性   基于 StackExchange.Redis 封装
        //连接串 （注：IP:端口,属性=,属性=)
        public string ConnectionString { set; get; }
        #endregion

        #region 管理器对象

        /// <summary>
        /// 获取redis操作类对象
        /// </summary>
        private static StackRedisHelper _ClusterRedis;
        private static StackRedisHelper _StackRedis;
        private static object _locker_StackRedis = new object();
        public static StackRedisHelper Current
        {
            get
            {
                if (_StackRedis == null)
                {
                    lock (_locker_StackRedis)
                    {
                        _StackRedis = _StackRedis ?? new StackRedisHelper();
                        return _StackRedis;
                    }
                }
                return _StackRedis;
            }
        }

        public static StackRedisHelper CurrentCluster
        {
            get
            {
                if (_ClusterRedis == null)
                {
                    lock (_locker_StackRedis)
                    {
                        _ClusterRedis = _ClusterRedis ?? new StackRedisHelper();
                        return _ClusterRedis;
                    }
                }
                return _ClusterRedis;
            }
        }

        public static void Initialization(string connectionString, string clusterConnectionString)
        {
            _StackRedis = new StackRedisHelper();
            _StackRedis.ConnectionString = connectionString;

            _ClusterRedis = new StackRedisHelper();
            _ClusterRedis.ConnectionString = clusterConnectionString;
        }

        /// <summary>
        /// 获取并发链接管理器对象
        /// </summary>
        private ConnectionMultiplexer _redis;
        private static object _locker = new object();
        public ConnectionMultiplexer Manager
        {
            get
            {
                if (_redis == null)
                {
                    lock (_locker)
                    {
                        _redis = _redis ?? GetManager(this.ConnectionString);
                        return _redis;
                    }
                }

                return _redis;
            }
        }

        /// <summary>
        /// 获取链接管理器
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public ConnectionMultiplexer GetManager(string connectionString)
        {
            return ConnectionMultiplexer.Connect(GetConfiguration(connectionString));
        }

        private ConfigurationOptions GetConfiguration(string config)
        {
            var connectionOption = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                KeepAlive = 30,
                ConnectTimeout = 60000,
                SyncTimeout = 60000,
                ResponseTimeout = 60000,
                HighPrioritySocketThreads = true,
                AllowAdmin = true,
                //PreserveAsyncOrder = false
            };
            connectionOption.ReconnectRetryPolicy = new ExponentialRetry(5000);
            var clients = config.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var item in clients)
            {
                var array = item.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (array.Length > 1)
                {
                    string password = array[1].Replace("password=", "");
                    string hostport = array[0];
                    connectionOption.EndPoints.Add(hostport);
                    connectionOption.Password = password;
                }
                else
                    connectionOption.EndPoints.Add(item);
            }
            return connectionOption;
        }

        /// <summary>
        /// 获取操作数据库对象
        /// </summary>
        /// <returns></returns>
        public IDatabase GetDb()
        {
            return Manager.GetDatabase();
        }
        #endregion

        #region 操作方法

        #region string 操作

        /// <summary>
        /// 根据Key移除
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<bool> Remove(string key)
        {
            var db = this.GetDb();

            return await db.KeyDeleteAsync(key);
        }

        /// <summary>
        /// 根据key获取string结果
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<string> GetAsync(string key)
        {
            var db = this.GetDb();
            return await db.StringGetAsync(key);
        }
        public string Get(string key)
        {
            var db = this.GetDb();
            return db.StringGet(key);
        }
        /// <summary>
        /// 根据key获取string中的对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<T> GetAsync<T>(string key)
        {
            var t = default(T);

            var _str = await this.GetAsync(key);
            if (string.IsNullOrWhiteSpace(_str)) { return t; }

            t = JsonConvert.DeserializeObject<T>(_str);
            return t;
        }
        public T Get<T>(string key)
        {
            var t = default(T);

            var _str = this.Get(key);
            if (string.IsNullOrWhiteSpace(_str)) { return t; }

            t = JsonConvert.DeserializeObject<T>(_str);
            return t;
        }
        /// <summary>
        /// 存储string数据
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expireMinutes"></param>
        /// <returns></returns>
        public async Task<bool> SetAsync(string key, string value, int expireMinutes = 0)
        {
            var db = this.GetDb();
            if (expireMinutes > 0)
            {
                return db.StringSet(key, value, TimeSpan.FromMinutes(expireMinutes));
            }
            return await db.StringSetAsync(key, value);
        }
        public bool Set(string key, string value, int expireMinutes = 0)
        {
            var db = this.GetDb();
            if (expireMinutes > 0)
            {
                return db.StringSet(key, value, TimeSpan.FromMinutes(expireMinutes));
            }
            return db.StringSet(key, value);
        }
        /// <summary>
        /// 存储对象数据到string
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expireMinutes"></param>
        /// <returns></returns>
        public async Task<bool> SetAsync<T>(string key, T value, int expireMinutes = 0)
        { 
            var jsonOption = new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                FloatParseHandling = FloatParseHandling.Decimal
            };
            jsonOption.Converters.Add(new DecimalJsonConverter());
            var _str = JsonConvert.SerializeObject(value, jsonOption);
            if (string.IsNullOrWhiteSpace(_str)) { return false; }

            return await this.SetAsync(key, _str, expireMinutes);
        }
        public bool Set<T>(string key, T value, int expireMinutes = 0)
        {
            var jsonOption = new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                FloatParseHandling = FloatParseHandling.Decimal
            };
            jsonOption.Converters.Add(new DecimalJsonConverter());
            var _str = JsonConvert.SerializeObject(value, jsonOption);
            if (string.IsNullOrWhiteSpace(_str)) { return false; }

            return this.Set(key, _str, expireMinutes);
        }
        /// <summary>
        /// 是否存在key
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<bool> KeyExists(string key)
        {
            var db = this.GetDb();
            return await db.KeyExistsAsync(key);
        }

        #endregion

        #region hash操作

        /// <summary>
        /// 是否存在hash的列
        /// </summary>
        /// <param name="key"></param>
        /// <param name="filedKey"></param>
        /// <returns></returns>
        public async Task<bool> HashFieldExists(string key, string filedKey)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(filedKey)) { return false; }

            var result = await this.HashFieldsExists(key, new Dictionary<string, bool> { { filedKey, false } });
            return result[filedKey];
        }

        /// <summary>
        /// 是否存在hash的列集合
        /// </summary>
        /// <param name="key"></param>
        /// <param name="dics"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, bool>> HashFieldsExists(string key, Dictionary<string, bool> dics)
        {
            if (dics.Count <= 0) { return dics; }

            var db = this.GetDb();

            var newDics = new Dictionary<string, bool>();
            foreach (var fieldKey in dics.Keys)
            {
                newDics.Add(fieldKey, await db.HashExistsAsync(key, fieldKey));
            }
            return newDics;
        }

        /// <summary>
        /// 设置hash
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="filedKey"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public async Task<long> SetOrUpdateHashsField<T>(string key, string filedKey, T t, bool isAdd = true)
        {
            return await this.SetOrUpdateHashsFieldsAsync<T>(key, new Dictionary<string, T> { { filedKey, t } }, isAdd);
        }

        /// <summary>
        /// 设置hash集合，添加和更新操作
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="dics"></param>
        /// <returns></returns>
        public async Task<long> SetOrUpdateHashsFieldsAsync<T>(string key, Dictionary<string, T> dics, bool isAdd = true)
        {
            var result = 0L;
            var jsonOption = new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                FloatParseHandling = FloatParseHandling.Decimal
            };
            jsonOption.Converters.Add(new DecimalJsonConverter());
            var db = this.GetDb();
            foreach (var fieldKey in dics.Keys)
            {
                var item = dics[fieldKey];
                var _str = JsonConvert.SerializeObject(item, jsonOption);
                result += await db.HashSetAsync(key, fieldKey, _str) ? 1 : 0;
                if (!isAdd) { result++; }
            }
            return result;
        }

        public long SetOrUpdateHashsFields<T>(string key, Dictionary<string, T> dics, bool isAdd = true)
        {
            var result = 0L;
            var jsonOption = new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                FloatParseHandling = FloatParseHandling.Decimal
            };
            jsonOption.Converters.Add(new DecimalJsonConverter());
            var db = this.GetDb();
            foreach (var fieldKey in dics.Keys)
            {
                var item = dics[fieldKey];
                var _str = JsonConvert.SerializeObject(item, jsonOption);
                result += db.HashSet(key, fieldKey, _str) ? 1 : 0;
                if (!isAdd) { result++; }
            }
            return result;
        }

        /// <summary>
        /// 移除hash的列
        /// </summary>
        /// <param name="key"></param>
        /// <param name="filedKey"></param>
        /// <returns></returns>
        public async Task<bool> RemoveHashField(string key, string filedKey)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(filedKey)) { return false; }

            var result = await this.RemoveHashFields(key, new Dictionary<string, bool> { { filedKey, false } });
            return result[filedKey];
        }

        /// <summary>
        /// 异常hash的列集合
        /// </summary>
        /// <param name="key"></param>
        /// <param name="dics"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, bool>> RemoveHashFields(string key, Dictionary<string, bool> dics)
        { 
            var db = this.GetDb();
            var newDics = new Dictionary<string, bool>();
            foreach (var fieldKey in dics.Keys)
            {
                newDics.Add(fieldKey, await db.HashDeleteAsync(key, fieldKey));
            }
            return dics;
        }

        /// <summary>
        /// 设置hash
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="filedKey"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        public async Task<T> GetHashField<T>(string key, string filedKey)
        {
            var t = default(T);
            var dics = await this.GetHashFields<T>(key, new Dictionary<string, T> { { filedKey, t } });
            if (dics.ContainsKey(filedKey))
                return dics[filedKey];
            else
                return t;
        }

        /// <summary>
        /// 获取hash的列值集合
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="dics"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, T>> GetHashFields<T>(string key, Dictionary<string, T> dics)
        {

            var db = this.GetDb();
            var newDics = new Dictionary<string, T>();
            foreach (var fieldKey in dics.Keys)
            {
                var str = await db.HashGetAsync(key, fieldKey);
                if (string.IsNullOrWhiteSpace(str)) { continue; }

                newDics.Add(fieldKey, JsonConvert.DeserializeObject<T>(str));
            }
            return newDics;

        }

        /// <summary>
        /// 获取hash的key的所有列的值
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, T>> GetHashs<T>(string key)
        {
            var dic = new Dictionary<string, T>();

            var db = this.GetDb();

            var hashFiles = await db.HashGetAllAsync(key);
            var newDics = new Dictionary<string, T>();
            foreach (var field in hashFiles)
            {
                newDics.Add(field.Name, JsonConvert.DeserializeObject<T>(field.Value));
            }
            return newDics;
        }

        /// <summary>
        /// 获取hash的Key的所有列的值的list集合
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<List<T>> GetHashsToList<T>(string key)
        {
            var list = new List<T>();
            var db = this.GetDb();
            //var hashFiles = await db.HashGetAllAsync(key);
            var hashFiles = db.HashScan(key);
            foreach (var field in hashFiles)
            {
                var item = JsonConvert.DeserializeObject<T>(field.Value);
                if (item == null) { continue; }
                list.Add(item);
            }
            return await Task.FromResult(list);
        }

        public async Task<List<T>> GetOrSetHashsToList<T>(string key, Func<Dictionary<string, T>> getFromDb)
        {
            var db = this.GetDb();
            if (await db.HashLengthAsync(key) > 0)
                return await this.GetHashsToList<T>(key);
            var dics = getFromDb();
            var result = await this.SetOrUpdateHashsFieldsAsync<T>(key, dics, true);
            return dics.Select(x=>x.Value).ToList();
        }

        public async Task<long> GetHashLengthAsync(string key)
        {
            var db = this.GetDb();
            var count = await db.HashLengthAsync(key);
            return count;
        }
        #endregion

        #region List操作（注：可以当做队列使用）

        /// <summary>
        /// list长度
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<long> GetListLen<T>(string key)
        {
            var db = this.GetDb();
            return await db.ListLengthAsync(key);
        }

        /// <summary>
        /// 获取List数据
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<List<T>> GetList<T>(string key)
        {
            var t = new List<T>();
            var db = this.GetDb();
            var _values = await db.ListRangeAsync(key);
            foreach (var item in _values)
            {
                if (string.IsNullOrWhiteSpace(item)) { continue; }
                t.Add(JsonConvert.DeserializeObject<T>(item));
            }
            return t;
        }

        /// <summary>
        /// 获取队列出口数据并移除
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<T> GetListAndPop<T>(string key)
        {
            var t = default(T);

            var db = this.GetDb();
            var _str = await db.ListRightPopAsync(key);
            if (string.IsNullOrWhiteSpace(_str)) { return t; }
            t = JsonConvert.DeserializeObject<T>(_str);
            return t;
        }

        /// <summary>
        /// 集合对象添加到list左边
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public async Task<long> SetLists<T>(string key, List<T> values)
        {
            var result = 0L;

            var jsonOption = new JsonSerializerSettings()
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                FloatParseHandling = FloatParseHandling.Decimal
            };
            jsonOption.Converters.Add(new DecimalJsonConverter());
            var db = this.GetDb();
            foreach (var item in values)
            {
                var _str = JsonConvert.SerializeObject(item, jsonOption);
                result += await db.ListLeftPushAsync(key, _str);
            }
            return result;
        }

        /// <summary>
        /// 单个对象添加到list左边
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task<long> SetList<T>(string key, T value)
        {
            var result = 0L;

            result = await this.SetLists(key, new List<T> { value });
            return result;
        }


        #endregion

        #region 额外扩展

        public async Task<List<string>> MatchKeys(params string[] paramArr)
        {
            var list = new List<string>();

            var result = await this.ExecuteAsync("keys", paramArr);

            var valArr = ((RedisValue[])result);
            foreach (var item in valArr)
            {
                list.Add(item);
            }
            return list;
        }

        /// <summary>
        /// 执行redis原生命令
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="paramArr"></param>
        /// <returns></returns>
        public async Task<RedisResult> ExecuteAsync(string cmd, params string[] paramArr)
        {
            var db = this.GetDb();
            return await db.ExecuteAsync(cmd, paramArr);
        }

        /// <summary>
        /// 手动回收管理器对象
        /// </summary>
        public void Dispose()
        {
            this.Dispose(_redis);
        }

        public void Dispose(ConnectionMultiplexer con)
        {
            if (con != null)
            {
                con.Close();
                con.Dispose();
            }
        }

        #endregion

        #endregion
    }
}
