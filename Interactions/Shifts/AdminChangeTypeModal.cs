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
    public class AdminChangeTypeModal : InteractionCommandData
    {
        public override string CustomId => "sa_changetype";
        public override InteractionType Type => InteractionType.ModalSubmit;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count < 2 || ctx.interaction is not IModalInteraction modal) return;
            var data = modal.Data;
            if (await ctx.CheckAllowed()) return;

            if (!await WhispPermissions.CheckPermissionsInteraction(ctx, BotPermissions.ManageShifts)) return;

            string? shift_id = ctx.args[1];
            string? new_type_id = data.Components.FirstOrDefault(c => c.CustomId == "new_type")?.Values.FirstOrDefault();

            if (shift_id is null || new_type_id is null)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("shifts.admin.errors.no_shift")}");
                return;
            }

            if (!long.TryParse(new_type_id, out _))
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("shifts.admin.errors.invalid_shift_id")}");
                return;
            }

            await ctx.DeferResponse();

            Shift? shift = Postgres.SelectFirst<Shift>(
                @"UPDATE shifts SET type = @1 WHERE id = @2 AND guild_id = @3 RETURNING *;",
                [ulong.Parse(new_type_id), ulong.Parse(shift_id), ctx.GuildId]
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
