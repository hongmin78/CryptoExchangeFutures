using Dynamitey;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Strategy
{
    public class FuturesShortStrategy : IStrategy, ITransientDependency
    {
        public int MaxActiveDeals { set; get; } = 6;
    }
}
