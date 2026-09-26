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

namespace Whispbot.Interactions.Shifts
{
    public class AdminBack : InteractionCommandData
    {
        public override string CustomId => "sa_main";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count <= 1) return;
            if (await ctx.CheckAllowed()) return;

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

            await ctx.DeferResponse();
            await ctx.EditMessage(async m => m.Components = await ShiftAdminMessages.GetMainMessage(ctx.GuildId.Value, ulong.Parse(ctx.args[1]), ulong.Parse(ctx.args[0]), type));
        }
    }
}
