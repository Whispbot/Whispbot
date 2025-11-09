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
    public class AdminClockin : InteractionData
    {
        public override string CustomId => "sa_clockin";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count <= 1) return;
            if (await ctx.CheckAllowed()) return;

            if (!await WhispPermissions.CheckPermissionsInteraction(ctx, BotPermissions.ManageShifts)) return;

            string adminId = ctx.args[0];
            string userId = ctx.args[1];
            string? typeId = ctx.args.Count >= 3 ? ctx.args[2] : null;

            List<ShiftType>? types = await WhispCache.ShiftTypes.Get(ctx.GuildId);
            if (types is null)
            {
                await ctx.Respond("{emoji.cross} {string.errors.clockin.dbfailed}");
                return;
            }

            ShiftType? type = types.Find(t => typeId is not null ? t.id.ToString() == typeId : t.is_default);
            if (type is null)
            {
                await ctx.Respond("{emoji.cross} {string.errors.clockin.typenotfound}");
                return;
            }

            await ctx.DeferResponse(true);

            var (shift, errormessage) = await Procedures.Clockin(long.Parse(ctx.GuildId), long.Parse(userId), type, long.Parse(adminId));

            if (errormessage is null)
            {
                _ = ctx.DeleteResponse();
                await ctx.EditMessage(await ShiftAdminMessages.GetMainMessage(ctx.GuildId, userId, adminId, ctx.args.Count > 2 ? type : null));
            }
            else
            {
                await ctx.SendFollowup($"{{emoji.cross}} {errormessage ?? "{string.errors.clockin.failed}"}");
            }
        }
    }
}
