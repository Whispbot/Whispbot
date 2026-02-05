using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Extensions;
using Whispbot.Tools.Infra;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands.Staff
{
    public class Page : Command
    {
        public override string Name => "Page";
        public override string Description => "Page the on call developer";
        public override Module Module => Module.Staff;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["page"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            string title = ctx.args.Join(" ").Split("::")[0]; // >page title::description
            if (string.IsNullOrEmpty(title))
            {
                await ctx.Reply("{emoji.cross} Please provide a reason.");
                return;
            }

            string? description = ctx.args.Join(" ").Split("::").Skip(1).Join(" ");
            description += $"\n\nSent by @{ctx.User?.username} ({ctx.UserId})"; // Sign page to avoid annoying fucks abusing

            var (message, _) = await ctx.Reply("{emoji.loading} Sending page...");

            var page = await Incident.TriggerEscalation(title, description); // Trigger page

            if (message is null) return; // Cant edit message, just return
            if (page.Item2 is not null)
            {
                await message.Edit("{emoji.cross} Failed to send page.".Process());
            }
            else if (page.Item1 is not null)
            {
                int numFailed = 0; // Stop updating data if either its failing to get data or everyone has acked
                await message.Edit(GetMessageData(page.Item1.escalation, DateTimeOffset.UtcNow, false, ref numFailed, out bool _));

                DateTimeOffset firstUpdate = DateTimeOffset.UtcNow;
                while ((DateTime.UtcNow - firstUpdate).TotalSeconds < 360) // 6 minutes should be enough time to ack or fail
                {
                    Thread.Sleep(10000); // Update every 10 seconds

                    var escalation = await Incident.GetEscalation(page.Item1.escalation.id);

                    if (escalation.Item1 is not null)
                    {
                        await message.Edit(GetMessageData(escalation.Item1.escalation, firstUpdate, false, ref numFailed, out bool shouldStop));
                        if (shouldStop) return; // Everyone has acked
                    }
                    else numFailed++;
                }

                Thread.Sleep(5000);

                var finalEscalation = await Incident.GetEscalation(page.Item1.escalation.id);
                if (finalEscalation.Item1 is not null) await message.Edit(GetMessageData(finalEscalation.Item1.escalation, firstUpdate, true, ref numFailed, out bool _));
            }
        }

        private MessageBuilder GetMessageData(Incident.IncidentEscalationData escalation, DateTimeOffset firstSent, bool finalUpdate, ref int numFailed, out bool shouldStop)
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

            return new MessageBuilder($"{{emoji.tick}} Sent page successfully.\n{users}\n-# Sent <t:{firstSent.ToUnixTimeSeconds()}:R>, updated <t:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:R>".Process());
        }
    }
}