using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Primitives
{
    public class CacheOptions
    {
        public CacheType CacheType { get; set; }
        public string RedisEndpoint { get; set; }
        public string ClusterRedisEndpoint { get; set; }
        public bool UpdateOrderCacheOnServerRestart { set; get; }
        public int MaxDayOfHistoryOrder { set; get; }
    }
}
