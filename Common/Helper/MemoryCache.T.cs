using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common
{
    public class MemoryCache<TKey, TValue> : IMemoryCache<TKey, TValue>
        where TKey : notnull, IEquatable<TKey>
        where TValue : notnull
    {
        private readonly IMemoryCache _memoryCache;

        public MemoryCache(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        public TValue this[TKey key] 
        { 
            get => (TValue)_memoryCache.Get(key);
            set => Set(key, value);
        }

        public TValue GetOrAdd(TKey key, Func<TValue> func, 
            MemoryCacheEntryOptions memoryCacheEntryOptions = null)
        {
            TValue value = (TValue)_memoryCache.Get(key);
            if (value == null || value.Equals(default))
            {
                value = func();
                Set(key, value, memoryCacheEntryOptions);
            }

            return value;
        }

        public void Remove(TKey key)
            =>_memoryCache.Remove(key);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="cacheEntryOptions">默认2分钟滑动过期</param>
        public void Set(TKey key, TValue value, MemoryCacheEntryOptions cacheEntryOptions = null)
        {
            if (cacheEntryOptions == null)
                //默认2分钟滑动过期
                cacheEntryOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(2));

            _memoryCache.Set(key, value, cacheEntryOptions);
        }
    }
}
