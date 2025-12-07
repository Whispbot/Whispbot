using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Tools;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.ERLCCommands
{
    public class ERLC_KillLogs : Command
    {
        public override string Name => "ER:LC Kill Logs";
        public override string Description => "View the players who have recently been killed.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["killlogs", "erlc killlogs", "erlc kills", "erlc killlog"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.User?.id is null) return;

            if (ctx.GuildId is null) // Make sure ran in server
            {
                await ctx.Reply("{emoji.cross} {string.errors.general.guildonly}.");
                return;
            }

            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.ERLC)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseERLC)) return;

            ERLCServerConfig? server = await ERLC.TryGetServer(ctx);
            if (server is null) return;

            var response = await ERLC.GetEndpointData<List<ERLC.PRC_KillLog>>(ctx, server, ERLC.Endpoint.ServerKilllogs);
            var killLogs = response?.data;

            if (killLogs is not null)
            {
                if (killLogs.Count == 0)
                {
                    await ctx.EditResponse($"{{emoji.cross}} {{string.errors.erlckilllogs.nokills}}\n-# {{string.content.erlcserver.updated}}: {(response!.cachedAt is not null ? $"{Math.Round((decimal)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - response.cachedAt) / 1000)}s ago" : "{string.content.erlcserver.justnow}")}");
                    return;
                }

                killLogs.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
                killLogs = [.. killLogs.Take(20)];

                List<long> robloxIds = [.. killLogs.Select(j => long.Parse(j.Killed.Split(":")[1])), .. killLogs.Select(k => long.Parse(k.Killer.Split(":")[1]))];
                robloxIds = [..robloxIds.Distinct()];
                List<UserConfig> userConfigs = await Users.GetConfigsFromRobloxIds(robloxIds);
                List<Member>? members = await Users.GetMembersFromConfigs(userConfigs, ctx);

                StringBuilder strings = new();
                foreach (var log in killLogs)
                {
                    UserConfig? killedConfig = userConfigs?.Find(u => u.roblox_id.ToString() == log.Killed.Split(":")[1]);
                    Member? killedMember = members?.Find(m => m.user?.id == killedConfig?.id.ToString());

                    UserConfig? killerConfig = userConfigs?.Find(u => u.roblox_id.ToString() == log.Killer.Split(":")[1]);
                    Member? killerMember = members?.Find(m => m.user?.id == killerConfig?.id.ToString());

                    StringBuilder killedFlags = new();
                    if (killedMember is not null)
                    {
                        killedFlags.Append("{emoji.indiscord}");
                        if (killedMember.premium_since is not null) killedFlags.Append("{emoji.booster}");
                    }

                    StringBuilder killerFlags = new();
                    if (killerMember is not null)
                    {
                        killerFlags.Append("{emoji.indiscord}");
                        if (killerMember.premium_since is not null) killerFlags.Append("{emoji.booster}");
                    }

                    strings.AppendLine($"{killerFlags}{(killerFlags.Length > 0 ? " " : "")}**@{log.Killer.Split(":")[0]}** killed {killedFlags}{(killedFlags.Length > 0 ? " " : "")}**@{log.Killed.Split(":")[0]}**");
                }

                await ctx.EditResponse(
                    new MessageBuilder
                    {
                        content = "",
                        embeds = [
                            new EmbedBuilder
                            {
                                title = $"{{string.title.killlogs}}",
                                description = strings.ToString(),
                                footer = new EmbedFooter { text = $"{{string.content.erlcserver.updated}}: {(response!.cachedAt is not null ? $"{Math.Round((decimal)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - response.cachedAt)/1000)}s ago" : "{string.content.erlcserver.justnow}")}" }
                            }
                        ]
                    }
                );
            }
            else
            {
                await ctx.EditResponse($"{{emoji.cross}} [{response!.code}] {response.message ?? "An unknown error occured"}.");
            }
        }
    }
}
