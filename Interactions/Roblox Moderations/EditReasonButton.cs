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

namespace Whispbot.Interactions.Roblox_Moderations
{
    public class EditReasonButton : InteractionData
    {
        public override string CustomId => "rm_log_editreason";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count < 1) return;

            DBReason? reason = Postgres.SelectFirst<DBReason>(
                "SELECT reason FROM roblox_moderations WHERE guild_id = @1 AND \"case\" = @2",
                [long.Parse(ctx.GuildId), int.Parse(ctx.args[0])]
            );

            ModalBuilder modal = new()
            {
                custom_id = $"rm_modal_editreason {ctx.args[0]}",
                title = "{string.button.rmlog.editreason}",
                components = [
                    new ActionRowBuilder(
                        new TextInputBuilder("{string.title.rmlog.reason}")
                        {
                            custom_id = "reason",
                            required = true,
                            value = reason?.reason ?? ""
                        }
                    )
                ]
            };

            await ctx.ShowModal(modal);
        }
    }

    public class DBReason
    {
        public string? reason;
    }
}
