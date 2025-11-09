using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands.Shifts;
using Whispbot.Databases;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Interactions.Shifts
{
    public class Clockin : InteractionData
    {
        public override string CustomId => "clockin";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count < 1) return;
            if (await ctx.CheckAllowed()) return;

            await ctx.DeferResponse(true);

            List<ShiftType>? types = await WhispCache.ShiftTypes.Get(ctx.GuildId);
            ShiftType? thisType = ctx.args.Count >= 2 ? types?.Find(t => t.id.ToString() == ctx.args[1]) : types?.Find(t => t.is_default);

            if (thisType is null)
            {
                await ctx.Respond("{emoji.cross} {string.errors.clockin.typenotfound}");
                return;
            }

            var (shift, errormessage) = await Procedures.Clockin(long.Parse(ctx.GuildId), long.Parse(ctx.UserId), thisType);

            if (shift is null)
            {
                await ctx.Respond($"{{emoji.cross}} {errormessage ?? "{string.errors.clockin.failed}"}");
            } 
            else
            {
                ShiftsData? data = ShiftsData.Get(long.Parse(ctx.UserId), long.Parse(ctx.GuildId), thisType);

                await ctx.DeleteResponse();
                data ??= new ShiftsData { currentShiftStart = shift.start_time };

                await ctx.EditMessage(
                    data.generateMessage(ctx.UserId, thisType, false)
                );
            }
        }
    }
}
