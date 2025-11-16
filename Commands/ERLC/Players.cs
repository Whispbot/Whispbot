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
    public class ERLC_Players: Command
    {
        public override string Name => "ER:LC Players";
        public override string Description => "Get the currently in-game players.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [
            new RateLimit()
            {
                type = RateLimitType.User
            }
        ];
        public override List<string> Aliases => ["players", "ingame", "erlc players"];
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

            var response = Tools.ERLC.CheckCache(Tools.ERLC.Endpoint.ServerPlayers, server.DecryptedApiKey);

            if (response is null)
            {
                await ctx.Reply("{emoji.loading} {string.content.erlcplayers.fetching}...");
                response = await Tools.ERLC.GetPlayers(server);

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

            List<Tools.ERLC.PRC_Player>? players = string.IsNullOrWhiteSpace(response.data?.ToString()) ? [] : JsonConvert.DeserializeObject<List<Tools.ERLC.PRC_Player>>(response.data.ToString()!);

            if (players is not null)
            {
                List<long> playerIds = [.. players.Select(p => long.Parse(p.player.Split(":")[1]))];
                List<UserConfig>? userConfigs = WhispCache.UserConfig.FindMany((u, _) => playerIds.Contains(u.id));
                List<long> missingIds = [.. playerIds.Where(id => !userConfigs.Any(u => u.id == id))];
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

                Dictionary<string, StringBuilder> teams = [];

                Dictionary<string, int> roles = new()
                {
                    { "Server Owner", 5 },
                    { "Server Co-Owner", 4 },
                    { "Server Administrator", 3 },
                    { "Server Moderator", 2 },
                    { "Server Helper", 1 }
                };

                players = [.. players.OrderByDescending(p => roles.GetValueOrDefault(p.permission, 0))
                    .ThenByDescending(p => {
                        string playerId = p.player.Split(':').Length > 1 ? p.player.Split(':')[1] : "N/A";
                        UserConfig? userConfig = userConfigs?.FirstOrDefault(uc => uc.roblox_id.ToString() == playerId);
                        if (userConfig is not null)
                        {
                            Member? member = members?.FirstOrDefault(m => m.user?.id == userConfig.id.ToString());
                            if (member is not null)
                            {
                                // 2 = booster, 1 = member, 0 = not in server
                                return member.premium_since is not null ? 2 : 1;
                            }
                        }
                        return 0;
                    })
                    .ThenBy(p => p.player)];

                foreach (var player in players)
                {
                    StringBuilder? team = teams.GetValueOrDefault(player.team);
                    if (team is null)
                    {
                        team = new StringBuilder();
                        teams[player.team] = team;
                    }

                    string[] split = player.player.Split(':');
                    string name = split[0];
                    string id = split.Length > 1 ? split[1] : "N/A";

                    StringBuilder flags = new();

                    switch (player.permission)
                    {
                        case "Server Owner":
                            flags.Append("{emoji.owner}");
                            break;
                        case "Server Co-Owner":
                            flags.Append("{emoji.coowner}");
                            break;
                        case "Server Administrator":
                            flags.Append("{emoji.administrator}");
                            break;
                        case "Server Moderator":
                            flags.Append("{emoji.moderator}");
                            break;
                        case "Server Helper":
                            flags.Append("{emoji.helper}");
                            break;
                    }

                    UserConfig? userConfig = userConfigs?.FirstOrDefault(uc => uc.roblox_id.ToString() == id);
                    if (userConfig is not null)
                    {
                        Member? member = members?.FirstOrDefault(m => m.user?.id == userConfig.id.ToString());
                        if (member is not null)
                        {
                            flags.Append("{emoji.indiscord}");

                            if (member.premium_since is not null) flags.Append("{emoji.booster}");
                        }
                        
                    }
                    
                    team.AppendLine($"{flags}{(flags.Length > 0 ? " " : "")}{(player.callsign is not null ? $"[{player.callsign}] " : "")}**@{name}** ({id})");
                }

                await ctx.EditResponse(
                    new MessageBuilder()
                    {
                        content = "",
                        embeds = [
                            new EmbedBuilder
                            {
                                title = $"{{string.title.erlcserver.players}} [{players.Count}]",
                                description = teams.Count == 0 ? "{string.errors.erlcserver.empty}" : null,
                                fields = [.. teams.ForAll((kvp) => new EmbedField() { name = $"{kvp.Key} [{players.Sum(p=> p.team == kvp.Key ? 1 : 0 )}]", value = kvp.Value.ToString(), inline = false })],
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
