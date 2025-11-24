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

            if (ctx.args.Count < 1)
            {
                await ctx.Reply("{emoji.cross} {string.errors.rmcase.missingargs}.");
                return;
            }


            RobloxModeration? updatedModeration;
            if (ctx.args[0].Equals("last", StringComparison.InvariantCultureIgnoreCase))
            {
                ctx.args.RemoveAt(0);
                updatedModeration = await Procedures.ChangeRMReason(ctx.GuildId, ctx.UserId, ctx.args.Join(" "), -1);
            }
            else if (new List<string>() { "slast", "server-last", "serverlast" }.Contains(ctx.args[0].ToLower()))
            {
                ctx.args.RemoveAt(0);
                updatedModeration = await Procedures.ChangeRMReason(ctx.GuildId, ctx.UserId, ctx.args.Join(" "), -2);
            }
            else
            {
                bool isNum = int.TryParse(ctx.args[0], out int caseId);

                if (!isNum)
                {
                    await ctx.Reply("{emoji.cross} {string.errors.rmcase.invalidid}");
                    return;
                }

                if (caseId <= 0 || caseId >= 100_000)
                {
                    await ctx.Reply("{emoji.cross} {string.errors.rmcase.invalidid}");
                    return;
                }

                ctx.args.RemoveAt(0);
                updatedModeration = await Procedures.ChangeRMReason(ctx.GuildId, ctx.UserId, ctx.args.Join(" "), caseId);
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
