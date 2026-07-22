using Discord;
using Discord.WebSocket;
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
using Whispbot.Cache;
using Whispbot.Databases;
using Whispbot.Tools;
using Whispbot.Tools.Disc;
using Whispbot.Tools.Games.ERLCAPI;

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
        public override List<string>? SlashCommand => ["erlc", "players"];
        public override List<SlashCommandArg>? Arguments => [
            new ("server", "The ERLC server to check. If not provided, the default will be used.", CommandArgType.ERLCServer, optional: true)
        ];
        public override List<string> Schema => ["<server:erlcserver?>"];
        public override List<string> Aliases => ["players", "ingame", "erlc players"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.ERLC)) return;

            ERLCServerConfig? server = await ERLCDatabase.TryGetServer(ctx);
            if (server is null) return;

            var response = await ERLCAPI.GetERLCServer(ctx, server);
            if (response is null) return;
            var players = response?.Server?.Players;

            if (players is not null)
            {
                List<ulong> playerIds = [.. players.Select(p => ulong.Parse(p.Player.Split(":")[1]))];
                List<UserConfig> userConfigs = await Users.GetConfigsFromRobloxIds(playerIds);
                List<SocketGuildUser>? members = await Users.GetMembersFromConfigs(userConfigs, ctx);

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
                            SocketGuildUser? member = members?.FirstOrDefault(m => m.Id == userConfig.id);
                            if (member is not null)
                            {
                                // 2 = booster, 1 = member, 0 = not in server
                                return member.PremiumSince is not null ? 2 : 1;
                            }
                        }
                        return 0;
                    })
                    .ThenBy(p => p.Player)];

                var OWNER = ctx.Emoji("owner");
                var COOWNER = ctx.Emoji("coowner");
                var ADMIN = ctx.Emoji("administrator");
				var MODERATOR = ctx.Emoji("moderator");
				var HELPER = ctx.Emoji("helper");
				var INDISCORD = ctx.Emoji("indiscord");
				var BOOSTER = ctx.Emoji("booster");

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
                            flags.Append(OWNER);
                            break;
                        case "Server Co-Owner":
                            flags.Append(COOWNER);
                            break;
                        case "Server Administrator":
                            flags.Append(ADMIN);
                            break;
                        case "Server Moderator":
                            flags.Append(MODERATOR);
                            break;
                        case "Server Helper":
                            flags.Append(HELPER);
                            break;
                    }

                    UserConfig? userConfig = userConfigs?.FirstOrDefault(uc => uc.roblox_id.ToString() == id);
                    if (userConfig is not null)
                    {
                        SocketGuildUser? member = members?.FirstOrDefault(m => m.Id == userConfig.id);
                        if (member is not null)
                        {
                            flags.Append(INDISCORD);

                            if (member.PremiumSince is not null) flags.Append(BOOSTER);
                        }
                    }
                    
                    team.AppendLine($"{flags}{(flags.Length > 0 ? " " : "")}{(player.Callsign is not null ? $"[{player.Callsign}] " : "")}**@{name}** ({id})");
                }

                await ctx.EditResponse(
                    text: "",
                    embed: new EmbedBuilder()
                        .WithTitle($"{ctx.String("erlc.players.title")} [{players.Count}/{response!.Server!.MaxPlayers}]")
                        .WithDescription(teams.Count == 0 ? $"{ctx.String("erlc.players.errors.none")}" : null)
                        .WithFields(teams.Select((kvp) => new EmbedFieldBuilder() { Name = $"{kvp.Key} [{players.Sum(p => p.Team == kvp.Key ? 1 : 0)}]", Value = kvp.Value.ToString(), IsInline = false }))
                        .WithFooter(ERLCCache.GenerateFooter(ctx, response!))
                        .Build()
                );
            }
            else
            {
                await ctx.EditResponse(response.GenerateErrorMessage(ctx));
            }
        }
    }
}

