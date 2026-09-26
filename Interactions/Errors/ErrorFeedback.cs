using Discord;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;

namespace Whispbot.Interactions.Errors
{
    public class ErrorFeedback : InteractionCommandData
    {
        public override string CustomId => "error_feedback";
        public override InteractionType Type => InteractionType.MessageComponent;
        public override async Task ExecuteAsync(InteractionContext ctx)
        {
            if (await ctx.CheckAllowed()) return;

            await ctx.ShowModal(
                new ModalBuilder()
                .WithTitle("Error Feedback")
                .WithCustomId("error_feedback_modal")
                .Build()
            );
        }
    }
}
