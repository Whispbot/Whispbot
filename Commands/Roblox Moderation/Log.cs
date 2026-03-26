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
        public override List<string>? SlashCommand => ["roblox", "log"];
        public override List<SlashCommandArg>? Arguments => [
            new ("user", "The Roblox user to moderate.", CommandArgType.RobloxUser),
            new ("type", "The type of moderation to use.", CommandArgType.RobloxType),
            new ("reason", "The reason for the moderation.", CommandArgType.String, optional: true)
        ];
        public override List<string> Schema => ["<type:rtype>", "<user:ruser>", "<reason:string?>"];
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

            string? type = ctx.args.Get("type")?.GetString();

            if (String.IsNullOrWhiteSpace(type))
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmlog.invalidtype}.");
                return;
            }

            RobloxModerationType? modType = types.Find(t => t.triggers.Contains(type) || t.id.ToString() == type);
            if (modType is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmlog.invalidtype}.");
                return;
            }

            Roblox.RobloxUser? user = ctx.args.Get("user")?.GetRobloxUser();

            if (user is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmlog.invaliduser}.");
                return;
            }    

            string? reason = ctx.args.Get("reason")?.GetString();

            if (ctx.GuildConfig?.roblox_moderation?.require_reason == true && string.IsNullOrWhiteSpace(reason))
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmlog.reasonrequired}");
                return;
            }        

            var (log, errormessage) = await Procedures.CreateModeration(
                ctx.GuildId,
                ctx.UserId,
                user.id,
                modType,
                reason ?? "*No reason provided.*"
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
                                    value = $"{{emoji.folder}} {modType.name}",
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

