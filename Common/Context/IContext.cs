using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Context
{
    public interface IContext
    {
        int MaxFutureCount { set; get; }
        Task ExecuteAsync(CancellationToken ct = default);
        Task SyncExchangeDataAsync();

        Task SyncAdlOrderAsync();
    }
}
