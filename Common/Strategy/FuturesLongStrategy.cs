using CEF.Common.Entity;
using CEF.Common.Exchange;
using CEF.Common.Extentions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trady.Core.Infrastructure;

namespace CEF.Common.Strategy
{
    public class FuturesLongStrategy : IStrategy, ITransientDependency
    {  
        public CEF.Common.Exchange.PositionSide Side => CEF.Common.Exchange.PositionSide.Long;

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
                if (per15MinuteIndexedOhlcv.Prev.Close > per15MinuteIndexedOhlcv.Prev.Open)
                    await openFunc?.Invoke(future.Symbol, OrderType.Market, Side, future.BaseOrderSize);
            }
            else
            {
                if (per15MinuteIndexedOhlcv.Close > future.EntryPrice)
                {
                    if (((per15MinuteIndexedOhlcv.Close - future.EntryPrice) / future.EntryPrice) > future.TargetProfit &&
                        per15MinuteIndexedOhlcv.Prev.Close < per15MinuteIndexedOhlcv.Prev.Open)
                        await closeFunc?.Invoke(future.Symbol, OrderType.Limit, Side, future.Size);
                }
                else if (future.OrdersCount == 1)
                {
                    if (((future.LastTransactionOpenPrice - per15MinuteIndexedOhlcv.Close) / future.LastTransactionOpenPrice) > future.SafetyOrderPriceDeviation &&
                        ((future.LastTransactionOpenPrice - per15MinuteIndexedOhlcv.Close) / future.LastTransactionOpenPrice) < 0.03m &&
                       per15MinuteIndexedOhlcv.Prev.Close > per15MinuteIndexedOhlcv.Prev.Open &&
                       per15MinuteIndexedOhlcv.Close > per15MinuteIndexedOhlcv.Prev.Close)
                        await openFunc?.Invoke(future.Symbol, OrderType.Limit, Side, future.SafetyOrderSize);
                    else if (((future.LastTransactionOpenPrice - per15MinuteIndexedOhlcv.Close) / future.LastTransactionOpenPrice) > 0.03m &&
                        fourHourlyIndexedOhlcv.Prev.Close > fourHourlyIndexedOhlcv.Prev.Open && 
                        per15MinuteIndexedOhlcv.Prev.Close > per15MinuteIndexedOhlcv.Prev.Open &&
                       per15MinuteIndexedOhlcv.Close > per15MinuteIndexedOhlcv.Prev.Close)
                        await openFunc?.Invoke(future.Symbol, OrderType.Limit, Side, future.SafetyOrderSize);
                }
                else if (future.OrdersCount < (future.MaxSafetyOrdersCount + 1))
                {
                    if (((future.LastTransactionOpenPrice - per15MinuteIndexedOhlcv.Close) / future.LastTransactionOpenPrice) > (future.SafetyOrderPriceDeviation * future.SafetyOrderPriceScale *(future.OrdersCount - 1)) &&
                       fourHourlyIndexedOhlcv.Prev.Close > fourHourlyIndexedOhlcv.Prev.Open && 
                       per15MinuteIndexedOhlcv.Prev.Close > per15MinuteIndexedOhlcv.Prev.Open &&
                       fourHourlyIndexedOhlcv.Close > fourHourlyIndexedOhlcv.Prev.Close)
                        await openFunc?.Invoke(future.Symbol, OrderType.Limit, Side, future.SafetyOrderSize * future.SafetyOrderVolumeScale * (future.OrdersCount - 1));
                }
            }
        }
    }
}