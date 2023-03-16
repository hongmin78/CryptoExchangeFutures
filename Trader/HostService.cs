﻿using CEF.Common;
using CEF.Common.Context;
using CEF.Common.Primitives;
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
        GlobalConfigure.ServiceLocatorInstance = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //var symbols = new List<string>() { "BTCUSDT", "ETHUSDT", "BCHUSDT", "XRPUSDT", "LTCUSDT", "LINKUSDT", "ATOMUSDT", "DOGEUSDT", "UNIUSDT", "AVAXUSDT", "FTMUSDT", "MATICUSDT" }; 
        this._context.ExecuteAsync(stoppingToken);
        //JobHelper.SetIntervalJob(async () => 
        //{
        //    await this._context.SyncAdlOrderAsync();
        //}, TimeSpan.FromMinutes(15));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await this._context.SyncExchangeDataAsync();
            }
            catch (Exception e)
            {
                this._logger.LogError(e, e.Message);
            }
            finally
            {
                await Task.Delay(TimeSpan.FromSeconds(15));
            }
        }
    }
}