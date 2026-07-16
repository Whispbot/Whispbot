using Discord;
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
    public class ERLC_JoinServer : Command
    {
        public override string Name => "ER:LC Join Server";
        public override string Description => "Get a link to join an erlc server.";
        public override Module Module => Module.ERLC;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["erlc", "join"];
        public override List<SlashCommandArg>? Arguments => [
            new ("server", "The ERLC server to join. If not provided, the default will be used.", CommandArgType.ERLCServer, optional: true)
        ];
        public override List<string> Schema => ["<server:erlcserver?>"];
        public override List<string> Aliases => ["erlc join", "erlc joincode"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.ERLC)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseERLC)) return;

            ERLCServerConfig? server = await ERLCDatabase.TryGetServer(ctx);
            if (server is null) return;

            string url = $"https://beta.whisp.bot/join/erlc/{server.id}";

            await ctx.Reply(
                components:
                    new ComponentBuilderV2()
                        .WithContainer(
                            new ContainerBuilder()
                                .WithSection(
                                    new SectionBuilder()
                                        .WithTextDisplay($"Join **{server.name ?? "no name"}** using code '[{server.code ?? "nocode"}](<{url}>)'.")
										.WithAccessory(
											new ButtonBuilder()
												.WithStyle(ButtonStyle.Link)
												.WithLabel("Quick Join")
												.WithUrl(url)
                                        )
								)
                        )
                        .Build(),
                flags: MessageFlags.ComponentsV2
            );
        }
    }
}

