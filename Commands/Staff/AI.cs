using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.AI;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.Staff
{
    public class AIRequest : Command
    {
        public override string Name => "AI";
        public override string Description => "Use AI.";
        public override Module Module => Module.Staff;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["ai"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            string message = ctx.args.Join(" ");
            if (string.IsNullOrWhiteSpace(message))
            {
                await ctx.Reply("Please provide a message to send to the AI.");
                return;
            }

            try
            {
                List<string> updates = ["{emoji.loading} Processing..."];
                async Task onUpdate()
                {
                    await ctx.EditResponse(new MessageBuilder()
                    {
                        components = [
                            new ContainerBuilder()
                            {
                                components = [
                                    new TextDisplayBuilder(updates.Join("\n"))
                                ]
                            }
                        ],
                        flags = MessageFlags.IsComponentsV2
                    });
                }
                void updater(string update)
                {
                    updates.Add(update);
                    Task _ = onUpdate();
                }
                Task _ = onUpdate();

                string? response = AIModel.SendMessage(message, $"staff-{ctx.User?.id}",
                    $"""
                    You are talking to: @{ctx.User?.username} ({ctx.User?.id})
                    In the channel: {ctx.message.channel?.name ?? "err"} ({ctx.message.channel?.id})
                    In the server: {ctx.Guild?.name ?? "err"} ({ctx.GuildId})
                    As the bot: {ctx.client.readyData?.user?.username ?? "err"} ({ctx.client.readyData?.user?.id})
                    """,
                    AIModel.AIType.Staff,
                    updater
                );

                await ctx.EditResponse(
                    new MessageBuilder()
                    {
                        components = [
                            new ContainerBuilder()
                            {
                                components = [
                                    new TextDisplayBuilder(response ?? "No response from AI.")
                                ]
                            }
                        ],
                        flags = MessageFlags.IsComponentsV2
                    }
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
