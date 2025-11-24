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
    public class RobloxEditType : InteractionData
    {
        public override string CustomId => "rm_edittype";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null || ctx.GuildId is null || ctx.args.Count < 1) return;
            if (await ctx.CheckAllowed()) return;

            await ctx.DeferUpdate();

            string? typeid = ctx.interaction.data?.values?.FirstOrDefault();

            if (typeid is null) return;

            List<RobloxModerationType>? types = await WhispCache.RobloxModerationTypes.Get(ctx.GuildId);
            RobloxModerationType? type = types?.FirstOrDefault(t => t.id.ToString() == typeid);

            if (type is null)
            {
                await ctx.SendFollowup("{emoji.cross} {string.error.rmcase.invalidtype}", true);
                return;
            }

            int caseId = int.Parse(ctx.args[1]);

            RobloxModeration? moderation = await Procedures.ChangeRMType(ctx.GuildId, ctx.UserId, type, caseId);

            if (moderation is null)
            {
                await ctx.SendFollowup("{emoji.cross} {string.error.rmcase.notfound}", true);
                return;
            }

            await ctx.EditMessage(new MessageBuilder
            {
                content = $"{{emoji.tick}} {{string.success.rmedit.updated:case={moderation.@case}}}.",
                components = []
            });
        }
    }
}
