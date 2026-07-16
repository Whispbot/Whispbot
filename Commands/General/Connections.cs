using Discord;
using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Databases;
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
        public override List<string>? SlashCommand => ["connections"];
        public override List<SlashCommandArg>? Arguments => null;
        public override List<string> Schema => [];
        public override List<string> Aliases => ["connections", "connect"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.User is null) return;

            UserConfig? userConfig = await WhispCache.UserConfig.Get(ctx.UserId);

            RobloxUser? robloxUser = userConfig?.roblox_id is not null ? Users.FromCache(userConfig.roblox_id.Value.ToString()) : null;

            if (userConfig?.roblox_id is not null && robloxUser is null)
            {
                await ctx.Reply(components: new ComponentBuilderV2().WithTextDisplay(new TextDisplayBuilder("{emoji.loading} {string.content.connections.fetchingroblox}...")).Build(), flags: MessageFlags.ComponentsV2);

                robloxUser = await GetUserById(userConfig.roblox_id.ToString()!);
            }

            if (ctx.hasResponded)
            {
                await ctx.EditResponse(m =>
                {
                    m.Components = GetConnectionsMessage(false, ctx.UserId, robloxUser);
                });
            }
            else
            {
                await ctx.Reply(components: GetConnectionsMessage(false, ctx.UserId, robloxUser), flags: MessageFlags.ComponentsV2);
            }
        }

        public static MessageComponent GetConnectionsMessage(bool updating, ulong userId, RobloxUser? robloxUser)
        {
            bool roblox = robloxUser is not null;

            return new ComponentBuilderV2()
                .WithContainer(
                    new ContainerBuilder()
                        .WithTextDisplay("**{string.title.yourconnections}**")
                        .WithSection(
                            new SectionBuilder()
                                .WithAccessory(
                                    roblox ? new ButtonBuilder("Disconnect", $"disconnect_roblox {userId}", ButtonStyle.Secondary, isDisabled: updating)
                                           : new ButtonBuilder("Connect", null, ButtonStyle.Link, url: $"{Config.websiteUrl}/login/roblox", isDisabled: updating)
                                )
                                .WithComponents(
                                    robloxUser is not null ? [
                                        new TextDisplayBuilder($"{{emoji.roblox}} **Roblox**"),
                                        new TextDisplayBuilder($"> **@{robloxUser.name}** ({robloxUser.id})")
                                    ] : [
                                        new TextDisplayBuilder("{emoji.roblox} *Not connected to Roblox.*")
                                    ]
                                )
                        )
                )
                .Build();
        }
    }
}

