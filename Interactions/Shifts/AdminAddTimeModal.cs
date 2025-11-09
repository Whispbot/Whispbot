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
    public class AdminAddTimeModal : InteractionData
    {
        public override string CustomId => "sa_addtime";
        public override InteractionType Type => InteractionType.ModalSubmit;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count <= 1) return;
            if (await ctx.CheckAllowed()) return;

            if (!await WhispPermissions.CheckPermissionsInteraction(ctx, BotPermissions.ManageShifts)) return;

            string? time = ctx.interaction.GetStringField("time");
            if (time is null)
            {
                await ctx.Respond("{emoji.cross} {string.errors.adminmodify.invalidtime}");
                return;
            }

            double timeMs = Time.ConvertStringToMilliseconds(time);
            if (timeMs <= 0)
            {
                await ctx.Respond("{emoji.cross} {string.errors.adminmodify.invalidtime}");
                return;
            }

            Log.Debug(timeMs.ToString());

            await ctx.DeferUpdate();

            Shift? shift = Postgres.SelectFirst<Shift>(
                @"UPDATE shifts SET end_time = end_time + (@1 * INTERVAL '1 millisecond') WHERE id = @2 AND guild_id = @3 RETURNING *;",
                [timeMs, long.Parse(ctx.args[1]), long.Parse(ctx.GuildId)]
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
