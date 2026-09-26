using System;
using System.Collections.Generic;
using System.Text;

namespace Whispbot.Tools.Games.ERLCAPI
{
    public static class ERLCCommands
    {
        public static readonly Dictionary<string, (int, string)> modCommands = new() {
            { "hint",           new (1, "[message]") },
            { "h",              new (1, "[message]") },
            { "m",              new (1, "[message]") },
            { "message",        new (1, "[message]") },
            { "pm",             new (2, "[user] [message]") },
            { "privatemessage", new (2, "[user] [message]") },
            { "kick",           new (1, "[user] (reason)") },
            { "kill",           new (1, "[user]") },
            { "down",           new (1, "[user]") },
            { "refresh",        new (1, "[user]") },
            { "heal",           new (1, "[user]") },
            { "startfire",      new (0, "(location)") },
            { "unwanted",       new (1, "[user]") },
            { "unjail",         new (1, "[user]") },
            { "free",           new (1, "[user]") },
            { "jail",           new (1, "[user]") },
            { "arrest",         new (1, "[user]") },
            { "prty",           new (1, "[length]") },
            { "priority",       new (1, "[length]") },
            { "wanted",         new (1, "[user]") },
            { "time",           new (1, "[time (0-24)]") },
            { "stopfire",       new (0, "") },
            { "respawn",        new (1, "[user]") },
            { "load",           new (1, "[user]") },
            { "pt",             new (1, "[length]") },
            { "peacetime",      new (1, "[length]") },
        };
        public static readonly Dictionary<string, (int, string)> adminCommands = new() {
            { "weather",         new (1, "[weather]") },
            { "mod",             new (1, "[user/id]") },
            { "unmod",           new (1, "[user/id]") },
            { "ban",             new (1, "[user/id]") },
            { "unban",           new (1, "[user/id]") },
            { "loadlayout",      new (1, "[layout]") },
            { "unloadlayout",    new (1, "[layout]") },
            { "shutdown",        new (0, "") }
        };
        public static readonly Dictionary<string, (int, string)> ownerCommands = new() {
            { "admin",             new (1, "[user/id]") },
            { "unadmin",           new (1, "[user/id]") },
        };
    }
}
