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
using System.Net.NetworkInformation;
using Trady.Core;
using Trady.Core.Infrastructure;
using static System.Formats.Asn1.AsnWriter;

namespace CEF.Common.Context
{
    public class TraderContext : IContext, ISingletonDependency
    {
        public int MaxFutureCount { set; get; } = 1;
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
                                    var positionCount = futures.Count(x=>x.OrdersCount > 0 || x.Status != FutureStatus.None);
                                    if (future.OrdersCount == 0 && positionCount >= this.MaxFutureCount) return;

                                    var quantity = amount / per15MinuteKlineIC.Close;
                                    var multiple = Convert.ToInt32(quantity / futureInfo.MinTradeQuantity);
                                    quantity = (multiple + 1) * futureInfo.MinTradeQuantity;                                    
                                    future.Status = FutureStatus.Openning;
                                    future.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                                    await this.UpdateFutureAsync(future, new List<string>() { "Status", "UpdateTime" });
                                    await this._trader.OpenPositionAsync(future.Id, symbol, orderType, positionSide, quantity, orderType == OrderType.Market ? null : per15MinuteKlineIC.Close);
                                },
                                async (symbol, orderType, positionSide, quantity) =>
                                {                                    
                                    future.Status = FutureStatus.Closing;
                                    future.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                                    await this.UpdateFutureAsync(future, new List<string>() { "Status", "UpdateTime" });
                                    await this._trader.ClosePositionAsync(future.Id, symbol, orderType, positionSide, quantity, orderType == OrderType.Market ? null : per15MinuteKlineIC.Close);
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
                    await Task.Delay(TimeSpan.FromMilliseconds(1200));
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
                    if (order.Status == OrderStatus.Expired) return; 
                    if (order.Status == OrderStatus.Filled)
                    {
                        using var scope = this._serviceProvider.CreateScope();
                        using var dbAccessor = scope.ServiceProvider.GetService<IDbAccessor>();
                        long orderId = -1;
                        if (!long.TryParse(order.ClientOrderId, out orderId))
                        {
                            this._logger.LogInformation($"Order Not Found. {order.ToJson()}");
                            return;
                        }
                        var dbOrder = await dbAccessor.GetIQueryable<Entity.Order>().FirstOrDefaultAsync(x => x.Id == orderId);
                        if (dbOrder == null)
                        {
                            this._logger.LogInformation($"Order Not Found. {order.ToJson()}");
                            return;
                        }
                        //this._logger.LogInformation($"Order Update {order.ToJson()}");
                        dbOrder.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                        dbOrder.Status = order.Status.GetDescription();
                        dbOrder.AvgPrice = order.AvgPrice;
                        await dbAccessor.UpdateAsync(dbOrder, new List<string>() { "UpdateTime", "Status", "AvgPrice" });
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
                            this._logger.LogWarning($"[{future.Symbol} {(future.PositionSide == 1 ? "Long" : "Short")}] Open Position.  safety order [{future.OrdersCount - 1}/{future.MaxSafetyOrdersCount}]. Price:{order.AvgPrice}, Size:{order.AvgPrice * order.Quantity} USDT.");
                        }
                        else if (future.Status == FutureStatus.Closing)
                        {
                            var avgPrice = order.AvgPrice;
                            var entryPrice = future.EntryPrice;
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
                            this._logger.LogWarning($"[{future.Symbol} {(future.PositionSide==1 ? "Long" : "Short")}] Close Position. Entry Price:{entryPrice}, Open Price:{avgPrice}, PNL:{dbOrder.PNL ?? 0} USDT.");
                        }
                        else
                            this._logger.LogError($"错误的合约配置状态{order.Id}/{order.Symbol}/{order.PositionSide.GetDescription()}/{future.Status.GetDescription()}");
                    }
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

        public async Task<IEnumerable<Future>> GetFuturesAsync()
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
                if (DateTime.Now.Subtract(DateTime.Parse(dbOrder.CreateTime.Remove(dbOrder.CreateTime.Length - 4))).TotalSeconds < 15)
                    continue;
                var orderResult = await this._exchange.GetOrderAsync(dbOrder.Symbol, long.Parse(dbOrder.ClientOrderId));
                if (!orderResult.Success)
                {   
                    //if (DateTime.Now.Subtract(DateTime.Parse(dbOrder.CreateTime.Remove(dbOrder.CreateTime.Length - 4))).TotalSeconds < 60)
                    //    continue;
                    this._logger.LogError($"无法从交易所获取定单详细. orderId:{dbOrder.Id}.  errorcode:{orderResult.ErrorCode} msg:{orderResult.Msg}");
                    this._logger.LogInformation($"{orderResult.ToJson()}");
                    //if (orderResult.ErrorCode == -2013)
                    //{
                    //    dbOrder.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                    //    dbOrder.Status = OrderStatus.Invalid.GetDescription();
                    //    await dbAccessor.UpdateAsync(dbOrder, new List<string>() { "UpdateTime", "Status" });
                    //    var future = futures.FirstOrDefault(x => x.Symbol == dbOrder.Symbol && x.Status != FutureStatus.None && x.Id == dbOrder.FutureId);
                    //    if (future != null)
                    //    {
                    //        future.Status = FutureStatus.None;
                    //        future.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                    //        await this.UpdateFutureAsync(future, new List<string>() {
                    //            "Status",
                    //            "UpdateTime" });
                    //    }
                    //}
                    continue;
                }
                var order = orderResult.Data;
                if (order.Status == OrderStatus.New) continue;

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
                        this._logger.LogWarning($"[{future.Symbol} {(future.PositionSide == 1 ? "Long" : "Short")}] Open Position. safety order [{future.OrdersCount - 1}/{future.MaxSafetyOrdersCount}]. Price:{order.AvgPrice}, Size:{order.AvgPrice * order.Quantity} USDT.");
                    }
                    else if (future.Status == FutureStatus.Closing)
                    {
                        var avgPrice = order.AvgPrice;
                        var entryPrice = future.EntryPrice;
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
                        this._logger.LogWarning($"[{future.Symbol} {(future.PositionSide == 1 ? "Long" : "Short")}] Close Position. Entry Price:{entryPrice}, Close Price:{avgPrice}, PNL:{dbOrder.PNL ?? 0} USDT.");
                    }
                    else
                        this._logger.LogError($"错误的合约配置状态{order.Id}/{order.Symbol}/{order.PositionSide.GetDescription()}/{future.Status.GetDescription()}");
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
                        this._logger.LogError($"[{future.Symbol} {(future.PositionSide == 1 ? "Long" : "Short")}] 执行开仓仅部份成交. safety order [{future.OrdersCount - 1}/{future.MaxSafetyOrdersCount}]. 数量:{order.LastFilledQuantity}, 价格:{order.AvgPrice}, 大小:{order.AvgPrice * order.LastFilledQuantity} USDT.");
                    }
                    else if (future.Status == FutureStatus.Closing)
                    {
                        var avgPrice = order.AvgPrice;
                        var entryPrice = future.EntryPrice;
                        dbOrder.PNL = (order.PositionSide == PositionSide.Long ? 1 : -1) * (order.AvgPrice - future.EntryPrice) * order.LastFilledQuantity;
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
                        this._logger.LogError($"[{future.Symbol} {(future.PositionSide == 1 ? "Long" : "Short")}] Close Position. 仅部份成交. 持仓价:{entryPrice}, 数量:{order.LastFilledQuantity}, 平仓价:{avgPrice}, 利润:{dbOrder.PNL ?? 0} USDT.");
                    }
                    else
                        this._logger.LogError($"错误的合约配置状态{order.Id}/{order.Symbol}/{order.PositionSide.GetDescription()}/{future.Status.GetDescription()}");
                }
                else if (order.Status != OrderStatus.PartiallyFilled && order.Status != OrderStatus.New)
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

        public async Task SyncAdlOrderAsync()
        {
            using var scope = this._serviceProvider.CreateScope();
            using var dbAccessor = scope.ServiceProvider.GetService<IDbAccessor>();
            var futures = await this.GetFuturesAsync();
            var symbols= futures.Select(x => x.Symbol).Distinct();
            foreach(var symbol in symbols)
            {  
                var orderResult = await this._exchange.GetAllOrdersAsync(symbol);
                if (!orderResult.Success)
                {
                    this._logger.LogError($"获取{symbol}所有单错误. errorcode:{orderResult.ErrorCode}, message:{orderResult.Msg}");
                    continue;
                }
                var adlOrders = orderResult.Data.Where(x => x.ClientOrderId == "adl_autoclose");
                foreach(var adlOrder in adlOrders)
                {
                    var future = futures.FirstOrDefault(x => x.Symbol == symbol && x.Status == FutureStatus.None && x.PositionSide == (int)adlOrder.PositionSide);
                    if (future == null) continue;
                    var dbOrder = await dbAccessor.GetIQueryable<Entity.Order>().Where(x => x.Id == adlOrder.Id).FirstOrDefaultAsync();
                    if(dbOrder == null)
                    {
                        var order = new Order()
                        {
                            Id = adlOrder.Id,
                            AvgPrice = adlOrder.AvgPrice,
                            FilledQuantity = adlOrder.Quantity,
                            ClientOrderId = adlOrder.ClientOrderId,
                            CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"),
                            Side = adlOrder.Side.GetDescription(),
                            PositionSide = adlOrder.PositionSide.GetDescription(),
                            Price = adlOrder.Price,
                            Quantity = adlOrder.Quantity,
                            Status = adlOrder.Status.GetDescription(),
                            Symbol = symbol,
                            Type = adlOrder.Type.GetDescription(),
                            UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"),
                            OrderSide = "Close",
                            FutureId = future.Id,
                            PNL = (adlOrder.PositionSide == PositionSide.Long ? 1 : -1) * (adlOrder.AvgPrice - future.EntryPrice) * adlOrder.Quantity
                        };
                        await dbAccessor.InsertAsync(order);
                        if (future.Size > order.FilledQuantity)
                        {
                            var avgPrice = order.AvgPrice;
                            var entryPrice = future.EntryPrice;
                            future.PNL += order.PNL ?? 0;
                            future.Size -= adlOrder.Quantity;
                            future.AbleSize = future.Size;
                            if (future.Size == 0)
                            {
                                future.EntryPrice = 0;
                                future.LastTransactionOpenPrice = 0;
                                future.LastTransactionOpenSize = 0;
                                future.OrdersCount = 0;
                            }
                            future.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                            await this.UpdateFutureAsync(future, new List<string>() {
                                "PNL",
                                "Size",
                                "AbleSize",
                                "EntryPrice",
                                "LastTransactionOpenPrice",
                                "LastTransactionOpenSize",
                                "OrdersCount",
                                "UpdateTime" });
                            this._logger.LogWarning($"[{future.Symbol} {(future.PositionSide == 1 ? "Long" : "Short")}] ADL Close Position. Entry Price:{entryPrice}, Close Price:{avgPrice}, PNL:{order.PNL ?? 0} USDT.");
                        } 
                        futures = await this.GetFuturesAsync();
                    } 
                }
                await Task.Delay(1000);
            }
        }

        protected Func<IEnumerable<IOhlcv>, int, IIndexedOhlcv> IndexedObjectConstructor
           => (l, i) => new Trady.Analysis.IndexedCandle(l, i);
    }
}
