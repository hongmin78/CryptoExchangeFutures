using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects;
using CEF.Common.Extentions;
using CEF.Common.Primitives;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.CommonObjects;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Index.HPRtree;
using NetTopologySuite.Utilities;
using OfficeOpenXml.Style;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using Trady.Analysis.Backtest;
using Trady.Core;
using Trady.Core.Infrastructure;

namespace CEF.Common.Exchange
{
    public class BinanceFuturesUSDT_MExchange : IExchange, ITransientDependency
    { 
        public SpotExchange Exchange => SpotExchange.BinanceFuturesUSDT_M;
        private string APIKEY { set; get; }
        //private readonly IConfiguration _configuration;
        private readonly ILogger<BinanceFuturesUSDT_MExchange> _logger;
        private readonly BinanceClient Client;
        private readonly BinanceSocketClient Ws;
        private readonly IMemoryCache _memoryCache;
        public BinanceFuturesUSDT_MExchange(
            ILogger<BinanceFuturesUSDT_MExchange> logger,
            IConfiguration configuration,
            IMemoryCache memoryCache)
        {
            this._logger = logger;
            this._memoryCache = memoryCache;
            //this._configuration = configuration;
            var isTestenet = configuration.GetSection("metaTrade:IsTestenet").Get<bool>();
            var ApiKey = configuration.GetSection("metaTrade:ApiKey").Get<string>()?? "e33d5a33bcadde8e55532c37cbc051be3a2dd52422d6330908b6e9bdc524d5ed";
            var ApiSeret = configuration.GetSection("metaTrade:ApiSeret").Get<string>()?? "6c61036b51a8110ef7118fca42ac16a651f580d1735b949ce86d1a65b30cc1ac";
            var restBaseurl = "https://testnet.binancefuture.com";
            var websocketBaseurl = "wss://stream.binancefuture.com";
            this.APIKEY = ApiKey;
            BinanceClientOptions clientOption = new BinanceClientOptions
            {
                LogWriters = new List<ILogger> { logger },
                LogLevel = LogLevel.Information,
                ApiCredentials = new CryptoExchange.Net.Authentication.ApiCredentials(ApiKey, ApiSeret),
                UsdFuturesApiOptions = new Binance.Net.Objects.BinanceApiClientOptions()
                {
                    AutoTimestamp = false,
                    TradeRulesBehaviour = Binance.Net.Enums.TradeRulesBehaviour.None,
                }
            };
            BinanceSocketClientOptions socketOption = new BinanceSocketClientOptions
            {
                LogWriters = new List<ILogger> { logger },
                LogLevel = LogLevel.Information,
                ApiCredentials = new CryptoExchange.Net.Authentication.ApiCredentials(ApiKey, ApiSeret),
                UsdFuturesStreamsOptions = new CryptoExchange.Net.Objects.SocketApiClientOptions()
                {
                    AutoReconnect = true,
                    ReconnectInterval = TimeSpan.FromMinutes(1),
                }
            };
           
            if (isTestenet)
            {
                clientOption.UsdFuturesApiOptions.BaseAddress = restBaseurl;
                socketOption.UsdFuturesStreamsOptions.BaseAddress = websocketBaseurl;
            }
            this.Client = new Binance.Net.Clients.BinanceClient(clientOption);
            this.Ws = new Binance.Net.Clients.BinanceSocketClient(socketOption);           
        }

        public async Task<CallResult<IEnumerable<FutureInfo>>> GetSymbolsAsync()
        {
            var callResult = await this.Client.UsdFuturesApi.CommonFuturesClient.GetSymbolsAsync();
            return new CallResult<IEnumerable<FutureInfo>>()
            {
                Success = callResult.Success,
                ErrorCode = callResult.Error?.Code,
                Msg = callResult.Error?.Message,
                Data = callResult.Data?.Select(x => new FutureInfo()
                {
                    MinTradeQuantity = x.MinTradeQuantity,
                    Name = x.Name,
                    PriceDecimals = x.PriceDecimals,
                    PriceStep = x.PriceStep,
                    QuantityDecimals = x.QuantityDecimals,
                    QuantityStep = x.QuantityStep,
                })
            };
        }

        public async Task<CallResult> CancelAllOrdersAsync(string symbol)
        {
            var result = await this.Client.UsdFuturesApi.Trading.CancelAllOrdersAsync(symbol);
            return new CallResult() { Success = result.Success, ErrorCode = result.Error?.Code, Msg = result.Error?.Message };
        }

        public async Task<CallResult> CancelOrderAsync(string symbol, long? orderId = null, string? origClientOrderId = null)
        {
            var result = await this.Client.UsdFuturesApi.Trading.CancelOrderAsync(symbol, orderId, origClientOrderId);
            return new CallResult() { Success = result.Success, ErrorCode = result.Error?.Code, Msg = result.Error?.Message };
        }

        public async Task<CallResult<IEnumerable<FutureBalance>>> GetBalanceAsync()
        {
            var result = await this.Client.UsdFuturesApi.Account.GetBalancesAsync();
            var balances = result.Data?.Select(x => new FutureBalance()
            {
                Asset = x.Asset,
                AvailableBalance = x.AvailableBalance,
                CrossUnrealizedPnl = x.CrossUnrealizedPnl,
                CrossWalletBalance = x.CrossWalletBalance,
                MarginAvailable = x.MarginAvailable,
                WalletBalance = x.WalletBalance,
            });
            return new CallResult<IEnumerable<FutureBalance>>()
            {
                Success = result.Success,
                Data = balances,
                ErrorCode = result.Error?.Code,
                Msg = result.Error?.Message
            };
        }

        public async Task<CallResult<IEnumerable<IOhlcv>>> GetKlineDataAsync(string symbol, PeriodOption period, DateTime? startTime = null, DateTime? endTime = null, int? limit = null)
        {
            var result = await this.Client.UsdFuturesApi.ExchangeData.GetKlinesAsync(symbol, period switch
            {
                PeriodOption.Per5Minute => Binance.Net.Enums.KlineInterval.FiveMinutes,
                PeriodOption.PerMinute => Binance.Net.Enums.KlineInterval.OneMinute,
                PeriodOption.Per15Minute => Binance.Net.Enums.KlineInterval.FifteenMinutes,
                PeriodOption.Per30Minute => Binance.Net.Enums.KlineInterval.ThirtyMinutes,
                PeriodOption.Hourly => Binance.Net.Enums.KlineInterval.OneHour,
                PeriodOption.TwoHourly => Binance.Net.Enums.KlineInterval.TwoHour,
                PeriodOption.FourHourly => Binance.Net.Enums.KlineInterval.FourHour,
                PeriodOption.Daily => Binance.Net.Enums.KlineInterval.OneDay,
                _ => throw new NotSupportedException($"不支持类型{period}.")
            }, startTime, endTime, limit);
            var candles = result.Data?.Select(x => new Candle(DateTime.SpecifyKind(x.OpenTime, DateTimeKind.Utc), x.OpenPrice, x.HighPrice, x.LowPrice, x.ClosePrice, x.Volume));
            return new CallResult<IEnumerable<IOhlcv>>()
            {
                Success = result.Success,
                Data = candles,
                ErrorCode = result.Error?.Code,
                Msg = result.Error?.Message
            };
        }

        public async Task<CallResult<IEnumerable<FutureOrder>>> GetOpenOrdersAsync(string? symbol = null)
        {
            var result = await this.Client.UsdFuturesApi.Trading.GetOpenOrdersAsync(symbol);
            var balances = result.Data?.Select(x => new FutureOrder()
            {
                AvgPrice = x.AvgPrice,
                ClientOrderId = x.ClientOrderId,
                Id = x.Id,
                Price = x.Price,
                Quantity = x.Quantity,
                ReduceOnly = x.ReduceOnly,
                Symbol = x.Symbol,
                UpdateTime = x.UpdateTime,
                PositionSide = x.PositionSide switch
                {
                    Binance.Net.Enums.PositionSide.Short => PositionSide.Short,
                    Binance.Net.Enums.PositionSide.Both => PositionSide.Both,
                    Binance.Net.Enums.PositionSide.Long => PositionSide.Long,
                    _ => throw new NotSupportedException($"不支持类型{x.PositionSide}"),
                },
                Type = x.Type switch
                {
                    Binance.Net.Enums.FuturesOrderType.Market => OrderType.Market,
                    Binance.Net.Enums.FuturesOrderType.TakeProfitMarket => OrderType.TakeProfitMarket,
                    Binance.Net.Enums.FuturesOrderType.StopMarket => OrderType.StopMarket,
                    Binance.Net.Enums.FuturesOrderType.Liquidation => OrderType.Liquidation,
                    Binance.Net.Enums.FuturesOrderType.TakeProfit => OrderType.TakeProfit,
                    Binance.Net.Enums.FuturesOrderType.Limit => OrderType.Limit,
                    Binance.Net.Enums.FuturesOrderType.Stop => OrderType.Stop,
                    Binance.Net.Enums.FuturesOrderType.TrailingStopMarket => OrderType.TrailingStopMarket,
                    _ => throw new NotSupportedException($"不支持类型{x.Type}."),
                },
                Side = x.Side switch
                {
                    Binance.Net.Enums.OrderSide.Buy => OrderSide.Buy,
                    Binance.Net.Enums.OrderSide.Sell => OrderSide.Sell,
                    _ => throw new NotSupportedException($"不支持类型{x.Side}."),
                },
                Status = x.Status switch
                {
                    Binance.Net.Enums.OrderStatus.Adl => OrderStatus.Adl,
                    Binance.Net.Enums.OrderStatus.Expired => OrderStatus.Expired,
                    Binance.Net.Enums.OrderStatus.Insurance => OrderStatus.Insurance,
                    Binance.Net.Enums.OrderStatus.Canceled => OrderStatus.Canceled,
                    Binance.Net.Enums.OrderStatus.Filled => OrderStatus.Filled,
                    Binance.Net.Enums.OrderStatus.New => OrderStatus.New,
                    Binance.Net.Enums.OrderStatus.PartiallyFilled => OrderStatus.PartiallyFilled,
                    Binance.Net.Enums.OrderStatus.PendingCancel => OrderStatus.PendingCancel,
                    Binance.Net.Enums.OrderStatus.Rejected => OrderStatus.Rejected,
                    _ => throw new NotSupportedException($"不支持类型{x.Status}."),
                },
                TimeInForce = x.TimeInForce switch
                {
                    Binance.Net.Enums.TimeInForce.GoodTillCanceled => TimeInForce.GoodTillCanceled,
                    Binance.Net.Enums.TimeInForce.GoodTillExpiredOrCanceled => TimeInForce.GoodTillExpiredOrCanceled,
                    Binance.Net.Enums.TimeInForce.ImmediateOrCancel => TimeInForce.ImmediateOrCancel,
                    Binance.Net.Enums.TimeInForce.FillOrKill => TimeInForce.FillOrKill,
                    Binance.Net.Enums.TimeInForce.GoodTillCrossing => TimeInForce.GoodTillCrossing,
                    _ => throw new NotSupportedException($"不支持类型{x.TimeInForce}."),
                },
                QuantityFilled = x.QuantityFilled,
                LastFilledQuantity = x.LastFilledQuantity,
                BaseQuantityFilled = x.BaseQuantityFilled,
                ClosePosition = x.ClosePosition,
                CreateTime = x.CreateTime,
                Pair = x.Pair,
                QuoteQuantityFilled = x.QuoteQuantityFilled,
            });
            return new CallResult<IEnumerable<FutureOrder>>()
            {
                Success = result.Success,
                Data = balances,
                ErrorCode = result.Error?.Code,
                Msg = result.Error?.Message
            };
        }

        public async Task<CallResult<IEnumerable<FutureOrder>>> GetAllOrdersAsync(string symbol)
        {
            var result = await this.Client.UsdFuturesApi.Trading.GetOrdersAsync(symbol);
            var balances = result.Data?.Select(x => new FutureOrder()
            {
                AvgPrice = x.AvgPrice,
                ClientOrderId = x.ClientOrderId,
                Id = x.Id,
                Price = x.Price,
                Quantity = x.Quantity,
                ReduceOnly = x.ReduceOnly,
                Symbol = x.Symbol,
                UpdateTime = x.UpdateTime,
                PositionSide = x.PositionSide switch
                {
                    Binance.Net.Enums.PositionSide.Short => PositionSide.Short,
                    Binance.Net.Enums.PositionSide.Both => PositionSide.Both,
                    Binance.Net.Enums.PositionSide.Long => PositionSide.Long,
                    _ => throw new NotSupportedException($"不支持类型{x.PositionSide}"),
                },
                Type = x.Type switch
                {
                    Binance.Net.Enums.FuturesOrderType.Market => OrderType.Market,
                    Binance.Net.Enums.FuturesOrderType.TakeProfitMarket => OrderType.TakeProfitMarket,
                    Binance.Net.Enums.FuturesOrderType.StopMarket => OrderType.StopMarket,
                    Binance.Net.Enums.FuturesOrderType.Liquidation => OrderType.Liquidation,
                    Binance.Net.Enums.FuturesOrderType.TakeProfit => OrderType.TakeProfit,
                    Binance.Net.Enums.FuturesOrderType.Limit => OrderType.Limit,
                    Binance.Net.Enums.FuturesOrderType.Stop => OrderType.Stop,
                    Binance.Net.Enums.FuturesOrderType.TrailingStopMarket => OrderType.TrailingStopMarket,
                    _ => throw new NotSupportedException($"不支持类型{x.Type}."),
                },
                Side = x.Side switch
                {
                    Binance.Net.Enums.OrderSide.Buy => OrderSide.Buy,
                    Binance.Net.Enums.OrderSide.Sell => OrderSide.Sell,
                    _ => throw new NotSupportedException($"不支持类型{x.Side}."),
                },
                Status = x.Status switch
                {
                    Binance.Net.Enums.OrderStatus.Adl => OrderStatus.Adl,
                    Binance.Net.Enums.OrderStatus.Expired => OrderStatus.Expired,
                    Binance.Net.Enums.OrderStatus.Insurance => OrderStatus.Insurance,
                    Binance.Net.Enums.OrderStatus.Canceled => OrderStatus.Canceled,
                    Binance.Net.Enums.OrderStatus.Filled => OrderStatus.Filled,
                    Binance.Net.Enums.OrderStatus.New => OrderStatus.New,
                    Binance.Net.Enums.OrderStatus.PartiallyFilled => OrderStatus.PartiallyFilled,
                    Binance.Net.Enums.OrderStatus.PendingCancel => OrderStatus.PendingCancel,
                    Binance.Net.Enums.OrderStatus.Rejected => OrderStatus.Rejected,
                    _ => throw new NotSupportedException($"不支持类型{x.Status}."),
                },
                TimeInForce = x.TimeInForce switch
                {
                    Binance.Net.Enums.TimeInForce.GoodTillCanceled => TimeInForce.GoodTillCanceled,
                    Binance.Net.Enums.TimeInForce.GoodTillExpiredOrCanceled => TimeInForce.GoodTillExpiredOrCanceled,
                    Binance.Net.Enums.TimeInForce.ImmediateOrCancel => TimeInForce.ImmediateOrCancel,
                    Binance.Net.Enums.TimeInForce.FillOrKill => TimeInForce.FillOrKill,
                    Binance.Net.Enums.TimeInForce.GoodTillCrossing => TimeInForce.GoodTillCrossing,
                    _ => throw new NotSupportedException($"不支持类型{x.TimeInForce}."),
                },
                QuantityFilled = x.QuantityFilled,
                LastFilledQuantity = x.LastFilledQuantity,
                BaseQuantityFilled = x.BaseQuantityFilled,
                ClosePosition = x.ClosePosition,
                CreateTime = x.CreateTime,
                Pair = x.Pair,
                QuoteQuantityFilled = x.QuoteQuantityFilled,
            });
            return new CallResult<IEnumerable<FutureOrder>>()
            {
                Success = result.Success,
                Data = balances,
                ErrorCode = result.Error?.Code,
                Msg = result.Error?.Message
            };
        }

        public async Task<CallResult<FutureOrder>> GetOrderAsync(string symbol, long? orderId = null, string? origClientOrderId = null)
        {
            var result = await this.Client.UsdFuturesApi.Trading.GetOrderAsync(symbol, orderId, origClientOrderId);
            return new CallResult<FutureOrder>()
            {
                Success = result.Success,
                Data = !result.Success ? null : new FutureOrder()
                {
                    AvgPrice = result.Data.AvgPrice,
                    ClientOrderId = result.Data.ClientOrderId,
                    Id = result.Data.Id,
                    Price = result.Data.Price,
                    Quantity = result.Data.Quantity,
                    ReduceOnly = result.Data.ReduceOnly,
                    Symbol = result.Data.Symbol,
                    UpdateTime = result.Data.UpdateTime,
                    PositionSide = result.Data.PositionSide switch
                    {
                        Binance.Net.Enums.PositionSide.Short => PositionSide.Short,
                        Binance.Net.Enums.PositionSide.Both => PositionSide.Both,
                        Binance.Net.Enums.PositionSide.Long => PositionSide.Long,
                        _ => throw new NotSupportedException($"不支持类型{result.Data.PositionSide}"),
                    },
                    Type = result.Data.Type switch
                    {
                        Binance.Net.Enums.FuturesOrderType.Market => OrderType.Market,
                        Binance.Net.Enums.FuturesOrderType.TakeProfitMarket => OrderType.TakeProfitMarket,
                        Binance.Net.Enums.FuturesOrderType.StopMarket => OrderType.StopMarket,
                        Binance.Net.Enums.FuturesOrderType.Liquidation => OrderType.Liquidation,
                        Binance.Net.Enums.FuturesOrderType.TakeProfit => OrderType.TakeProfit,
                        Binance.Net.Enums.FuturesOrderType.Limit => OrderType.Limit,
                        Binance.Net.Enums.FuturesOrderType.Stop => OrderType.Stop,
                        Binance.Net.Enums.FuturesOrderType.TrailingStopMarket => OrderType.TrailingStopMarket,
                        _ => throw new NotSupportedException($"不支持类型{result.Data.Type}."),
                    },
                    Side = result.Data.Side switch
                    {
                        Binance.Net.Enums.OrderSide.Buy => OrderSide.Buy,
                        Binance.Net.Enums.OrderSide.Sell => OrderSide.Sell,
                        _ => throw new NotSupportedException($"不支持类型{result.Data.Side}."),
                    },
                    Status = result.Data.Status switch
                    {
                        Binance.Net.Enums.OrderStatus.Adl => OrderStatus.Adl,
                        Binance.Net.Enums.OrderStatus.Expired => OrderStatus.Expired,
                        Binance.Net.Enums.OrderStatus.Insurance => OrderStatus.Insurance,
                        Binance.Net.Enums.OrderStatus.Canceled => OrderStatus.Canceled,
                        Binance.Net.Enums.OrderStatus.Filled => OrderStatus.Filled,
                        Binance.Net.Enums.OrderStatus.New => OrderStatus.New,
                        Binance.Net.Enums.OrderStatus.PartiallyFilled => OrderStatus.PartiallyFilled,
                        Binance.Net.Enums.OrderStatus.PendingCancel => OrderStatus.PendingCancel,
                        Binance.Net.Enums.OrderStatus.Rejected => OrderStatus.Rejected,
                        _ => throw new NotSupportedException($"不支持类型{result.Data.Status}."),
                    },
                    TimeInForce = result.Data.TimeInForce switch
                    {
                        Binance.Net.Enums.TimeInForce.GoodTillCanceled => TimeInForce.GoodTillCanceled,
                        Binance.Net.Enums.TimeInForce.GoodTillExpiredOrCanceled => TimeInForce.GoodTillExpiredOrCanceled,
                        Binance.Net.Enums.TimeInForce.ImmediateOrCancel => TimeInForce.ImmediateOrCancel,
                        Binance.Net.Enums.TimeInForce.FillOrKill => TimeInForce.FillOrKill,
                        Binance.Net.Enums.TimeInForce.GoodTillCrossing => TimeInForce.GoodTillCrossing,
                        _ => throw new NotSupportedException($"不支持类型{result.Data.TimeInForce}."),
                    },
                    QuantityFilled = result.Data.QuantityFilled,
                    LastFilledQuantity = result.Data.LastFilledQuantity,
                    BaseQuantityFilled = result.Data.BaseQuantityFilled,
                    ClosePosition = result.Data.ClosePosition,
                    CreateTime = result.Data.CreateTime,
                    Pair = result.Data.Pair,
                    QuoteQuantityFilled = result.Data.QuoteQuantityFilled,
                },
                ErrorCode = result.Error?.Code,
                Msg = result.Error?.Message
            };
        }

        public async Task<CallResult<IEnumerable<PositionDetail>>> GetPositionInformationAsync()
        {
            var result = await this.Client.UsdFuturesApi.Account.GetPositionInformationAsync();
            var balances = result.Data?.Select(x => new PositionDetail()
            {
                Quantity = x.Quantity,
                Symbol = x.Symbol,
                UpdateTime = x.UpdateTime,
                PositionSide = x.PositionSide switch
                {
                    Binance.Net.Enums.PositionSide.Short => PositionSide.Short,
                    Binance.Net.Enums.PositionSide.Both => PositionSide.Both,
                    Binance.Net.Enums.PositionSide.Long => PositionSide.Long,
                    _ => throw new NotSupportedException($"不支持类型{x.PositionSide}"),
                },
                EntryPrice = x.EntryPrice,
                IsolatedMargin = x.IsolatedMargin,
                Leverage = x.Leverage,
                LiquidationPrice = x.LiquidationPrice,
                MarkPrice = x.MarkPrice,
                UnrealizedPnl = x.UnrealizedPnl,
                MarginType = x.MarginType switch
                {
                    Binance.Net.Enums.FuturesMarginType.Isolated => MarginType.Isolated,
                    Binance.Net.Enums.FuturesMarginType.Cross => MarginType.Cross,
                    _ => throw new NotSupportedException($"不支持类型{x.MarginType}."),
                }
            });
            return new CallResult<IEnumerable<PositionDetail>>()
            {
                Success = result.Success,
                Data = balances,
                ErrorCode = result.Error?.Code,
                Msg = result.Error?.Message
            };
        }

        public async Task<CallResult<FutureOrder>> OpenPositionAsync(string symbol, OrderType orderType, PositionSide side, decimal? quantity, decimal? price = null, string? newClientOrderId = null)
        {
            var positionModeResult = await this.Client.UsdFuturesApi.Account.GetPositionModeAsync();
            if (!positionModeResult.Success)
               return new CallResult<FutureOrder>()
                {
                    Success = positionModeResult.Success,
                    ErrorCode = positionModeResult.Error?.Code,
                    Msg = positionModeResult.Error?.Message
                };
            var dualSidePosition = positionModeResult.Data.PositionMode == PositionMode.Hedge;
            var result = await this.Client.UsdFuturesApi.Trading.PlaceOrderAsync(
                symbol,
                side switch
                {
                    PositionSide.Short => Binance.Net.Enums.OrderSide.Sell,
                    PositionSide.Long => Binance.Net.Enums.OrderSide.Buy,
                    _ => throw new NotSupportedException($"不支持类型{side},且仅可选择 LONG 或 SHORT"),
                },               
                orderType switch
                {
                    OrderType.Market => Binance.Net.Enums.FuturesOrderType.Market,
                    OrderType.TakeProfitMarket => Binance.Net.Enums.FuturesOrderType.TakeProfitMarket,
                    OrderType.StopMarket => Binance.Net.Enums.FuturesOrderType.StopMarket,
                    OrderType.Liquidation => Binance.Net.Enums.FuturesOrderType.Liquidation,
                    OrderType.TakeProfit => Binance.Net.Enums.FuturesOrderType.TakeProfit,
                    OrderType.Limit => Binance.Net.Enums.FuturesOrderType.Limit,
                    OrderType.Stop => Binance.Net.Enums.FuturesOrderType.Stop,
                    OrderType.TrailingStopMarket => Binance.Net.Enums.FuturesOrderType.TrailingStopMarket,
                    _ => throw new NotSupportedException($"不支持类型{orderType}."),
                },
                quantity,
                price,
                dualSidePosition ? side switch
                {
                    PositionSide.Short => Binance.Net.Enums.PositionSide.Short,
                    PositionSide.Long => Binance.Net.Enums.PositionSide.Long,
                    _ => throw new NotSupportedException($"不支持类型{side},且仅可选择 LONG 或 SHORT"),
                } : null,
                orderType == OrderType.Market ? null : Binance.Net.Enums.TimeInForce.GoodTillCanceled,
                dualSidePosition ? null : false,
                newClientOrderId);

            return new CallResult<FutureOrder>()
            {
                Success = result.Success,
                Data = !result.Success ? null : new FutureOrder()
                {
                    AvgPrice = result.Data.AveragePrice,
                    ClientOrderId = result.Data.ClientOrderId,
                    Id = result.Data.Id,
                    Price = result.Data.Price,
                    Quantity = result.Data.Quantity,
                    ReduceOnly = result.Data.ReduceOnly,
                    Symbol = result.Data.Symbol,
                    UpdateTime = result.Data.UpdateTime,
                    PositionSide = result.Data.PositionSide switch
                    {
                        Binance.Net.Enums.PositionSide.Short => PositionSide.Short,
                        Binance.Net.Enums.PositionSide.Both => PositionSide.Both,
                        Binance.Net.Enums.PositionSide.Long => PositionSide.Long,
                        _ => throw new NotSupportedException($"不支持类型{result.Data.PositionSide}"),
                    },
                    Type = result.Data.Type switch
                    {
                        Binance.Net.Enums.FuturesOrderType.Market => OrderType.Market,
                        Binance.Net.Enums.FuturesOrderType.TakeProfitMarket => OrderType.TakeProfitMarket,
                        Binance.Net.Enums.FuturesOrderType.StopMarket => OrderType.StopMarket,
                        Binance.Net.Enums.FuturesOrderType.Liquidation => OrderType.Liquidation,
                        Binance.Net.Enums.FuturesOrderType.TakeProfit => OrderType.TakeProfit,
                        Binance.Net.Enums.FuturesOrderType.Limit => OrderType.Limit,
                        Binance.Net.Enums.FuturesOrderType.Stop => OrderType.Stop,
                        Binance.Net.Enums.FuturesOrderType.TrailingStopMarket => OrderType.TrailingStopMarket,
                        _ => throw new NotSupportedException($"不支持类型{result.Data.Type}."),
                    },
                    Side = result.Data.Side switch
                    {
                        Binance.Net.Enums.OrderSide.Buy => OrderSide.Buy,
                        Binance.Net.Enums.OrderSide.Sell => OrderSide.Sell,
                        _ => throw new NotSupportedException($"不支持类型{result.Data.Side}."),
                    },
                    Status = result.Data.Status switch
                    {
                        Binance.Net.Enums.OrderStatus.Adl => OrderStatus.Adl,
                        Binance.Net.Enums.OrderStatus.Expired => OrderStatus.Expired,
                        Binance.Net.Enums.OrderStatus.Insurance => OrderStatus.Insurance,
                        Binance.Net.Enums.OrderStatus.Canceled => OrderStatus.Canceled,
                        Binance.Net.Enums.OrderStatus.Filled => OrderStatus.Filled,
                        Binance.Net.Enums.OrderStatus.New => OrderStatus.New,
                        Binance.Net.Enums.OrderStatus.PartiallyFilled => OrderStatus.PartiallyFilled,
                        Binance.Net.Enums.OrderStatus.PendingCancel => OrderStatus.PendingCancel,
                        Binance.Net.Enums.OrderStatus.Rejected => OrderStatus.Rejected,
                        _ => throw new NotSupportedException($"不支持类型{result.Data.Status}."),
                    },
                    TimeInForce = result.Data.TimeInForce switch
                    {
                        Binance.Net.Enums.TimeInForce.GoodTillCanceled => TimeInForce.GoodTillCanceled,
                        Binance.Net.Enums.TimeInForce.GoodTillExpiredOrCanceled => TimeInForce.GoodTillExpiredOrCanceled,
                        Binance.Net.Enums.TimeInForce.ImmediateOrCancel => TimeInForce.ImmediateOrCancel,
                        Binance.Net.Enums.TimeInForce.FillOrKill => TimeInForce.FillOrKill,
                        Binance.Net.Enums.TimeInForce.GoodTillCrossing => TimeInForce.GoodTillCrossing,
                        _ => throw new NotSupportedException($"不支持类型{result.Data.TimeInForce}."),
                    },
                    QuantityFilled = result.Data.QuantityFilled,
                    LastFilledQuantity = result.Data.LastFilledQuantity,
                    BaseQuantityFilled = result.Data.BaseQuantityFilled,
                    ClosePosition = result.Data.ClosePosition,
                    CreateTime = DateTime.Now,
                    Pair = result.Data.Pair,
                    QuoteQuantityFilled = result.Data.QuoteQuantityFilled,
                },
                ErrorCode = result.Error?.Code,
                Msg = result.Error?.Message
            };
        }
       
        public async Task<CallResult<FutureOrder>> ClosePositionAsync(string symbol, OrderType orderType, PositionSide side, decimal? quantity, decimal? price = null, string? newClientOrderId = null)
        {
            var positionModeResult = await this.Client.UsdFuturesApi.Account.GetPositionModeAsync();
            if (!positionModeResult.Success)
               return new CallResult<FutureOrder>()
               {
                   Success = positionModeResult.Success,
                   ErrorCode = positionModeResult.Error?.Code,
                   Msg = positionModeResult.Error?.Message
               };
            var dualSidePosition = positionModeResult.Data.PositionMode == PositionMode.Hedge;
            var result = await this.Client.UsdFuturesApi.Trading.PlaceOrderAsync(
                symbol,
                side switch
                {
                    PositionSide.Short => Binance.Net.Enums.OrderSide.Buy,
                    PositionSide.Long => Binance.Net.Enums.OrderSide.Sell,
                    _ => throw new NotSupportedException($"不支持类型{side},且仅可选择 LONG 或 SHORT"),
                },
                orderType switch
                {
                    OrderType.Market => Binance.Net.Enums.FuturesOrderType.Market,
                    OrderType.TakeProfitMarket => Binance.Net.Enums.FuturesOrderType.TakeProfitMarket,
                    OrderType.StopMarket => Binance.Net.Enums.FuturesOrderType.StopMarket,
                    OrderType.Liquidation => Binance.Net.Enums.FuturesOrderType.Liquidation,
                    OrderType.TakeProfit => Binance.Net.Enums.FuturesOrderType.TakeProfit,
                    OrderType.Limit => Binance.Net.Enums.FuturesOrderType.Limit,
                    OrderType.Stop => Binance.Net.Enums.FuturesOrderType.Stop,
                    OrderType.TrailingStopMarket => Binance.Net.Enums.FuturesOrderType.TrailingStopMarket,
                    _ => throw new NotSupportedException($"不支持类型{orderType}."),
                },
                quantity,
                price,
                dualSidePosition ? side switch
                {
                    PositionSide.Short => Binance.Net.Enums.PositionSide.Short,
                    PositionSide.Long => Binance.Net.Enums.PositionSide.Long,
                    _ => throw new NotSupportedException($"不支持类型{side},且仅可选择 LONG 或 SHORT"),
                } : null,
                orderType == OrderType.Market ? null : Binance.Net.Enums.TimeInForce.GoodTillCanceled,
                dualSidePosition ? null : true,
                newClientOrderId);

            return new CallResult<FutureOrder>()
            {
                Success = result.Success,
                Data = !result.Success ? null : new FutureOrder()
                {
                    AvgPrice = result.Data.AveragePrice,
                    ClientOrderId = result.Data.ClientOrderId,
                    Id = result.Data.Id,
                    Price = result.Data.Price,
                    Quantity = result.Data.Quantity,
                    ReduceOnly = result.Data.ReduceOnly,
                    Symbol = result.Data.Symbol,
                    UpdateTime = result.Data.UpdateTime,
                    PositionSide = result.Data.PositionSide switch
                    {
                        Binance.Net.Enums.PositionSide.Short => PositionSide.Short,
                        Binance.Net.Enums.PositionSide.Both => PositionSide.Both,
                        Binance.Net.Enums.PositionSide.Long => PositionSide.Long,
                        _ => throw new NotSupportedException($"不支持类型{result.Data.PositionSide}"),
                    },
                    Type = result.Data.Type switch
                    {
                        Binance.Net.Enums.FuturesOrderType.Market => OrderType.Market,
                        Binance.Net.Enums.FuturesOrderType.TakeProfitMarket => OrderType.TakeProfitMarket,
                        Binance.Net.Enums.FuturesOrderType.StopMarket => OrderType.StopMarket,
                        Binance.Net.Enums.FuturesOrderType.Liquidation => OrderType.Liquidation,
                        Binance.Net.Enums.FuturesOrderType.TakeProfit => OrderType.TakeProfit,
                        Binance.Net.Enums.FuturesOrderType.Limit => OrderType.Limit,
                        Binance.Net.Enums.FuturesOrderType.Stop => OrderType.Stop,
                        Binance.Net.Enums.FuturesOrderType.TrailingStopMarket => OrderType.TrailingStopMarket,
                        _ => throw new NotSupportedException($"不支持类型{result.Data.Type}."),
                    },
                    Side = result.Data.Side switch
                    {
                        Binance.Net.Enums.OrderSide.Buy => OrderSide.Buy,
                        Binance.Net.Enums.OrderSide.Sell => OrderSide.Sell,
                        _ => throw new NotSupportedException($"不支持类型{result.Data.Side}."),
                    },
                    Status = result.Data.Status switch
                    {
                        Binance.Net.Enums.OrderStatus.Adl => OrderStatus.Adl,
                        Binance.Net.Enums.OrderStatus.Expired => OrderStatus.Expired,
                        Binance.Net.Enums.OrderStatus.Insurance => OrderStatus.Insurance,
                        Binance.Net.Enums.OrderStatus.Canceled => OrderStatus.Canceled,
                        Binance.Net.Enums.OrderStatus.Filled => OrderStatus.Filled,
                        Binance.Net.Enums.OrderStatus.New => OrderStatus.New,
                        Binance.Net.Enums.OrderStatus.PartiallyFilled => OrderStatus.PartiallyFilled,
                        Binance.Net.Enums.OrderStatus.PendingCancel => OrderStatus.PendingCancel,
                        Binance.Net.Enums.OrderStatus.Rejected => OrderStatus.Rejected,
                        _ => throw new NotSupportedException($"不支持类型{result.Data.Status}."),
                    },
                    TimeInForce = result.Data.TimeInForce switch
                    {
                        Binance.Net.Enums.TimeInForce.GoodTillCanceled => TimeInForce.GoodTillCanceled,
                        Binance.Net.Enums.TimeInForce.GoodTillExpiredOrCanceled => TimeInForce.GoodTillExpiredOrCanceled,
                        Binance.Net.Enums.TimeInForce.ImmediateOrCancel => TimeInForce.ImmediateOrCancel,
                        Binance.Net.Enums.TimeInForce.FillOrKill => TimeInForce.FillOrKill,
                        Binance.Net.Enums.TimeInForce.GoodTillCrossing => TimeInForce.GoodTillCrossing,
                        _ => throw new NotSupportedException($"不支持类型{result.Data.TimeInForce}."),
                    },
                    QuantityFilled = result.Data.QuantityFilled,
                    LastFilledQuantity = result.Data.LastFilledQuantity,
                    BaseQuantityFilled = result.Data.BaseQuantityFilled,
                    ClosePosition = result.Data.ClosePosition,
                    CreateTime = DateTime.Now,
                    Pair = result.Data.Pair,
                    QuoteQuantityFilled = result.Data.QuoteQuantityFilled,
                },
                ErrorCode = result.Error?.Code,
                Msg = result.Error?.Message
            };
        }
         
        string? _listenKey;
        async Task KeepAliveUserStreamAsync()
        {
            if (!string.IsNullOrEmpty(_listenKey))
            {
                var result = await this.Client.UsdFuturesApi.Account.KeepAliveUserStreamAsync(_listenKey);
                if(!result.Success)
                    this._logger.LogError($"Binance KeepAliveUserStreamAsync error. {result.Error?.Code} {result.Error?.Message}!");
            }
        }

        async Task<string?> GetListenKeyAsync()
        {
            if (!string.IsNullOrEmpty(_listenKey)) return _listenKey;
            var startUserStreamResult = await this.Client.UsdFuturesApi.Account.StartUserStreamAsync();
            if (startUserStreamResult.Success)
            {
                _listenKey = startUserStreamResult.Data;
                JobHelper.SetIntervalJob(async () => await KeepAliveUserStreamAsync(), TimeSpan.FromMinutes(15)); ;
            }
            else
                this._logger.LogError($"Binance StartUserStreamAsync error, {startUserStreamResult.Error?.Code} {startUserStreamResult.Error?.Message}!");
            return _listenKey;
        }

        public async Task<CallResult> SubscribeToKlineUpdatesAsync(IEnumerable<string> symbols, IEnumerable<PeriodOption> periods, Action<KlineData> onMessage, CancellationToken ct = default)
        {
            var intervals = new List<Binance.Net.Enums.KlineInterval>();
            foreach (var period in periods)
                intervals.Add(
                    period switch
                    {
                        PeriodOption.Per5Minute => Binance.Net.Enums.KlineInterval.FiveMinutes,
                        PeriodOption.PerMinute => Binance.Net.Enums.KlineInterval.OneMinute,
                        PeriodOption.Per15Minute => Binance.Net.Enums.KlineInterval.FifteenMinutes,
                        PeriodOption.Per30Minute => Binance.Net.Enums.KlineInterval.ThirtyMinutes,
                        PeriodOption.Hourly => Binance.Net.Enums.KlineInterval.OneHour,
                        PeriodOption.TwoHourly => Binance.Net.Enums.KlineInterval.TwoHour,
                        PeriodOption.FourHourly => Binance.Net.Enums.KlineInterval.FourHour,
                        PeriodOption.Daily => Binance.Net.Enums.KlineInterval.OneDay,
                        _ => throw new NotSupportedException($"不支持类型{period}.")
                    });
            var result = await this.Ws.UsdFuturesStreams.SubscribeToKlineUpdatesAsync(symbols, intervals, message =>
            {
                var klineData = new KlineData()
                {
                    Interval = message.Data.Data.Interval switch
                    {
                        Binance.Net.Enums.KlineInterval.FiveMinutes => PeriodOption.Per5Minute,
                        Binance.Net.Enums.KlineInterval.OneMinute => PeriodOption.PerMinute,
                        Binance.Net.Enums.KlineInterval.FifteenMinutes => PeriodOption.Per15Minute,
                        Binance.Net.Enums.KlineInterval.ThirtyMinutes => PeriodOption.Per30Minute,
                        Binance.Net.Enums.KlineInterval.OneHour => PeriodOption.Hourly,
                        Binance.Net.Enums.KlineInterval.TwoHour => PeriodOption.TwoHourly,
                        Binance.Net.Enums.KlineInterval.FourHour => PeriodOption.FourHourly,
                        Binance.Net.Enums.KlineInterval.OneDay => PeriodOption.Daily,
                        _ => throw new NotSupportedException($"不支持类型{message.Data.Data.Interval}.")
                    },
                    ClosePrice = message.Data.Data.ClosePrice,
                    CloseTime = message.Data.Data.CloseTime,
                    HighPrice = message.Data.Data.HighPrice,
                    LowPrice = message.Data.Data.LowPrice,
                    OpenPrice = message.Data.Data.OpenPrice,
                    OpenTime = message.Data.Data.OpenTime,
                    QuoteVolume = message.Data.Data.QuoteVolume,
                    Symbol = message.Data.Symbol,
                    Volume = message.Data.Data.Volume
                };
                onMessage?.Invoke(klineData);
            });
            if (!result.Success)
                this._logger.LogError($"Binance Subscribe Kline, {result.Error?.Message}!");
            else
            {
                result.Data.ConnectionClosed += () => this._logger.LogInformation($"Binance Subscribe Kline Connection Closed!");
                result.Data.ConnectionRestored += (e) => this._logger.LogInformation($"Binance Subscribe Kline Connection Restored, {e.TotalSeconds}s!");
                result.Data.Exception += (e) => this._logger.LogError(e, $"Binance Subscribe Kline Error!");
            }
            return new CallResult() { Success = result.Success, ErrorCode = result.Error?.Code, Msg = result.Error?.Message };
        }

        public async Task<CallResult> SubscribeToUserDataUpdatesAsync(Action<MarginUpdate>? onMarginUpdate, Action<AccountUpdateData>? onAccountUpdate, Action<FutureOrder>? onOrderUpdate, CancellationToken ct = default)
        {
            var listenKey = await this.GetListenKeyAsync();
            if (string.IsNullOrEmpty(listenKey)) throw new Exception("未能获取listenKey.");
            var result = await this.Ws.UsdFuturesStreams.SubscribeToUserDataUpdatesAsync(
                listenKey,
                null,
                margin =>
                {
                    onMarginUpdate?.Invoke(new MarginUpdate()
                    {
                        CrossWalletBalance = margin.Data.CrossWalletBalance,
                        ListenKey = margin.Data.ListenKey,
                        Positions = margin.Data.Positions.Select(x => new MarginPosition()
                        {
                            IsolatedWallet = x.IsolatedWallet,
                            MaintMargin = x.MaintMargin,
                            Symbol = x.Symbol,
                            MarkPrice = x.MarkPrice,
                            PositionQuantity = x.PositionQuantity,
                            UnrealizedPnl = x.UnrealizedPnl,
                            PositionSide = x.PositionSide switch
                            {
                                Binance.Net.Enums.PositionSide.Short => PositionSide.Short,
                                Binance.Net.Enums.PositionSide.Long => PositionSide.Long,
                                Binance.Net.Enums.PositionSide.Both => PositionSide.Both,
                                _ => throw new NotSupportedException($"不支持类型{x.PositionSide}."),
                            },
                            MarginType = x.MarginType switch
                            {
                                Binance.Net.Enums.FuturesMarginType.Isolated => MarginType.Isolated,
                                Binance.Net.Enums.FuturesMarginType.Cross => MarginType.Cross,
                                _ => throw new NotSupportedException($"不支持类型{x.MarginType}."),
                            },
                        })
                    });
                },
                account =>
                {
                    onAccountUpdate?.Invoke(new AccountUpdateData()
                    {
                        Balances = account.Data.UpdateData.Balances.Select(x => new FutureBalance()
                        {
                            Asset = x.Asset,
                            WalletBalance = x.WalletBalance,
                            CrossWalletBalance = x.CrossWalletBalance
                        }),
                        Positions = account.Data.UpdateData.Positions.Select(x => new PositionDetail()
                        {
                            EntryPrice = x.EntryPrice,
                            IsolatedMargin = x.IsolatedMargin,
                            Quantity = x.Quantity,
                            Symbol = x.Symbol,
                            UnrealizedPnl = x.UnrealizedPnl,
                            UpdateTime = DateTime.Now,
                            MarginType = x.MarginType switch
                            {
                                Binance.Net.Enums.FuturesMarginType.Isolated => MarginType.Isolated,
                                Binance.Net.Enums.FuturesMarginType.Cross => MarginType.Cross,
                                _ => throw new NotSupportedException($"不支持类型{x.MarginType}."),
                            },
                            PositionSide = x.PositionSide switch
                            {
                                Binance.Net.Enums.PositionSide.Both => PositionSide.Both,
                                Binance.Net.Enums.PositionSide.Short => PositionSide.Short,
                                Binance.Net.Enums.PositionSide.Long => PositionSide.Long,
                                _ => throw new NotSupportedException($"不支持类型{x.PositionSide}."),
                            }
                        })
                    });
                },
                order =>
                {
                    onOrderUpdate?.Invoke(new FutureOrder()
                    {
                        Id = order.Data.UpdateData.OrderId,
                        AvgPrice = order.Data.UpdateData.AveragePrice,
                        ClientOrderId = order.Data.UpdateData.ClientOrderId,
                        ReduceOnly = order.Data.UpdateData.IsReduce,
                        Price = order.Data.UpdateData.Price,
                        Quantity = order.Data.UpdateData.Quantity,
                        Symbol = order.Data.UpdateData.Symbol,
                        UpdateTime = order.Data.UpdateData.UpdateTime,
                        Type = order.Data.UpdateData.Type switch
                        {
                            Binance.Net.Enums.FuturesOrderType.Market => OrderType.Market,
                            Binance.Net.Enums.FuturesOrderType.TakeProfitMarket => OrderType.TakeProfitMarket,
                            Binance.Net.Enums.FuturesOrderType.StopMarket => OrderType.StopMarket,
                            Binance.Net.Enums.FuturesOrderType.Liquidation => OrderType.Liquidation,
                            Binance.Net.Enums.FuturesOrderType.TakeProfit => OrderType.TakeProfit,
                            Binance.Net.Enums.FuturesOrderType.Limit => OrderType.Limit,
                            Binance.Net.Enums.FuturesOrderType.Stop => OrderType.Stop,
                            Binance.Net.Enums.FuturesOrderType.TrailingStopMarket => OrderType.TrailingStopMarket,
                            _ => throw new NotSupportedException($"不支持类型{order.Data.UpdateData.Type}."),
                        },
                        PositionSide = order.Data.UpdateData.PositionSide switch
                        {
                            Binance.Net.Enums.PositionSide.Both => PositionSide.Both,
                            Binance.Net.Enums.PositionSide.Short => PositionSide.Short,
                            Binance.Net.Enums.PositionSide.Long => PositionSide.Long,
                            _ => throw new NotSupportedException($"不支持类型{order.Data.UpdateData.PositionSide}."),
                        },
                        Side = order.Data.UpdateData.Side switch
                        {
                            Binance.Net.Enums.OrderSide.Buy => OrderSide.Buy,
                            Binance.Net.Enums.OrderSide.Sell => OrderSide.Sell,
                            _ => throw new NotSupportedException($"不支持类型{order.Data.UpdateData.Side}."),
                        },
                        Status = order.Data.UpdateData.Status switch
                        {
                            Binance.Net.Enums.OrderStatus.Adl => OrderStatus.Adl,
                            Binance.Net.Enums.OrderStatus.Expired => OrderStatus.Expired,
                            Binance.Net.Enums.OrderStatus.Insurance => OrderStatus.Insurance,
                            Binance.Net.Enums.OrderStatus.Canceled => OrderStatus.Canceled,
                            Binance.Net.Enums.OrderStatus.Filled => OrderStatus.Filled,
                            Binance.Net.Enums.OrderStatus.New => OrderStatus.New,
                            Binance.Net.Enums.OrderStatus.PartiallyFilled => OrderStatus.PartiallyFilled,
                            Binance.Net.Enums.OrderStatus.PendingCancel => OrderStatus.PendingCancel,
                            Binance.Net.Enums.OrderStatus.Rejected => OrderStatus.Rejected,
                            _ => throw new NotSupportedException($"不支持类型{order.Data.UpdateData.Status}."),
                        },
                        TimeInForce = order.Data.UpdateData.TimeInForce switch
                        {
                            Binance.Net.Enums.TimeInForce.GoodTillCanceled => TimeInForce.GoodTillCanceled,
                            Binance.Net.Enums.TimeInForce.GoodTillExpiredOrCanceled => TimeInForce.GoodTillExpiredOrCanceled,
                            Binance.Net.Enums.TimeInForce.ImmediateOrCancel => TimeInForce.ImmediateOrCancel,
                            Binance.Net.Enums.TimeInForce.FillOrKill => TimeInForce.FillOrKill,
                            Binance.Net.Enums.TimeInForce.GoodTillCrossing => TimeInForce.GoodTillCrossing,
                            _ => throw new NotSupportedException($"不支持类型{order.Data.UpdateData.TimeInForce}."),
                        },
                        QuantityFilled = order.Data.UpdateData.AccumulatedQuantityOfFilledTrades,
                        LastFilledQuantity = order.Data.UpdateData.QuantityOfLastFilledTrade
                    });
                },
                listenKeyExpired =>
                {
                });
            if (!result.Success)
                this._logger.LogError($"Binance Subscribe UserData, {result.Error?.Message}!");
            else
            {
                result.Data.ConnectionClosed += () => this._logger.LogInformation($"Binance Subscribe UserData Connection Closed!");
                result.Data.ConnectionRestored += (e) => this._logger.LogInformation($"Binance Subscribe UserData Connection Restored, {e.TotalSeconds}s!");
                result.Data.Exception += (e) => this._logger.LogError(e, $"Binance Subscribe UserData Error!");
            }
            return new CallResult() { Success = result.Success, ErrorCode = result.Error?.Code, Msg = result.Error?.Message };
        }
    }
}