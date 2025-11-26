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

namespace Whispbot.Interactions.Roblox_Moderations
{
    public class BanRequestDeny : InteractionData
    {
        public override string CustomId => "rm_br_deny";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count < 1) return;

            await ctx.DeferUpdate();

            var delete = await Procedures.DeleteBanRequest(long.Parse(ctx.args[0]), long.Parse(ctx.GuildId), long.Parse(ctx.UserId));

            if (delete.Item1 is null)
            {
                await ctx.SendFollowup($"{{emoji.cross}} {delete.Item2}");
            }
        }
    }
}
