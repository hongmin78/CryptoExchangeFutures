using CEF.Common.Entity;
using CEF.Common.Exchange;
using CEF.Common.Extentions;
using CEF.Common.Helper;
using CEF.Common.Strategy;
using CEF.Common.Trader; 
using EFCore.Sharding;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net; 
using Trady.Core.Infrastructure; 

namespace CEF.Common.Context
{
    public class TraderContext : IContext, ISingletonDependency
    {
        public int MaxFutureCount { set; get; } = 0;
        private readonly string futuresMemoryKey = "GetFuturesAsync";
        private readonly string klineDataMemoryKey = "GetKlineDataAsync_{0}_{1}";
        private readonly ILogger<TraderContext> _logger;
        private readonly IServiceProvider _serviceProvider;     
        private readonly ITrader _trader;
        private readonly IEnumerable<IStrategy> _strategyList;
        private readonly IExchange _exchange;
        private readonly IMemoryCache _memoryCache;
        private readonly IConfiguration _configuration;
        public TraderContext(IServiceProvider serviceProvider,
                            ITrader trader,
                            ILogger<TraderContext> logger,
                            IExchange exchange,
                            IEnumerable<IStrategy> strategyList,
                            IConfiguration configuration,
                            IMemoryCache memoryCache)
        {
            _serviceProvider = serviceProvider;
            _trader = trader;
            _logger = logger;
            _strategyList = strategyList;
            _exchange = exchange;
            _memoryCache = memoryCache;
            _configuration = configuration;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
        }

        public async Task ExecuteAsync(CancellationToken ct = default)
        {
            var futureInfoList = await this.GetSymbolsAsync();
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var futures = await this.GetFuturesAsync();
                    var allKlineData = await this.GetAllKlineData(true);
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
                        if (future.Status != FutureStatus.None)
                            continue;
                        var per15MinuteMemoryKey = string.Format(klineDataMemoryKey, symbol, (int)PeriodOption.Per15Minute);
                        var fourHourlyMemoryKey = string.Format(klineDataMemoryKey, symbol, (int)PeriodOption.FourHourly);
                        if (!(allKlineData?.ContainsKey(per15MinuteMemoryKey)??false)) continue;
                        if (!(allKlineData?.ContainsKey(fourHourlyMemoryKey)??false)) continue;
                        //var per15MinuteKlines = await this.GetKlineData(symbol, PeriodOption.Per15Minute);
                        //var fourHourlyKlines = await this.GetKlineData(symbol, PeriodOption.FourHourly);
                        var per15MinuteKlines = allKlineData[per15MinuteMemoryKey];
                        var fourHourlyKlines = allKlineData[fourHourlyMemoryKey]; 
                        if (!(per15MinuteKlines?.Any() ?? false)) continue;
                        foreach (var strategy in this._strategyList)
                        { 
                            if((int)strategy.Side != future.PositionSide) continue; 
                            var per15MinuteKlineIC = IndexedObjectConstructor(per15MinuteKlines, per15MinuteKlines.Count() - 1);
                            var fourHourlyKlinesIC = IndexedObjectConstructor(fourHourlyKlines, fourHourlyKlines.Count() - 1);
                            await strategy.ExecuteAsync(
                                future,
                                per15MinuteKlineIC,
                                fourHourlyKlinesIC,
                                async (symbol, orderType, positionSide, amount) =>
                                {
                                    if (future.IsEnabled != 1 && future.OrdersCount == 0) return;
                                    var positionCount = futures.Count(x => x.OrdersCount > 0 || x.Status != FutureStatus.None);
                                    if (future.OrdersCount == 0 && positionCount >= this.MaxFutureCount) return;

                                    var quantity = amount / per15MinuteKlineIC.Close;
                                    var multiple = Convert.ToInt32(quantity / futureInfo.MinTradeQuantity);
                                    quantity = (multiple + 1) * futureInfo.MinTradeQuantity;
                                    var success = await this._trader.OpenPositionAsync(future.Id, symbol, orderType, positionSide, quantity, orderType == OrderType.Market ? null : per15MinuteKlineIC.Close);
                                    if (success)
                                    {
                                        future.Status = FutureStatus.Openning;
                                        future.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                                        await this.UpdateFutureAsync(future, new List<string>() { "Status", "UpdateTime" });
                                    }
                                },
                                async (symbol, orderType, positionSide, quantity) =>
                                {        
                                    var success = await this._trader.ClosePositionAsync(future.Id, symbol, orderType, positionSide, quantity, orderType == OrderType.Market ? null : per15MinuteKlineIC.Close);
                                    if (success)
                                    {
                                        future.Status = FutureStatus.Closing;
                                        future.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                                        await this.UpdateFutureAsync(future, new List<string>() { "Status", "UpdateTime" });
                                    }
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

        public async Task CreateFutureAsync(Future future)
        {
            using var scope = this._serviceProvider.CreateScope();
            using var dbAccessor = scope.ServiceProvider.GetService<IDbAccessor>();
            var count = await dbAccessor.GetIQueryable<Future>().Where(x => x.Symbol == future.Symbol.ToUpper() && x.PositionSide == future.PositionSide).CountAsync();
            if (count > 0)
                throw new Exception($"{future.Symbol} {future.PositionSide} contract already exists.");
            var supportedSymbols = await this._exchange.GetSymbolsAsync();
            if (!supportedSymbols.Success)
                throw new Exception($"exchange get symbols error. detail:{supportedSymbols.Msg}");
            if (!(supportedSymbols.Data?.Any(x => x.Name == future.Symbol) ?? false))
                throw new Exception($"trading pairs {future.Symbol} are not supported");
            await dbAccessor.InsertAsync(future);
            this._memoryCache.Remove(futuresMemoryKey);
        }

        public async Task<List<Ohlcv>> GetKlineData(string symbol, PeriodOption period)
        {
            var result = await this.GetAllKlineData();
            var memoryKey = string.Format(klineDataMemoryKey, symbol, (int)period);
            if (result.ContainsKey(memoryKey))
                return result[memoryKey];
            else
                return new List<Ohlcv>();
        }

        public async Task<Dictionary<string, List<Ohlcv>>> GetAllKlineData(bool isRealTime = false)
        {
            var baseUrl = this._configuration["QuotesBaseUrl"];
            if (isRealTime)
            {
                var result = await RestSharpHttpHelper.RestActionAsync(baseUrl, string.Empty);
                return result.ToObject<Dictionary<string, List<Ohlcv>>>();
            }
            else
            {
                var result = await this._memoryCache.GetOrSetObjectAsync<Dictionary<string, List<Ohlcv>>>(futuresMemoryKey, async () =>
                {
                    var result = await RestSharpHttpHelper.RestActionAsync(baseUrl, string.Empty);
                    return result.ToObject<Dictionary<string, List<Ohlcv>>>();
                }, new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(3) });
                return result;
            }
        }

        async Task<IEnumerable<FutureOrder>> GetFutureOrders()
        {
            var baseUrl = this._configuration["QuotesBaseUrl"];
            var result = await RestSharpHttpHelper.RestActionAsync(baseUrl, "/orders");
            return result.ToObject<IEnumerable<FutureOrder>>();
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
            var result = await this._memoryCache.GetOrSetObjectAsync<IEnumerable<Future>>(futuresMemoryKey, async () =>
            {
                using var scope = this._serviceProvider.CreateScope();
                using var dbAccessor = scope.ServiceProvider.GetService<IDbAccessor>();
                var futures = await dbAccessor.GetIQueryable<Future>().ToListAsync();
                return futures;
            }, new MemoryCacheEntryOptions() { SlidingExpiration = TimeSpan.FromDays(30) });
            return result.OrderBy(x=>Guid.NewGuid()).ToList();
        }

        public async Task UpdateFutureAsync(Future future, List<string> properties)
        {
            using var scope = this._serviceProvider.CreateScope();
            using var dbAccessor = scope.ServiceProvider.GetService<IDbAccessor>();
            await dbAccessor.UpdateAsync(future, properties);
            this._memoryCache.Remove(futuresMemoryKey);
        }

        public async Task SyncExchangeDataAsync()
        {
            var futureOrders = await this.GetFutureOrders();
            using var scope = this._serviceProvider.CreateScope();
            using var dbAccessor = scope.ServiceProvider.GetService<IDbAccessor>();
            var futures = await this.GetFuturesAsync();
            var dbOrders = await dbAccessor.GetIQueryable<Entity.Order>().Where(x => x.Status != OrderStatus.Filled.GetDescription() && x.Status != OrderStatus.Invalid.GetDescription() && x.Status != OrderStatus.Expired.GetDescription() && x.Status != OrderStatus.Canceled.GetDescription()).ToListAsync();
            foreach (var dbOrder in dbOrders)
            { 
                var order = futureOrders?.FirstOrDefault(x=>x.Symbol == dbOrder.Symbol && x.Id == long.Parse(dbOrder.ClientOrderId));
                if (order == null)
                {
                    var orderResult = await this._exchange.GetOrderAsync(dbOrder.Symbol, long.Parse(dbOrder.ClientOrderId));
                    if (!orderResult.Success)
                    {
                        //if (DateTime.Now.Subtract(DateTime.Parse(dbOrder.CreateTime.Remove(dbOrder.CreateTime.Length - 4))).TotalSeconds < 60)
                        //    continue;
                        this._logger.LogError($"无法从交易所获取定单详细. orderId:{dbOrder.Id}, clientOrderId:{dbOrder.ClientOrderId}.  errorcode:{orderResult.ErrorCode} msg:{orderResult.Msg}");
                        this._logger.LogError($"{dbOrder.ToJson()}");
                        if (orderResult.ErrorCode == -2013)
                        {
                            if (DateTime.Now.Subtract(DateTime.Parse(dbOrder.CreateTime.Remove(dbOrder.CreateTime.Length - 4))).TotalSeconds < 60)
                                continue;
                            dbOrder.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                            dbOrder.Status = OrderStatus.Invalid.GetDescription();
                            await dbAccessor.UpdateAsync(dbOrder, new List<string>() { "UpdateTime", "Status" });
                            var future = futures.FirstOrDefault(x => x.Status != FutureStatus.None && x.Id == dbOrder.FutureId);
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
                    order = orderResult.Data;
                }
                if (order.Status == OrderStatus.New || order.Status == OrderStatus.PartiallyFilled)
                {
                    if (DateTime.Now.Subtract(DateTime.Parse(dbOrder.CreateTime.Remove(dbOrder.CreateTime.Length - 4))).TotalSeconds > 100)
                    {
                        var cancelOrderResult = await this._exchange.CancelOrderAsync(order.Symbol, order.Id);
                        this._logger.LogWarning($"pending order timed out, execute order cancellation. detail:{order.ClientOrderId}/{order.Symbol}/{order.Type.GetDescription()}/{order.Price}.");
                        if (!cancelOrderResult.Success)
                            this._logger.LogError(cancelOrderResult.Msg);
                        continue;
                    }
                    continue;
                }

                dbOrder.UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff");
                dbOrder.Status = order.Status.GetDescription();
                dbOrder.AvgPrice = order.AvgPrice;
                await dbAccessor.UpdateAsync(dbOrder, new List<string>() { "UpdateTime", "Status", "AvgPrice" });
                if (order.Status == OrderStatus.Filled)
                {
                    dbOrder.FilledQuantity = dbOrder.Quantity;
                    await dbAccessor.UpdateAsync(dbOrder, new List<string>() { "FilledQuantity" });
                    var future = futures.FirstOrDefault(x => x.Id == dbOrder.FutureId);
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
                        this._logger.LogWarning($"[{future.Symbol} {(future.PositionSide == 1 ? "Long" : "Short")}] Open Position. safety order [{future.OrdersCount - 1}/{future.MaxSafetyOrdersCount}]. Size:{order.Quantity}, Price/Value:{order.AvgPrice}/{order.AvgPrice * order.Quantity} USDT.");
                    }
                    else if (future.Status == FutureStatus.Closing)
                    {
                        var avgPrice = order.AvgPrice;
                        var entryPrice = future.EntryPrice;
                        dbOrder.PNL = (dbOrder.PositionSide == "Long" ? 1 : -1) * (order.AvgPrice - future.EntryPrice) * dbOrder.Quantity;
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
                        this._logger.LogWarning($"[{future.Symbol} {(future.PositionSide == 1 ? "Long" : "Short")}] Close Position. Size:{order.Quantity}, EntryPrice/ClosePrice:{entryPrice}/{avgPrice}, PNL/Value:{dbOrder.PNL ?? 0}/{order.AvgPrice * order.Quantity} USDT.");
                    }
                    else
                        this._logger.LogError($"错误的合约配置状态{order.Id}/{order.Symbol}/{order.PositionSide.GetDescription()}/{future.Status.GetDescription()}");
                }
                else if (order.Status == OrderStatus.Expired || order.Status == OrderStatus.Canceled)
                {
                    dbOrder.FilledQuantity = order.LastFilledQuantity;
                    await dbAccessor.UpdateAsync(dbOrder, new List<string>() { "FilledQuantity" });

                    var future = futures.FirstOrDefault(x => x.Id == dbOrder?.FutureId);
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
                        dbOrder.PNL = (dbOrder.PositionSide == "Long" ? 1 : -1) * (order.AvgPrice - future.EntryPrice) * order.LastFilledQuantity;
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
                    //var future = futures.FirstOrDefault(x => x.Status != FutureStatus.None && x.Id == dbOrder.FutureId);
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
                            PNL = future.EntryPrice == 0 ? 0 : (adlOrder.PositionSide == PositionSide.Long ? 1 : -1) * (adlOrder.AvgPrice - future.EntryPrice) * adlOrder.Quantity
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
