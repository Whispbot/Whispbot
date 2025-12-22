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

            ERLCServerConfig? server = await ERLC.TryGetServer(ctx);
            if (server is null) return;

            var response = await ERLC.GetEndpointData<Dictionary<string, string>>(ctx, server, ERLC.Endpoint.ServerBans);
            var bans = response?.data;

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
                await ctx.EditResponse($"{{emoji.cross}} [{response?.code}] {response?.message ?? "An unknown error occured"}.");
            }
        }
    }
}
