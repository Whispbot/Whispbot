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
    public class AdminModifyModal : InteractionCommandData
    {
        public override string CustomId => "sa_modify2";
        public override InteractionType Type => InteractionType.ModalSubmit;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count <= 1 || ctx.interaction.Data is not IModalInteractionData data) return;
            if (await ctx.CheckAllowed()) return;

            if (!await WhispPermissions.CheckPermissionsInteraction(ctx, BotPermissions.ManageShifts)) return;

            List<ShiftType>? types = await WhispCache.ShiftTypes.Get(ctx.GuildId.Value);
            if (types is null)
            {
                await ctx.Respond("{emoji.cross} {string.errors.clockin.dbfailed}");
                return;
            }

            ShiftType? type = types.Find(t => ctx.args.Count >= 3 && t.id.ToString() == ctx.args[2]);
            if (type is null && ctx.args.Count > 2)
            {
                await ctx.Respond("{emoji.cross} {string.errors.clockin.typenotfound}");
                return;
            }

            string userId = ctx.args[1];

            string? entered_shift_id = data.Components.FirstOrDefault(c => c.CustomId == "shift_id")?.Value;
            string? shift_id = string.IsNullOrEmpty(entered_shift_id) ? data.Components.FirstOrDefault(c => c.CustomId == "recent_shift")?.Value : entered_shift_id;

            if (shift_id is null)
            {
                await ctx.Respond("{emoji.cross} {string.errors.adminmodify.noshift}");
                return;
            }

            if (!long.TryParse(shift_id, out _))
            {
                await ctx.Respond("{emoji.cross} {string.errors.adminmodify.invalidshiftid}");
                return;
            }

            await ctx.DeferResponse();

            Shift? shift = Postgres.SelectFirst<Shift>(
                @"SELECT * FROM shifts WHERE id = @1 AND guild_id = @2 AND moderator_id = @3;",
                [long.Parse(shift_id), ctx.GuildId.Value, long.Parse(userId)]
            );

            if (shift is null)
            {
                await ctx.Respond("{emoji.cross} {string.errors.adminmodify.shiftnotfound}");
                return;
            }

            await ctx.EditMessage(async m => m.Components = await ShiftAdminMessages.GetModifyMessage(shift, ctx.args[0]));
        }
    }
}
