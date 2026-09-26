using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Tools;
using Whispbot.Tools.Disc;
using Discord;
using Whispbot.Languages;

namespace Whispbot.Commands.General
{
    public class Prefix : Command
    {
        public override string Name => "Prefix";
        public override string Description => "View or update the bot's prefix.";
        public override Module Module => Module.General;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["prefix"];
        public override List<SlashCommandArg>? Arguments => [
            new("prefix", "The new prefix for the server.", CommandArgType.String, optional: true) { min_length = 1, max_length = 10 }
        ];
        public override List<string> Schema => ["<prefix:string?>"];
        public override List<string> Aliases => ["prefix", "pre", "p"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.args.Count == 0)
            {
                await ctx.Reply(ctx.String("prefix.is", Users.FixUsername(ctx.GuildConfig?.prefix ?? Config.prefix)));
            }
            else
            {
                if (!DiscordPermissions.HasPermissionOrAdmin(ctx.Member, GuildPermission.ManageGuild))
                {
                    await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("prefix.errors.noperms")}");
                    return;
                }

                string newPrefix = ctx.args.Get("prefix")?.GetString() ?? "!";

                if (newPrefix.Length > 10)
                {
                    await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("prefix.errors.toolong")}");
                    return;
                }

                if (Regex.IsMatch(newPrefix, "[{}]"))
                {
                    await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("prefix.errors.invalid")}");
                    return;
                }

                Postgres.Execute("UPDATE guild_config SET prefix = @1 WHERE id = @2", [newPrefix, ctx.GuildId]);

                await ctx.Reply($"{ctx.Emoji("tick")} {ctx.String("prefix.success", Users.FixUsername(newPrefix))}.");
            }
        }
    }
}

