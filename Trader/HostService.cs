using CEF.Common.Context;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

internal class HostService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HostService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IContext _context;
    public HostService(ILogger<HostService> logger,
                        IMemoryCache cache,
                            IConfiguration configuration,
                    IServiceProvider serviceProvider,
                    IContext context)
    {
        this._logger = logger;
        this._serviceProvider = serviceProvider;
        this._cache = cache;
        this._configuration = configuration;
        this._context = context;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var symbols = new List<string>() { "BTCUSDT", "ETHUSDT", "BCHUSDT", "XRPUSDT", "LTCUSDT", "LINKUSDT", "ATOMUSDT", "DOGEUSDT", "UNIUUSDT", "AVAXUSDT", "FTMUSDT", "MATICUSDT" }; 
        await this._context.ExecuteAsync(symbols, stoppingToken);

        //while (!stoppingToken.IsCancellationRequested)
        //{
        //    try
        //    {
        //    }
        //    catch (Exception e)
        //    {
        //        this._logger.LogError(e, e.Message);
        //    }
        //    finally
        //    {
        //        await Task.Delay(TimeSpan.FromMilliseconds(10));
        //    }
        //}
    }
}