using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Extensions;

namespace Whispbot.Commands.ERLC.Commands.Moderation
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
            if (ctx.args.Count < 1)
            {
                await ctx.Reply($"{ctx.String("erlc.errors.reason_missing_case")}.");
                return;
            }

            if (ctx.args.Count < 2)
            {
                await ctx.Reply($"{ctx.String("erlc.errors.reason_missing_reason")}.");
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
                    await ctx.Reply($"{ctx.String("rmod.case.errors.invalid_id")}.");
                    return;
                }

                if (caseId <= 0 || caseId >= 100_000)
                {
                    await ctx.Reply($"{ctx.String("rmod.case.errors.invalid_id")}.");
                    return;
                }

                ctx.args.RemoveAt(0);
                updatedModeration = await Procedures.ChangeRMReason(ctx.GuildId, ctx.UserId, ctx.args.Join(" "), caseId);
            }

            if (updatedModeration is null)
            {
                await ctx.Reply($"{ctx.String("rmod.case.errors.not_found")}.");
                return;
            }

            await ctx.Reply($"{ctx.String("erlc.success.reason_updated")}.");
        }
    }
}

