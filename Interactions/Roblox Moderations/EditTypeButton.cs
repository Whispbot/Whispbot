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
    public class EditTypeButton : InteractionData
    {
        public override string CustomId => "rm_log_edittype";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count < 1) return;

            List<RobloxModerationType>? types = (await WhispCache.RobloxModerationTypes.Get(ctx.GuildId))?.Where(t => !t.is_deleted)?.ToList();
            if (types is null || types.Count == 0)
            {
                await ctx.Respond("{emoji.cross} {string.errors.rmlog.dbfailed}", true);
                return;
            }

            ModalBuilder modal = new()
            {
                custom_id = $"rm_modal_edittype {ctx.args[0]}",
                title = "{string.button.rmlog.edittype}",
                components = [
                    new LabelBuilder
                    {
                        label = "{string.title.rmlog.type}",
                        component = new StringSelectBuilder("type")
                        {
                            options = [..types.Select(t => new StringSelectOption { label = t.name, value = t.id.ToString(), description = t.triggers.Count > 0 ? t.triggers.Join() : null })],
                            required = true
                        }
                    }
                ]
            };

            await ctx.ShowModal(modal);
        }
    }
}
