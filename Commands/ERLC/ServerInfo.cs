using Discord;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Languages;
using Whispbot.Tools;
using Whispbot.Tools.Games.ERLCAPI;

namespace Whispbot.Commands.ERLC
{
    public class ERLC_ServerInfo: Command
    {
        public override string Name => "ER:LC Server Info";
        public override string Description => "Get information about an ER:LC server.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["erlc", "server"];
        public override List<SlashCommandArg>? Arguments => [
            new ("server", "The ERLC server to get info on. If not provided, the default will be used.", CommandArgType.ERLCServer, optional: true)
        ];
        public override List<string> Schema => ["<server:erlcserver?>"];
        public override List<string> Aliases => ["erlcserver", "erlcinfo", "eserver", "eserverinfo", "erlc server", "erlc info"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.ERLC)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseERLC)) return;

            ERLCServerConfig? server = await ERLCDatabase.TryGetServer(ctx);
            if (server is null) return;

            var response = await ERLCAPI.GetERLCServer(ctx, server);
            if (response is null) return;
            var serverInfo = response?.Server;

            if (serverInfo is not null)
            {
                List<string> userIds = [..serverInfo.CoOwnerIds.Select(u=>u.ToString()), serverInfo.OwnerId.ToString()];
                List<Roblox.RobloxUser>? relatedUsers = await Roblox.GetUserById(userIds);
                Roblox.RobloxUser? owner = relatedUsers?.Find(u => u.id == serverInfo.OwnerId.ToString());
                List<Roblox.RobloxUser> coOwners = relatedUsers?.FindAll(u => serverInfo.CoOwnerIds.Contains(ulong.Parse(u.id))) ?? [];

                var INLINE = ctx.Emoji("alignment");

                await ctx.EditResponse(
                    text: "",
                    embed: new EmbedBuilder()
                        .WithTitle(ctx.String("erlc.serverinfo.title"))
                        .WithThumbnailUrl(ctx.Guild.IconUrl)
                        .WithDescription(ctx.String("erlc.serverinfo.data", 
                            serverInfo.Name,
                            $"[@{owner?.name ?? "unknown"}](https://roblox.com/users/{serverInfo.OwnerId})",
							$"[{serverInfo.JoinKey}](https://policeroleplay.community/join/{serverInfo.JoinKey})",
							serverInfo.CurrentPlayers.ToString(),
							serverInfo.MaxPlayers.ToString()
						))
                        .WithFields(coOwners.Count > 0 ? [
                            new EmbedFieldBuilder() {
                                Name = ctx.String("erlc.serverinfo.coowners"),
								Value = coOwners.Select(u => $"{INLINE} [@{u.name}](https://roblox.com/users/{u.id})").Join("\n"),
								IsInline = false
							}
                        ] : [])
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

