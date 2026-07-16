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

namespace Whispbot.Interactions.Roblox_Moderations
{
    public class RobloxEditType : InteractionCommandData
    {
        public override string CustomId => "rm_edittype";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.GuildId is null || ctx.args.Count < 1 || ctx.interaction.Data is not IComponentInteractionData data) return;
            if (await ctx.CheckAllowed()) return;

            await ctx.DeferResponse();

            string? typeid = data.Value;
            if (typeid is null) return;

            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(ctx.GuildId.Value);
            RobloxModerationType? type = types?.FirstOrDefault(t => t.id.ToString() == typeid);

            if (type is null)
            {
                await ctx.SendFollowup("{emoji.cross} {string.error.rmcase.invalidtype}", ephemeral: true);
                return;
            }

            int caseId = int.Parse(ctx.args[1]);

            RobloxModeration? moderation = await Procedures.ChangeRMType(ctx.GuildId.Value, ctx.UserId, type, caseId);

            if (moderation is null)
            {
                await ctx.SendFollowup("{emoji.cross} {string.error.rmcase.notfound}", ephemeral: true);
                return;
            }

            await ctx.EditMessage(m =>
            {
                m.Content = $"{{emoji.tick}} {{string.success.rmedit.updated:case={moderation.@case}}}.";
                m.Components = MessageComponent.Empty;
            });
        }
    }
}
