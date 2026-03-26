using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Whispbot.Tools;
using YellowMacaroni.Discord.Core;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Commands
{
    public class CommandArguments
    {
        public Dictionary<string, CommandArgument> args = [];

        public CommandArgument? Get(string name)
        {
            return args.GetValueOrDefault(name);
        }

        public int Count => args.Count;
    }

    public class CommandArgument (string name, CommandArgType type)
    {
        public string name = name;
        public CommandArgType type = type;
        private readonly string? _stringValue;
        private readonly User? _userValue;
        private readonly Roblox.RobloxUser? _robloxUser;
        private readonly Channel? _channelValue;
        private readonly Role? _roleValue;
        private readonly int? _intValue;
        private readonly long? _longValue;
        private readonly TimeSpan? _durationValue;

        public CommandArgument(string name, string value) : this(name, CommandArgType.String)
        {
            _stringValue = value;
        }
        public CommandArgument(string name, Roblox.RobloxUser value) : this(name, CommandArgType.RobloxUser)
        {
            _robloxUser = value;
        }
        public CommandArgument(string name, User value) : this(name, CommandArgType.User)
        {
            _userValue = value;
        }
        public CommandArgument(string name, Channel value) : this(name, CommandArgType.Channel)
        {
            _channelValue = value;
        }
        public CommandArgument(string name, Role value) : this(name, CommandArgType.Role)
        {
            _roleValue = value;
        }
        public CommandArgument(string name, int value) : this(name, CommandArgType.String)
        {
            _intValue = value;
        }
        public CommandArgument(string name, long value) : this(name, CommandArgType.String)
        {
            _longValue = value;
        }
        public CommandArgument(string name, TimeSpan value) : this(name, CommandArgType.Duration)
        {
            _durationValue = value;
        }
        public CommandArgument(string name, TimeSpan duration, string str) : this(name, CommandArgType.DurationString)
        {
            _durationValue = duration;
            _stringValue = str;
        }

        public string? GetString() => _stringValue;
        public User? GetUser() => _userValue;
        public Roblox.RobloxUser? GetRobloxUser() => _robloxUser;
        public Channel? GetChannel() => _channelValue;
        public Role? GetRole() => _roleValue;
        public int? GetInt() => _intValue;
        public long? GetLong() => _longValue;
        public TimeSpan? GetDuration() => _durationValue;
    }

    public static partial class ArgParser
    {
        private static readonly Dictionary<string, Func<Message, string, List<string>, bool, Task<(CommandArgument?, string?)>>> _argFunctions = new()
        {
            { "user",       UserArg         },
            { "ruser",      RobloxUserArg   },
            { "duration",   DurationArg     }
        };

        public static async Task<(CommandArguments?, string?)> GetArguments(Message message, Command command, List<string> args)
        {
            var schema = command.Schema;

            CommandArguments arguments = new();

            foreach (var arg in schema)
            {
                var match = CommandSchemaRegex().Match(arg);
                if (!match.Success) continue;

                string name = match.Groups[1].Value;
                string value = match.Groups[2].Value;
                bool isOptional = match.Groups[3].Value == "?";

                if (args.Count == 0)
                {
                    if (isOptional) continue;
                    else return (null, $"Missing required argument '{name}'");
                }

                var func = _argFunctions.GetValueOrDefault(value, DefaultArg);
                var (result, error) = await func(message, name, args, arg == schema.Last());

                if (error is not null) return (null, error);
                if (result is not null) arguments.args.Add(name, result);
            }

            return (arguments, null);
        }

        [GeneratedRegex(@"<([^:]+):([^?]+)(\??)>")]
        private static partial Regex CommandSchemaRegex();

        public static async Task SendArgError(Message message, string error)
        {
            if (message.channel is null) return;
            await message.channel.Send($"err: {error}");
        }

        public static async Task<(CommandArgument?, string?)> DefaultArg(Message _, string name, List<string> args, bool isLast)
        {
            if (!isLast)
            {
                string arg = args[0];
                args.RemoveAt(0);
                return (new CommandArgument(name, arg), null);
            }
            else
            {
                var arg = args.Join(" ");
                args.Clear();
                return (new CommandArgument(name, arg), null);
            }
        }

        public static async Task<(CommandArgument?, string?)> UserArg(Message message, string name, List<string> args, bool isLast)
        {
            string arg = args[0];
            args.RemoveAt(0);

            User? user = await Users.GetUserByString(arg, 3, message.channel?.guild_id);
            if (user is null) return (null, $"Could not find that user.");

            return (new CommandArgument(name, user), null);
        }

        public static async Task<(CommandArgument?, string?)> RobloxUserArg(Message _, string name, List<string> args, bool isLast)
        { 
            string arg = args[0];
            args.RemoveAt(0);

            Roblox.RobloxUser? user = await Roblox.GetUser(arg);
            if (user is null) return (null, $"Could not find that Roblox user.");

            return (new CommandArgument(name, user), null);
        }

        public static async Task<(CommandArgument?, string?)> DurationArg(Message _, string name, List<string> args, bool isLast)
        {
            string arg = args[0];
            args.RemoveAt(0);

            double ms = Time.ConvertStringToMilliseconds(arg);

            TimeSpan duration = TimeSpan.FromMilliseconds(ms);

            return (new CommandArgument(name, duration), null);
        }

        public static async Task<(CommandArgument?, string?)> DurationStringArg(Message _, string name, List<string> args, bool isLast)
        {
            string arg = isLast ? args.Join(" ") : args[0];
            args.RemoveAt(0);

            double ms = Time.ConvertMessageToMilliseconds(arg, out string remaining);

            TimeSpan duration = TimeSpan.FromMilliseconds(ms);

            return (new(name, duration, remaining), null);
        }
    }
}
