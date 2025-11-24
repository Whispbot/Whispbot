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
    public class LogModeration : Command
    {
        public override string Name => "Log Moderation";
        public override string Description => "Log a Roblox moderation";
        public override Module Module => Module.RobloxModeration;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["log", "moderate", "rlog"];
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
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseRobloxModerations)) return;

            if (ctx.args.Count < 2)
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmlog.missingargs}.");
                return;
            }

            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(ctx.GuildId);

            if (types is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmlog.dbfailed}.");
                return;
            }

            bool typearg = false;

            string arg1 = ctx.args[0].ToLowerInvariant();
            string arg2 = ctx.args[1].ToLowerInvariant();

            RobloxModerationType? type = types.Find(t => t.triggers.Contains(arg1));
            if (type is null)
            {
                type = types.Find(t => t.triggers.Contains(arg2));
                if (type is null)
                {
                    await ctx.Reply("{emoji.cross} {string.errors.rmlog.invalidtype}.");
                    return;
                }
                else
                {
                    typearg = true;
                }
            }
            
            string reason = string.Join(' ', ctx.args.Skip(2));

            if (ctx.GuildConfig?.roblox_moderation_require_reason == true && string.IsNullOrWhiteSpace(reason))
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmlog.reasonrequired}");
                return;
            }

            Roblox.RobloxUser? user = await Roblox.GetUser(typearg ? ctx.args[0] : ctx.args[1]);

            if (user is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmlog.invaliduser}.");
                return;
            }            

            var (log, errormessage) = await Procedures.CreateModeration(
                ctx.GuildId,
                ctx.UserId,
                user.id,
                type,
                reason
            );

            if (log is not null)
            {
                await ctx.Reply(new MessageBuilder
                {
                    embeds = [
                        new EmbedBuilder
                        {
                            title = "{string.title.rmlog.logged}",
                            description = $"{{emoji.tick}} {{string.success.rmlog:caseid={log.@case}}}.",
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
                                    value = $"{{emoji.user}} **@{user.name}** ({user.id})",
                                    inline = true
                                },
                                new EmbedField
                                {
                                    name = "{string.title.rmlog.type}",
                                    value = $"{{emoji.folder}} {type.name}",
                                    inline = true
                                },
                                new EmbedField
                                {
                                    name = "{string.title.rmlog.reason}",
                                    value = $"{{emoji.alignment}} {reason}",
                                    inline = false
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
