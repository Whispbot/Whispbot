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
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.ERLCCommands
{
    public class ERLC_Player : Command
    {
        public override string Name => "ER:LC Player";
        public override string Description => "View information about a currently ingame player";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["player", "erlc player"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.User?.id is null) return;

            if (ctx.GuildId is null) // Make sure ran in server
            {
                await ctx.Reply("{emoji.cross} {string.errors.general.guildonly}.");
                return;
            }

            if (ctx.args.Count < 1)
            {
                await ctx.Reply("{emoji.cross} {string.errors.erlcplayer.nouser}");
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

            ERLCServerConfig? server = Tools.ERLC.GetServerFromString(servers, ctx.args.Skip(1).Join(" "));

            if (server is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.erlcserver.notfound}");
                return;
            }

            string? playerData = await Commands.ERLCCommandUtils.GetUserFromPartialName(ctx.args[0], server);

            if (playerData is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.erlcplayer.notfound}");
                return;
            }

            var data = await ERLC.GetServerDataV2(ctx, server);
            if (data is null) return;

            var player = data.Data?.Players?.Find(p => p.Player == playerData);
            // We can just return because GetUserFromPartialName does the same search
            // this is just a precaution to prevent null reference exceptions etc
            if (player is null) return;

            string username = playerData.Split(':')[0];
            string userId = playerData.Split(':')[1];

            var vehicle = data.Data?.Vehicles?.Find(v => v.Owner == username);

            UserConfig? cachedUserConfig = WhispCache.UserConfig.Find((uc, _) => uc.roblox_id.ToString() == userId);
            UserConfig? userConfig =
                cachedUserConfig
                ?? Postgres.SelectFirst<UserConfig>("SELECT * FROM user_config WHERE roblox_id = @1", [long.Parse(userId)]);

            if (userConfig is not null && cachedUserConfig is null)
            {
                WhispCache.UserConfig.Insert(userConfig.id.ToString(), userConfig);
            }

            Member? discordMember = userConfig is not null && ctx.Guild is not null ? 
                await ctx.Guild.members.Get(userConfig.id.ToString()) : null;

            StringBuilder badges = new();
            switch (player.Permission)
            {
                case "Server Owner":
                    badges.Append("{emoji.owner}");
                    break;
                case "Server Co-Owner":
                    badges.Append("{emoji.coowner}");
                    break;
                case "Server Administrator":
                    badges.Append("{emoji.administrator}");
                    break;
                case "Server Moderator":
                    badges.Append("{emoji.moderator}");
                    break;
                case "Server Helper":
                    badges.Append("{emoji.helper}");
                    break;
            }

            await ctx.EditResponse(new MessageBuilder
            {
                embeds = [
                    new EmbedBuilder
                    {
                        title = "{string.title.erlcplayer}",
                        thumbnail = new EmbedThumbnail
                        {
                            url = await Roblox.GetUserAvatar(playerData.Split(":")[1])
                        },
                        description = 
                            $"{(badges.Length > 0 ? badges.ToString() + " " : "")}" + // Emojis representing badges
                            $"**@{username}** ({userId})", // @YellowMacaroni (1231233)
                        fields = [
                           ..(discordMember is not null ? 
                           new List<EmbedField>() { 
                               new() 
                               {
                                   name = "{string.title.erlcplayer.discord}",
                                   value = 
                                    $"{{emoji.indiscord}}" +
                                    $"{(discordMember.premium_since is not null ? "{emoji.booster}" : "")} " +
                                    $"<@{discordMember.user?.id}> ({discordMember.user?.id})"
                               }
                           } : []),
                           new EmbedField
                           {
                                name = "{string.title.erlcplayer.location}",
                                value = $"{{string.content.erlcplayer.location:postal={player.Location.PostalCode},street={player.Location.StreetName}}}"
                           },
                           ..(vehicle is not null ?
                           new List<EmbedField>() {
                               new()
                               {
                                   name = "{string.title.erlcplayer.vehicle}",
                                   value = 
                                    $"**{{string.title.erlcplayer.vehiclename}}**: {vehicle.Name}\n" +
                                    $"**{{string.title.erlcplayer.vehicletexture}}**: {vehicle.Texture ?? "{string.general.none}"}\n" +
                                    $"**{{string.title.erlcplayer.vehiclecolor}}**: {vehicle.ColorName} ({vehicle.ColorHex.ToUpper()})"
                               }
                           } : [])
                        ],
                        footer = new EmbedFooter
                        {
                            text = ERLC.GenerateFooter(data)
                        }
                    }
                ]
            });
        }
    }
}
