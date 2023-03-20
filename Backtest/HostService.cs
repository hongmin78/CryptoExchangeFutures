using CEF.Common.Context;
using CEF.Common.Entity;
using CEF.Common.Extentions;
using CEF.Common.Primitives;
using EFCore.Sharding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

internal class HostService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ILogger<HostService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDbAccessor _dbAccessor;
    public HostService(ILogger<HostService> logger,
                        IMemoryCache cache,
                            IConfiguration configuration,
                    IServiceProvider serviceProvider,
                    IDbAccessor dbAccessor)
    {
        this._logger = logger;
        this._serviceProvider = serviceProvider;
        this._cache = cache;
        this._configuration = configuration;
        this._dbAccessor = dbAccessor;
        GlobalConfigure.ServiceLocatorInstance = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var futures = await _dbAccessor.GetIQueryable<Future>().ToListAsync();
        Console.WriteLine(futures.ToJson());
    }
}
