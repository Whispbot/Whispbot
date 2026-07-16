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
    public class AdminList : InteractionCommandData
    {
        public override string CustomId => "sa_list";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count <= 3) return;
            if (await ctx.CheckAllowed()) return;

            List<ShiftType>? types = await WhispCache.ShiftTypes.Get(ctx.GuildId.Value);
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

            _ = ctx.DeferResponse();
            await ctx.Respond(components: await ShiftAdminMessages.GetListMessage(ctx.GuildId.Value, ulong.Parse(ctx.args[1]), ulong.Parse(ctx.args[0]), type, page), flags: MessageFlags.ComponentsV2);
        }
    }
}
