using Discord;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whispbot.Databases;
using Whispbot.Extensions;

namespace Whispbot.Commands.Staff
{
    public class ResolveError : Command
    {
        public override string Name => "Resolve Error";
        public override string Description => "Resolve an error ID into a user-friendly message.";
        public override Module Module => Module.Staff;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => null;
        public override List<SlashCommandArg>? Arguments => null;
        public override List<string> Schema => ["<error:string>"];
        public override List<string> Aliases => ["error"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            if (ctx.args.Count == 0)
            {
                await ctx.Reply("{emoji.cross} Please provide an error ID to resolve.");
                return;
            }

            string? errorId = ctx.args.Get("error")?.GetString();

            if (String.IsNullOrWhiteSpace(errorId) || errorId.Length != 32)
            {
                await ctx.Reply("{emoji.cross} The provided error ID is not valid. Please provide a valid error ID.");
                return;
            }

            var error = await SentryResolver.ResolveEventId(errorId);

            if (error is null)
            {
                await ctx.Reply("{emoji.cross} Could not find any error with the provided ID.");
                return;
            }

            string eventUrl = $"https://{error.organizationSlug}.sentry.io/issues/{error.@event.groupID}/?project={error.@event.projectID}";

            var containers = new ComponentBuilderV2()
                .WithContainer(
                    new ContainerBuilder()
                        .WithTextDisplay($"**{error.@event.title}**\n-# [View in sentry](<{eventUrl}>)")
                        .WithTextDisplay($"> {error.@event.culprit ?? "unknown culprit"}")
                );

            foreach (var value in error.@event.entries?.Where(entry => entry.data.values is not null).SelectMany(entry => entry.data.values!).Take(5) ?? [])
            {
                containers.WithContainer(
                    new ContainerBuilder()
                        .WithTextDisplay($"```\n{value.type}: {value.value}\n{value.stacktrace.frames.Select(frame => $" at {frame.function} in {(frame.inApp ? frame.absPath : frame.filename)}:{frame.lineNo}:{frame.colNo?.ToString() ?? "??"}").Join("\n")}\n```")
                );
            }

            containers.WithActionRow([new ButtonBuilder(label: "View in Sentry", url: eventUrl, style: ButtonStyle.Link)]);

            await ctx.Reply(
                components: containers.Build(),
                flags: MessageFlags.ComponentsV2
            );
        }
    }
}
