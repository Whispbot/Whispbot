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
using Whispbot.Tools.Games.ERLCAPI;

namespace Whispbot.Commands.ERLC
{
    public class ERLC_Player : Command
    {
        public override string Name => "ER:LC Player";
        public override string Description => "View information about a currently ingame player";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["erlc", "player"];
        public override List<SlashCommandArg>? Arguments => [
            new ("user", "The Roblox user to look up.", CommandArgType.RobloxUser),
            new ("server", "The ERLC server to check. If not provided, the default will be used.", CommandArgType.ERLCServer, optional: true)
        ];
        public override List<string> Schema => ["<user:string>", "<server:erlcserver?>"];
        public override List<string> Aliases => ["player", "erlc player"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.args.Count < 1)
            {
                await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("erlc.player.errors.nouser")}");
                return;
            }

            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.ERLC)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseERLC)) return;

            List<ERLCServerConfig>? servers = await WhispCache.ERLCServerConfigs.Get(ctx.GuildId);

            if (servers is null || servers.Count == 0)
            {
                await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("erlc.errors.noservers")}");
                return;
            }

            ERLCServerConfig? server = ERLCDatabase.GetServerFromString(servers, ctx.args.Get("server")?.GetString() ?? "");

            if (server is null)
            {
                await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("erlc.errors.notfound")}");
                return;
            }

            string? playerData = await Commands.ERLCCommandUtils.GetUserFromPartialName(ctx.args.Get("user")?.GetString() ?? "", server);

            if (playerData is null)
            {
                await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("erlc.player.errors.notfound")}");
                return;
            }

            var data = await ERLCAPI.GetERLCServer(ctx, server);
            if (data is null) return;

            var player = data.Server?.Players?.Find(p => p.Player == playerData);
            // We can just return because GetUserFromPartialName does the same search
            // this is just a precaution to prevent null reference exceptions etc
            if (player is null) return;

            string username = playerData.Split(':')[0];
            string userId = playerData.Split(':')[1];

            var vehicle = data.Server?.Vehicles?.Find(v => v.Owner == username);

            UserConfig? cachedUserConfig = WhispCache.UserConfig.Find((uc, _) => uc.roblox_id.ToString() == userId);
            UserConfig? userConfig =
                cachedUserConfig
                ?? Postgres.SelectFirst<UserConfig>("SELECT * FROM user_config WHERE roblox_id = @1", [long.Parse(userId)]);

            if (userConfig is not null && cachedUserConfig is null)
            {
                WhispCache.UserConfig.Insert(userConfig.id, userConfig);
            }

            SocketGuildUser? discordMember = userConfig is not null && ctx.Guild is not null ? ctx.Guild.GetUser(userConfig.id) : null;

            StringBuilder badges = new();
            switch (player.Permission)
            {
                case "Server Owner":
                    badges.Append(ctx.Emoji("owner"));
                    break;
                case "Server Co-Owner":
                    badges.Append(ctx.Emoji("coowner"));
                    break;
                case "Server Administrator":
                    badges.Append(ctx.Emoji("administrator"));
                    break;
                case "Server Moderator":
                    badges.Append(ctx.Emoji("moderator"));
                    break;
                case "Server Helper":
                    badges.Append(ctx.Emoji("helper"));
                    break;
            }

            await ctx.EditResponse(
                text: "",
                embed: new EmbedBuilder()
                    .WithTitle(ctx.String("erlc.player.title"))
                    .WithThumbnailUrl(await Roblox.GetUserAvatar(playerData.Split(':')[1]))
                    .WithDescription(
                        $"{(badges.Length > 0 ? badges.ToString() + " " : "")}" + // Emojis representing badges
                        $"**@{username}** ({userId})" // @YellowMacaroni (1231233)
                    )
                    .WithFields(
                        [
                            ..(discordMember is not null ? 
                                new List<EmbedFieldBuilder>() { 
                                    new EmbedFieldBuilder()
                                        .WithName(ctx.String("erlc.player.fields.discord"))
                                        .WithValue(
											$"{ctx.Emoji("indiscord")}" +
											$"{(discordMember.PremiumSince is not null ? ctx.Emoji("booster") : "")} " +
											$"<@{discordMember.Id}> ({discordMember.Id})"
										)
                               } : []),

                            new EmbedFieldBuilder()
                                .WithName(ctx.String("erlc.player.fields.location"))
                                .WithValue(ctx.String(
                                    "erlc.player.location",
                                    player.Location.BuildingNumber,
                                    player.Location.StreetName,
                                    player.Location.PostalCode
                                )),

							..(vehicle is not null ?
                                new List<EmbedFieldBuilder>() {
									new EmbedFieldBuilder()
										.WithName(ctx.String("erlc.player.fields.vehicle"))
										.WithValue(ctx.String(
											"erlc.player.vehicle",
                                            vehicle.Name,
                                            vehicle.Plate,
                                            vehicle.Texture ?? ctx.String("erlc.player.vehicle.no_texture"),
                                            vehicle.ColorName,
                                            vehicle.ColorHex
										))
							   } : [])
						]
                    )
                    .WithFooter(ERLCCache.GenerateFooter(ctx, data))
                    .Build()
            );
        }
    }
}

