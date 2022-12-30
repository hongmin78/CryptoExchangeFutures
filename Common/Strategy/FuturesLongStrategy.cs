using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Strategy
{
    public class FuturesLongStrategy : IStrategy, ITransientDependency
    {
        public int MaxActiveDeals { set; get; } = 6;
    }
}
