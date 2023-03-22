using Castle.Core.Configuration;
using CEF.Common.Entity;
using CEF.Common.Exchange;
using CEF.Common.Extentions;
using CEF.Common.Helper;
using CEF.Common.OapiRobot;
using CEF.Common.Primitives;
using EFCore.Sharding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework.Internal;
using Trady.Core;
using Trady.Core.Infrastructure;

namespace CEF.UnitTests
{
    public class Tests
    {
        IServiceProvider Services { set; get; }
        [SetUp]
        public void Setup()
        {
            var builder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
               .ConfigureAppConfiguration((hostingContext, config) =>
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
                       var connectionString = "DataSource=trade.db";
                       config.UseDatabase(connectionString, DatabaseType.SQLite);
                       config.SetEntityAssemblies(GlobalConfigure.AllAssemblies);
                       config.CreateShardingTableOnStarting(false);
                       config.EnableShardingMigration(false);
                   });
               });
            //.UseConsoleLifetime();
            var host = builder.Build();
            (new EFCoreShardingBootstrapper(host.Services)).StartAsync(new CancellationToken()).ConfigureAwait(false).GetAwaiter().GetResult();
            this.Services = host.Services;
            GlobalConfigure.ServiceLocatorInstance = this.Services.CreateScope().ServiceProvider;
        }

        [Test]
        public void Test1()
        {
            var logger = Services.CreateScope().ServiceProvider.GetService<ILogger<Test>>();
            logger.LogWarning("≤‚ ‘»’÷ææØ∏Ê");
            Assert.Pass();
        }

        [Test]
        public void test2()
        {
            var exchange = Services.CreateScope().ServiceProvider.GetService<IExchange>();
            var orders = exchange.GetOrderAsync("LINKUSDT", null, "1635558913199837185").GetAwaiter().GetResult();

            Assert.Pass();

        }

        [Test]
        public async Task test3()
        {
            var symbol = "BTCUSDT";
            var period = PeriodOption.Per15Minute;
            var configuration = Services.CreateScope().ServiceProvider.GetService<Microsoft.Extensions.Configuration.IConfiguration>();
            var baseUrl = configuration["QuotesBaseUrl"];
            var result = await RestSharpHttpHelper.RestActionAsync(baseUrl, $"/{symbol}/{(int)period}");
            var klines = result.ToObject<List<Ohlcv>>();

            result = await RestSharpHttpHelper.RestActionAsync(baseUrl, "/orders");
            var orders = result.ToObject<IEnumerable<FutureOrder>>();

            Assert.Pass();
        }
    }
}