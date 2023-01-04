using CEF.Common;
using CEF.Common.Entity;
using CEF.Common.Exchange;
using CEF.Common.Extentions;
using Coldairarrow.Util;
using CryptoExchange.Net.Objects;
using EFCore.Sharding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace CEF.ConsoleApp
{
    public class HostService : BackgroundService
    { 
        private readonly ILogger<HostService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IExchange _exchange;
        private readonly IDbAccessor _dbAccessor;
        public HostService(ILogger<HostService> logger,
                        IServiceProvider serviceProvider,
                        IExchange exchange,
                        IDbAccessor dbAccessor)
        {
            this._logger = logger;
            this._serviceProvider = serviceProvider;
            this._exchange = exchange;
            this._dbAccessor = dbAccessor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //await TestSubscribeToKlineUpdatesAsync();
            //await TestSubscribeToUserDataUpdatesAsync();
            //await CreateFutureAsync("BTCUSDT");
            //await CreateFutureAsync("ETHUSDT");
            //await CreateFutureAsync("BCHUSDT");
            //await CreateFutureAsync("XRPUSDT");
            //await CreateFutureAsync("LTCUSDT");
            //await CreateFutureAsync("LINKUSDT");
            //await CreateFutureAsync("ATOMUSDT");
            //await CreateFutureAsync("DOGEUSDT");
            //await CreateFutureAsync("UNIUUSDT");
            //await CreateFutureAsync("AVAXUSDT");
            //await CreateFutureAsync("FTMUSDT");
            //await CreateFutureAsync("MATICUSDT");
            await GetFuturesAsync();
            //await TestGetFuturesInfo();
        }

        private async Task GetFuturesAsync()
        {
            var list = await this._dbAccessor.GetIQueryable<Future>().ToListAsync();
            foreach(var item in list)
            {
                item.UpdateTime = DateTime.Now.ToLongDateString();
                item.Size = 0;  
                item.AbleSize = 0;  
            }
            Console.WriteLine("操作成功");
        }

        private async Task CreateFutureAsync(string symbol)
        {
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
                PositionSide = (int)PositionSide.Short,
                Size = 0,
                TargetProfit = 0.012m,
                SafetyOrderSize = 300m,
                SafetyOrderPriceDeviation = 0.018m,
                SafetyOrderPriceScale = 2m,
                SafetyOrderVolumeScale = 2m,
                IsEnabled = 1
            };
            await this._dbAccessor.InsertAsync(entity);
            entity = new Future()
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
                PositionSide = (int)PositionSide.Long,
                Size = 0,
                TargetProfit = 0.012m,
                SafetyOrderSize = 300m,
                SafetyOrderPriceDeviation = 0.018m,
                SafetyOrderPriceScale = 2m,
                SafetyOrderVolumeScale = 2m,
                IsEnabled = 1
            };
            await this._dbAccessor.InsertAsync(entity);
        }

        private async Task TestGetFuturesInfo()
        {
            var result = await this._exchange.GetSymbolsAsync();
            Console.WriteLine(result.ToJson());
        }

        private async Task ClosePositionAsync()
        {
            var result = await this._exchange.ClosePositionAsync("BTCUSDT", OrderType.Market, Common.Exchange.PositionSide.Long, 0.001m);
            if (result.Success)
                Console.WriteLine("平仓成功.");
            else
                Console.WriteLine(result.Msg);

            //result = await this._exchange.ClosePositionAsync("BTCUSDT", OrderType.Market, Common.Exchange.PositionSide.Short, 0.001m);
            //if (result.Success)
            //    Console.WriteLine("平仓成功.");
            //else
            //    Console.WriteLine(result.Msg);
        }

        private async Task OpenPositionAsync()
        {
            var result = await this._exchange.OpenPositionAsync("BTCUSDT", OrderType.Market, Common.Exchange.PositionSide.Long, 0.001m);
            if (result.Success)
                Console.WriteLine("开仓成功.");
            else
                Console.WriteLine(result.Msg);

            //result = await this._exchange.OpenPositionAsync("BTCUSDT", OrderType.Market, Common.Exchange.PositionSide.Short, 0.001m);
            //if (result.Success)
            //    Console.WriteLine("开仓成功.");
            //else
            //    Console.WriteLine(result.Msg);
        }

        private async Task TestSubscribeToUserDataUpdatesAsync()
        {
            var result = await this._exchange.SubscribeToUserDataUpdatesAsync(
                m => Console.WriteLine($"onMarginUpdate:{m.ToJson()}"),
                a => Console.WriteLine($"onAccountUpdate:{a.ToJson()}"),
                o => Console.WriteLine($"onOrderUpdate:{o.ToJson()}"));
            if (result.Success)
                Console.WriteLine($"订阅用户数据成功.");
            else
                Console.WriteLine($"订阅用户数据失败, ErrorCode:{result.ErrorCode}, ErrorMessage:{result.Msg}");
        }

        private async Task TestSubscribeToKlineUpdatesAsync()
        {
            var symbols = new List<string>() { "BTCUSDT", "ETHUSDT" };
            var periods = new List<PeriodOption>() { PeriodOption.Per15Minute, PeriodOption.FourHourly };
            var result = await this._exchange.SubscribeToKlineUpdatesAsync(
                symbols,
                periods,
                x => Console.WriteLine(x.ToJson()));
            if (result.Success)
                Console.WriteLine($"订阅K线{string.Join(",", symbols)}成功.");
            else
                Console.WriteLine($"订阅K线{string.Join(",", symbols)}失败, ErrorCode:{result.ErrorCode}, ErrorMessage:{result.Msg}");
        }
    }
}
