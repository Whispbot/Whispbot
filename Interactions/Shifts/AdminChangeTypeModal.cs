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
    public class AdminChangeTypeModal : InteractionData
    {
        public override string CustomId => "sa_changetype";
        public override InteractionType Type => InteractionType.ModalSubmit;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count < 2) return;
            if (await ctx.CheckAllowed()) return;

            if (!await WhispPermissions.CheckPermissionsInteraction(ctx, BotPermissions.ManageShifts)) return;

            string? shift_id = ctx.args[1];
            string? new_type_id = ctx.interaction.GetStringSelectField("new_type")?.FirstOrDefault();

            if (shift_id is null || new_type_id is null)
            {
                await ctx.Respond("{emoji.cross} {string.errors.adminmodify.noshift}");
                return;
            }

            if (!long.TryParse(new_type_id, out _))
            {
                await ctx.Respond("{emoji.cross} {string.errors.adminmodify.invalidshiftid}");
                return;
            }

            await ctx.DeferUpdate();

            Shift? shift = Postgres.SelectFirst<Shift>(
                @"UPDATE shifts SET type = @1 WHERE id = @2 AND guild_id = @3 RETURNING *;",
                [long.Parse(new_type_id), long.Parse(shift_id), long.Parse(ctx.GuildId)]
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
