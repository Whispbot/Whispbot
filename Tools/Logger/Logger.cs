using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.SystemConsole.Themes;

namespace Whispbot
{
    public static class Logger
    {
        public static void Initialize()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(Config.IsDev ? Serilog.Events.LogEventLevel.Verbose : Serilog.Events.LogEventLevel.Information)
                .Enrich.With(new LogEnricher())
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff}][{Level:u3}][Cluster {ClusterId}] {Message:lj}{NewLine}{Exception}", theme: SystemConsoleTheme.Colored)
                .CreateLogger();
        }

        public static void Shutdown()
        {
            Log.CloseAndFlush();
        }
    }

    public class LogEnricher: ILogEventEnricher
    {
        public void Enrich(Serilog.Events.LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("ClusterId", Config.cluster.ToString().PadLeft(Config.replicas.Count.ToString().Length)));
        }
    }
}
