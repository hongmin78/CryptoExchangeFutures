using CEF.Common.Context;
using CEF.Common.Exchange;
using CEF.Common.Strategy;
using Coldairarrow.Util;
using EFCore.Sharding;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CEF.Common.Entity;
using CEF.Common.Extentions;
using Microsoft.Extensions.DependencyInjection;

namespace CEF.Common.Trader
{
    public class FuturesTrader : ITrader, ITransientDependency
    {
        private readonly ILogger<FuturesTrader> _logger;
        private readonly IServiceProvider _serviceProvider; 
        private readonly IEnumerable<IStrategy> _strategyList;
        private readonly IExchange _exchange;
        private readonly IMemoryCache _memoryCache;
        public FuturesTrader(IServiceProvider serviceProvider,
                            ILogger<FuturesTrader> logger, 
                            IExchange exchange,
                            IEnumerable<IStrategy> strategyList,
                            IMemoryCache memoryCache)
        {
            _serviceProvider = serviceProvider;
            _logger = logger; 
            _strategyList = strategyList;
            _exchange = exchange;
            _memoryCache = memoryCache;
        }


        public async Task<bool> ClosePositionAsync(long futureId, string symbol, OrderType orderType, PositionSide side, decimal? quantity, decimal? price = null)
        {
            using var scope = this._serviceProvider.CreateScope();
            using var dbAccessor = scope.ServiceProvider.GetService<IDbAccessor>();
            var clientOrderId = IdHelper.GetLongId();
            var callResult = await this._exchange.ClosePositionAsync(symbol, orderType, side, quantity, price, clientOrderId.ToString());
            if (callResult.Success)
            {
                this._logger.LogInformation($"平仓 {symbol}/{orderType.GetDescription()}/{side.GetDescription()}/{quantity}/{price}"); 
                var order = new Order()
                {
                    Id = clientOrderId,
                    AvgPrice = null,
                    ClientOrderId =  callResult.Data.Id.ToString(),
                    CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"),
                    Side = (side switch
                    {
                        PositionSide.Short => Binance.Net.Enums.OrderSide.Buy,
                        PositionSide.Long => Binance.Net.Enums.OrderSide.Sell,
                        _ => throw new NotSupportedException($"不支持类型{side},且仅可选择 LONG 或 SHORT"),
                    }).GetDescription(),
                    PositionSide = side.GetDescription(),
                    Price = price,
                    Quantity = quantity.Value,
                    Status = OrderStatus.New.GetDescription(),
                    Symbol = symbol,
                    Type = orderType.GetDescription(),
                    UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"),
                    OrderSide = "Close",
                    FutureId = futureId
                };
                await dbAccessor.InsertAsync(order);
            }
            else
                this._logger.LogError($"平仓失败. errorcode:{callResult.ErrorCode}, message:{callResult.Msg}, 参数:{symbol}/{orderType}/{side}/{quantity}/{price}");
            return callResult.Success;
        }

        public async Task<bool> OpenPositionAsync(long futureId, string symbol, OrderType orderType, PositionSide side, decimal? quantity, decimal? price = null)
        {
            using var scope = this._serviceProvider.CreateScope();
            using var dbAccessor = scope.ServiceProvider.GetService<IDbAccessor>();
            var clientOrderId = IdHelper.GetLongId();
            var callResult = await this._exchange.OpenPositionAsync(symbol, orderType, side, quantity, price, clientOrderId.ToString());
            if (callResult.Success)
            {
                this._logger.LogInformation($"开仓 {symbol}/{orderType.GetDescription()}/{side.GetDescription()}/{quantity}/{price}");
                var order = new Order()
                {
                    Id = clientOrderId,
                    AvgPrice = null,
                    ClientOrderId = callResult.Data.Id.ToString(),
                    CreateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"),
                    Side = (side switch
                    {
                        PositionSide.Short => Binance.Net.Enums.OrderSide.Sell,
                        PositionSide.Long => Binance.Net.Enums.OrderSide.Buy,
                        _ => throw new NotSupportedException($"不支持类型{side},且仅可选择 LONG 或 SHORT"),
                    }).GetDescription(),
                    PositionSide = side.GetDescription(),
                    Price = price,
                    Quantity = quantity.Value,
                    Status = OrderStatus.New.GetDescription(),
                    Symbol = symbol,
                    Type = orderType.GetDescription(),
                    UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"),
                    OrderSide = "Open",
                    FutureId = futureId
                };
                await dbAccessor.InsertAsync(order);
            }
            else
                this._logger.LogError($"开仓失败. errorcode:{callResult.ErrorCode}, message:{callResult.Msg}, 参数:{symbol}/{orderType}/{side}/{quantity}/{price}");
            return callResult.Success;
        }
    }
}
