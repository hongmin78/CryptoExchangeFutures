using Serilog.Configuration;
using Serilog.Events;
using Serilog.Sinks.Exceptionless;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CEF.Common.Sinks
{
    public static class LoggerSinkConfigurationExtensions
    { 
        public static LoggerConfiguration Dingding(
            this LoggerSinkConfiguration loggerConfiguration
        )
        {
            if (loggerConfiguration == null)
                throw new ArgumentNullException(nameof(loggerConfiguration));

            return loggerConfiguration.Sink(new DingdingSink());
        }

        internal static LogLevel GetLevel(this LogEvent log)
        {
            switch (log.Level)
            {
                case LogEventLevel.Verbose:
                    return LogLevel.Trace;
                case LogEventLevel.Debug:
                    return LogLevel.Debug;
                case LogEventLevel.Information:
                    return LogLevel.Information;
                case LogEventLevel.Warning:
                    return LogLevel.Warning;
                case LogEventLevel.Error:
                    return LogLevel.Error;
                case LogEventLevel.Fatal:
                    return LogLevel.Critical;
                default:
                    return LogLevel.None;
            }
        }
    }
}
