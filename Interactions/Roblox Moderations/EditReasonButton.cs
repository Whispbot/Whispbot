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

namespace Whispbot.Interactions.Roblox_Moderations
{
    public class EditReasonButton : InteractionCommandData
    {
        public override string CustomId => "rm_log_editreason";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count < 1) return;

            DBReason? reason = Postgres.SelectFirst<DBReason>(
                "SELECT reason FROM roblox_moderations WHERE guild_id = @1 AND \"case\" = @2",
                [ctx.GuildId, int.Parse(ctx.args[0])]
            );

            var modal = new ModalBuilder()
                .WithCustomId($"rm_modal_editreason {ctx.args[0]}")
                .WithTitle("{string.button.rmlog.editreason}")
                .AddTextInput(
                    "Reason",
                    customId: "reason",
                    required: true,
                    value: reason?.reason,
                    placeholder: "New reason"
                )
                .Build();

            await ctx.ShowModal(modal);
        }
    }

    public class DBReason
    {
        public string? reason;
    }
}
