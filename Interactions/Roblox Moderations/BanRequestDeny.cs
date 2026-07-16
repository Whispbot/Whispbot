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
using Whispbot.Tools;

namespace Whispbot.Interactions.Roblox_Moderations
{
    public class BanRequestDeny : InteractionCommandData
    {
        public override string CustomId => "rm_br_deny";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count < 1) return;

            await ctx.DeferResponse();

            var delete = await Procedures.DeleteBanRequest(ulong.Parse(ctx.args[0]), ctx.GuildId.Value, ctx.UserId);

            if (delete.Item1 is null)
            {
                await ctx.SendFollowup($"{{emoji.cross}} {delete.Item2}", ephemeral: true);
            }
        }
    }
}
