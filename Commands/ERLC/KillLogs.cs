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

namespace Whispbot.Commands.ERLC
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

            List<ERLCServerConfig>? servers = await WhispCache.ERLCServerConfigs.Get(ctx.GuildId);

            if (servers is null || servers.Count == 0)
            {
                await ctx.Reply("{emoji.cross} {string.errors.erlcserver.notfound}");
                return;
            }

            ERLCServerConfig? server = Tools.ERLC.GetServerFromString(servers, ctx.args.Join(" "));

            if (server is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.erlcserver.notfound}");
                return;
            }

            if (server.api_key is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.erlcserver.nokey}");
                return;
            }

            var response = Tools.ERLC.CheckCache(Tools.ERLC.Endpoint.ServerKilllogs, server.DecryptedApiKey);

            if (response is null)
            {
                await ctx.Reply("{emoji.loading} {string.content.erlckilllogs.fetching}...");
                response = await Tools.ERLC.GetKills(server);

                if (response is null)
                {
                    await ctx.EditResponse("{emoji.cross} {string.errors.erlcserver.apierror}");
                    return;
                }
            }

            if (Tools.ERLC.ResponseHasError(response, out var errorMessage))
            {
                await ctx.EditResponse(errorMessage!);
                return;
            }

            List<Tools.ERLC.PRC_KillLog>? killLogs = JsonConvert.DeserializeObject<List<Tools.ERLC.PRC_KillLog>>(response.data?.ToString() ?? "[]");

            if (killLogs is not null)
            {
                if (killLogs.Count == 0)
                {
                    await ctx.EditResponse($"{{emoji.cross}} {{string.errors.erlckilllogs.nokills}}\n-# {{string.content.erlcserver.updated}}: {(response.cachedAt is not null ? $"{Math.Round((decimal)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - response.cachedAt) / 1000)}s ago" : "{string.content.erlcserver.justnow}")}");
                    return;
                }

                killLogs.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
                killLogs = [.. killLogs.Take(20)];

                List<long> robloxIds = [.. killLogs.Select(j => long.Parse(j.Killed.Split(":")[1])), .. killLogs.Select(k => long.Parse(k.Killer.Split(":")[1]))];
                robloxIds = [..robloxIds.Distinct()];
                List<UserConfig>? userConfigs = WhispCache.UserConfig.FindMany((u, _) => robloxIds.Contains(u.roblox_id ?? 0));
                List<long> missingIds = [.. robloxIds.Where(id => !userConfigs.Any(u => u.roblox_id == id))];
                if (missingIds.Count > 0)
                {
                    List<UserConfig>? fetchedConfigs = Postgres.Select<UserConfig>(
                        @"SELECT * FROM user_config WHERE roblox_id IS NOT NULL AND roblox_id = ANY(@1);",
                        [missingIds]
                    );
                    if (fetchedConfigs is not null && fetchedConfigs.Count > 0)
                    {
                        userConfigs.AddRange(fetchedConfigs);
                        foreach (var config in fetchedConfigs)
                        {
                            WhispCache.UserConfig.Insert(config.id.ToString(), config);
                        }
                    }
                }

                List<Member>? members = null;
                if (userConfigs is not null && userConfigs.Count > 0 && ctx.Guild is not null)
                {
                    members = ctx.Guild.members.FindMany((m, _) => userConfigs.Any(u => u.id.ToString() == m.user?.id));
                    List<string> remainingMembers = [.. userConfigs.Where(u => !members.Any(m => m.user!.id == u.id.ToString())).Select(u => u.id.ToString())];
                    if (remainingMembers.Count > 0)
                    {
                        members.AddRange(await ctx.Guild.GetMembers(ctx.client, [.. userConfigs.Select(u => u.id.ToString())]));
                    }
                }

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
                                footer = new EmbedFooter { text = $"{{string.content.erlcserver.updated}}: {(response.cachedAt is not null ? $"{Math.Round((decimal)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - response.cachedAt)/1000)}s ago" : "{string.content.erlcserver.justnow}")}" }
                            }
                        ]
                    }
                );
            }
            else
            {
                await ctx.EditResponse($"{{emoji.cross}} [{response.code}] {response.message ?? "An unknown error occured"}.");
            }
        }
    }
}
