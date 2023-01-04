using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Context
{
    public interface IContext
    {
        Task ExecuteAsync(IEnumerable<string> symbols, CancellationToken ct = default);
        Task SyncExchangeDataAsync();
    }
}
