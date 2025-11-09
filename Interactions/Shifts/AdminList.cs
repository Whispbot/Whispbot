using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands.Shifts;
using Whispbot.Databases;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Interactions.Shifts
{
    public class AdminList : InteractionData
    {
        public override string CustomId => "sa_list";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count <= 3) return;
            if (await ctx.CheckAllowed()) return;

            List<ShiftType>? types = await WhispCache.ShiftTypes.Get(ctx.GuildId);
            if (types is null)
            {
                await ctx.Respond("{emoji.cross} {string.errors.clockin.dbfailed}");
                return;
            }

            ShiftType? type = types.Find(t => t.id.ToString() == ctx.args[2]);
            if (type is null && ctx.args[2] != "0")
            {
                await ctx.Respond("{emoji.cross} {string.errors.clockin.typenotfound}");
                return;
            }

            int page = int.Parse(ctx.args[3]);

            _ = ctx.DeferUpdate();
            await ctx.EditMessage(await ShiftAdminMessages.GetListMessage(ctx.GuildId, ctx.args[1], ctx.args[0], type, page));
        }
    }
}
