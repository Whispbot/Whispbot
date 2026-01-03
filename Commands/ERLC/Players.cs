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

            ERLCServerConfig? server = await ERLC.TryGetServer(ctx);
            if (server is null) return;

            var response = await ERLC.GetEndpointData<List<ERLC.PRC_Player>>(ctx, server, ERLC.Endpoint.ServerPlayers);
            var players = response?.data;

            if (players is not null)
            {
                List<long> playerIds = [.. players.Select(p => long.Parse(p.player.Split(":")[1]))];
                List<UserConfig> userConfigs = await Users.GetConfigsFromRobloxIds(playerIds);
                List<Member>? members = await Users.GetMembersFromConfigs(userConfigs, ctx);

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
                                footer = new EmbedFooter { text = await ERLC.GenerateFooter(response!) }
                            }
                        ]
                    }
                );
            }
            else
            {
                await ctx.EditResponse($"{{emoji.cross}} [{response?.code}] {response?.message ?? "An unknown error occured"}.");
            }
        }
    }
}
