using CEF.Common.Context;
using CEF.Common.Entity;
using CEF.Common.Exchange;
using CEF.Common.Extentions;
using CEF.Common.Primitives;
using CEF.Quotes;
using CryptoExchange.Net.CommonObjects;
using EFCore.Sharding;
using Microsoft.Extensions.Caching.Memory;
using Trady.Core.Infrastructure;

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
//app.Urls.Add("http://*");
app.MapGet("/orders", GetFutureOrders);
app.MapGet("/", GetKlineData);
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