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
using YellowMacaroni.Discord.Websocket.Events;

namespace Whispbot.Commands.ERLCCommands
{
    public class ERLC_ModCalls : Command
    {
        public override string Name => "ER:LC Mod Calls";
        public override string Description => "View the recent mod calls.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["modcalls", "erlc modcalls", "erlc calls"];
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

            var response = await ERLC.GetEndpointData<List<ERLC.PRC_CallLog>>(ctx, server, ERLC.Endpoint.ServerModcalls);
            var callLogs = response?.data;

            if (callLogs is not null)
            {
                if (callLogs.Count == 0)
                {
                    await ctx.EditResponse($"{{emoji.cross}} {{string.errors.erlccalllogs.nologs}}\n-# {{string.content.erlcserver.updated}}: {(response!.cachedAt is not null ? $"{Math.Round((decimal)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - response.cachedAt) / 1000)}s ago" : "{string.content.erlcserver.justnow}")}");
                    return;
                }

                callLogs.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
                callLogs = [.. callLogs.Take(20)];

                List<long> robloxIds = [.. callLogs.Select(j => long.Parse(j.Caller.Split(":")[1])), .. callLogs.Where(c => c.Moderator is not null).Select(c => long.Parse(c.Moderator!.Split(":")[1]))];
                List<UserConfig> userConfigs = await Users.GetConfigsFromRobloxIds(robloxIds);
                List<Member>? members = await Users.GetMembersFromConfigs(userConfigs, ctx);

                StringBuilder strings = new();
                foreach (var log in callLogs)
                {
                    UserConfig? callerConfig = userConfigs?.Find(u => u.roblox_id.ToString() == log.Caller.Split(":")[1]);
                    Member? callerMember = members?.Find(m => m.user?.id == callerConfig?.id.ToString());

                    UserConfig? modConfig = userConfigs?.Find(u => u.roblox_id.ToString() == log.Moderator?.Split(":")?[1]);
                    Member? modMember = members?.Find(m => m.user?.id == modConfig?.id.ToString());

                    StringBuilder callerFlags = new();
                    if (callerMember is not null)
                    {
                        callerFlags.Append("{emoji.indiscord}");
                        if (callerMember.premium_since is not null) callerFlags.Append("{emoji.booster}");
                    }

                    StringBuilder modFlags = new();
                    if (modMember is not null)
                    {
                        modFlags.Append("{emoji.indiscord}");
                        if (modMember.premium_since is not null) modFlags.Append("{emoji.booster}");
                    }

                    if (log.Moderator is not null && log.Moderator.Split(':')[1] != "1")
                    {
                        strings.AppendLine($"{{emoji.clockedin}} [<t:{log.Timestamp}:T>] {modFlags}{(modFlags.Length > 0 ? " " : "")}**@{log.Moderator.Split(':')[0]}** helped {callerFlags}{(callerFlags.Length > 0 ? " " : "")}**@{log.Caller.Split(':')[0]}**");
                    }
                    else
                    {
                        strings.AppendLine($"{{emoji.clockedout}} [<t:{log.Timestamp}:T>] {callerFlags}{(callerFlags.Length > 0 ? " " : "")}**@{log.Caller.Split(':')[0]}** called for mod");
                    }
                }

                await ctx.EditResponse(
                    new MessageBuilder
                    {
                        content = "",
                        embeds = [
                            new EmbedBuilder
                            {
                                title = $"{{string.title.modcalls}}",
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
