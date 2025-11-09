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
    public class AdminDeleteShiftConfirm : InteractionData
    {
        public override string CustomId => "sa_deleteshift";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count < 3) return;
            if (await ctx.CheckAllowed()) return;

            if (!await WhispPermissions.CheckPermissionsInteraction(ctx, BotPermissions.ManageShifts)) return;

            await ctx.DeferUpdate();

            Shift? deletedShift = Postgres.SelectFirst<Shift>(
                @"DELETE FROM shifts WHERE id = @1 AND guild_id = @2 RETURNING *;",
                [long.Parse(ctx.args[2]), long.Parse(ctx.GuildId)]
            );

            if (deletedShift is null)
            {
                await ctx.Respond("{emoji.cross} {string.errors.adminmodify.shiftnotfound}");
                return;
            }

            ShiftType? type = (await WhispCache.ShiftTypes.Get(deletedShift.guild_id.ToString()))?.Find(t => t.id == deletedShift.type);

            await ctx.EditMessage(await ShiftAdminMessages.GetMainMessage(deletedShift.guild_id.ToString(), deletedShift.moderator_id.ToString(), ctx.args[0], type));
        }
    }
}
