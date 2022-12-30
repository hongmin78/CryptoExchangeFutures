using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public interface IMemoryCache<TKey, TValue>
        where TKey : notnull, IEquatable<TKey>
        where TValue : notnull
    {
        /// <summary>
        /// 索引
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        TValue this[TKey key] { get; set; }

        /// <summary>
        /// 获取或添加
        /// </summary>
        /// <param name="key"></param>
        /// <param name="func"></param>
        /// <param name="memoryCacheEntryOptions"></param>
        /// <returns></returns>
        TValue GetOrAdd(TKey key, Func<TValue> func, MemoryCacheEntryOptions memoryCacheEntryOptions = null);

        /// <summary>
        /// 设置
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="memoryCacheEntryOptions"></param>
        void Set(TKey key, TValue value, MemoryCacheEntryOptions memoryCacheEntryOptions = null);

        /// <summary>
        /// 移除
        /// </summary>
        /// <param name="key"></param>
        void Remove(TKey key);
    }
}
