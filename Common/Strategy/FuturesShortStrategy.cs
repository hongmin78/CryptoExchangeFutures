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
            if (per15MinuteIndexedOhlcv == null || fourHourlyIndexedOhlcv == null) return;
            if (future.IsEnabled != 1 || future.Status != FutureStatus.None) return;
            if (future.OrdersCount == 0)
            {
                if (per15MinuteIndexedOhlcv.Prev.Close < per15MinuteIndexedOhlcv.Prev.Open)
                    await openFunc?.Invoke(future.Symbol, OrderType.Market, Side, future.BaseOrderSize);
            }
            else
            {
                if (per15MinuteIndexedOhlcv.Close < future.EntryPrice)
                {
                    if (((future.EntryPrice - per15MinuteIndexedOhlcv.Close) / future.EntryPrice) > future.TargetProfit &&
                        per15MinuteIndexedOhlcv.Prev.Close > per15MinuteIndexedOhlcv.Prev.Open)
                        await closeFunc?.Invoke(future.Symbol, OrderType.Market, Side, future.Size);
                }
                else if (future.OrdersCount == 1)
                {
                    if (((per15MinuteIndexedOhlcv.Close - future.LastTransactionOpenPrice) / future.LastTransactionOpenPrice) > future.SafetyOrderPriceDeviation &&
                        ((per15MinuteIndexedOhlcv.Close - future.LastTransactionOpenPrice) / future.LastTransactionOpenPrice) < 0.1m &&
                       per15MinuteIndexedOhlcv.Prev.Close < per15MinuteIndexedOhlcv.Prev.Open)
                        await openFunc?.Invoke(future.Symbol, OrderType.Market, Side, future.SafetyOrderSize);
                    else if (((per15MinuteIndexedOhlcv.Close - future.LastTransactionOpenPrice) / future.LastTransactionOpenPrice) > 0.1m &&
                        fourHourlyIndexedOhlcv.Prev.Close < fourHourlyIndexedOhlcv.Prev.Open)
                        await openFunc?.Invoke(future.Symbol, OrderType.Market, Side, future.SafetyOrderSize);
                }
                else if (future.OrdersCount < (future.MaxSafetyOrdersCount + 1))
                {
                    if (((per15MinuteIndexedOhlcv.Close - future.LastTransactionOpenPrice) / future.LastTransactionOpenPrice) > 0.1m &&
                       fourHourlyIndexedOhlcv.Prev.Close < fourHourlyIndexedOhlcv.Prev.Open)
                        await openFunc?.Invoke(future.Symbol, OrderType.Market, Side, future.SafetyOrderSize * future.SafetyOrderVolumeScale);
                }
            }
        }
    }
}
