using CEF.Common.Primitives;
using CryptoExchange.Net.CommonObjects;
using Newtonsoft.Json;
using System.ComponentModel;
using Trady.Core.Infrastructure;

namespace CEF.Common.Exchange
{
    public interface IExchange
    { 
        SpotExchange Exchange { get; } 
        Task<CallResult> SubscribeToKlineUpdatesAsync(
            IEnumerable<string> symbols, 
            IEnumerable<PeriodOption> periods, 
            Action<KlineData> onMessage, 
            CancellationToken ct = default);

        Task<CallResult> SubscribeToUserDataUpdatesAsync(
            Action<MarginUpdate>? onMarginUpdate,
            Action<AccountUpdateData>? onAccountUpdate,
            Action<FutureOrder>? onOrderUpdate,
            CancellationToken ct = default);

        Task<CallResult<IEnumerable<FutureInfo>>> GetSymbolsAsync();

        Task<CallResult<IEnumerable<PositionDetail>>> GetPositionInformationAsync();

        Task<CallResult<IEnumerable<FutureBalance>>> GetBalanceAsync();

        Task<CallResult<IEnumerable<IOhlcv>>> GetKlineDataAsync(
            string symbol, 
            PeriodOption period,
            DateTime? startTime = null, DateTime? endTime = null, int? limit = null);

        Task<CallResult> CancelAllOrdersAsync(string symbol);

        Task<CallResult> CancelOrderAsync(
            string symbol, 
            long? orderId = null, 
            string? origClientOrderId = null);

        Task<CallResult<IEnumerable<FutureOrder>>> GetOpenOrdersAsync(string? symbol = null);

        Task<CallResult<FutureOrder>> GetOrderAsync(
            string symbol, 
            long? orderId = null, 
            string? origClientOrderId = null);

        Task<CallResult<IEnumerable<FutureOrder>>> GetAllOrdersAsync(string symbol);

        Task<CallResult<FutureOrder>> OpenPositionAsync(string symbol, OrderType orderType, PositionSide side, decimal? quantity, decimal? price = null, string? newClientOrderId = null);

        Task<CallResult<FutureOrder>> ClosePositionAsync(string symbol, OrderType orderType, PositionSide side, decimal? quantity, decimal? price = null, string? newClientOrderId = null);
        Task UnsubscribeAllAsync();
    }

    #region enum
    public enum PeriodOption
    {
        PerMinute,
        Per5Minute,
        Per15Minute,
        Per30Minute,
        Hourly,
        TwoHourly,
        FourHourly,
        Daily
    }
    public enum OrderType
    {
        /// <summary>
        /// Limit orders will be placed at a specific price. If the price isn't available in the order book for that asset the order will be added in the order book for someone to fill.
        /// </summary>
        [Description("Limit")]
        Limit,
        /// <summary>
        /// Market order will be placed without a price. The order will be executed at the best price available at that time in the order book.
        /// </summary>
        [Description("Market")]
        Market,
        /// <summary>
        /// Stop order. Execute a limit order when price reaches a specific Stop price
        /// </summary>
        [Description("Stop")]
        Stop,
        /// <summary>
        /// Stop market order. Execute a market order when price reaches a specific Stop price
        /// </summary>
        [Description("StopMarket")]
        StopMarket,
        /// <summary>
        /// Take profit order. Will execute a limit order when the price rises above a price to sell and therefor take a profit
        /// </summary>
        [Description("TakeProfit")]
        TakeProfit,
        /// <summary>
        /// Take profit market order. Will execute a market order when the price rises above a price to sell and therefor take a profit
        /// </summary>
        [Description("TakeProfitMarket")]
        TakeProfitMarket,
        /// <summary>
        /// A trailing stop order will execute an order when the price drops below a certain percentage from its all time high since the order was activated
        /// </summary>
        [Description("TrailingStopMarket")]
        TrailingStopMarket,
        /// <summary>
        /// A liquidation order
        /// </summary>
        [Description("Liquidation")]
        Liquidation
    }
    public enum OrderSide
    {
        /// <summary>
        /// Buy
        /// </summary>
        [Description("Buy")]
        Buy,
        /// <summary>
        /// Sell
        /// </summary>
        [Description("Sell")]
        Sell
    }
    public enum SpotExchange
    { 
        /// <summary>
        /// 币安USDT永续合约
        /// </summary>
        [Description("Binance Futures USDT-M")]
        BinanceFuturesUSDT_M
    }
    public enum PositionSide
    {
        /// <summary>
        /// Short
        /// </summary>
        [Description("Short")]
        Short,
        /// <summary>
        /// Long
        /// </summary>
        [Description("Long")]
        Long,
        [Description("Both")]
        Both
    }
    public enum MarginType
    {
        /// <summary>
        /// Isolated margin
        /// </summary>
        Isolated,

        /// <summary>
        /// Crossed margin
        /// </summary>
        Cross
    }
    public enum TimeInForce
    {
        /// <summary>
        /// GoodTillCanceled orders will stay active until they are filled or canceled
        /// </summary>
        GoodTillCanceled,
        /// <summary>
        /// ImmediateOrCancel orders have to be at least partially filled upon placing or will be automatically canceled
        /// </summary>
        ImmediateOrCancel,
        /// <summary>
        /// FillOrKill orders have to be entirely filled upon placing or will be automatically canceled
        /// </summary>
        FillOrKill,
        /// <summary>
        /// GoodTillCrossing orders will post only
        /// </summary>
        GoodTillCrossing,
        /// <summary>
        /// Good til the order expires or is canceled
        /// </summary>
        GoodTillExpiredOrCanceled
    }
    public enum ExecutionType
    {
        /// <summary>
        /// New
        /// </summary>
        New,
        /// <summary>
        /// Canceled
        /// </summary>
        Canceled,
        /// <summary>
        /// Replaced
        /// </summary>
        Replaced,
        /// <summary>
        /// Rejected
        /// </summary>
        Rejected,
        /// <summary>
        /// Trade
        /// </summary>
        Trade,
        /// <summary>
        /// Expired
        /// </summary>
        Expired,
        /// <summary>
        /// Amendment
        /// </summary>
        Amendment
    }
    public enum OrderStatus
    {
        /// <summary>
        /// Order is new
        /// </summary>
        [Description("New")]
        New,
        /// <summary>
        /// Order is partly filled, still has quantity left to fill
        /// </summary>
        [Description("PartiallyFilled")]
        PartiallyFilled,
        /// <summary>
        /// The order has been filled and completed
        /// </summary>
        [Description("Filled")]
        Filled,
        /// <summary>
        /// The order has been canceled
        /// </summary>
        [Description("Canceled")]
        Canceled,
        /// <summary>
        /// The order is in the process of being canceled  (currently unused)
        /// </summary>
        [Description("PendingCancel")]
        PendingCancel,
        /// <summary>
        /// The order has been rejected
        /// </summary>
        [Description("Rejected")]
        Rejected,
        /// <summary>
        /// The order has expired
        /// </summary>
        [Description("Expired")]
        Expired,
        /// <summary>
        /// Liquidation with Insurance Fund
        /// </summary>
        [Description("Insurance")]
        Insurance,
        /// <summary>
        /// Counterparty Liquidation
        /// </summary>
        [Description("Adl")]
        Adl,
        /// <summary>
        /// 无效
        /// </summary>
        [Description("Invalid")]
        Invalid
    }
    #endregion

    public class KlineData
    {
        /// <summary>
        /// The symbol the data is for
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        /// Interval
        /// </summary>
        public PeriodOption Interval { get; set; }

        /// <summary>
        /// The time this candlestick opened
        /// </summary>
        public DateTime OpenTime { get; set; }

        /// <summary>
        /// The price at which this candlestick opened
        /// </summary>
        public decimal OpenPrice { get; set; }

        /// <summary>
        /// The highest price in this candlestick
        /// </summary>
        public decimal HighPrice { get; set; }

        /// <summary>
        /// The lowest price in this candlestick
        /// </summary>
        public decimal LowPrice { get; set; }

        /// <summary>
        /// The price at which this candlestick closed
        /// </summary>
        public decimal ClosePrice { get; set; }

        /// <summary>
        /// The volume traded during this candlestick
        /// </summary>
        public decimal Volume { get; set; }

        /// <summary>
        /// The close time of this candlestick
        /// </summary>
        public DateTime CloseTime { get; set; }

        /// <summary>
        /// The volume traded during this candlestick in the asset form
        /// </summary>
        public decimal QuoteVolume { get; set; }
    }

    public class MarginUpdate
    {
        /// <summary>
        /// Cross Wallet Balance. Only pushed with crossed position margin call
        /// </summary>
        public decimal? CrossWalletBalance { get; set; }
        /// <summary>
        /// Positions
        /// </summary>
        public IEnumerable<MarginPosition> Positions { get; set; } = Array.Empty<MarginPosition>();

        /// <summary>
        /// The listen key the update was for
        /// </summary>
        public string ListenKey { get; set; } = string.Empty;
    }
    
    public class MarginPosition
    {
        /// <summary>
        /// Symbol
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Position Side
        /// </summary>
        public PositionSide PositionSide { get; set; }

        /// <summary>
        /// Position quantity
        /// </summary>
        public decimal PositionQuantity { get; set; }

        /// <summary>
        /// Margin type
        /// </summary>
        public MarginType MarginType { get; set; }

        /// <summary>
        /// Isolated Wallet (if isolated position)
        /// </summary>
        public decimal IsolatedWallet { get; set; }

        /// <summary>
        /// Mark Price
        /// </summary>
        public decimal MarkPrice { get; set; }

        /// <summary>
        /// Unrealized PnL
        /// </summary>
        public decimal UnrealizedPnl { get; set; }

        /// <summary>
        /// Maintenance Margin Required
        /// </summary>
        public decimal MaintMargin { get; set; }
    }

    public class AccountUpdateData
    {
        public IEnumerable<FutureBalance> Balances { get; set; } = Array.Empty<FutureBalance>();

        public IEnumerable<PositionDetail> Positions { get; set; } = Array.Empty<PositionDetail>();
    }

    public class FutureBalance
    {
        /// <summary>
        /// The asset this balance is for
        /// </summary>
        public string Asset { get; set; } = string.Empty;
        /// <summary>
        /// The quantity that isn't locked in a trade
        /// </summary>
        public decimal WalletBalance { get; set; }
        /// <summary>
        /// The quantity that is locked in a trade
        /// </summary>
        public decimal CrossWalletBalance { get; set; }
        public decimal CrossUnrealizedPnl { get; set; } 
        /// <summary>
        /// Available balance
        /// </summary>
        public decimal AvailableBalance { get; set; } 
        /// <summary>
        /// Whether the asset can be used as margin in Multi-Assets mode
        /// </summary>
        public bool? MarginAvailable { get; set; }
    }
       
    public class FutureOrder
    {
        /// <summary>
        /// The symbol the order is for
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Pair
        /// </summary>
        public string? Pair { get; set; }

        /// <summary>
        /// The order id as assigned by Binance
        /// </summary>
        public long Id { get; set; }
        /// <summary>
        /// The order id as assigned by the client
        /// </summary>
        public string ClientOrderId { get; set; } = string.Empty;
        /// <summary>
        /// The price of the order
        /// </summary>
        public decimal Price { get; set; }
        /// <summary>
        /// The average price of the order
        /// </summary>
        public decimal AvgPrice { get; set; }
        /// <summary>
        /// Cumulative quantity
        /// </summary>
        public decimal QuantityFilled { get; set; }
        /// <summary>
        /// Cumulative quantity in quote asset ( for USD futures )
        /// </summary>
        public decimal? QuoteQuantityFilled { get; set; }

        /// <summary>
        /// Cumulative quantity in quote asset ( for Coin futures )
        /// </summary>
        public decimal? BaseQuantityFilled { get; set; }
        /// <summary>
        /// The quantity of the order that is executed
        /// </summary>
        public decimal LastFilledQuantity { get; set; }
        /// <summary>
        /// The original quantity of the order
        /// </summary>
        public decimal Quantity { get; set; }
        /// <summary>
        /// Reduce Only
        /// </summary>
        public bool ReduceOnly { get; set; }

        /// <summary>
        /// if Close-All
        /// </summary>
        public bool ClosePosition { get; set; }

        /// <summary>
        /// The side of the order
        /// </summary>
        public OrderSide Side { get; set; }

        /// <summary>
        /// The current status of the order
        /// </summary>
        public OrderStatus Status { get; set; }  
        /// <summary>
        /// For what time the order lasts
        /// </summary>
        public TimeInForce TimeInForce { get; set; } 
        /// <summary>
        /// The type of the order
        /// </summary>
        public OrderType Type { get; set; } 
        /// <summary>
        /// The time the order was updated
        /// </summary>
        public DateTime UpdateTime { get; set; }

        /// <summary>
        /// The time the order was created
        /// </summary>
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// The position side of the order
        /// </summary>
        public PositionSide PositionSide { get; set; } 
    }

    public class PositionDetail
    {
        /// <summary>
        /// Symbol
        /// </summary>
        public string Symbol { get; set; } = string.Empty;
        /// <summary>
        /// Entry price
        /// </summary>
        public decimal EntryPrice { get; set; }

        /// <summary>
        /// Leverage
        /// </summary>
        public int Leverage { get; set; }
        /// <summary>
        /// Unrealized profit
        /// </summary>
        public decimal UnrealizedPnl { get; set; }

        /// <summary>
        /// Position side
        /// </summary>
        public PositionSide PositionSide { get; set; }
        /// <summary>
        /// Margin type
        /// </summary>
        public MarginType MarginType { get; set; }       

        /// <summary>
        /// Isolated margin
        /// </summary>
        public decimal IsolatedMargin { get; set; }

        /// <summary>
        /// Liquidation price
        /// </summary>
        public decimal LiquidationPrice { get; set; }

        /// <summary>
        /// Mark price
        /// </summary>
        public decimal MarkPrice { get; set; }

        /// <summary>
        /// Position quantity
        /// </summary>
        public decimal Quantity { get; set; }

        /// <summary>
        /// Last update time
        /// </summary>
        public DateTime UpdateTime { get; set; }
    }

    public class FutureInfo
    {
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 订单的最小数量
        /// </summary>
        public decimal? MinTradeQuantity { get; set; }

        /// <summary>
        /// 订单数量增量
        /// </summary>
        public decimal? QuantityStep { get; set; }

        /// <summary>
        /// 价格上涨的增量
        /// </summary>
        public decimal? PriceStep { get; set; }

        /// <summary>
        /// 数量的最大小数位数
        /// </summary>
        public int? QuantityDecimals { get; set; }

        /// <summary>
        /// 价格的最大小数位数
        /// </summary>
        public int? PriceDecimals { get; set; }
    }
}
