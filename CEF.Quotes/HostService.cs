using CEF.Common;
using CEF.Common.Context;
using CEF.Common.Primitives;
using Microsoft.Extensions.Caching.Memory;

namespace CEF.Quotes
{
    public class HostService : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<HostService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IQuotesContext _context;
        public HostService(ILogger<HostService> logger,
                            IMemoryCache cache,
                                IConfiguration configuration,
                        IServiceProvider serviceProvider,
                        IQuotesContext context)
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
            await this._context.Subscribe();
            this._logger.LogWarning("服务启动完成.");
        }
    }
}