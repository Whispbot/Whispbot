using Discord;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Whispbot.Commands.General
{
    public class About: Command
    {
        public override string Name => "About";
        public override string Description => "View information about the bot.";
        public override Module Module => Module.General;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string>? SlashCommand => ["about"];
        public override List<SlashCommandArg>? Arguments => null;
        public override List<string> Schema => [];
        public override List<string> Aliases => ["about", "info", "botinfo"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            TimeSpan uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();

            await ctx.Reply(
                components: new ComponentBuilderV2()
                    .WithContainer(
                        new ContainerBuilder()
                            .WithSection(
                                new SectionBuilder()
                                    .WithTextDisplay($"# About Whispbot{(Config.isDev ? " [DEV]" : "")}")
                                    .WithTextDisplay("Whispbot is a multipurpose Discord bot built to be a reliable solution for your perfect Discord server.")
                                    .WithAccessory(new ThumbnailBuilder(Config.client!.CurrentUser.GetDisplayAvatarUrl()))
                            )
                            .WithSeparator(SeparatorSpacingSize.Large, true)
                            .WithSection(
                                new SectionBuilder()
                                    .WithTextDisplay($"## System")
                                    .WithAccessory(new ButtonBuilder("Our Host", style: ButtonStyle.Link, url: "https://railway.com?referralCode=whisp"))
                            )
                            .WithTextDisplay(
                                $"**Uptime:** {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m"
                            )
                            .WithSeparator()
                            .WithTextDisplay(
                                $"## Versions" +
                                $"\n**Whisp Version:** {Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]}" +
                                $"\n**Discord API Version:** {DiscordConfig.APIVersion}" +
                                $"\n**Discord Lib Version:** {Assembly.Load("Discord.Net.Core").GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]}" +
                                $"\n**C# Version:** {Environment.Version}"
                            )
                            .WithSection([new TextDisplayBuilder("View our website and configure your server:")], new ButtonBuilder("Our Website", style: ButtonStyle.Link, url: "https://whisp.bot"))
                            .WithSection([new TextDisplayBuilder("Invite Whisp to your server:")], new ButtonBuilder("Invite Whisp", style: ButtonStyle.Link, url: "https://whisp.bot/invite"))
                            .WithSection([new TextDisplayBuilder("Get help from our team:")], new ButtonBuilder("Get Support", style: ButtonStyle.Link, url: "https://whisp.bot/support"))
                            .WithSection([new TextDisplayBuilder("Contribute to Whisp:")], new ButtonBuilder("GitHub Repo", style: ButtonStyle.Link, url: "https://github.com/Whispbot/Whispbot"))
                    )
                    .Build(),
                flags: MessageFlags.ComponentsV2
            );
        }
    }
}

