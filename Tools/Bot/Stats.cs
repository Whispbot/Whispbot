using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Whispbot.Commands;

namespace Whispbot.Tools.Bot
{
    public static class Stats
    {
        public static long GetMemoryUsageBytes()
        {
            using var proc = Process.GetCurrentProcess();
            return proc.PrivateMemorySize64;
        }
        public static long GetMemoryUsageMB()
        {
            return GetMemoryUsageBytes() / (1024 * 1024);
        }

        public static void LogCommand(CommandContext ctx, Command command, TimeSpan duration)
        {
            SentrySdk.Metrics.EmitDistribution("bot.command.duration", duration.TotalMilliseconds, MeasurementUnit.Duration.Millisecond, new List<KeyValuePair<string, object>>()
            {
                new("command", command.Name),
                new("env", Config.EnvId.ToString()),
                new("guild", ctx.GuildId),
                new("user", ctx.UserId),
                new("language", (int)ctx.Language)
            });
        }
    }
}
