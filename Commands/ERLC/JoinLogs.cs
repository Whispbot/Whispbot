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
    public class ERLC_JoinLogs : Command
    {
        public override string Name => "ER:LC Join Logs";
        public override string Description => "View the players who have recently joined / left.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["joinlogs", "erlc joinlogs", "erlc joins", "erlc joinlog"];
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

            var response = await ERLC.GetEndpointData<List<ERLC.PRC_JoinLog>>(ctx, server, ERLC.Endpoint.ServerJoinlogs);
            var joinlogs = response?.data;

            if (joinlogs is not null)
            {
                if (joinlogs.Count == 0)
                {
                    await ctx.EditResponse($"{{emoji.cross}} {{string.errors.erlcjoinlogs.nojoins}}\n-# {{string.content.erlcserver.updated}}: {(response!.cachedAt is not null ? $"{Math.Round((decimal)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - response.cachedAt) / 1000)}s ago" : "{string.content.erlcserver.justnow}")}");
                    return;
                }

                joinlogs.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
                joinlogs = [..joinlogs.Take(20)];

                List<long> robloxIds = [..joinlogs.Select(j => long.Parse(j.Player.Split(":")[1]))];
                robloxIds = [.. robloxIds.Distinct()];
                List<UserConfig> userConfigs = await Users.GetConfigsFromRobloxIds(robloxIds);
                List<Member>? members = await Users.GetMembersFromConfigs(userConfigs, ctx);

                StringBuilder strings = new();
                foreach (var log in joinlogs)
                {
                    UserConfig? config = userConfigs?.Find(u => u.roblox_id.ToString() == log.Player.Split(":")[1]);
                    Member? member = members?.Find(m => m.user?.id == config?.id.ToString());

                    StringBuilder flags = new();
                    if (member is not null)
                    {
                        flags.Append("{emoji.indiscord}");
                        if (member.premium_since is not null) flags.Append("{emoji.booster}");
                    }

                    strings.AppendLine($"{(log.Join ? "{emoji.clockedin}" : "{emoji.clockedout}")} [<t:{log.Timestamp}:T>] {flags}{(flags.Length > 0 ? " " : "")}**@{log.Player.Split(":")[0]}** {(log.Join ? "{string.content.erlcjoinlogs.joined}" : "{string.content.erlcjoinlogs.left}")}");
                }

                await ctx.EditResponse(
                    new MessageBuilder
                    {
                        content = "",
                        embeds = [
                            new EmbedBuilder
                            {
                                title = $"{{string.title.joinlogs}}",
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
