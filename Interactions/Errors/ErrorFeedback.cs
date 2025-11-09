using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Interactions.Roblox
{
    public class ErrorFeedback : InteractionData
    {
        public override string CustomId => "error_feedback";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (ctx.UserId is null) return;
            if (await ctx.CheckAllowed()) return;

            await ctx.ShowModal(
                new ModalBuilder
                {
                    title = "Error Feedback",
                    custom_id = "error_feedback_modal"
                }
            );
        }
    }
}
