using CEF.Common.Exchange;
using CEF.Common.Extentions;
using CEF.Common.OapiRobot;
using CEF.Common.Primitives;
using EFCore.Sharding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUnit.Framework.Internal; 

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
            
        }
    }
}