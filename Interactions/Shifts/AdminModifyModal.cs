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
using Whispbot.Tools.Logger;

namespace Whispbot.Interactions.Shifts
{
    public class AdminModifyModal : InteractionCommandData
    {
        public override string CustomId => "sa_modify2";
        public override InteractionType Type => InteractionType.ModalSubmit;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count <= 1 || ctx.interaction is not IModalInteraction modal) return;
            var data = modal.Data;
            if (await ctx.CheckAllowed()) return;

            if (!await WhispPermissions.CheckPermissionsInteraction(ctx, BotPermissions.ManageShifts)) return;

            List<ShiftType>? types = await WhispCache.ShiftTypes.Get(ctx.GuildId.Value);
            if (types is null)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("shifts.errors.failed_get_shift_data")}");
                return;
            }

            ShiftType? type = types.Find(t => ctx.args.Count >= 3 && t.id.ToString() == ctx.args[2]);
            if (type is null && ctx.args.Count > 2)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("shifts.errors.type_not_found")}");
                return;
            }

            string userId = ctx.args[1];

            string? entered_shift_id = data.Components.FirstOrDefault(c => c.CustomId == "shift_id")?.Value;
            string? shift_id = string.IsNullOrEmpty(entered_shift_id) ? data.Components.FirstOrDefault(c => c.CustomId == "recent_shift")?.Values.FirstOrDefault() : entered_shift_id;

            if (shift_id is null)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("shifts.admin.errors.no_shift")}");
                return;
            }

            if (!long.TryParse(shift_id, out _))
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("shifts.admin.errors.invalid_shift_id")}");
                return;
            }

            await ctx.DeferResponse();

            Shift? shift = Postgres.SelectFirst<Shift>(
                @"SELECT * FROM shifts WHERE id = @1 AND guild_id = @2 AND moderator_id = @3;",
                [long.Parse(shift_id), ctx.GuildId.Value, long.Parse(userId)]
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
