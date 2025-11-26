using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Tools;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.Roblox_Moderation
{
    public class BanRequest : Command
    {
        public override string Name => "Log Ban Request";
        public override string Description => "Log a Roblox Ban Request";
        public override Module Module => Module.RobloxModeration;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["br", "banrequest", "bolo"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.UserId is null) return;

            if (ctx.GuildId is null || ctx.Guild is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.general.guildonly}.");
                return;
            }

            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.RobloxModeration)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseBanRequests)) return;

            if (ctx.args.Count < 1)
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmbr.missingargs}.");
                return;
            }

            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(ctx.GuildId);

            if (types is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmlog.dbfailed}.");
                return;
            }

            bool hasBanType = types.Any(t => t.is_ban_type);

            if (!hasBanType)
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmbr.nobantype}");
                return;
            }

            string reason = string.Join(' ', ctx.args.Skip(1));

            if (ctx.GuildConfig?.roblox_moderation?.require_reason == true && string.IsNullOrWhiteSpace(reason))
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmlog.reasonrequired}");
                return;
            }

            Roblox.RobloxUser? user = await Roblox.GetUser(ctx.args[0]);

            if (user is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmlog.invaliduser}.");
                return;
            }

            var (log, errormessage) = await Procedures.CreateBanRequest(
                ctx.GuildId,
                ctx.UserId,
                user.id,
                reason
            );

            if (log is not null)
            {
                await ctx.Reply(new MessageBuilder
                {
                    embeds = [
                        new EmbedBuilder
                        {
                            title = "{string.title.rmbr.logged}",
                            description = "{emoji.tick} {string.success.rmbr}.",
                            author = new EmbedAuthor
                            {
                                name = $"{(ctx.User?.global_name is not null ? ctx.User.global_name : $"@{ctx.User?.username ?? "unknown"}")}",
                                icon_url = ctx.User?.avatar_url
                            },
                            thumbnail = new EmbedThumbnail
                            {
                                url = await Roblox.GetUserAvatar(user.id)
                            },
                            fields = [
                                new EmbedField
                                {
                                    name = "{string.title.rmlog.user}",
                                    value = $"{{emoji.user}} **@{user.name}** ({user.id})"
                                },
                                new EmbedField
                                {
                                    name = "{string.title.rmlog.reason}",
                                    value = $"{{emoji.alignment}} {reason}"
                                }
                            ]
                        }
                    ]
                });
            }
            else
            {
                await ctx.Reply("{emoji.cross} {string.errors}.");
            }
        }
    }
}
