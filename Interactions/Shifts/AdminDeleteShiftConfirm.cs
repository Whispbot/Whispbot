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
    public class AdminDeleteShiftConfirm : InteractionCommandData
    {
        public override string CustomId => "sa_deleteshift";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count < 3) return;
            if (await ctx.CheckAllowed()) return;

            if (!await WhispPermissions.CheckPermissionsInteraction(ctx, BotPermissions.ManageShifts)) return;

            await ctx.DeferResponse();

            Shift? deletedShift = Postgres.SelectFirst<Shift>(
                @"DELETE FROM shifts WHERE id = @1 AND guild_id = @2 RETURNING *;",
                [long.Parse(ctx.args[2]), ctx.GuildId]
            );

            if (deletedShift is null)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("shifts.admin.errors.shift_not_found")}");
                return;
            }

            ShiftType? type = (await WhispCache.ShiftTypes.Get(deletedShift.guild_id))?.Find(t => t.id == deletedShift.type);

            await ctx.EditMessage(async m => m.Components = await ShiftAdminMessages.GetMainMessage(deletedShift.guild_id, deletedShift.moderator_id, ulong.Parse(ctx.args[0]), type));
        }
    }
}
