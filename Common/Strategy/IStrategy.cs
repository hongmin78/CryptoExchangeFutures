using CEF.Common.Entity;
using CEF.Common.Exchange;
using CryptoExchange.Net.CommonObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trady.Core.Infrastructure;

namespace CEF.Common.Strategy
{
    public interface IStrategy
    {
        CEF.Common.Exchange.PositionSide Side { get; }
        Task ExecuteAsync(Future future,
            IIndexedOhlcv per15MinuteIndexedOhlcv,
            IIndexedOhlcv fourHourlyIndexedOhlcv,
            Func<string, OrderType, PositionSide, decimal?, Task> openFunc,
            Func<string, OrderType, PositionSide, decimal?, Task> closeFunc);
    }
}
