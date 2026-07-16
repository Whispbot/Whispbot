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
    public class DeleteConfirm : InteractionCommandData
    {
        public override string CustomId => "rm_log_delete_confirm";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count < 1) return;

            _ = ctx.DeferResponse();

            await Procedures.DeleteRM(ctx.GuildId.Value, ctx.UserId, int.Parse(ctx.args[0]));
        }
    }
}
