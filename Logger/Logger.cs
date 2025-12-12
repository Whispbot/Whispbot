using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace Whispbot
{
    public static class Logger
    {
        public static void Initialize()
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(Config.IsDev ? Serilog.Events.LogEventLevel.Verbose : Serilog.Events.LogEventLevel.Information)
                .WriteTo.Console(outputTemplate: $"[{{Timestamp:HH:mm:ss.fff}}][{{Level:u3}}] {(Config.replicaId is not null ? $"[{Config.replicaId}] " : "")}{{Message:lj}}{{NewLine}}{{Exception}}", theme: SystemConsoleTheme.Colored)
                .CreateLogger();
        }

        public static void Shutdown()
        {
            Log.CloseAndFlush();
        }
    }
}
