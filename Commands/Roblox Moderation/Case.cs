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
                await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("rmod.case.errors.missing_arguments")}.");
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
                    await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("rmod.case.errors.invalid_id")}");
                    return;
                }

                moderation = Postgres.SelectFirst<RobloxModeration>(
                    "SELECT * FROM roblox_moderations WHERE guild_id = @1 AND \"case\" = @2",
                    [ctx.GuildId, intCaseId]
                );
            }

            if (moderation is null)
            {
                await ctx.Reply($"{ctx.Emoji("cross")} {ctx.String("rmod.case.errors.not_found")}");
                return;
            }

            IUser? moderator = await Config.client!.GetUserAsync(moderation.moderator_id, CacheMode.AllowDownload, RequestOptions.Default);
            Roblox.RobloxUser? target = await Roblox.GetUser(moderation.target_id.ToString());

            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(ctx.GuildId);
            RobloxModerationType? type = types?.Find(t => t.id == moderation.type);

            await ctx.Reply(
                embed: new EmbedBuilder()
                    .WithAuthor(moderator.GlobalName ?? $"@{moderator.Username}")
                    .WithTitle(ctx.String("rmod.case.title", moderation.@case.ToString()))
                    .WithThumbnailUrl(await Roblox.GetUserAvatar(moderation.target_id.ToString(), 250))
                    .WithFields(
                        new EmbedFieldBuilder() { Name = ctx.String("rmod.log.field.user"), Value = $"{ctx.Emoji("user")} {target?.name}\n{(!string.IsNullOrWhiteSpace(target?.displayName) && target.displayName != target.name ? $"{ctx.Emoji("chat")} {target?.displayName}\n" : "")}{ctx.Emoji("folder")} {target?.id}\n{ctx.Emoji("clock")} <t:{target?.CreateTime?.ToUnixTimeSeconds()}:d> (<t:{target?.CreateTime?.ToUnixTimeSeconds()}:R>)" },
                        new EmbedFieldBuilder() { Name = $"{ctx.String("rmod.log.field.type")}", Value = $"{ctx.Emoji("folder")} {type?.name ?? "Unknown Type"}{(type?.is_deleted == true ? $" ({ctx.String("rmod.case.type_deleted")})" : "")}" },
                        new EmbedFieldBuilder() { Name = $"{ctx.String("rmod.log.field.reason")}", Value = $"{ctx.Emoji("alignment")} {moderation.reason}" }
                    )
                    .Build()
            );
        }
    }
}

