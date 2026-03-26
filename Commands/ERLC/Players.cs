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
        public override List<string>? SlashCommand => ["erlc", "players"];
        public override List<SlashCommandArg>? Arguments => [
            new ("server", "The ERLC server to check. If not provided, the default will be used.", CommandArgType.ERLCServer, optional: true)
        ];
        public override List<string> Schema => ["<server:erlcserver?>"];
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

            var response = await ERLC.GetServerDataV2(ctx, server);
            if (response is null) return;
            var players = response?.Data?.Players;

            if (players is not null)
            {
                List<long> playerIds = [.. players.Select(p => long.Parse(p.Player.Split(":")[1]))];
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

                players = [.. players.OrderByDescending(p => roles.GetValueOrDefault(p.Permission, 0))
                    .ThenByDescending(p => {
                        string playerId = p.Player.Split(':').Length > 1 ? p.Player.Split(':')[1] : "N/A";
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
                    .ThenBy(p => p.Player)];

                foreach (var player in players)
                {
                    StringBuilder? team = teams.GetValueOrDefault(player.Team);
                    if (team is null)
                    {
                        team = new StringBuilder();
                        teams[player.Team] = team;
                    }

                    string[] split = player.Player.Split(':');
                    string name = split[0];
                    string id = split.Length > 1 ? split[1] : "N/A";

                    StringBuilder flags = new();

                    switch (player.Permission)
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
                    
                    team.AppendLine($"{flags}{(flags.Length > 0 ? " " : "")}{(player.Callsign is not null ? $"[{player.Callsign}] " : "")}**@{name}** ({id})");
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
                                fields = [.. teams.ForAll((kvp) => new EmbedField() { name = $"{kvp.Key} [{players.Sum(p=> p.Team == kvp.Key ? 1 : 0 )}]", value = kvp.Value.ToString(), inline = false })],
                                footer = new EmbedFooter { text = ERLC.GenerateFooter(response!) }
                            }
                        ]
                    }
                );
            }
            else
            {
                await ctx.EditResponse($"{{emoji.cross}} [{response?.Code}] {response?.Message ?? "An unknown error occured"}.");
            }
        }
    }
}

