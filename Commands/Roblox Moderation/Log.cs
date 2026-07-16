using Discord;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Databases;
using Whispbot.Tools;

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
                ulong.Parse(user.id),
                modType,
                reason ?? "*No reason provided.*"
            );

            if (log is not null)
            {
                await ctx.Reply(
                    embed: new EmbedBuilder()
                        .WithTitle("{string.title.rmlog.logged}")
                        .WithDescription($"{{emoji.tick}} {{string.success.rmlog:caseid={log.@case}}}")
                        .WithAuthor(ctx.User.GlobalName ?? $"@{ctx.User.Username}", ctx.User.GetDisplayAvatarUrl())
                        .WithThumbnailUrl(await Roblox.GetUserAvatar(user.id, 250))
                        .WithFields(
                            new EmbedFieldBuilder() { Name = "{string.title.rmlog.user}", Value = $"{{emoji.user}} **@{user.name}** ({user.id})", IsInline = true },
                            new EmbedFieldBuilder() { Name = "{string.title.rmlog.type}", Value = $"{{emoji.folder}} {modType.name}", IsInline = true },
                            new EmbedFieldBuilder() { Name = "{string.title.rmlog.reason}", Value = $"{{emoji.alignment}} {reason}", IsInline = false }
                        )
                        .Build()
                );
            }
            else
            {
                await ctx.Reply("{emoji.cross} {string.errors}.");
            }
        }
    }
}

