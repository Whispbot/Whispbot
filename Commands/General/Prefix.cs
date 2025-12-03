using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Tools;
using YellowMacaroni.Discord.Core;

namespace Whispbot.Commands.General
{
    public class Prefix : Command
    {
        public override string Name => "Prefix";
        public override string Description => "View or update the bot's prefix.";
        public override Module Module => Module.General;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["prefix", "pre", "p"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.GuildId is null) return;

            if (ctx.args.Count == 0)
            {
                await ctx.Reply($"{{string.content.prefix:prefix={ctx.GuildConfig?.prefix ?? Config.prefix}}}.");
            }
            else
            {
                string newPrefix = ctx.args[0];

                if (newPrefix.Length > 10)
                {
                    await ctx.Reply("{emoji.cross} {string.errors.prefix.toolong}.");
                    return;
                }

                if (Regex.IsMatch(newPrefix, "[{}]"))
                {
                    await ctx.Reply("{emoji.cross} {string.errors.prefix.invalid}.");
                    return;
                }

                Postgres.Execute("UPDATE guild_config SET prefix = @1 WHERE id = @2", [newPrefix, long.Parse(ctx.GuildId)]);

                await ctx.Reply($"{{emoji.tick}} {{string.success.prefix:prefix={newPrefix}}}.");
            }
        }
    }
}
