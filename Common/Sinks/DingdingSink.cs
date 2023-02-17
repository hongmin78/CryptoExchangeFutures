using CEF.Common.Extentions;
using Microsoft.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CEF.Common.OapiRobot;

namespace CEF.Common.Sinks
{
    public class DingdingSink : ILogEventSink, IDisposable
    {
        public void Dispose()
        { 
        }

        public void Emit(LogEvent logEvent)
        {
            if (logEvent.GetLevel() < LogLevel.Warning)
                return;
            OapiRobotHelper.Message($"{logEvent.MessageTemplate.Text}", logEvent.GetLevel() == LogLevel.Warning ? "Information" : "Error").ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}
