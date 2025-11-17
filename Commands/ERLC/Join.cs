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
    public class ERLC_JoinServer : Command
    {
        public override string Name => "ER:LC Join Server";
        public override string Description => "Get a link to join an erlc server.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["erlc join", "erlc joincode"];
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

            string url = $"https://whisp.bot/join-erlc/{server.id}";

            await ctx.Reply(new MessageBuilder
            {
                components = [
                    new ContainerBuilder
                    {
                        components = [
                            new SectionBuilder
                            {
                                components = [
                                    new TextDisplayBuilder($"Join **{server.name ?? "no name"}** using code '[{server.code ?? "nocode"}](<{url}>)'.")
                                ],
                                accessory = new ButtonBuilder
                                {
                                    style = ButtonStyle.Link,
                                    label = "Quick Join",
                                    url = url
                                }
                            }
                        ],
                    }
                ],
                flags = MessageFlags.IsComponentsV2
            });
        }
    }
}
