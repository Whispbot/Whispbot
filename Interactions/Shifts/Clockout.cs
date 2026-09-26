using Discord;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Cache;
using Whispbot.Commands.Shifts;
using Whispbot.Databases;

namespace Whispbot.Interactions.Shifts
{
    public class Clockout : InteractionCommandData
    {
        public override string CustomId => "clockout";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count < 1) return;
            if (await ctx.CheckAllowed()) return;

            await ctx.DeferResponse(true);

            List<ShiftType>? types = await WhispCache.ShiftTypes.Get(ctx.GuildId.Value);
            ShiftType? thisType = ctx.args.Count >= 2 ? types?.Find(t => t.id.ToString() == ctx.args[1]) : types?.Find(t => t.is_default);

            if (thisType is null)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("shifts.errors.type_not_found")}");
                return;
            }

            var (shift, errormessage) = await Procedures.Clockout(ctx.GuildId.Value, ctx.UserId, thisType);

            if (shift is null)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {errormessage ?? ctx.String("shifts.clockout.errors.failed")}");
            }
            else
            {
                ShiftsData? data = ShiftsData.Get(ctx.UserId, ctx.GuildId.Value, thisType);

                await ctx.DeleteResponse();
                data ??= new ShiftsData { currentShiftStart = shift.start_time };

                await ctx.EditMessage(
                    m => m.Components = data.GenerateMessage(ctx.UserId, thisType, true, shift)
                );
            }
        }
    }
}
