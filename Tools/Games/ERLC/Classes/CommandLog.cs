using System;
using System.Collections.Generic;
using System.Text;

namespace Whispbot.Tools.Games.ERLC.Classes
{
    public class ERLCCommandLog
    {
        public string Player { get; init; } = default!;
        public string Command { get; init; } = default!;
        public long Timestamp { get; init; } = default!;
    }
}
