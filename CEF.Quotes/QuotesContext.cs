using CEF.Common;
using CEF.Common.Context;
using CEF.Common.Exchange;
using CEF.Common.Strategy;
using CEF.Common.Trader;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Threading.Tasks.Dataflow;
using Trady.Core.Infrastructure;
using CEF.Common.Extentions;
using Trady.Core;
using CEF.Common.Entity;
using System.IO.MemoryMappedFiles;
using System.IO;
using System.Runtime.InteropServices;
using System.IO.Pipes;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Microsoft.CodeAnalysis.Text;

namespace CEF.Quotes
{
    public class QuotesContext : IQuotesContext, ISingletonDependency
    {
        private readonly string klineDataMemoryKey = "GetKlineDataAsync_{0}_{1}";
        private readonly ILogger<QuotesContext> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IExchange _exchange;
        private readonly IMemoryCache _memoryCache;
        private readonly IConfiguration _configuration;
        private List<FutureOrder> FutureOrders = new List<FutureOrder>();
        public QuotesContext(IServiceProvider serviceProvider,
                            ILogger<QuotesContext> logger,
                            IExchange exchange,
                            IConfiguration configuration,
                            IMemoryCache memoryCache)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _exchange = exchange;
            _memoryCache = memoryCache;
            this._configuration = configuration;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
        }

        public async Task Subscribe()
        {
            var symbols = this.GetSymbols();
            foreach (var symbol in symbols)
            {
                await GetKlineData(symbol, PeriodOption.Per15Minute);
                await GetKlineData(symbol, PeriodOption.FourHourly);
                await Task.Delay(1000);
                //SpinWait.SpinUntil(() => false, 1000);
            }
            await SubscribeToKlineUpdatesAsync(symbols);
            await SubscribeToUserDataUpdatesAsync();
        }

        private IEnumerable<string> GetSymbols()
        {
            string symbolString = this._configuration["Symbols"].ToString();
            var symbolArray = symbolString.Split(',', StringSplitOptions.RemoveEmptyEntries);
            return symbolArray;
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
                order =>
                {
                    try
                    {
                        var index = this.FutureOrders.FindIndex(x => x.Id == order.Id);
                        if (index != -1)
                            this.FutureOrders.RemoveAt(index);

                        this.FutureOrders.Add(order);
                        for (int i = this.FutureOrders.Count - 1; i >= 0; i--)
                        {
                            if (DateTime.Now.Subtract(this.FutureOrders[i].UpdateTime).TotalMinutes > 1)
                                this.FutureOrders.RemoveAt(i);
                        }
                    }
                    catch (Exception e)
                    {
                        this._logger.LogError(e, e.Message);
                    }
                });
        }

        async Task SubscribeToKlineUpdatesAsync(IEnumerable<string> symbols)
        {
            if (!symbols.Any()) return;
            var periods = new List<PeriodOption>() { PeriodOption.Per15Minute, PeriodOption.FourHourly };
            await this._exchange.SubscribeToKlineUpdatesAsync(symbols, periods, async kline =>
            {
                try
                {
                    var symbol = kline.Symbol;
                    var period = kline.Interval;
                    var memoryKey = string.Format(klineDataMemoryKey, symbol, (int)period);
                    var dt = DateTime.SpecifyKind(kline.OpenTime, DateTimeKind.Utc);
                    var klines = (await this.GetKlineData(symbol, period)).ToList();
                    var currentKLine = klines.FirstOrDefault(x => x.DateTime == dt);
                    if (currentKLine == null)
                    {
                        currentKLine = new Ohlcv() { DateTime = dt, Open = kline.OpenPrice, High = kline.HighPrice, Low = kline.LowPrice, Close = kline.ClosePrice, Volume = kline.Volume };
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
                    if (klines.Count > 20)
                        klines.RemoveAt(0);
                    this._memoryCache.Set(memoryKey, klines);
                }
                catch (Exception e)
                {
                    this._logger.LogError(e, e.Message);
                }
            });
        }

        async Task<List<Ohlcv>> GetKlineData(string symbol, PeriodOption period)
        {
            var symbols = this.GetSymbols();
            if (!symbols.Contains(symbol)) return new List<Ohlcv>();
            var memoryKey = string.Format(klineDataMemoryKey, symbol, (int)period);
            var result = await this._memoryCache.GetOrSetObjectAsync<List<Ohlcv>>(memoryKey, async () =>
            {
                var callResult = await this._exchange.GetKlineDataAsync(symbol, period, DateTime.Now.AddDays(-1));
                if (callResult.Success)
                {
                    //await Task.Delay(1000);
                    return callResult.Data.Select(x => new Ohlcv() { Close = x.Close, High = x.High, Low = x.Low, Open = x.Open, Volume = x.Volume, DateTime = x.DateTime }).ToList();
                }
                else
                {
                    this._logger.LogError($"GetKlineDataAsync 调用失败. errorcode:{callResult.ErrorCode} detail:{callResult.Msg}");
                    return default;
                }
            }, new MemoryCacheEntryOptions() { SlidingExpiration = TimeSpan.FromDays(999) });
            return result;
        }

        void RemoveKlineData(string symbol, PeriodOption period)
        {
            var memoryKey = string.Format(klineDataMemoryKey, symbol, (int)period);
            this._memoryCache.Remove(memoryKey);
        }

        public IEnumerable<FutureOrder> GetFutureOrders()
        {
            return this.FutureOrders;
        }

        public async Task<Dictionary<string, List<Ohlcv>>> GetAllKlineData()
        {
            var result = new Dictionary<string, List<Ohlcv>>();
            var symbols = this.GetSymbols();
            var periods = new List<PeriodOption>() { PeriodOption.Per15Minute, PeriodOption.FourHourly };
            foreach (var symbol in symbols)
            {
                foreach (var period in periods)
                {
                    var memoryKey = string.Format(klineDataMemoryKey, symbol, (int)period);
                    var ohlcvs = await this.GetKlineData(symbol, period);
                    result.Add(memoryKey, ohlcvs);
                }
            }
            return result;
        }

        public async Task<string> Add(string symbol) 
        {
            var symbols = this._configuration["Symbols"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (symbols.Contains(symbol))
                return $"the {symbol} already exists.";
            var supportedSymbols = await this._exchange.GetSymbolsAsync();
            if (!supportedSymbols.Success)
                return $"exchange get symbols error. detail:{supportedSymbols.Msg}";
            if (!(supportedSymbols.Data?.Any(x => x.Name == symbol) ?? false))
                return $"Trading pairs {symbol} are not supported";
            symbols.Add(symbol);
            WriteToAppsettings(string.Join(",", symbols));
            await UnSubscribeAll();
            await GetKlineData(symbol, PeriodOption.Per15Minute);
            await GetKlineData(symbol, PeriodOption.FourHourly);
            await SubscribeToKlineUpdatesAsync(symbols);
            await SubscribeToUserDataUpdatesAsync();
            return await Task.FromResult("ok");
        }

        private async Task UnSubscribeAll()
        {
            await this._exchange.UnsubscribeAllAsync();
        }

        public async Task<string> Remove(string symbol)
        {
            var symbols = this._configuration["Symbols"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (!symbols.Contains(symbol))
                return $"the {symbol} not exists.";
            symbols.Remove(symbol);
            WriteToAppsettings(string.Join(",", symbols));
            await UnSubscribeAll();
            RemoveKlineData(symbol, PeriodOption.Per15Minute);
            RemoveKlineData(symbol, PeriodOption.FourHourly);
            await SubscribeToKlineUpdatesAsync(symbols);
            await SubscribeToUserDataUpdatesAsync();
            return await Task.FromResult("ok");
        }

        void WriteToAppsettings(string value)
        {
            string filePath = $"{System.AppDomain.CurrentDomain.BaseDirectory}/appsettings.json";
            JObject jsonObject;
            using (StreamReader file = new StreamReader(filePath))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                jsonObject = (JObject)JToken.ReadFrom(reader);
                jsonObject["Symbols"] = value;
            }
            using (var writer = new StreamWriter(filePath))
            using (JsonTextWriter jsonwriter = new JsonTextWriter(writer))
            {
                jsonObject.WriteTo(jsonwriter);
            }
        }
    }

    public interface IQuotesContext 
    {
        Task Subscribe();
        Task<Dictionary<string, List<Ohlcv>>> GetAllKlineData();
        IEnumerable<FutureOrder> GetFutureOrders();

        Task<string> Add(string symbol);

        Task<string> Remove(string symbol);
    }
}
