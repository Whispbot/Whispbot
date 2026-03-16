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
    public class VoidCase : Command
    {
        public override string Name => "Void";
        public override string Description => "Void a Discord moderation";
        public override Module Module => Module.DiscordModeration;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["void"];
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

            var updatedCase = await DiscordModeration.VoidCase(ctx.Guild, caseId, ctx.User!);

            if (updatedCase is null)
            {
                await ctx.Reply("{emoji.cross} {string.errors.dm.failed_void_case}.");
            }
            else
            {
                await ctx.Reply($"{{emoji.tick}} {{string.success.dm.voided_case:id={updatedCase.case_id}}}!");
            }
        }
    }
}
