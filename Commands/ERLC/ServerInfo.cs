using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Tools;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.ERLCCommands
{
    public class ERLC_ServerInfo: Command
    {
        public override string Name => "ER:LC Server Info";
        public override string Description => "Get information about an ER:LC server.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["erlcserver", "erlcinfo", "eserver", "eserverinfo", "erlc server"];
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

            var response = await ERLC.GetEndpointData<ERLC.PRC_Server>(ctx, server, ERLC.Endpoint.ServerInfo);
            var serverInfo = response?.data;

            if (serverInfo is not null)
            {
                List<string> userIds = [..serverInfo.coOwnerIds.Select(u=>u.ToString()), serverInfo.ownerId.ToString()];
                List<Roblox.RobloxUser>? relatedUsers = await Roblox.GetUserById(userIds);
                Roblox.RobloxUser? owner = relatedUsers?.Find(u => u.id == serverInfo.ownerId.ToString());
                List<Roblox.RobloxUser> coOwners = relatedUsers?.FindAll(u => serverInfo.coOwnerIds.Contains(long.Parse(u.id))) ?? [];

                await ctx.EditResponse(
                    new MessageBuilder
                    {
                        content = "",
                        embeds = [
                            new EmbedBuilder
                            {
                                title = "{string.title.erlcserver}",
                                thumbnail = new EmbedThumbnail
                                {
                                    url = ctx.Guild?.icon_url
                                },
                                description = "{string.content.erlcserver}".Process(ctx.Language, new() {
                                    { "name", serverInfo.name },
                                    { "owner", $"[@{owner?.name ?? "unknown"}](https://roblox.com/users/{serverInfo.ownerId})" },
                                    { "joinkey", $"[{serverInfo.joinKey}](https://policeroleplay.community/join/{serverInfo.joinKey})" },
                                    { "current", serverInfo.currentPlayers.ToString() },
                                    { "max", serverInfo.maxPlayers.ToString() }
                                }),
                                fields = coOwners.Count > 0 ? [
                                    new EmbedField
                                    {
                                        name = "{string.fields.erlcserver.coowners}",
                                        value = coOwners.Select(u => $"{{emoji.inline}} [@{u.name}](https://roblox.com/users/{u.id})").Join("\n"),
                                        inline = false
                                    }
                                ] : []
                            }
                        ]
                    }
                );
            }
            else
            {
                await ctx.EditResponse($"{{emoji.cross}} [{response?.code} ]  {response?.message ?? "An unknown error occured"}.");
            }
        }
    }
}
