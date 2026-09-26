using Discord;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.AI;
using Whispbot.Extensions;

namespace Whispbot.Commands.Staff
{
    public class AIRequest : Command
    {
        public override string Name => "AI";
        public override string Description => "Use AI.";
        public override Module Module => Module.Staff;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => null;
        public override List<SlashCommandArg>? Arguments => null;
        public override List<string> Schema => ["<content:string>"];
        public override List<string> Aliases => ["ai"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            string? message = ctx.args.Get("content")?.GetString();
            if (string.IsNullOrWhiteSpace(message))
            {
                await ctx.Reply("Please provide a message to send to the AI.");
                return;
            }

            try
            {
                List<string> updates = [$"{ctx.Emoji("loading")} Processing..."];
                async Task onUpdate()
                {
                    await ctx.EditResponse(
                        components: new ComponentBuilderV2()
                            .WithContainer(new TextDisplayBuilder(updates.Join("\n")))
                            .Build(),
                        flags: MessageFlags.ComponentsV2
                    );
                }
                void updater(string update)
                {
                    updates.Add(update);
                    Task _ = onUpdate();
                }
                Task _ = onUpdate();

                string? response = AIModel.SendMessage(message, $"staff-{ctx.UserId}",
                    $"""
                    You are talking to: @{ctx.User?.Username} ({ctx.UserId})
                    In the channel: {ctx.message!.Channel.Name} ({ctx.message!.Channel.Id})
                    In the server: {ctx.Guild.Name} ({ctx.GuildId})
                    As the bot: {ctx.client.CurrentUser.Username} ({ctx.client.CurrentUser.Id})
                    """,
                    AIModel.AIType.Staff,
                    updater
                );

                await ctx.EditResponse(
                    components: new ComponentBuilderV2()
                        .WithContainer(new TextDisplayBuilder(response ?? "No response from AI."))
                        .Build(),
                    flags: MessageFlags.ComponentsV2
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                await ctx.EditResponse("An error occurred while processing your request. Please try again later.");
            }
        }
    }
}

