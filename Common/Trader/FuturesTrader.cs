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

namespace CEF.Common.Trader
{
    public class FuturesTrader : ITrader, ITransientDependency
    {
        private readonly ILogger<FuturesTrader> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDbAccessor _dbAccessor;
        private readonly ITrader _trader;
        private readonly IEnumerable<IStrategy> _strategyList;
        private readonly IExchange _exchange;
        private readonly IMemoryCache _memoryCache;
        public FuturesTrader(IServiceProvider serviceProvider,
                            ITrader trader,
                            ILogger<FuturesTrader> logger,
                            IDbAccessor dbAccessor,
                            IExchange exchange,
                            IEnumerable<IStrategy> strategyList,
                            IMemoryCache memoryCache)
        {
            _serviceProvider = serviceProvider;
            _trader = trader;
            _logger = logger;
            _dbAccessor = dbAccessor;
            _strategyList = strategyList;
            _exchange = exchange;
            _memoryCache = memoryCache;
        }


        public async Task ClosePositionAsync(string symbol, OrderType orderType, PositionSide side, decimal? quantity, decimal? price = null)
        {
            var callResult = await this._exchange.ClosePositionAsync(symbol, orderType, side, quantity, price, IdHelper.GetId());
            if (callResult.Success)
            {
                var order = new Order()
                {
                    Id = callResult.Data.Id,
                    AvgPrice = callResult.Data?.AvgPrice,
                    ClientOrderId = callResult.Data?.ClientOrderId,
                    CreateTime = callResult.Data?.CreateTime.ToString("yyyy-MM-dd HH:mm:ss fff"),
                    Side = callResult.Data?.Side.GetDescription(),
                    PositionSide = callResult.Data?.PositionSide.GetDescription(),
                    Price = callResult.Data?.Price,
                    Quantity = callResult.Data.Quantity,
                    Status = callResult.Data?.Status.GetDescription(),
                    Symbol = callResult.Data?.Symbol,
                    Type = callResult.Data?.Type.GetDescription(),
                    UpdateTime = callResult.Data?.UpdateTime.ToString("yyyy-MM-dd HH:mm:ss fff"),
                    OrderSide = "Close"
                };
                await this._dbAccessor.InsertAsync(order);
                this._logger.LogInformation($"平仓 {symbol}/{orderType.GetDescription()}/{side.GetDescription()}/{quantity}/{price}");
            }
            else
                this._logger.LogError($"平仓失败. errorcode:{callResult.ErrorCode}, message:{callResult.Msg}, 参数:{symbol}/{orderType}/{side}/{quantity}/{price}");           
        }

        public async Task OpenPositionAsync(string symbol, OrderType orderType, PositionSide side, decimal? quantity, decimal? price = null)
        {
            var callResult = await this._exchange.OpenPositionAsync(symbol, orderType, side, quantity, price, IdHelper.GetId());
            if (callResult.Success)
            {
                var order = new Order()
                {
                    Id = callResult.Data.Id,
                    AvgPrice = callResult.Data?.AvgPrice,
                    ClientOrderId = callResult.Data?.ClientOrderId,
                    CreateTime = callResult.Data?.CreateTime.ToString("yyyy-MM-dd HH:mm:ss fff"),
                    Side = callResult.Data?.Side.GetDescription(),
                    PositionSide = callResult.Data?.PositionSide.GetDescription(),
                    Price = callResult.Data?.Price,
                    Quantity = callResult.Data.Quantity,
                    Status = callResult.Data?.Status.GetDescription(),
                    Symbol = callResult.Data?.Symbol,
                    Type = callResult.Data?.Type.GetDescription(),
                    UpdateTime = callResult.Data?.UpdateTime.ToString("yyyy-MM-dd HH:mm:ss fff"),
                    OrderSide = "Open"
                };
                await this._dbAccessor.InsertAsync(order);
                this._logger.LogInformation($"开仓 {symbol}/{orderType.GetDescription()}/{side.GetDescription()}/{quantity}/{price}");
            }
            else
                this._logger.LogError($"开仓失败. errorcode:{callResult.ErrorCode}, message:{callResult.Msg}, 参数:{symbol}/{orderType}/{side}/{quantity}/{price}");
        } 
    }
}
