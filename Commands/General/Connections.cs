using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using YellowMacaroni.Discord.Core;
using static Whispbot.Tools.Roblox;

namespace Whispbot.Commands.General
{
    public class Connections : Command
    {
        public override string Name => "Connections";
        public override string Description => "Check and update your connections to Whisp.";
        public override Module Module => Module.General;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["connections", "connect"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.User is null) return;

            UserConfig? userConfig = await WhispCache.UserConfig.Get(ctx.User.id);

            if (userConfig is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.userconfig.notfound}");
                return;
            }

            Tools.Roblox.RobloxUser? robloxUser = userConfig.roblox_id is not null ? Tools.Roblox.Users.FromCache(userConfig.roblox_id.Value.ToString()) : null;

            if (userConfig.roblox_id is not null && robloxUser is null)
            {
                await ctx.Reply(new MessageBuilder() { components = [new TextDisplayBuilder("{emoji.loading} {string.content.connections.fetchingroblox}...")], flags = MessageFlags.IsComponentsV2 });

                robloxUser = await Tools.Roblox.GetUserById(userConfig.roblox_id.Value);
            }

            await ctx.EditResponse(GetConnectionsMessage(false, ctx.User.id, robloxUser));
        }

        public static MessageBuilder GetConnectionsMessage(bool updating, string userId, Tools.Roblox.RobloxUser? robloxUser)
        {
            bool roblox = robloxUser is not null;

            return new MessageBuilder()
            {
                components = [
                    new ContainerBuilder()
                    {
                        components = [
                            new TextDisplayBuilder("**{string.title.yourconnections}**"),
                            new SectionBuilder()
                            .SetAccessory(
                                new ButtonBuilder(roblox ? $"disconnect_roblox {userId}" : $"connection_roblox {userId}") { disabled = updating }.SetLabel(roblox ? "Disconnect" : "Connect").SetStyle(roblox ? ButtonStyle.Danger : ButtonStyle.Secondary))
                            .SetComponents(
                                robloxUser is not null ? [
                                    new TextDisplayBuilder($"{{emoji.roblox}} **Roblox**"),
                                    new TextDisplayBuilder($"> **@{robloxUser.name}** ({robloxUser.id})")
                                ] : [
                                    new TextDisplayBuilder("{emoji.roblox} *Not connected to Roblox.*")
                                ]
                            )
                        ]
                    }
                ],
                flags = MessageFlags.IsComponentsV2
            };
        }
    }
}
