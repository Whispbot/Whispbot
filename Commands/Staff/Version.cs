using System;
using System.Collections.Generic;
using System.Text;
using Whispbot.Databases;

namespace Whispbot.Commands.Staff
{
    public class GuildVersion : Command
    {
        public override string Name => "Version";
        public override string Description => "Change / view the version for this guild.";
        public override Module Module => Module.Staff;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => null;
        public override List<SlashCommandArg>? Arguments => null;
        public override List<string> Schema => ["<new:string?>"];
        public override List<string> Aliases => ["version", "ver"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            var newVersion = ctx.args.Get("new")?.GetString();

            if (newVersion is null)
            {
                await ctx.Reply($"The version for this guild is '{ctx.GuildConfig?.version}', this bot is '{Config.EnvId}'.");
            }
            else
            {
                EnvironmentType? ver = newVersion switch
                {
                    "prod" => EnvironmentType.Prod,
                    "beta" => EnvironmentType.Beta,
                    "dev" => EnvironmentType.Dev,
                    _ => null
                };

                if (ver is null)
                {
                    await ctx.Reply($"Invalid version '{newVersion}'. Valid versions are 'prod', 'beta', and 'dev'.");
                    return;
                }

                ctx.GuildConfig!.version = ver.Value;

                Postgres.Execute("UPDATE guild_config SET version = @1 WHERE id = @2", [(int)ver.Value, ctx.GuildId]);

                await ctx.Reply($"Version updated to '{newVersion}'.");
            }
        }
    }
}
