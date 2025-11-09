using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Tools;
using YellowMacaroni.Discord.Core;

namespace Whispbot.Commands.General
{
    public class Ping: Command
    {
        public override string Name => "Ping";
        public override string Description => "Check the status of the bot.";
        public override Module Module => Module.General;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["ping"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            await ctx.Reply(
                new MessageBuilder
                {
                    embeds = [
                        new EmbedBuilder()
                        .SetTitle("Pong!")
                        .AddField(
                            new EmbedField { name = "Ping", value = $"{Math.Floor(ctx.client.ping)}ms", inline = true },
                            new EmbedField { name = "Database", value = $"{(Postgres.IsConnected() ? $"Connected ({Math.Floor(Postgres.Ping)}ms)" : "Disconnected")}", inline = true }
                        )
                        .SetFooter($"Shard {ctx.client.shard?.id} • {Time.ConvertMillisecondsToRelativeString(ctx.client.startupTime.ToUnixTimeMilliseconds(), true, ", ", false, 60000)}")
                    ]
                }
            );
        }
    }
}
