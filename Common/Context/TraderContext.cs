using CEF.Common;
using CEF.Common.Entity;
using CEF.Common.Exchange;
using CEF.Common.Extentions;
using CEF.Common.Strategy;
using CEF.Common.Trader;
using Coldairarrow.Util;
using CryptoExchange.Net.CommonObjects;
using EFCore.Sharding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trady.Core;
using Trady.Core.Infrastructure;

namespace CEF.Common.Context
{
    public class TraderContext : IContext, ITransientDependency
    {
        readonly string klineDataMemoryKey = "GetKlineDataAsync_{0}_{1}";
        private readonly ILogger<TraderContext> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDbAccessor _dbAccessor;
        private readonly ITrader _trader;
        private readonly IEnumerable<IStrategy> _strategyList;
        private readonly IExchange _exchange;
        private readonly IMemoryCache _memoryCache;
        public TraderContext(IServiceProvider serviceProvider,
                            ITrader trader,
                            ILogger<TraderContext> logger,
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

        public async Task ExecuteAsync()
        {
            var futures = await GetFuturesAsync();
            await SubscribeToKlineUpdatesAsync(futures.Select(x => x.Symbol), new List<PeriodOption>() { PeriodOption.Per15Minute, PeriodOption.FourHourly });



        }

        async Task SubscribeToKlineUpdatesAsync(IEnumerable<string> symbols, IEnumerable<PeriodOption> periods)
        {
            await this._exchange.SubscribeToKlineUpdatesAsync(symbols, periods, async kline =>
            {
                var symbol = kline.Symbol;
                var period = kline.Interval;
                var memoryKey = string.Format(klineDataMemoryKey, symbol, (int)period);
                var klineData = (await this.GetKlineDataAsync(symbol, period)).ToList();
                var dt = DateTime.SpecifyKind(kline.OpenTime, DateTimeKind.Utc);
                var currentKLine = klineData.FirstOrDefault(x => x.DateTime == dt);
                if (currentKLine != null)
                {
                    currentKLine.Volume = kline.Volume;
                    currentKLine.Close = kline.ClosePrice;
                    currentKLine.High = kline.HighPrice;
                    currentKLine.Low = kline.LowPrice;
                    currentKLine.Open = kline.OpenPrice;
                }
                else
                {
                    currentKLine = new Candle(DateTime.SpecifyKind(kline.OpenTime, DateTimeKind.Utc), kline.OpenPrice, kline.HighPrice, kline.LowPrice, kline.ClosePrice, kline.Volume);
                    klineData.Add(currentKLine);
                }
                this._memoryCache.Set(memoryKey, klineData);
            });
        }

        async Task<IEnumerable<IOhlcv>> GetKlineDataAsync(string symbol, PeriodOption period)
        {
            var memoryKey = string.Format(klineDataMemoryKey, symbol, (int)period);
            var result = await this._memoryCache.GetOrSetObjectAsync<IEnumerable<IOhlcv>>(memoryKey, async () =>
            {
                var callResult = await this._exchange.GetKlineDataAsync(symbol, period, DateTime.Now.AddDays(-1));
                if (callResult.Success)
                    return callResult.Data;
                else
                {
                    this._logger.LogError($"GetKlineDataAsync 调用失败. errorcode:{callResult.ErrorCode} detail:{callResult.Msg}");
                    return default;
                }
            }, new MemoryCacheEntryOptions() { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(9999) });
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
            var memoryKey = "GetFuturesAsync";
            var result = await this._memoryCache.GetOrSetObjectAsync<IEnumerable<Future>>(memoryKey, async () =>
            {
                var futures = await this._dbAccessor.GetIQueryable<Future>().ToListAsync();
                return futures;
            }, new MemoryCacheEntryOptions() { SlidingExpiration = TimeSpan.FromDays(30) });
            return result;
        }
    }
}
