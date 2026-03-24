using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Tools.Discord;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.Discord_Moderation
{
    public class Reason : Command
    {
        public override string Name => "Reason";
        public override string Description => "Modify the reason of a Discord moderation";
        public override Module Module => Module.DiscordModeration;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["reason"];
        public override List<SlashCommandArg>? Arguments => [
            new ("case", "The case ID to modify.", SlashCommandArgType.Case),
            new ("reason", "The new reason for the moderation.", SlashCommandArgType.String)
        ];
        public override List<string> Schema => ["<case:case>", "<reason:string>"];
        public override List<string> Aliases => ["reason"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.Guild is null || ctx.UserId is null) return;

            if (!await DiscordPermissions.HasPermissionOrAdmin(
                ctx.Guild,
                ctx.UserId,
                Permissions.ManageGuild |
                Permissions.BanMembers |
                Permissions.KickMembers |
                Permissions.ModerateMembers
            ))
            {
                await ctx.Reply("{emoji.cross} {string.errors.dm.no_permission}.");
                return;
            }

            string? caseIdArg = ctx.args.FirstOrDefault();
            if (caseIdArg is null) 
            {
                await ctx.Reply("{emoji.cross} {string.errors.dm.no_case_provided}.");
                return;
            }

            ctx.args.RemoveAt(0);
            if (ctx.args.Count == 0)
            {
                await ctx.Reply("{emoji.cross} {string.errors.dm.no_reason_provided}.");
                return;
            }
            string newReason = ctx.args.Join(" ");

            int caseId = 0;
            if (int.TryParse(caseIdArg, out int parsedArg))
            {
                caseId = parsedArg;
            }
            else if (caseIdArg.Equals("last", StringComparison.CurrentCultureIgnoreCase))
            {
                caseId = -1;
            }
            else if (caseIdArg.Equals("slast", StringComparison.CurrentCultureIgnoreCase))
            {
                caseId = -2;
            }

            if (caseId == 0)
            {
                await ctx.Reply("{emoji.cross} {string.errors.dm.invalid_case_id}.");
                return;
            }

            var updatedCase = await DiscordModeration.EditReason(ctx.Guild, caseId, ctx.User!, newReason);

            if (updatedCase is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.dm.failed_update_case}.");
            }
            else
            {
                await ctx.Reply($"{{emoji.tick}} {{string.success.dm.updated_case:id={updatedCase.case_id}}}!");
            }
        }
    }
}
