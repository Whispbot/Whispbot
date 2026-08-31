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
using Whispbot.Extensions;

namespace Whispbot.Interactions.Roblox_Moderations
{
    public class EditTypeButton : InteractionCommandData
    {
        public override string CustomId => "rm_log_edittype";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count < 1) return;

            List<RobloxModerationType>? types = (await WhispCache.RobloxModerationTypes.Get(ctx.GuildId.Value))?.Where(t => !t.is_deleted)?.ToList();
            if (types is null || types.Count == 0)
            {
                await ctx.Respond($"{ctx.Emoji("cross")} {ctx.String("rmod.log.errors.database")}", ephemeral: true);
                return;
            }

            var modal = new ModalBuilder()
                .WithCustomId($"rm_modal_edittype {ctx.args[0]}")
                .WithTitle($"{ctx.String("rmod.log.button.edit_type")}")
                .AddSelectMenu(
                    label: $"{ctx.String("rmod.log.field.type")}",
                    customId: "type",
                    options: [.. types.Select(t => new SelectMenuOptionBuilder().WithLabel(t.name).WithValue(t.id.ToString()).WithDescription(t.triggers.Count > 0 ? t.triggers.Join(", ") : null))],
                    required: true
                )
                .Build();

            await ctx.ShowModal(modal);
        }
    }
}
