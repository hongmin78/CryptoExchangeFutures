using CEF.Common.Extentions;
using CEF.Common.Primitives;
using EFCore.Sharding;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        var connectionString = "DataSource=G:\\Git\\QuantitativeTrading\\Code\\Trader\\trade.db";
        config.UseDatabase(connectionString, DatabaseType.SQLite);
        config.SetEntityAssemblies(GlobalConfigure.AllAssemblies);
        config.CreateShardingTableOnStarting(false);
        config.EnableShardingMigration(false);
    });
    services.AddHostedService<HostService>();
})
.UseConsoleLifetime();
var app = builder.Build();
app.Urls.Add("http://*");
app.MapGet("/", () => "Trading Robot");
await app.RunAsync();