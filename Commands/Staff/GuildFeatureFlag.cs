using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Tools.Bot;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;
using static Whispbot.Tools.Bot.FeatureFlags;

namespace Whispbot.Commands.Staff
{
    public class GuildFeatureFlags : Command
    {
        public override string Name => "Guild Feature Flags";
        public override string Description => "View or toggle feature flags for a guild";

        public override Module Module => Module.Staff;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["gff", "guildfeatureflags", "guildff"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.args.Count == 0)
            {
                List<FeatureFlag>? flags = Postgres.Select<FeatureFlag>(@"SELECT * FROM feature_flags WHERE affects = TRUE AND type = TRUE");
                if (flags is null || flags.Count == 0)
                {
                    await ctx.Reply("No guild feature flags found.");
                    return;
                }

                StringBuilder sb = new();
                sb.AppendLine("**Guild Manual Flags:**");
                foreach (var flag in flags)
                {
                    sb.AppendLine($"{(flag.enabled ? "{emoji.clockedin}" : "{emoji.clockedout}")} {flag.name}: {flag.description}");
                }

                await ctx.Reply(sb.ToString());
                return;
            }
            else if (ctx.args.Count == 1)
            {
                string guildId = ctx.args[0];
                List<FeatureFlag>? flags = Postgres.Select<FeatureFlag>(@"SELECT * FROM feature_flags ff LEFT JOIN guild_feature_flags gff ON gff.feature_flag_id = ff.id WHERE gff.guild_id = @1", [long.Parse(guildId)]);
                if (flags is null || flags.Count == 0)
                {
                    await ctx.Reply("No guild feature flags found for this server.");
                    return;
                }

                StringBuilder sb = new();
                sb.AppendLine("**Enabled Flags:**");
                foreach (var flag in flags)
                {
                    sb.AppendLine($"{(flag.enabled ? "{emoji.clockedin}" : "{emoji.clockedout}")} {flag.name}");
                }

                await ctx.Reply(sb.ToString());
                return;
            }
            else if (ctx.args.Count == 2)
            {
                string guildId = ctx.args[0];
                string flagName = ctx.args[1];
                FeatureFlagUpdate? flag = Postgres.SelectFirst<FeatureFlagUpdate>("SELECT toggle_guild_feature_flag(@1, (SELECT id FROM feature_flags WHERE name = @2 AND affects = TRUE and type = TRUE)) as status;", [long.Parse(guildId), flagName]);

                if (flag is null)
                {
                    await ctx.Reply("Failed to toggle feature flag. Make sure the flag exists and affects guilds.");
                    return;
                }

                Guild? guild = await DiscordCache.Guilds.Get(guildId);

                await ctx.Reply($"Feature flag `{flagName}` {(flag.status == 1 ? "enabled" : "disabled")} for `{guild?.name ?? "unknown guild"}`.");
            }
        }
    }
}
