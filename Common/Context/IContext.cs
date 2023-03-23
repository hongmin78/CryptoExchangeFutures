using CEF.Common.Entity;
using CEF.Common.Exchange;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Trady.Core.Infrastructure;

namespace CEF.Common.Context
{
    public interface IContext
    {
        int MaxFutureCount { set; get; }
        Task ExecuteAsync(CancellationToken ct = default);
        Task SyncExchangeDataAsync();

        Task SyncAdlOrderAsync();

        Task<List<Ohlcv>> GetKlineData(string symbol, PeriodOption period);
        Task<Dictionary<string, List<Ohlcv>>> GetAllKlineData(bool isRealTime = false);
    }
}
