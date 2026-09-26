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
using Whispbot.Tools;

namespace Whispbot.Interactions.Shifts
{
    public class AdminClockout : InteractionCommandData
    {
        public override string CustomId => "sa_clockout";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count <= 1) return;
            if (await ctx.CheckAllowed()) return;

            if (!await WhispPermissions.CheckPermissionsInteraction(ctx, BotPermissions.ManageShifts)) return;

            ulong adminId = ulong.Parse(ctx.args[0]);
            ulong userId = ulong.Parse(ctx.args[1]);
            string? typeId = ctx.args.Count >= 3 ? ctx.args[2] : null;

            List<ShiftType>? types = await WhispCache.ShiftTypes.Get(ctx.GuildId.Value);
            if (types is null)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("shifts.errors.failed_get_shift_data")}");
                return;
            }

            ShiftType? type = types.Find(t => typeId is not null ? t.id.ToString() == typeId : t.is_default);
            if (type is null)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("shifts.errors.type_not_found")}");
                return;
            }

            await ctx.DeferResponse(true);

            var (shift, errormessage) = await Procedures.Clockout(ctx.GuildId.Value, userId, type, adminId);

            if (shift is not null)
            {
                await ctx.EditMessage(async m => m.Components = await ShiftAdminMessages.GetMainMessage(ctx.GuildId.Value, userId, adminId, ctx.args.Count > 2 ? type : null));
            }
            else
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {errormessage ?? ctx.String("shifts.clockout.errors.failed")}");
            }
        }
    }
}
