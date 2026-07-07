using System;
using System.Collections.Generic;
using System.Text;

namespace Whispbot.Tools.Games.ERLC.Classes
{
    public class ERLCVehicle
    {
        public string Name { get; init; } = default!;
        public string Owner { get; init; } = default!;
        public string? Texture { get; init; }
        public string ColorHex { get; init; } = default!;
        public string ColorName { get; init; } = default!;
    }
}
