using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Tools.Disc;
using Whispbot.Tools.Infra;

namespace Whispbot.Commands.Staff
{
    public class Page : Command
    {
        public override string Name => "Page";
        public override string Description => "Page the on call developer";
        public override Module Module => Module.Staff;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => null;
        public override List<SlashCommandArg>? Arguments => null;
        public override List<string> Schema => ["<content:string>"];
        public override List<string> Aliases => ["page"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            string? title = ctx.args.Get("content")?.GetString()?.Split("::")?[0]; // >page title::description
            if (string.IsNullOrEmpty(title))
            {
                await ctx.Reply("{emoji.cross} Please provide a reason.");
                return;
            }

            string? description = ctx.args.Get("content")!.GetString()!.Split("::").Skip(1).Join(" ");
            description += $"\n\nSent by @{ctx.User.Username} ({ctx.UserId})"; // Sign page to avoid annoying fucks abusing

            await ctx.Reply("{emoji.loading} Sending page...");

            var page = await Incident.TriggerEscalation(title, description); // Trigger page

            if (page.Item2 is not null)
            {
                await ctx.EditResponse(m => m.Content = $"{Emojis.Get("cross")} Failed to send page.");
            }
            else if (page.Item1 is not null)
            {
                int numFailed = 0; // Stop updating data if either its failing to get data or everyone has acked
                bool shouldStop = false;
                await ctx.EditResponse(m => m.Content = GetMessageData(page.Item1.escalation, DateTimeOffset.UtcNow, false, ref numFailed, out bool _));

                DateTimeOffset firstUpdate = DateTimeOffset.UtcNow;
                while ((DateTime.UtcNow - firstUpdate).TotalSeconds < 360) // 6 minutes should be enough time to ack or fail
                {
                    Thread.Sleep(10000); // Update every 10 seconds

                    var escalation = await Incident.GetEscalation(page.Item1.escalation.id);

                    if (escalation.Item1 is not null)
                    {
                        await ctx.EditResponse(m => m.Content = GetMessageData(escalation.Item1.escalation, firstUpdate, false, ref numFailed, out bool shouldStop));
                        if (shouldStop) return; // Everyone has acked
                    }
                    else numFailed++;
                }

                Thread.Sleep(5000);

                var finalEscalation = await Incident.GetEscalation(page.Item1.escalation.id);
                if (finalEscalation.Item1 is not null) await ctx.EditResponse(m => m.Content = GetMessageData(finalEscalation.Item1.escalation, firstUpdate, true, ref numFailed, out shouldStop));
            }
        }

        private static string GetMessageData(Incident.IncidentEscalationData escalation, DateTimeOffset firstSent, bool finalUpdate, ref int numFailed, out bool shouldStop)
        {
            StringBuilder users = new();
            Dictionary<string, bool> userAck = [];
            List<Incident.IncidentEventUser> allUsers = [];
            foreach (var ev in escalation.events)
            {
                foreach (var user in ev.users)
                {
                    if (!allUsers.Any(u => u.id == user.id))
                    {
                        allUsers.Add(user); // Users may be spread across events, collect them all
                    }

                    if (!userAck.GetValueOrDefault(user.id, false))
                    {
                        userAck[user.id] = ev.@event == "acked"; // Check if the user has ever acked
                    }
                }
            }

            if (allUsers.Count == 0)
            {
                numFailed++; // No users to page, something went wrong or no one is on call
            } 
            else if (allUsers.Count(u => userAck.GetValueOrDefault(u.id, false)) == allUsers.Count) 
            { 
                numFailed += 3; // Everyone has acked, stop updating
            }

            if (numFailed >= 3) { shouldStop = true; } else { shouldStop = false; }

            foreach (var user in allUsers)
            {
                bool acked = userAck.GetValueOrDefault(user.id, false);
                users.AppendLine($"> {{{(acked ? "emoji.tick" : finalUpdate ? "emoji.cross" : "emoji.loading")}}} {(acked ? "Acknowledged by" : finalUpdate ? "Unable to reach" : "Waiting for")} {user.name} (`{user.email}`)");
            }

            return $"{{emoji.tick}} Sent page successfully.\n{users}\n-# Sent <t:{firstSent.ToUnixTimeSeconds()}:R>, updated <t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>";
        }
    }
}
