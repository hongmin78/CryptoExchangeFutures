using CEF.Common.Entity;
using CEF.Common.Exchange;
using CEF.Common.Extentions;
using CEF.Common.Primitives;
using CEF.Quotes;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
    services.AddHostedService<HostService>();
});
var app = builder.Build();
app.Urls.Add(app.Configuration["QuotesBaseUrl"]);
app.MapGet("/orders", GetFutureOrders);
app.MapGet("/", GetKlineData); 
app.MapGet("/symbols", GetSymbols);
app.MapGet("/add/{symbol}", Add);
app.MapGet("/remove/{symbol}", Remove);
await app.RunAsync();
  
static async Task<Dictionary<string, List<Ohlcv>>> GetKlineData()
{
    using var scope = GlobalConfigure.ServiceLocatorInstance.CreateScope(); 
    var context = scope.ServiceProvider.GetService<IQuotesContext>();
    return await context.GetAllKlineData();
}

static IEnumerable<FutureOrder> GetFutureOrders()
{
    using var scope = GlobalConfigure.ServiceLocatorInstance.CreateScope();
    var context = scope.ServiceProvider.GetService<IQuotesContext>();
    return context.GetFutureOrders();
}

static async Task<string> GetSymbols()
{
    using var scope = GlobalConfigure.ServiceLocatorInstance.CreateScope();
    var configuration = scope.ServiceProvider.GetService<IConfiguration>();
    return configuration["Symbols"].ToString();
}

static async Task<string> Add(string symbol)
{
    symbol = symbol.ToUpper().Trim();
    using var scope = GlobalConfigure.ServiceLocatorInstance.CreateScope(); 
    var quotesContext = scope.ServiceProvider.GetService<IQuotesContext>();
    return await quotesContext.Add(symbol); 
}

static async Task<string> Remove(string symbol)
{
    symbol = symbol.ToUpper().Trim();
    using var scope = GlobalConfigure.ServiceLocatorInstance.CreateScope();
    var quotesContext = scope.ServiceProvider.GetService<IQuotesContext>();
    return await quotesContext.Remove(symbol);
}

