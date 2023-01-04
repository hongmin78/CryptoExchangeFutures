using CEF.Common.Entity;
using CEF.Common.Exchange;
using CEF.Common.Extentions;
using Dynamitey;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trady.Core.Infrastructure;

namespace CEF.Common.Strategy
{
    public class FuturesShortStrategy : IStrategy, ITransientDependency
    { 
        public CEF.Common.Exchange.PositionSide Side => CEF.Common.Exchange.PositionSide.Short;

        public async Task ExecuteAsync(
            Future future, 
            IIndexedOhlcv per15MinuteIndexedOhlcv,
            IIndexedOhlcv fourHourlyIndexedOhlcv,
            Func<string, OrderType, PositionSide, decimal?, Task> openFunc, 
            Func<string, OrderType, PositionSide, decimal?, Task> closeFunc)
        {
            await Task.CompletedTask;
        }
    }
}
