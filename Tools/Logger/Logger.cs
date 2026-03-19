using Amazon;
using Amazon.S3;
using Newtonsoft.Json;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands.ERLCCommands.Commands.Debug;

namespace Whispbot
{
    public static class Logger
    {
        public static void Initialize()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(LogEventLevel.Verbose)
                .Enrich.With(new LogEnricher())
                .WriteTo.Console(
                outputTemplate:
                    Config.isDev ?
                    "[{Timestamp:HH:mm:ss.fff}][{Level:u4}][Cluster {ClusterId}] {Message:lj} {Data}{NewLine}{Exception}" :
                    "{{\"message\": \"[Cluster {ClusterId}] {Message:lj}\", \"level\": \"{Level:u4}\", \"data\": {Data}, \"error\": \"{Exception}\"}}{NewLine}",
                theme: SystemConsoleTheme.Colored)
                .CreateLogger();
        }

        public static void Shutdown()
        {
            Log.CloseAndFlush();
        }

        public static ILogger WithData(object data)
        {
            return Log.ForContext("Data", JsonConvert.SerializeObject(data));
        }
    }

    public class LogEnricher: ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("ClusterId", Config.cluster.ToString().PadLeft(Config.replicas.Count.ToString().Length)));

            if (!logEvent.Properties.ContainsKey("Data"))
                logEvent.AddPropertyIfAbsent(new LogEventProperty("Data", new ScalarValue(null)));
        }
    }
}
