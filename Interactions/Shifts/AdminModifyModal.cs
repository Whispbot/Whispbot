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
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Interactions.Shifts
{
    public class AdminModifyModal : InteractionData
    {
        public override string CustomId => "sa_modify2";
        public override InteractionType Type => InteractionType.ModalSubmit;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count <= 1) return;
            if (await ctx.CheckAllowed()) return;

            if (!await WhispPermissions.CheckPermissionsInteraction(ctx, BotPermissions.ManageShifts)) return;

            List<ShiftType>? types = await WhispCache.ShiftTypes.Get(ctx.GuildId);
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

            string? entered_shift_id = ctx.interaction.GetStringField("shift_id");
            string? shift_id = string.IsNullOrEmpty(entered_shift_id) ? ctx.interaction.GetStringSelectField("recent_shift")?.FirstOrDefault() : entered_shift_id;

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

            await ctx.DeferUpdate();

            Shift? shift = Postgres.SelectFirst<Shift>(
                @"SELECT * FROM shifts WHERE id = @1 AND guild_id = @2 AND moderator_id = @3;",
                [long.Parse(shift_id), long.Parse(ctx.GuildId), long.Parse(userId)]
            );

            if (shift is null)
            {
                await ctx.Respond("{emoji.cross} {string.errors.adminmodify.shiftnotfound}");
                return;
            }

            await ctx.EditMessage(await ShiftAdminMessages.GetModifyMessage(shift, ctx.args[0]));
        }
    }
}
