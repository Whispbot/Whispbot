using Discord;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Tools;

namespace Whispbot.Commands.General
{
    public class Ping: Command
    {
        public override string Name => "Ping";
        public override string Description => "Check the status of the bot.";
        public override Module Module => Module.General;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["ping"];
        public override List<SlashCommandArg>? Arguments => null;
        public override List<string> Schema => [];
        public override List<string> Aliases => ["ping"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            TimeSpan uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();

            await ctx.Reply(
                embed: new EmbedBuilder()
                    .WithTitle(ctx.String("ping.title"))
                    .WithFields(
                        new EmbedFieldBuilder() { Name = ctx.String("ping.field.ping"), Value = $"{ctx.client.Latency}ms", IsInline = true },
                        new EmbedFieldBuilder() { Name = ctx.String("ping.field.database"), Value = $"{(Postgres.IsConnected() ? $"{ctx.String("ping.field.database.connected")} ({Math.Floor(Postgres.Ping)}ms)" : $"{ctx.String("ping.field.database.disconnected")}")}", IsInline = true }
                    )
                    .WithFooter($"Cluster {Config.cluster} • {Time.ConvertMillisecondsToString(uptime.TotalMilliseconds, Small: true, RoundTo: 60_000, language: ctx.Language)}")
                    .Build()
            );
        }
    }
}
