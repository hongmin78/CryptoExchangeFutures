using Colder.DistributedLock.Hosting;
using CEF.Common.Helper;
using CEF.Common.Primitives;
using CSRedis;
using Exceptionless;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CEF.Common.Sinks;

namespace CEF.Common.Extentions
{
    public static class HostExtentions
    {
        /// <summary>
        /// 配置日志
        /// </summary>
        /// <param name="hostBuilder">建造者</param>
        /// <returns></returns>
        public static IHostBuilder ConfigureLoggingDefaults(this IHostBuilder hostBuilder)
        {
            //ThreadPool.GetMinThreads(out int workerThreadsMin, out int completionPortThreadsMin);
            //Console.WriteLine($"最小 workerThreadsMin：{workerThreadsMin}  completionPortThreadsMin：{completionPortThreadsMin}");

            //ThreadPool.GetMaxThreads(out int workerThreadsMax, out int completionPortThreadsMax);
            //Console.WriteLine($"最大 workerThreadsMax：{workerThreadsMax}   completionPortThreadsMax：{completionPortThreadsMax}");

            ////if(!ThreadPool.SetMaxThreads(32767, 1000))
            ////    Console.WriteLine($"ThreadPool.SetMaxThreads 设置失败");

            //if (!ThreadPool.SetMinThreads(300, 300))
            //    Console.WriteLine($"ThreadPool.SetMinThreads 设置失败");

            var rootPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var path = Path.Combine(rootPath, "logs", "log.txt");
            SelfLog.Enable(Console.Error);

            #region 控制台exceptionless
            //hostBuilder.ConfigureLogging((config, builder) =>
            //{
            //    builder.AddExceptionless(config.Configuration.GetSection("Exceptionless:ApiKey").Value, config.Configuration.GetSection("Exceptionless:ServerUrl").Value);

            //}).ConfigureServices(services =>
            //{
            //    services.AddExceptionless();
            //}).UseExceptionless();
            #endregion
            return hostBuilder.UseSerilog((hostingContext, serviceProvider, serilogConfig) =>
            {
                var envConfig = hostingContext.Configuration;
                LogOptions logConfig = new LogOptions();
                envConfig.GetSection("log").Bind(logConfig);

                logConfig.Overrides.ForEach(aOverride =>
                {
                    serilogConfig
                        .MinimumLevel
                        .Override(aOverride.Source, (Serilog.Events.LogEventLevel)aOverride.MinLevel);
                });
                serilogConfig.MinimumLevel.Is((Serilog.Events.LogEventLevel)logConfig.MinLevel);
                if (logConfig.Console.Enabled)
                {
                    serilogConfig.WriteTo.Console();
                }
                if (logConfig.Debug.Enabled)
                {
                    serilogConfig.WriteTo.Debug();
                }
                if (logConfig.File.Enabled)
                {
                    string template = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3} {SourceContext:l}] {Message:lj}{NewLine}{Exception}";

                    serilogConfig.WriteTo.File(
                        path,
                        outputTemplate: template,
                        rollingInterval: RollingInterval.Day,
                        shared: true,
                        fileSizeLimitBytes: 10 * 1024 * 1024,
                        rollOnFileSizeLimit: true
                        );
                }
                if (logConfig.Elasticsearch.Enabled)
                {
                    var uris = logConfig.Elasticsearch.Nodes.Select(x => new Uri(x)).ToList();

                    serilogConfig.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(uris)
                    {
                        IndexFormat = logConfig.Elasticsearch.IndexFormat,
                        AutoRegisterTemplate = true,
                        AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
                    });
                }

                if (logConfig.Exceptionless.Enabled)
                {
                    serilogConfig.WriteTo.Exceptionless(restrictedToMinimumLevel: LogEventLevel.Error);
                }
                if (logConfig.Dingding.Enabled)
                {
                    serilogConfig.WriteTo.Dingding();
                }
                //自定义属性
                serilogConfig.Enrich.WithProperty("MachineName", Environment.MachineName);
                serilogConfig.Enrich.WithProperty("ApplicationName", Assembly.GetEntryAssembly().GetName().Name);
                serilogConfig.Enrich.WithProperty("ApplicationVersion", Assembly.GetEntryAssembly().GetName().Version);
                var httpContext = serviceProvider.GetService<IHttpContextAccessor>()?.HttpContext;
                if (httpContext != null)
                {
                    serilogConfig.Enrich.WithProperty("RequestPath", httpContext.Request.Path);
                    serilogConfig.Enrich.WithProperty("RequestIp", httpContext.Connection.RemoteIpAddress);
                }
            });
        }
        /// <summary>
        /// 使用IdHelper
        /// </summary>
        /// <param name="hostBuilder">建造者</param>
        /// <returns></returns>
        public static IHostBuilder UseIdHelper(this IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices((buidler, services) =>
            {
                new Coldairarrow.Util.IdHelperBootstrapper()
                    //设置WorkerId
                    .SetWorkderId(buidler.Configuration["WorkerId"].ToLong())
                    //使用Zookeeper
                    //.UseZookeeper("127.0.0.1:2181", 200, GlobalSwitch.ProjectName)
                    .Boot();
            });

            return hostBuilder;
        }

        /// <summary>
        /// 使用缓存
        /// </summary>
        /// <param name="hostBuilder">建造者</param>
        /// <returns></returns>
        public static IHostBuilder UseCache(this IHostBuilder hostBuilder)
        {
            hostBuilder.ConfigureServices((buidlerContext, services) =>
            {
                #region 内存缓存
                services.AddMemoryCache();
                services.AddScoped<IMemoryCacheHelper, MemoryCacheHelper>();//包装了框架自带memorycache 
                services.AddScoped(typeof(IMemoryCache<,>), typeof(MemoryCache<,>));
                #endregion
                var cacheOption = buidlerContext.Configuration.GetSection("Cache").Get<CacheOptions>();
                //StackRedisHelper.Initialization(cacheOption.RedisEndpoint, cacheOption.ClusterRedisEndpoint);
                services.AddScoped(typeof(IShareCache<,>), typeof(ShareCache<,>));
                switch (cacheOption.CacheType)
                {
                    case CacheType.Memory: services.AddDistributedMemoryCache(); break;
                    case CacheType.Redis:
                        {
                            var csredis = new CSRedisClient(cacheOption.RedisEndpoint);
                            RedisHelper.Initialization(csredis);
                            services.AddSingleton(csredis);
                            services.AddSingleton<IDistributedCache>(new CSRedisCache(RedisHelper.Instance));
                        }; break;
                    default: throw new Exception("缓存类型无效");
                }
                //services.AddCacheManagerConfiguration(buidlerContext.Configuration, ConstDefine.CacheManagerDefaultKey, cfg => cfg.WithMicrosoftLogging(services))
                //        .AddCacheManagerConfiguration(buidlerContext.Configuration, ConstDefine.CacheManagerRedisNeverExpiresKey, cfg => cfg.WithMicrosoftLogging(services))
                //        .AddCacheManager<object>(buidlerContext.Configuration, ConstDefine.CacheManagerDefaultKey, configure: builder => builder.WithJsonSerializer())
                //        .AddCacheManager();
            });
            return hostBuilder;
        }

        /// <summary>
        /// 使用分布式锁
        /// </summary>
        /// <param name="hostBuilder">建造者</param>
        /// <returns></returns>
        public static IHostBuilder UseDistributedLock(this IHostBuilder hostBuilder)
        {
            return hostBuilder.ConfigureDistributedLockDefaults();
        }
        /// <summary>
        /// aspnetcore web使用exceptionless
        /// </summary>
        /// <param name="applicationBuilder"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseExExceptionless(this IApplicationBuilder applicationBuilder, IConfiguration configuration)
        {
            ExceptionlessClient.Default.Configuration.ApiKey = configuration.GetSection("Exceptionless:ApiKey").Value;
            ExceptionlessClient.Default.Configuration.ServerUrl = configuration.GetSection("Exceptionless:ServerUrl").Value;
            return applicationBuilder.UseExceptionless();
        }

    }
}
