using CEF.Common.Context;
using CEF.Common.Entity;
using CEF.Common.Exchange;
using CEF.Common.Extentions;
using CEF.Common.Primitives;
using Coldairarrow.Util;
using EFCore.Sharding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);//Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
builder.Host.ConfigureAppConfiguration(config =>
{
    config.SetBasePath(AppContext.BaseDirectory)
     .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
     .AddEnvironmentVariables();
})
.ConfigureLoggingDefaults()
.UseIdHelper()
.UseCache()
.UseDistributedLock()
.ConfigureServices((hostContext, services) =>
{
    services.AddFxServices();
    services.AddAutoMapper();
    services.AddEFCoreSharding(config =>
    {
        var connectionString = hostContext.Configuration["DbConnectionString"].ToString();
        if(string.IsNullOrEmpty(connectionString))
            connectionString = "DataSource=trade.db";
        //var connectionString = $"DataSource={System.AppDomain.CurrentDomain.BaseDirectory}trade.db";
        //var connectionString = "DataSource=G:\\Git\\QuantitativeTrading\\Code\\Trader\\trade.db";
        config.UseDatabase(connectionString, DatabaseType.SQLite);
        config.SetEntityAssemblies(GlobalConfigure.AllAssemblies);
        config.CreateShardingTableOnStarting(false);
        config.EnableShardingMigration(false);
    });
    services.AddHostedService<HostService>();
});
var app = builder.Build();
app.Urls.Add(app.Configuration["WebRootUrl"]);
//app.Urls.Add("http://*");
app.MapGet("/", () => Results.Redirect("/p"));
app.MapGet("/p", GetFutures);
app.MapGet("/p/{symbol}", GetFutures);
app.MapGet("/p/all", GetAllFutures);
app.MapGet("/max", GetMaxFutureCount);
app.MapGet("/max/set/{count}", SetMaxFutureCount);
app.MapGet("/o", () => Results.Redirect("/o/1/20"));
app.MapGet("/o/{pageIndex:int}/{pageSize:int}", GetOrders);
app.MapGet("/o/{symbol}", (string symbol) => Results.Redirect($"/o/{symbol}/1/20"));
app.MapGet("/o/{symbol}/{pageIndex:int}/{pageSize:int}", GetOrdersBySymbol);
app.MapGet("/o/{symbol}/{positionSide:int}", (string symbol, int positionSide) => Results.Redirect($"/o/{symbol}/{positionSide}/1/20"));
app.MapGet("/o/{symbol}/{positionSide:int}/{pageIndex:int}/{pageSize:int}", GetOrdersBySymbolPositionSide);
app.MapGet("/o/s", () => Results.Redirect($"/o/s/Filled/1/20"));
app.MapGet("/o/s/{status}/{pageIndex:int}/{pageSize:int}", GetOrdersByStatus);
app.MapGet("/o/ns", () => Results.Redirect($"/o/ns/Filled/1/20"));
app.MapGet("/o/ns/{status}/{pageIndex:int}/{pageSize:int}", GetOrdersByNotStatus);
app.MapGet("/o/cancel/{symbol}/{orderId:int}", CancelOrder);
app.MapGet("/o/g", GetDailyReport);
app.MapGet("/get/{symbol}/{positionSide:int}", GetFutureDetail);
app.MapGet("/set/{id:long}/{isEnabled:int}", SetFutureEnable);
app.MapPost("/add", AddFuture);
app.MapPost("/update", UpdateFuture); 
await app.RunAsync();

static async Task<string> AddFuture([FromBody] Future future)
{
    using var scope = GlobalConfigure.ServiceLocatorInstance.CreateScope();
    var context = scope.ServiceProvider.GetService<IContext>();
    var entity = new Future()
    {
        Symbol = future.Symbol.ToUpper(),
        Id = IdHelper.GetLongId(),
        UpdateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss fff"),
        AbleSize = 0,
        BaseOrderSize = future.BaseOrderSize,
        EntryPrice = 0,
        LastTransactionOpenPrice = 0,
        LastTransactionOpenSize = 0,
        MaxSafetyOrdersCount = future.MaxSafetyOrdersCount,
        OrdersCount = 0,
        PositionSide = future.PositionSide,
        Size = 0,
        TargetProfit = future.TargetProfit,
        SafetyOrderSize = future.SafetyOrderSize,
        SafetyOrderPriceDeviation = future.SafetyOrderPriceDeviation,
        SafetyOrderPriceScale = future.SafetyOrderPriceScale,
        SafetyOrderVolumeScale = future.SafetyOrderVolumeScale,
        IsEnabled = future.IsEnabled,
        Status = future.Status,
        PNL = 0
    };
    await context.CreateFutureAsync(entity);
    return "ok";
}

static async Task<string> UpdateFuture([FromBody] Future future)
{
    using var scope = GlobalConfigure.ServiceLocatorInstance.CreateScope(); 
    var context = scope.ServiceProvider.GetService<IContext>(); 
    await context.UpdateFutureAsync(future, new List<string>() { "BaseOrderSize", "TargetProfit", "SafetyOrderSize", "MaxSafetyOrdersCount", "SafetyOrderVolumeScale", "SafetyOrderPriceScale", "SafetyOrderPriceDeviation", "IsEnabled" });
    return "ok";
}

static async Task<string> GetFutureDetail(string symbol, int positionSide)
{
    using var scope = GlobalConfigure.ServiceLocatorInstance.CreateScope();
    using var dbAccessor = scope.ServiceProvider.GetService<IDbAccessor>(); 
    var future = await dbAccessor.GetIQueryable<Future>().Where(x=>x.Symbol == symbol && x.PositionSide == positionSide).FirstOrDefaultAsync();
    return future.ToJson();
}

static async Task<string> SetFutureEnable(long id, int isEnabled)
{
    using var scope = GlobalConfigure.ServiceLocatorInstance.CreateScope();
    using var dbAccessor = scope.ServiceProvider.GetService<IDbAccessor>();
    var context = scope.ServiceProvider.GetService<IContext>();
    var future = await dbAccessor.GetIQueryable<Future>().Where(x => x.Id == id).FirstOrDefaultAsync();
    future.IsEnabled = isEnabled;
    await context.UpdateFutureAsync(future, new List<string>() { "IsEnabled" });
    return "ok";
}

static async Task<IResult> GetAllFutures()
{
    return await GetFuturesImpl(string.Empty, null);
}

static async Task<IResult> GetFutures(string? symbol)
{
    return await GetFuturesImpl(symbol, true);
}

static async Task<IResult> GetFuturesImpl(string? symbol, bool? enable)
{
    var sb = new StringBuilder();
    using var scope = GlobalConfigure.ServiceLocatorInstance.CreateScope();
    using var dbAccessor = scope.ServiceProvider.GetService<IDbAccessor>();
    var context = scope.ServiceProvider.GetService<IContext>();
    var futures = await dbAccessor.GetIQueryable<Future>().ToListAsync();
    if (!string.IsNullOrEmpty(symbol))
        futures = futures.Where(x => x.Symbol.Contains(symbol)).ToList();
    if(enable??false)
        futures = futures.Where(x => x.IsEnabled == 1 && x.Size > 0).ToList();
    futures = futures.OrderBy(x=>x.Symbol).ThenByDescending(x=>x.PositionSide).ToList();
    sb.Append("<html>");
    sb.Append("<head></head>"); 
    sb.Append("<body>");
    sb.Append("<table border='1px' cellpadding='7' cellspacing='1' bgcolor='lightyellow' style='font-family:Garamond; font-size:smaller'>");
    sb.Append("<tr style='font-weight: bold'>");
    sb.Append($"<td align='center'>Symbol</td>");
    sb.Append($"<td align='center'>PositionSide</td>");
    sb.Append($"<td align='center'>Size</td>");
    sb.Append($"<td align='center'>EntryPrice</td>");
    sb.Append($"<td align='center'>CurrentPrice</td>");
    sb.Append($"<td align='center'>Value</td>");
    sb.Append($"<td align='center'>LastTransactionPrice</td>");
    sb.Append($"<td align='center'>LastTransactionSize</td>");
    sb.Append($"<td align='center'>LastUpdateTime</td>");
    sb.Append($"<td align='center'>OrdersCount</td>");
    sb.Append($"<td align='center'>IsEnabled</td>");
    sb.Append($"<td align='center'>Status</td>");
    sb.Append($"<td align='center'>UnrealizedPNL</td>");
    sb.Append($"<td align='center'>PNL</td>");
    sb.Append("</tr>");

    var totalUnrealizedPNL = 0m;
    var allKlineData = await context.GetAllKlineData();
    foreach (var future in futures)
    {
        var per15MinuteMemoryKey = string.Format("GetKlineDataAsync_{0}_{1}", future.Symbol, (int)PeriodOption.Per15Minute); 
        var klines = (allKlineData?.ContainsKey(per15MinuteMemoryKey)??false) ? allKlineData[per15MinuteMemoryKey] : new List<Ohlcv>(); 
        var price = klines.LastOrDefault()?.Close ?? 0;
        var unrealizedPNL = (future.PositionSide == 1 ? 1 : -1) * (price - future.EntryPrice) * future.Size;
        totalUnrealizedPNL += unrealizedPNL;
        sb.Append("<tr >");
        sb.Append($"<td>{future.Symbol}</td>");
        sb.Append($"<td>{future.PositionSide}</td>");
        sb.Append($"<td>{future.Size}</td>");
        sb.Append($"<td>{future.EntryPrice}</td>");
        sb.Append($"<td>{price}</td>");
        sb.Append($"<td>{future.Size * price}</td>");
        sb.Append($"<td>{future.LastTransactionOpenPrice}</td>");
        sb.Append($"<td>{future.LastTransactionOpenSize}</td>");
        sb.Append($"<td>{future.UpdateTime}</td>");
        sb.Append($"<td>{future.OrdersCount}</td>");
        sb.Append($"<td>{future.IsEnabled}</td>");
        sb.Append($"<td>{future.Status}</td>");
        sb.Append($"<td>{unrealizedPNL}</td>");
        sb.Append($"<td>{future.PNL}</td>");
        sb.Append("</tr>");
    }

    sb.Append($"<tr><td colspan='10' align='right'>Total UnrealizedPNL:{totalUnrealizedPNL}</td><td colspan='4' align='right'>Total PNL:{futures?.Sum(x => x.PNL) ?? 0}</td></tr>");
    sb.Append("</table>");
    sb.Append("</body>");
    sb.Append("</html>");
    return Results.Extensions.Html(sb.ToString());
}

static async Task<int> GetMaxFutureCount()
{
    using var scope = GlobalConfigure.ServiceLocatorInstance.CreateScope();
    var context = scope.ServiceProvider.GetService<IContext>();
    return await Task.FromResult(context.MaxFutureCount);
}

static async Task<string> SetMaxFutureCount(int count)
{
    using var scope = GlobalConfigure.ServiceLocatorInstance.CreateScope();
    var context = scope.ServiceProvider.GetService<IContext>();
    context.MaxFutureCount = count;
    return await Task.FromResult("ok");
}

static async Task<IResult> GetOrders(int pageIndex = 1, int pageSize = 20)
{
    return await GetOrdersImpl(pageIndex: pageIndex, pageSize: pageSize);
}

static async Task<IResult> GetOrdersBySymbol(string symbol, int pageIndex = 1, int pageSize = 20)
{
    return await GetOrdersImpl(symbol: symbol, pageIndex: pageIndex, pageSize: pageSize);
}

static async Task<IResult> GetOrdersBySymbolPositionSide(string symbol, int? positionSide, int pageIndex = 1, int pageSize = 20)
{
    return await GetOrdersImpl(symbol: symbol, positionSide: positionSide, pageIndex: pageIndex, pageSize: pageSize);
}

static async Task<IResult> GetOrdersByStatus(string status, int pageIndex = 1, int pageSize = 20)
{
    var statusArr = status.Split(',');
    return await GetOrdersImpl(statusArr: statusArr, pageIndex: pageIndex, pageSize: pageSize);
}

static async Task<IResult> GetOrdersByNotStatus(string status, int pageIndex = 1, int pageSize = 20)
{
    var notStatusArr = status.Split(',');
    return await GetOrdersImpl(notStatusArr: notStatusArr, pageIndex: pageIndex, pageSize: pageSize);
}

static async Task<IResult> GetOrdersImpl(string? symbol = null, int? positionSide = null, string[]? statusArr = null, string[]? notStatusArr = null, int pageIndex = 1, int pageSize = 20)
{
    //Short Long
    var sb = new StringBuilder();
    using var scope = GlobalConfigure.ServiceLocatorInstance.CreateScope();
    using var dbAccessor = scope.ServiceProvider.GetService<IDbAccessor>();
    var query = dbAccessor.GetIQueryable<CEF.Common.Entity.Order>();
    if (!string.IsNullOrEmpty(symbol))
        query = query.Where(x => x.Symbol.Contains(symbol));
    if(positionSide.HasValue)
        query = query.Where(x => x.PositionSide == (positionSide == 0 ? "Short" : "Long"));
    if((statusArr??new string[] { }).Any())
    {
        foreach (var status in statusArr)
            query = query.Where(x=>x.Status.Contains(status));
    }
    if ((notStatusArr ?? new string[] { }).Any())
    {
        foreach (var status in notStatusArr)
            query = query.Where(x => !x.Status.Contains(status));
    }
    var orders = await query.OrderByDescending(x => x.CreateTime).Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();
    sb.Append("<html>");
    sb.Append("<head></head>");
    sb.Append("<body>");
    sb.Append("<table border='1px' cellpadding='7' cellspacing='1' bgcolor='lightyellow' style='font-family:Garamond; font-size:smaller'>");
    sb.Append("<tr style='font-weight: bold'>");
    sb.Append("<td align='center'>OrderId</td>");
    sb.Append("<td align='center'>Symbol</td>");
    sb.Append("<td align='center'>PositionSide</td>");
    sb.Append("<td align='center'>OrderSide</td>");
    sb.Append("<td align='center'>Side</td>");   
    sb.Append("<td align='center'>Size</td>");
    sb.Append("<td align='center'>FilledSize</td>");
    sb.Append("<td align='center'>Price</td>");
    sb.Append("<td align='center'>AvgPrice</td>");
    sb.Append("<td align='center'>OrderType</td>"); 
    sb.Append("<td align='center'>Status</td>"); 
    sb.Append("<td align='center'>CreateTime</td>");
    sb.Append("<td align='center'>UpdateTime</td>");
    sb.Append("<td align='center'>PNL</td>");
    sb.Append("<td align='center'>Function</td>"); 
    sb.Append("</tr>");
    foreach (var order in orders)
    {
        sb.Append("<tr >");
        sb.Append($"<td>{order.ClientOrderId}</td>");
        sb.Append($"<td>{order.Symbol}</td>");
        sb.Append($"<td>{order.PositionSide}</td>");
        sb.Append($"<td>{order.OrderSide}</td>");
        sb.Append($"<td>{order.Side}</td>");
        sb.Append($"<td>{order.Quantity}</td>");
        sb.Append($"<td>{order.FilledQuantity}</td>");
        sb.Append($"<td>{order.Price}</td>");
        sb.Append($"<td>{order.AvgPrice}</td>");
        sb.Append($"<td>{order.Type}</td>");
        sb.Append($"<td>{order.Status}</td>");
        sb.Append($"<td>{order.CreateTime}</td>");
        sb.Append($"<td>{order.UpdateTime}</td>");
        sb.Append($"<td>{order.PNL}</td>");
        if (order.Status == "New" || order.Status == "PartiallyFilled")
            sb.Append($"<td align='center'> <a href='/o/cancel/{order.Symbol}/{long.Parse(order.ClientOrderId)}'>cancel</a></td>");
        else
            sb.Append("<td align='center'>&nbsp;</td>");
        sb.Append("</tr>");
    }
    sb.Append("</table>");
    sb.Append("</body>");
    sb.Append("</html>");
    return Results.Extensions.Html(sb.ToString());
}

static async Task<bool> CancelOrder(string symbol, long orderId)
{
    using var scope = GlobalConfigure.ServiceLocatorInstance.CreateScope(); 
    var exchange = scope.ServiceProvider.GetService<IExchange>();
    var orderResult = await exchange.CancelOrderAsync(symbol, orderId);
    return orderResult?.Success??false;
}

static async Task<IResult> GetDailyReport()
{
    var sb = new StringBuilder();
    using var scope = GlobalConfigure.ServiceLocatorInstance.CreateScope();
    using var dbAccessor = scope.ServiceProvider.GetService<IDbAccessor>();
    var query = dbAccessor.GetIQueryable<CEF.Common.Entity.Order>().Where(x=>x.PNL != null && x.PNL != 0).GroupBy(x=>x.CreateTime.Substring(0, 10)).Select(x=>new { Date = x.Key, PNL = x.Sum(x=>x.PNL)}); 
    var result = await query.OrderByDescending(x=>x.Date).ToListAsync(); 
    sb.Append("<html>");
    sb.Append("<head></head>");
    sb.Append("<body>");
    sb.Append("<table border='1px' cellpadding='7' cellspacing='1' bgcolor='lightyellow' style='font-family:Garamond; font-size:smaller'>");
    sb.Append("<tr style='font-weight: bold'>");
    sb.Append("<td align='center'>Date</td>");
    sb.Append("<td align='center'>Sum PNL</td>");   
    sb.Append("</tr>");
    foreach (var item in result)
    {
        sb.Append("<tr >");
        sb.Append($"<td>{item.Date}</td>");
        sb.Append($"<td>{item.PNL}</td>"); 
        sb.Append("</tr>");
    }
    sb.Append("</table>");
    sb.Append("</body>");
    sb.Append("</html>");
    return Results.Extensions.Html(sb.ToString());
}
