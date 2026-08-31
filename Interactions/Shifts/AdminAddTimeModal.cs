using Discord;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands.Shifts;
using Whispbot.Databases;
using Whispbot.Tools;

namespace Whispbot.Interactions.Shifts
{
    public class AdminAddTimeModal : InteractionCommandData
    {
        public override string CustomId => "sa_addtime";
        public override InteractionType Type => InteractionType.ModalSubmit;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count <= 1 || ctx.interaction is not IModalInteraction modal) return;
            var data = modal.Data;
            if (await ctx.CheckAllowed()) return;

            if (!await WhispPermissions.CheckPermissionsInteraction(ctx, BotPermissions.ManageShifts)) return;

            string? time = data.Components.FirstOrDefault(c => c.CustomId == "time")?.Value;
            if (time is null)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("shifts.admin.errors.invalid_time")}");
                return;
            }

            double timeMs = Time.ConvertStringToMilliseconds(time);
            if (timeMs <= 0)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("shifts.admin.errors.invalid_time")}");
                return;
            }

            await ctx.DeferResponse();

            Shift? shift = Postgres.SelectFirst<Shift>(
                @"UPDATE shifts SET end_time = end_time + (@1 * INTERVAL '1 millisecond') WHERE id = @2 AND guild_id = @3 RETURNING *;",
                [timeMs, long.Parse(ctx.args[1]), ctx.GuildId]
            );

            if (shift is null)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("shifts.admin.errors.shift_not_found")}");
                return;
            }

            await ctx.EditMessage(async m => m.Components = await ShiftAdminMessages.GetModifyMessage(shift, ctx.args[0]));
        }
    }
}
