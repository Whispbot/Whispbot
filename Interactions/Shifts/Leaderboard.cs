using Discord;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Commands.Shifts;
using Whispbot.Databases;

namespace Whispbot.Interactions.Shifts
{
    public class ShiftLeaderboard : InteractionCommandData
    {
        public override string CustomId => "shift_leaderboard";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count < 2) return;
            if (await ctx.CheckAllowed()) return;

            bool goodPage = int.TryParse(ctx.args[1], out int page);

            if (!goodPage)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("errors.invalid_page")}.");
                return;
            }

            await ctx.DeferResponse();

            var (embed, components, error) = await Procedures.GenerateShiftLeaderboard(ctx.GuildId.Value, ctx.UserId, page, ctx.args.Count >= 3 ? ulong.Parse(ctx.args[2]) : null);

            if (error is not null)
            {
                await ctx.Respond(error);
            }
            else if (embed is not null && components is not null)
            {
                await ctx.EditMessage(m => { m.Embed = embed; m.Components = components; });
            }
        }
    }
}
