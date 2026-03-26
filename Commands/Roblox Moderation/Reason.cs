using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Tools;
using YellowMacaroni.Discord.Cache;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.Roblox_Moderation
{
    public class RobloxReason : Command
    {
        public override string Name => "Edit Roblox Reason";
        public override string Description => "Update the reason for a Roblox moderation";
        public override Module Module => Module.RobloxModeration;
        public override bool GuildOnly => true;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["roblox", "case", "reason"];
        public override List<SlashCommandArg>? Arguments => [
            new ("case", "The Roblox moderation case to edit.", CommandArgType.RobloxCase),
            new ("reason", "The new reason for the moderation.", CommandArgType.String)
        ];
        public override List<string> Schema => ["<case:rcase>", "<reason:string>"];
        public override List<string> Aliases => ["rcase reason", "rreason", "rmcase reason", "rmoderation reason"];
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

            string? caseId = ctx.args.Get("case")?.GetString();

            if (String.IsNullOrEmpty(caseId))
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmcase.missingargs}.");
                return;
            }

            string? reason = ctx.args.Get("reason")?.GetString();

            if (String.IsNullOrEmpty(reason))
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmcase.missingargs}.");
                return;
            }

            RobloxModeration? updatedModeration;
            if (caseId.Equals("last", StringComparison.InvariantCultureIgnoreCase))
            {
                updatedModeration = await Procedures.ChangeRMReason(ctx.GuildId, ctx.UserId, reason, -1);
            }
            else if (new List<string>() { "slast", "server-last", "serverlast" }.Contains(caseId.ToLower()))
            {
                updatedModeration = await Procedures.ChangeRMReason(ctx.GuildId, ctx.UserId, reason, -2);
            }
            else
            {
                bool isNum = int.TryParse(caseId, out int intCaseId);

                if (!isNum || intCaseId <= 0 || intCaseId >= 100_000)
                {
                    await ctx.Reply("{emoji.cross} {string.errors.rmcase.invalidid}");
                    return;
                }

                updatedModeration = await Procedures.ChangeRMReason(ctx.GuildId, ctx.UserId, reason, intCaseId);
            }

            if (updatedModeration is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmcase.notfound}");
                return;
            }

            await ctx.Reply($"{{emoji.tick}} {{string.success.rmedit.updated:case={updatedModeration.@case}}}.");
        }
    }
}

