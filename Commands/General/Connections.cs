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
using Whispbot.Languages;
using Whispbot.Tools;
using Whispbot.Tools.Disc;
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

            RobloxUser? robloxUser = userConfig?.roblox_id is not null ? Roblox.Users.FromCache(userConfig.roblox_id.Value.ToString()) : null;

            if (userConfig?.roblox_id is not null && robloxUser is null)
            {
                await ctx.Reply(components: new ComponentBuilderV2().WithTextDisplay(new TextDisplayBuilder($"{ctx.Emoji("loading")} {ctx.String("connections.loading")}...")).Build(), flags: MessageFlags.ComponentsV2);

                robloxUser = await GetUserById(userConfig.roblox_id.ToString()!);
            }

            await ctx.EditResponse(components: GetConnectionsMessage(false, ctx.UserId, robloxUser, ctx.Language), flags: MessageFlags.ComponentsV2);
        }

        public static MessageComponent GetConnectionsMessage(bool updating, ulong userId, RobloxUser? robloxUser, Language language)
        {
            bool roblox = robloxUser is not null;

            return new ComponentBuilderV2()
                .WithContainer(
                    new ContainerBuilder()
                        .WithTextDisplay(language.Translate("connections.title"))
                        .WithSection(
                            new SectionBuilder()
                                .WithAccessory(
                                    roblox ? new ButtonBuilder(language.Translate("connections.disconnect"), $"disconnect_roblox {userId}", ButtonStyle.Secondary, isDisabled: updating)
                                           : new ButtonBuilder(language.Translate("connections.connect"), null, ButtonStyle.Link, url: $"{Config.websiteUrl}/login/roblox", isDisabled: updating)
                                )
                                .WithComponents(
                                    robloxUser is not null ? [
                                        new TextDisplayBuilder($"{Emojis.Get("roblox")} **Roblox**"),
                                        new TextDisplayBuilder($"> **@{robloxUser.name}** ({robloxUser.id})")
                                    ] : [
                                        new TextDisplayBuilder($"{Emojis.Get("roblox")} *{language.Translate("connections.errors.notconnected")}*")
                                    ]
                                )
                        )
                )
                .Build();
        }
    }
}

