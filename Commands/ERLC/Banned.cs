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
    public class ERLC_Banned : Command
    {
        public override string Name => "ER:LC Banned";
        public override string Description => "Check if a user is banned.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["banned", "erlc banned"];
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

            if (ctx.args.Count < 1)
            {
                await ctx.Reply("{emoji.cross} {string.errors.erlcbanned.nouserprovided}");
                return;
            }

            Roblox.RobloxUser? user = await Roblox.GetUser(ctx.args[0]);
            ctx.args.RemoveAt(0);

            if (user is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.erlcbanned.usernotfound}");
                return;
            }

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

            var response = Tools.ERLC.CheckCache(Tools.ERLC.Endpoint.ServerBans, server.DecryptedApiKey);

            if (response is null)
            {
                await ctx.Reply("{emoji.loading} {string.content.erlcbans.fetching}...");
                response = await Tools.ERLC.GetBans(server);

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

            Dictionary<string, string>? bans = JsonConvert.DeserializeObject<Dictionary<string, string>>(response.data?.ToString() ?? "{}");

            if (bans is not null)
            {
                bool banned = bans.ContainsKey(user.id);

                await ctx.EditResponse(
                    new MessageBuilder
                    {
                        components = [
                            new ContainerBuilder
                            {
                                components = [
                                    new TextDisplayBuilder(
                                        banned ?
                                        $"{{emoji.tick}} **@{user.name}** {{string.content.erlcserver.banned}} {server.name}." :
                                        $"{{emoji.cross}} **@{user.name}** {{string.content.erlcserver.notbanned}} {server.name}."
                                    )
                                ],
                                accent = banned ? new Color(0, 150, 0) : new Color(150, 0, 0)
                            }
                        ],
                        flags = MessageFlags.IsComponentsV2
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
