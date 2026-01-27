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
    public class AdminWipeShiftsConfirm : InteractionData
    {
        public override string CustomId => "sa_wipeconfirm";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count < 3) return;
            if (await ctx.CheckAllowed()) return;

            if (!await WhispPermissions.CheckPermissionsInteraction(ctx, BotPermissions.ManageShifts)) return;

            await ctx.DeferUpdate();

            string? type_id = ctx.args.Count >= 3 ? ctx.args[2] : null;

            Postgres.Execute(
                @$"DELETE FROM shifts WHERE moderator_id = @1 AND guild_id = @2 {(type_id is not null ? "AND type = @3" : "")};",
                [long.Parse(ctx.args[1]), long.Parse(ctx.GuildId), ..(type_id is not null ? new List<long> { long.Parse(type_id) } : [])]
            );

            ShiftType? type = type_id is not null ? (await WhispCache.ShiftTypes.Get(ctx.GuildId))?.Find(t => t.id.ToString() == type_id) : null;
    
            await ctx.EditMessage(await ShiftAdminMessages.GetMainMessage(ctx.GuildId, ctx.args[1], ctx.args[0], type));
        }
    }
}
