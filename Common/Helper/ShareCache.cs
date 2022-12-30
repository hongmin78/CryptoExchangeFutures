using CEF.Common.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CEF.Common
{
    public class ShareCache<TKey, TValue> : IShareCache<TKey, TValue>
        where TKey : notnull
        where TValue : notnull, new()
    {
        private readonly static StackRedisHelper _currentHelper = StackRedisHelper.Current;

        public TValue this[TKey key] 
        {
            get => _currentHelper.Get<TValue>(key.ToString());
            set => Set(key, value);
        }

        public TValue GetOrAdd(TKey key, Func<TValue> func)
        {
            TValue value = _currentHelper.Get<TValue>(key.ToString());
            if (value.Equals(default))
            {
                value = func();
                Set(key, value);
                return value;
            }
            else
                return _currentHelper.Get<TValue>(key.ToString());
        }

        public async void Remove(TKey key)
        {
            await _currentHelper.Remove(key.ToString());
        }

        public void Set(TKey key, TValue value, int expireMinutes = 2)
            => _currentHelper.Set(key.ToString(), value, expireMinutes);
    }
}
