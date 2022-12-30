using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common
{
    /// <summary>
    /// 共享缓存
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public interface IShareCache<TKey, TValue>
        where TKey : notnull
        where TValue : notnull, new()
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
        /// <returns></returns>
        TValue GetOrAdd(TKey key, Func<TValue> func);

        /// <summary>
        /// 设置值
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="expireMinutes">超时时间.单位:分钟</param>
        void Set(TKey key, TValue value, int expireMinutes = 2);

        /// <summary>
        /// 移除
        /// </summary>
        /// <param name="key"></param>
        void Remove(TKey key);
    }
}
