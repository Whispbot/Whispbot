using System;
using System.Collections.Generic;
using System.Text;

namespace Whispbot.Tools.Games.ERLCAPI.Classes
{
    public class ERLCPlayer
    {
        public string Team { get; init; } = default!;
        public string Player { get; init; } = default!;
        public string? Callsign { get; init; }
        public string Permission { get; init; } = default!;
        public float WantedStars { get; init; } = default!;
        public ERLCPlayerLocation Location { get; init; } = default!;
    }

    public class ERLCPlayerLocation
    {
        public float LocationX { get; init; } = -1;
        public float LocationZ { get; init; } = -1;
        public string PostalCode { get; init; } = default!;
        public string StreetName { get; init; } = default!;
        public string BuildingNumber { get; init; } = default!;
    }
}
