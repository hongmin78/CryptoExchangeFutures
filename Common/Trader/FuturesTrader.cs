using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Trader
{
    public class FuturesTrader : ITrader, ITransientDependency
    {
        public void Buy(string symbol, decimal amount, decimal price)
        {
            throw new NotImplementedException();
        }

        public void Sell(string symbol, decimal amount, decimal price)
        {
            throw new NotImplementedException();
        }
    }
}
