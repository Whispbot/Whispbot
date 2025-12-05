using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using YellowMacaroni.Discord.Core;

namespace Whispbot.Commands.General
{
    public class About: Command
    {
        public override string Name => "About";
        public override string Description => "View information about the bot.";
        public override Module Module => Module.General;
        public override bool GuildOnly => false;
        public override List<RateLimit> Ratelimits => [];
        public override List<string> Aliases => ["about", "info", "botinfo"];
        public override List<string> Usage => [];
        public override async Task ExecuteAsync(CommandContext ctx)
        {
            TimeSpan uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();

            await ctx.Reply(
                new MessageBuilder
                {
                    components = [
                        new ContainerBuilder
                        {
                            components = [
                                new SectionBuilder
                                {
                                    components = [
                                        new TextDisplayBuilder($"# About Whispbot{(Config.IsDev ? " [DEV MODE]" : "")}"),
                                        new TextDisplayBuilder("Whispbot is a multipurpose Discord bot built to be a reliable solution for your perfect Discord server.")
                                    ],
                                    accessory = new ThumbnailBuilder($"https://cdn.discordapp.com/avatars/{ctx.client.readyData?.user.id}/{ctx.client.readyData?.user.avatar}.png")
                                },
                                new SeperatorBuilder(true, SeperatorSpacing.Large),
                                new SectionBuilder
                                {
                                    components = [
                                         new TextDisplayBuilder(
                                            $"## System"
                                        )
                                    ],
                                    accessory = new ButtonBuilder { label = "Our Host", url = "https://railway.com?referralCode=whisp" }
                                },
                                new SectionBuilder
                                {
                                    components = [
                                         new TextDisplayBuilder(
                                            $"\n**OS:** {System.Runtime.InteropServices.RuntimeInformation.OSDescription}" +
                                            $"\n**CPU:** {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture} ({Environment.ProcessorCount} cores)" +
                                            $"\n**Uptime:** {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m"
                                        )
                                    ],
                                    accessory = new ButtonBuilder { label = "Our Setup", url = "https://railway.com/project/480a8b6c-8ba4-416e-8113-fe1347d1b921?environmentId=4846519f-3fdb-431d-a477-c21ec8fec8ba&referralCode=whisp" }
                                },
                                new SeperatorBuilder(false),
                                new TextDisplayBuilder(
                                    $"## Versions" +
                                    $"\n**Whisp Version:** V{Config.versionText}" +
                                    $"\n**Discord API Version:** 10" +
                                    $"\n**Discord Lib Version:** {Assembly.Load("YellowMacaroni.Discord").GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]}" +
                                    $"\n**C# Version:** {Environment.Version}"
                                ),
                                new SeperatorBuilder(true, SeperatorSpacing.Large),
                                new SectionBuilder
                                {
                                    components = [ new TextDisplayBuilder("View our website and configure your server:") ],
                                    accessory = new ButtonBuilder { label = "Our Website", url = "https://whisp.bot" }
                                },
                                new SectionBuilder
                                {
                                    components = [ new TextDisplayBuilder("Invite Whisp to your server:") ],
                                    accessory = new ButtonBuilder { label= "Invite Whisp", url = "https://whisp.bot/invite" }
                                },
                                new SectionBuilder
                                {
                                    components = [ new TextDisplayBuilder("Get help from our team:") ],
                                    accessory = new ButtonBuilder { label = "Get Support", url = "https://whisp.bot/support" }
                                },
                                new SectionBuilder
                                {
                                    components = [ new TextDisplayBuilder("Contribute to Whisp:") ],
                                    accessory = new ButtonBuilder { label = "GitHub Repo", url = "https://github.com/Whispbot/Whispbot" }
                                }
                            ]
                        }
                    ],
                    flags = MessageFlags.IsComponentsV2
                }
            );
        }
    }
}
