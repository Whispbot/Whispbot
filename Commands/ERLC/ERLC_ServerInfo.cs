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

namespace Whispbot.Commands.ERLC
{
    public class ERLC_ServerInfo: Command
    {
        public override string Name => "ER:LC Server Info";
        public override string Description => "Get information about an ER:LC server.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["erlcserver", "erlcinfo", "eserver", "eserverinfo"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.User?.id is null) return;

            if (ctx.GuildId is null) // Make sure ran in server
            {
                await ctx.Reply("{emoji.cross} {string.errors.general.guildonly}.");
                return;
            }

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

            var response = Tools.ERLC.CheckCache(Tools.ERLC.Endpoint.ServerInfo, server.api_key);

            if (response is null)
            {
                await ctx.Reply("{emoji.loading} {string.content.erlcserver.fetching}...");
                response = await Tools.ERLC.GetServerInfo(server.api_key);

                if (response is null)
                {
                    await ctx.EditResponse("{emoji.cross} {string.errors.erlcserver.apierror}");
                    return;
                }
            }

            Tools.ERLC.PRC_Server? serverInfo = JsonConvert.DeserializeObject<Tools.ERLC.PRC_Server>(response.data?.ToString() ?? "{}");

            if (serverInfo is not null)
            {
                List<string> userIds = [..serverInfo.coOwnerIds.Select(u=>u.ToString()), serverInfo.ownerId.ToString()];
                List<Tools.Roblox.RobloxUser>? relatedUsers = Tools.Roblox.Users.FindMany((u, _) => userIds.Contains(u.id));
                userIds = [.. userIds.Except(relatedUsers.Select(u => u.id))];
                if (userIds.Count > 0)
                {
                    relatedUsers.AddRange(await Tools.Roblox.GetUserById(userIds) ?? []);
                }
                Tools.Roblox.RobloxUser? owner = relatedUsers?.Find(u => u.id == serverInfo.ownerId.ToString());
                List<Tools.Roblox.RobloxUser> coOwners = relatedUsers?.FindAll(u => serverInfo.coOwnerIds.Contains(long.Parse(u.id))) ?? [];

                await ctx.EditResponse(
                    new MessageBuilder
                    {
                        content = "",
                        embeds = [
                            new EmbedBuilder
                            {
                                title = "{string.title.erlcserver}",
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
                await ctx.EditResponse($"{{emoji.cross}} [{response.code}] {response.message ?? "An unknown error occured"}.");
            }
        }
    }
}
