using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Trader
{
    public interface ITrader
    { 

        void Buy(string symbol, decimal amount, decimal price);

        void Sell(string symbol, decimal amount, decimal price);
    }
}
