using CEF.Common.Entity;
using CEF.Common.Exchange;
using CEF.Common.Extentions;
using CEF.Common.Strategy;
using CEF.Common.Trader;
using Coldairarrow.Util;
using EFCore.Sharding;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trady.Core;
using Trady.Core.Infrastructure;

namespace CEF.Common.Context
{
    public class TraderContext : IContext, ITransientDependency
    {
        private readonly string futuresMemoryKey = "GetFuturesAsync";
        private readonly string klineDataMemoryKey = "GetKlineDataAsync_{0}_{1}";
        private readonly ILogger<TraderContext> _logger;
        private readonly IServiceProvider _serviceProvider;     
        private readonly ITrader _trader;
        private readonly IEnumerable<IStrategy> _strategyList;
        private readonly IExchange _exchange;
        private readonly IMemoryCache _memoryCache;
        public TraderContext(IServiceProvider serviceProvider,
                            ITrader trader,
                            ILogger<TraderContext> logger,
                            IExchange exchange,
                            IEnumerable<IStrategy> strategyList,
                            IMemoryCache memoryCache)
        {
            _serviceProvider = serviceProvider;
            _trader = trader;
            _logger = logger;
            _strategyList = strategyList;
            _exchange = exchange;
            _memoryCache = memoryCache;
        }

        public async Task ExecuteAsync(CancellationToken ct = default)
        {           
            var symbols = (await this.GetFuturesAsync()).Select(x=>x.Symbol).Distinct();
            await SubscribeToKlineUpdatesAsync(symbols);
            await SubscribeToUserDataUpdatesAsync();
            var futureInfoList = await this.GetSymbolsAsync();
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var futures = await this.GetFuturesAsync();
                    //await Parallel.ForEachAsync(symbols, async (symbol, cancellationToken) =>
                    foreach (var future in futures)
                    {
                        var symbol = future.Symbol;
                        var futureInfo = futureInfoList.FirstOrDefault(x => x.Name == symbol);
                        if (futureInfo == null)
                        {
                            this._logger.LogError($"未能在交易所发现交易对{symbol}.");
                            continue;
                        }
                        //var future = futures.FirstOrDefault(x => x.Symbol == symbol && x.PositionSide == (int)strategy.Side && x.Status == FutureStatus.None);
                        if (future.IsEnabled != 1 || future.Status != FutureStatus.None)
                            continue;
                        foreach (var strategy in this._strategyList)
                        { 
                            if((int)strategy.Side != future.PositionSide) continue;
                            var per15MinuteKlines = await this.GetKlineData(symbol, PeriodOption.Per15Minute);
                            var fourHourlyKlines = await this.GetKlineData(symbol, PeriodOption.FourHourly);
                            var per15MinuteKlineIC = IndexedObjectConstructor(per15MinuteKlines, per15MinuteKlines.Count() - 1);
                            var fourHourlyKlinesIC = IndexedObjectConstructor(fourHourlyKlines, fourHourlyKlines.Count() - 1);
                            await strategy.ExecuteAsync(
                                future,
                                per15MinuteKlineIC,
                                fourHourlyKlinesIC,
                                async (symbol, orderType, positionSide, amount) =>
                                {
                                    var quantity = amount / per15MinuteKlineIC.Close;
                                    var multiple = Convert.ToInt32(quantity / futureInfo.MinTradeQuantity);
                                    quantity = (multiple + 1) * futureInfo.MinTradeQuantity;                                    
                                    future.Status = FutureStatus.Openning;
                                    future.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                                    await this.UpdateFutureAsync(future, new List<string>() { "Status", "UpdateTime" });
                                    await this._trader.OpenPositionAsync(future.Id, symbol, orderType, positionSide, quantity, null);
                                },
                                async (symbol, orderType, positionSide, quantity) =>
                                {                                    
                                    future.Status = FutureStatus.Closing;
                                    future.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                                    await this.UpdateFutureAsync(future, new List<string>() { "Status", "UpdateTime" });
                                    await this._trader.ClosePositionAsync(future.Id, symbol, orderType, positionSide, quantity, null);
                                });
                        }
                    }//);
                }
                catch (Exception e)
                {
                    this._logger.LogError(e, e.Message);
                }
                finally
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(1000));
                }
            }
        }

        private async Task<Future> CreateFutureAsync(string symbol, PositionSide side)
        {
            using var scope = this._serviceProvider.CreateScope();
            using var dbAccessor = scope.ServiceProvider.GetService<IDbAccessor>();
            var entity = new Future()
            {
                Symbol = symbol,
                Id = IdHelper.GetLongId(),
                UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"),
                AbleSize = 0,
                BaseOrderSize = 100,
                EntryPrice = 0,
                LastTransactionOpenPrice = 0,
                LastTransactionOpenSize = 0,
                MaxSafetyOrdersCount = 3,
                OrdersCount = 0,
                PositionSide = (int)side,
                Size = 0,
                TargetProfit = 0.012m,
                SafetyOrderSize = 300m,
                SafetyOrderPriceDeviation = 0.018m,
                SafetyOrderPriceScale = 2m,
                SafetyOrderVolumeScale = 2m,
                IsEnabled = 1
            };
            await dbAccessor.InsertAsync(entity);
            this._memoryCache.Remove(futuresMemoryKey);
            return entity;
        }

        async Task SubscribeToUserDataUpdatesAsync()
        {
            await this._exchange.SubscribeToUserDataUpdatesAsync(
                margin =>
                {


                },
                account =>
                {


                },
                async order =>
                {
                    using var scope = this._serviceProvider.CreateScope();
                    using var dbAccessor = scope.ServiceProvider.GetService<IDbAccessor>();
                    var dbOrder = await dbAccessor.GetIQueryable<Entity.Order>().FirstOrDefaultAsync(x=>x.ClientOrderId == order.ClientOrderId);
                    if (dbOrder == null)
                    {
                        this._logger.LogInformation($"Order Not Found. ClientOrderId:{order.ClientOrderId}");
                        return;
                    }
                    //this._logger.LogInformation($"Order Update {order.ToJson()}");
                    dbOrder.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                    dbOrder.Status = order.Status.GetDescription();
                    dbOrder.AvgPrice = order.AvgPrice;
                    await dbAccessor.UpdateAsync(dbOrder, new List<string>() { "UpdateTime", "Status", "AvgPrice" });
                    if (order.Status == OrderStatus.Filled)
                    {
                        dbOrder.FilledQuantity = dbOrder.Quantity;
                        await dbAccessor.UpdateAsync(dbOrder, new List<string>() { "FilledQuantity" });

                        var futures = await this.GetFuturesAsync();
                        var future = futures.FirstOrDefault(x => x.Symbol == order.Symbol && x.PositionSide == (int)order.PositionSide && x.Id == dbOrder.FutureId);
                        if (future == null)
                        {
                            this._logger.LogError($"未发现合约配置{order.Symbol}/{order.PositionSide.GetDescription()}");
                            return;
                        }
                        if (future.Status == FutureStatus.Openning)
                        {
                            future.EntryPrice = (future.EntryPrice * future.Size + order.AvgPrice * order.Quantity) / (future.Size + order.Quantity);
                            future.Size += order.Quantity;
                            future.AbleSize += order.Quantity;
                            future.LastTransactionOpenPrice = order.AvgPrice;
                            future.LastTransactionOpenSize = order.Quantity;
                            future.OrdersCount++;
                            future.Status = FutureStatus.None;
                            future.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");                            
                            await this.UpdateFutureAsync(future, new List<string>() {
                                "Size",
                                "AbleSize",
                                "EntryPrice",
                                "LastTransactionOpenPrice",
                                "LastTransactionOpenSize",
                                "OrdersCount",
                                "Status",
                                "UpdateTime" });
                        }
                        else if (future.Status == FutureStatus.Closing)
                        {
                            dbOrder.PNL = (order.PositionSide == PositionSide.Long ? 1 : -1) * (order.AvgPrice - future.EntryPrice) * dbOrder.Quantity;
                            await dbAccessor.UpdateAsync(dbOrder, new List<string>() { "PNL" });
                            future.Size = 0;
                            future.PNL += dbOrder.PNL??0;
                            future.AbleSize = 0;
                            future.EntryPrice = 0;
                            future.LastTransactionOpenPrice = 0;
                            future.LastTransactionOpenSize = 0;
                            future.OrdersCount = 0;
                            future.Status = FutureStatus.None;
                            future.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                            await this.UpdateFutureAsync(future, new List<string>() {
                                "PNL",
                                "Size",
                                "AbleSize",
                                "EntryPrice",
                                "LastTransactionOpenPrice",
                                "LastTransactionOpenSize",
                                "OrdersCount",
                                "Status",
                                "UpdateTime" });
                        }
                        else
                            this._logger.LogError($"错误的合约配置状态{order.Symbol}/{order.PositionSide.GetDescription()}/{future.Status.GetDescription()}");
                    }
                    //else if(order.Status == OrderStatus.Expired)
                    //{
                    //    dbOrder.FilledQuantity = order.LastFilledQuantity;
                    //    await dbAccessor.UpdateAsync(dbOrder, new List<string>() { "FilledQuantity" });

                    //    var futures = await this.GetFuturesAsync();
                    //    var future = futures.FirstOrDefault(x => x.Symbol == order.Symbol && x.PositionSide == (int)order.PositionSide && x.Id == dbOrder?.FutureId);
                    //    if (future == null)
                    //    {
                    //        this._logger.LogError($"未发现合约配置{order.Symbol}/{order.PositionSide.GetDescription()}");
                    //        return;
                    //    }
                    //    if (future.Status == FutureStatus.Openning)
                    //    {
                    //        future.EntryPrice = (future.EntryPrice * future.Size + order.AvgPrice * order.LastFilledQuantity) / (future.Size + order.LastFilledQuantity);
                    //        future.Size += order.LastFilledQuantity;
                    //        future.AbleSize += order.LastFilledQuantity;
                    //        future.LastTransactionOpenPrice = order.AvgPrice;
                    //        future.LastTransactionOpenSize = order.LastFilledQuantity;
                    //        future.OrdersCount++;
                    //        future.Status = FutureStatus.None;
                    //        future.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                    //        await this.UpdateFutureAsync(future, new List<string>() {
                    //            "Size",
                    //            "AbleSize",
                    //            "EntryPrice",
                    //            "LastTransactionOpenPrice",
                    //            "LastTransactionOpenSize",
                    //            "OrdersCount",
                    //            "Status",
                    //            "UpdateTime" });
                    //    }
                    //    else if (future.Status == FutureStatus.Closing)
                    //    {
                    //        dbOrder.PNL = (order.PositionSide == PositionSide.Long ? 1 : -1) * (order.AvgPrice - future.EntryPrice) * dbOrder.Quantity;
                    //        await dbAccessor.UpdateAsync(dbOrder, new List<string>() { "PNL" });
                    //        future.Size -= order.LastFilledQuantity;
                    //        future.AbleSize = order.LastFilledQuantity;
                    //        future.EntryPrice = 0;
                    //        future.LastTransactionOpenPrice = 0;
                    //        future.LastTransactionOpenSize = 0;
                    //        future.OrdersCount = 0;
                    //        future.Status = FutureStatus.None;
                    //        future.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                    //        await this.UpdateFutureAsync(future, new List<string>() {
                    //            "Size",
                    //            "AbleSize",
                    //            "EntryPrice",
                    //            "LastTransactionOpenPrice",
                    //            "LastTransactionOpenSize",
                    //            "OrdersCount",
                    //            "Status",
                    //            "UpdateTime" });
                    //    }
                    //    else
                    //        this._logger.LogError($"错误的合约配置状态{order.Symbol}/{order.PositionSide.GetDescription()}/{future.Status.GetDescription()}");
                    //}
                });
        }

        async Task SubscribeToKlineUpdatesAsync(IEnumerable<string> symbols)
        { 
            var periods = new List<PeriodOption>() { PeriodOption.Per15Minute, PeriodOption.FourHourly };
            await this._exchange.SubscribeToKlineUpdatesAsync(symbols, periods, async kline =>
            { 
                var symbol = kline.Symbol;
                var period = kline.Interval;
                var memoryKey = string.Format(klineDataMemoryKey, symbol, (int)period); 
                var dt = DateTime.SpecifyKind(kline.OpenTime, DateTimeKind.Utc);
                var klines = (await this.GetKlineData(symbol, period)).ToList();
                var currentKLine = klines.FirstOrDefault(x=>x.DateTime == dt);
                if (currentKLine == null)
                {
                    currentKLine = new Candle(dt, kline.OpenPrice, kline.HighPrice, kline.LowPrice, kline.ClosePrice, kline.Volume);
                    klines.Add(currentKLine);
                }
                else
                {
                    currentKLine.Close = kline.ClosePrice;
                    currentKLine.High = kline.HighPrice;
                    currentKLine.Low = kline.LowPrice;
                    currentKLine.Open = kline.OpenPrice;
                    currentKLine.Volume = kline.Volume;
                } 
                this._memoryCache.Set(memoryKey, klines);
            });
        }

        async Task<List<IOhlcv>> GetKlineData(string symbol, PeriodOption period)
        {
            var memoryKey = string.Format(klineDataMemoryKey, symbol, (int)period);
            var result = await this._memoryCache.GetOrSetObjectAsync<List<IOhlcv>>(memoryKey, async () =>
            {
                var callResult = await this._exchange.GetKlineDataAsync(symbol, period, DateTime.Now.AddDays(-1));
                if (callResult.Success)
                    return callResult.Data.ToList();
                else
                {
                    this._logger.LogError($"GetKlineDataAsync 调用失败. errorcode:{callResult.ErrorCode} detail:{callResult.Msg}");
                    return default;
                }
            }, new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(999) });
            return result;
        }

        async Task<IEnumerable<FutureInfo>> GetSymbolsAsync()
        {
            var memoryKey = "GetSymbolsAsync";
            var result = await this._memoryCache.GetOrSetObjectAsync<IEnumerable<FutureInfo>>(memoryKey, async () =>
            {
                var callResult = await this._exchange.GetSymbolsAsync();
                if (callResult.Success)
                    return callResult.Data;
                else
                {
                    this._logger.LogError($"GetSymbolsAsync 调用失败. errorcode:{callResult.ErrorCode} detail:{callResult.Msg}");
                    return default;
                }
            }, new MemoryCacheEntryOptions() { SlidingExpiration = TimeSpan.FromDays(30) });
            return result;
        }

        async Task<IEnumerable<Future>> GetFuturesAsync()
        { 
            //var result = await this._memoryCache.GetOrSetObjectAsync<IEnumerable<Future>>(futuresMemoryKey, async () =>
            //{
                using var scope = this._serviceProvider.CreateScope();
                using var dbAccessor = scope.ServiceProvider.GetService<IDbAccessor>();
                var futures = await dbAccessor.GetIQueryable<Future>().ToListAsync();
                return futures;
            //}, new MemoryCacheEntryOptions() { SlidingExpiration = TimeSpan.FromDays(30) });
            //return result;
        }

        async Task UpdateFutureAsync(Future future, List<string> properties)
        {
            using var scope = this._serviceProvider.CreateScope();
            using var dbAccessor = scope.ServiceProvider.GetService<IDbAccessor>();
            await dbAccessor.UpdateAsync(future, properties);
            this._memoryCache.Remove(futuresMemoryKey);
        }

        public async Task SyncExchangeDataAsync()
        {
            using var scope = this._serviceProvider.CreateScope();
            using var dbAccessor = scope.ServiceProvider.GetService<IDbAccessor>();
            var futures = await this.GetFuturesAsync();
            var dbOrders = await dbAccessor.GetIQueryable<Entity.Order>().Where(x => x.Status != OrderStatus.Filled.GetDescription() && x.Status != OrderStatus.Invalid.GetDescription() && x.Status != OrderStatus.Expired.GetDescription()).ToListAsync();
            foreach (var dbOrder in dbOrders)
            {
                if (DateTime.Now.Subtract(DateTime.Parse(dbOrder.CreateTime.Remove(dbOrder.CreateTime.Length - 4))).TotalSeconds < 30)
                    continue;
                var orderResult = await this._exchange.GetOrderAsync(dbOrder.Symbol, null, dbOrder.ClientOrderId);
                if (!orderResult.Success)
                {
                    this._logger.LogError($"无法从交易所获取定单详细. orderId:{dbOrder.Id}.  errorcode:{orderResult.ErrorCode} msg:{orderResult.Msg}");
                    this._logger.LogInformation($"{orderResult.ToJson()}");
                    if (orderResult.ErrorCode == -2013)
                    {
                        dbOrder.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                        dbOrder.Status = OrderStatus.Invalid.GetDescription();
                        await dbAccessor.UpdateAsync(dbOrder, new List<string>() { "UpdateTime", "Status" });
                        var future = futures.FirstOrDefault(x => x.Symbol == dbOrder.Symbol && x.Status != FutureStatus.None && x.Id == dbOrder.FutureId);
                        if (future != null)
                        {
                            future.Status = FutureStatus.None;
                            future.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                            await this.UpdateFutureAsync(future, new List<string>() {
                                "Status",
                                "UpdateTime" });
                        }
                    }
                    continue;
                }
                var order = orderResult.Data;
                dbOrder.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                dbOrder.Status = order.Status.GetDescription();
                dbOrder.AvgPrice = order.AvgPrice;
                await dbAccessor.UpdateAsync(dbOrder, new List<string>() { "UpdateTime", "Status", "AvgPrice" });
                if (order.Status == OrderStatus.Filled)
                {
                    dbOrder.FilledQuantity = dbOrder.Quantity;
                    await dbAccessor.UpdateAsync(dbOrder, new List<string>() { "FilledQuantity" });
                    var future = futures.FirstOrDefault(x => x.Symbol == order.Symbol && x.PositionSide == (int)order.PositionSide && x.Id == dbOrder.FutureId);
                    if (future == null)
                    {
                        this._logger.LogError($"未发现合约配置{order.Symbol}/{order.PositionSide.GetDescription()}");
                        continue;
                    }
                    if (future.Status == FutureStatus.Openning)
                    {
                        future.EntryPrice = (future.EntryPrice * future.Size + order.AvgPrice * order.Quantity) / (future.Size + order.Quantity);
                        future.Size += order.Quantity;
                        future.AbleSize += order.Quantity;
                        future.LastTransactionOpenPrice = order.AvgPrice;
                        future.LastTransactionOpenSize = order.Quantity;
                        future.OrdersCount++;
                        future.Status = FutureStatus.None;
                        future.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                        await this.UpdateFutureAsync(future, new List<string>() {
                                "Size",
                                "AbleSize",
                                "EntryPrice",
                                "LastTransactionOpenPrice",
                                "LastTransactionOpenSize",
                                "OrdersCount",
                                "Status",
                                "UpdateTime" });
                    }
                    else if (future.Status == FutureStatus.Closing)
                    {
                        dbOrder.PNL = (order.PositionSide == PositionSide.Long ? 1 : -1) * (order.AvgPrice - future.EntryPrice) * dbOrder.Quantity;
                        await dbAccessor.UpdateAsync(dbOrder, new List<string>() { "PNL" });
                        future.PNL += dbOrder.PNL ?? 0;
                        future.Size = 0;
                        future.AbleSize = 0;
                        future.EntryPrice = 0;
                        future.LastTransactionOpenPrice = 0;
                        future.LastTransactionOpenSize = 0;
                        future.OrdersCount = 0;
                        future.Status = FutureStatus.None;
                        future.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                        await this.UpdateFutureAsync(future, new List<string>() {
                                "PNL",
                                "Size",
                                "AbleSize",
                                "EntryPrice",
                                "LastTransactionOpenPrice",
                                "LastTransactionOpenSize",
                                "OrdersCount",
                                "Status",
                                "UpdateTime" });
                    }
                    else
                        this._logger.LogError($"错误的合约配置状态{order.Symbol}/{order.PositionSide.GetDescription()}/{future.Status.GetDescription()}");
                }
                else if (order.Status == OrderStatus.Expired)
                {
                    dbOrder.FilledQuantity = order.LastFilledQuantity;
                    await dbAccessor.UpdateAsync(dbOrder, new List<string>() { "FilledQuantity" });

                    var future = futures.FirstOrDefault(x => x.Symbol == order.Symbol && x.PositionSide == (int)order.PositionSide && x.Id == dbOrder?.FutureId);
                    if (future == null)
                    {
                        this._logger.LogError($"未发现合约配置{order.Symbol}/{order.PositionSide.GetDescription()}");
                        return;
                    }
                    if (future.Status == FutureStatus.Openning)
                    {
                        future.EntryPrice = (future.EntryPrice * future.Size + order.AvgPrice * order.LastFilledQuantity) / (future.Size + order.LastFilledQuantity);
                        future.Size += order.LastFilledQuantity;
                        future.AbleSize += order.LastFilledQuantity;
                        if (order.LastFilledQuantity > 0)
                        {
                            future.LastTransactionOpenPrice = order.AvgPrice;
                            future.LastTransactionOpenSize = order.LastFilledQuantity;
                            future.OrdersCount++;
                        }
                        future.Status = FutureStatus.None;
                        future.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                        await this.UpdateFutureAsync(future, new List<string>() {
                                "Size",
                                "AbleSize",
                                "EntryPrice",
                                "LastTransactionOpenPrice",
                                "LastTransactionOpenSize",
                                "OrdersCount",
                                "Status",
                                "UpdateTime" });
                    }
                    else if (future.Status == FutureStatus.Closing)
                    {
                        dbOrder.PNL = (order.PositionSide == PositionSide.Long ? 1 : -1) * (order.AvgPrice - future.EntryPrice) * dbOrder.Quantity;
                        await dbAccessor.UpdateAsync(dbOrder, new List<string>() { "PNL" });
                        future.PNL += dbOrder.PNL ?? 0;
                        future.Size -= order.LastFilledQuantity;
                        future.AbleSize -= order.LastFilledQuantity;                       
                        future.Status = FutureStatus.None;
                        future.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                        await this.UpdateFutureAsync(future, new List<string>() {
                                "PNL",
                                "Size",
                                "AbleSize",                               
                                "Status",
                                "UpdateTime" });
                    }
                    else
                        this._logger.LogError($"错误的合约配置状态{order.Symbol}/{order.PositionSide.GetDescription()}/{future.Status.GetDescription()}");
                }
                else if (order.Status != OrderStatus.PartiallyFilled)
                {
                    this._logger.LogError($"定单状态异常. orderId:{order.Id}");
                    this._logger.LogInformation($"{ order.ToJson()}");
                    //dbOrder.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                    //dbOrder.Status = OrderStatus.Invalid.GetDescription();
                    //await dbAccessor.UpdateAsync(dbOrder, new List<string>() { "UpdateTime", "Status" });
                    //var future = futures.FirstOrDefault(x => x.Symbol == dbOrder.Symbol && x.Status != FutureStatus.None && x.Id == dbOrder.FutureId);
                    //if (future != null)
                    //{
                    //    future.Status = FutureStatus.None;
                    //    future.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                    //    await this.UpdateFutureAsync(future, new List<string>() {
                    //            "Status",
                    //            "UpdateTime" });
                    //}
                }
            }
        }

        protected Func<IEnumerable<IOhlcv>, int, IIndexedOhlcv> IndexedObjectConstructor
           => (l, i) => new Trady.Analysis.IndexedCandle(l, i);
    }
}
