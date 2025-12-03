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



            await ctx.Reply(new MessageBuilder
            {
                embeds = [
                    new EmbedBuilder
                    {
                        title = "{string.title.erlcplayer}",
                        thumbnail = new EmbedThumbnail
                        {
                            url = await Roblox.GetUserAvatar(playerData.Split(":")[1])
                        }
                    }
                ]
            });
        }
    }
}
