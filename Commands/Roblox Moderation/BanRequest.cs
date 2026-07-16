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
    public class BanRequest : Command
    {
        public override string Name => "Log Ban Request";
        public override string Description => "Log a Roblox Ban Request";
        public override Module Module => Module.RobloxModeration;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["roblox", "ban-request"];
        public override List<SlashCommandArg>? Arguments => [
            new ("user", "The Roblox user to create a ban request for.", CommandArgType.RobloxUser),
            new ("reason", "The reason for the ban request.", CommandArgType.String, optional: true)
        ];
        public override List<string> Schema => ["<user:ruser>", "<reason:string?>"];
        public override List<string> Aliases => ["br", "banrequest", "bolo"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
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

            string? reason = ctx.args.Get("reason")?.GetString();

            if (ctx.GuildConfig?.roblox_moderation?.require_reason == true && string.IsNullOrWhiteSpace(reason))
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmlog.reasonrequired}");
                return;
            }

            Roblox.RobloxUser? user = ctx.args.Get("user")?.GetRobloxUser();

            if (user is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmlog.invaliduser}.");
                return;
            }

            var (log, errormessage) = await Procedures.CreateBanRequest(
                ctx.GuildId,
                ctx.UserId,
                ulong.Parse(user.id),
                reason ?? "*No reason provided.*"
            );

            if (log is not null)
            {
                await ctx.Reply(
                    embed: new EmbedBuilder()
                        .WithTitle("{string.title.rmbr.logged}")
                        .WithDescription("{emoji.tick} {string.success.rmbr}")
                        .WithAuthor(ctx.User.GlobalName ?? $"@{ctx.User.Username}", ctx.User.GetDisplayAvatarUrl())
                        .WithThumbnailUrl(await Roblox.GetUserAvatar(user.id))
                        .WithFields(
                            new EmbedFieldBuilder() { Name = "{string.title.rmlog.user}", Value = $"{{emoji.user}} **@{user.name}** ({user.id})" },
                            new EmbedFieldBuilder() { Name = "{string.title.rmlog.reason}", Value = $"{{emoji.alignment}} {reason}" }
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

