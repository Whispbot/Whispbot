using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.ERLCCommands.Commands.Moderation
{
    public class EditReason : ERLCCommand
    {
        public override string Name => "Edit Roblox Moderation Reason";
        public override string Description => "Edit the reason of a Roblox moderation";
        public override List<string> Aliases => ["reason", "r"];
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(ERLCCommandContext ctx)
        {
            if (ctx.GuildId is null || ctx.UserId is null) return;

            if (ctx.args.Count < 1)
            {
                await ctx.Reply("{string.errors.erlccommand.ermr.missingcase}.");
                return;
            }

            if (ctx.args.Count < 2)
            {
                await ctx.Reply("{string.errors.erlccommand.ermr.missingreason}.");
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
                    await ctx.Reply("{string.errors.rmcase.invalidid}.");
                    return;
                }

                if (caseId <= 0 || caseId >= 100_000)
                {
                    await ctx.Reply("{string.errors.rmcase.invalidid}.");
                    return;
                }

                ctx.args.RemoveAt(0);
                updatedModeration = await Procedures.ChangeRMReason(ctx.GuildId, ctx.UserId, ctx.args.Join(" "), caseId);
            }

            if (updatedModeration is null)
            {
                await ctx.Reply("{string.errors.rmcase.notfound}.");
                return;
            }

            await ctx.Reply("{string.success.erlccommand.ermr.success}.");
        }
    }
}
