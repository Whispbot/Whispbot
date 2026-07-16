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
    public class RobloxCase : Command
    {
        public override string Name => "Roblox Moderation Case";
        public override string Description => "View information about a Roblox moderation";
        public override Module Module => Module.RobloxModeration;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["roblox", "case", "view"];
        public override List<SlashCommandArg>? Arguments => [
            new ("case", "The Roblox moderation case ID to view.", CommandArgType.RobloxCase)
        ];
        public override List<string> Schema => ["<case:rcase>"];
        public override List<string> Aliases => ["rcase", "rmoderation case", "rmcase"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (!await WhispPermissions.CheckModuleMessage(ctx, Module.RobloxModeration)) return;
            if (!await WhispPermissions.CheckPermissionsMessage(ctx, BotPermissions.UseRobloxModerations)) return;

            string? caseId = ctx.args.Get("case")?.GetString();

            if (String.IsNullOrWhiteSpace(caseId))
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmcase.missingargs}.");
                return;
            }

            RobloxModeration? moderation = null;

            if (caseId.Equals("last", StringComparison.InvariantCultureIgnoreCase))
            {
                moderation = Postgres.SelectFirst<RobloxModeration>(
                    "SELECT * FROM roblox_moderations WHERE guild_id = @1 AND moderator_id = @2 ORDER BY \"case\" DESC LIMIT 1",
                    [ctx.GuildId, ctx.UserId]
                );
            }
            else if (new List<string>() { "slast", "server-last", "serverlast" }.Contains(caseId.ToLower()))
            {
                moderation = Postgres.SelectFirst<RobloxModeration>(
                    "SELECT * FROM roblox_moderations WHERE guild_id = @1 ORDER BY \"case\" DESC LIMIT 1",
                    [ctx.GuildId]
                );
            }
            else
            {
                bool isNum = int.TryParse(caseId, out int intCaseId);

                if (!isNum || intCaseId <= 0 || intCaseId >= 100_000)
                {
                    await ctx.Reply("{emoji.cross} {string.errors.rmcase.invalidid}");
                    return;
                }

                moderation = Postgres.SelectFirst<RobloxModeration>(
                    "SELECT * FROM roblox_moderations WHERE guild_id = @1 AND \"case\" = @2",
                    [ctx.GuildId, intCaseId]
                );
            }

            if (moderation is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmcase.notfound}");
                return;
            }

            IUser? moderator = await Config.client!.GetUserAsync(moderation.moderator_id, CacheMode.AllowDownload, RequestOptions.Default);
            Roblox.RobloxUser? target = await Roblox.GetUser(moderation.target_id.ToString());

            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(ctx.GuildId);
            RobloxModerationType? type = types?.Find(t => t.id == moderation.type);

            await ctx.Reply(
                embed: new EmbedBuilder()
                    .WithAuthor(moderator.GlobalName ?? $"@{moderator.Username}")
                    .WithTitle($"{{string.title.rmcase:case={moderation.@case}}}")
                    .WithThumbnailUrl(await Roblox.GetUserAvatar(moderation.target_id.ToString(), 250))
                    .WithFields(
                        new EmbedFieldBuilder() { Name = "{string.title.rmlog.user}", Value = $"{{emoji.user}} {target?.name}\n{(!string.IsNullOrWhiteSpace(target?.displayName) && target.displayName != target.name ? $"{{emoji.chat}} {target?.displayName}\n" : "")}{{emoji.folder}} {target?.id}\n{{emoji.clock}} <t:{target?.createTime?.ToUnixTimeSeconds()}:d> (<t:{target?.createTime?.ToUnixTimeSeconds()}:R>)" },
                        new EmbedFieldBuilder() { Name = "{string.title.rmlog.type}", Value = $"{{emoji.folder}} {type?.name ?? "Unknown Type"}{(type?.is_deleted == true ? " ({string.content.rmcase.typedeleted})" : "")}" },
                        new EmbedFieldBuilder() { Name = "{string.title.rmlog.reason}", Value = $"{{emoji.alignment}} {moderation.reason}" }
                    )
                    .Build()
            );
        }
    }
}

