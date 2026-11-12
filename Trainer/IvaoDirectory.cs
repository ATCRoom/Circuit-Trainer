using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// The VID of a callsign connected to IVAO (the user, 7 Nov: for the log of the flights): IVAO's public list of who is
    /// connected (Whazzup API v2, refreshed by IVAO every 15 s). Asked rarely (at the connection, then at most every few
    /// minutes while not found), never on its own otherwise.
    /// </summary>
    internal static class IvaoDirectory
    {
        private const string Address = "https://api.ivao.aero/v2/tracker/whazzup";

        /// <summary>What a strip of an IVAO aircraft needs from its flight plan (the user, 9 Nov).</summary>
        internal sealed record FlightPlan(string Type, bool Ifr, string Departure, string Arrival, string Level);

        /// <summary>
        /// The flight plans of the pilots connected now, by callsign (IVAO's public list; asked at most once a minute by the
        /// trainer while IVAO aircraft are near); empty on any problem.
        /// </summary>
        public static async Task<Dictionary<string, FlightPlan>> FlightPlansAsync(string version)
        {
            Dictionary<string, FlightPlan> found = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(15) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("TWRTrainer/" + (version.Length > 0 ? version.Replace(' ', '-') : "0"));
                using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
                await using Stream body = await http.GetStreamAsync(Address, cts.Token);
                using JsonDocument json = await JsonDocument.ParseAsync(body, cancellationToken: cts.Token);
                if (!json.RootElement.TryGetProperty("clients", out JsonElement clients) || clients.ValueKind != JsonValueKind.Object
                    || !clients.TryGetProperty("pilots", out JsonElement pilots) || pilots.ValueKind != JsonValueKind.Array) return found;
                static string Text(JsonElement owner, string name) =>
                    owner.ValueKind == JsonValueKind.Object && owner.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "").Trim() : "";
                foreach (JsonElement pilot in pilots.EnumerateArray())
                {
                    string callsign = Text(pilot, "callsign");
                    if (callsign.Length == 0 || pilot.ValueKind != JsonValueKind.Object || !pilot.TryGetProperty("flightPlan", out JsonElement plan) || plan.ValueKind != JsonValueKind.Object) continue;
                    string type = plan.TryGetProperty("aircraft", out JsonElement aircraft) ? Text(aircraft, "icaoCode") : "";
                    if (type.Length == 0) type = Text(plan, "aircraftId");
                    string rules = Text(plan, "flightRules").ToUpperInvariant();
                    found[callsign] = new FlightPlan(type.ToUpperInvariant(), rules is "I" or "Y", Text(plan, "departureId").ToUpperInvariant(), Text(plan, "arrivalId").ToUpperInvariant(), Text(plan, "level").ToUpperInvariant());
                }
            }
            catch (Exception)
            {
                // No connection, a timeout, an odd answer: no flight plans (the strips show what is known).
            }
            return found;
        }

        /// <summary>The VID of the callsign (ATC, observer or pilot), or null (no internet, not in the list, an odd answer).</summary>
        public static async Task<string?> FindVidAsync(string callsign, string version)
        {
            string wanted = (callsign ?? "").Trim();
            if (wanted.Length == 0) return null;
            try
            {
                using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(15) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("TWRTrainer/" + (version.Length > 0 ? version.Replace(' ', '-') : "0"));
                // (The whole answer within 15 s, not only its start: a stuck download would stop the IVAO traffic.)
                using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
                await using Stream body = await http.GetStreamAsync(Address, cts.Token);
                using JsonDocument json = await JsonDocument.ParseAsync(body, cancellationToken: cts.Token);
                if (!json.RootElement.TryGetProperty("clients", out JsonElement clients) || clients.ValueKind != JsonValueKind.Object) return null;
                foreach (JsonProperty group in clients.EnumerateObject())
                {
                    if (group.Value.ValueKind != JsonValueKind.Array) continue;
                    foreach (JsonElement client in group.Value.EnumerateArray())
                    {
                        if (client.ValueKind != JsonValueKind.Object || !client.TryGetProperty("callsign", out JsonElement name)
                            || name.ValueKind != JsonValueKind.String || !string.Equals(name.GetString(), wanted, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!client.TryGetProperty("userId", out JsonElement id)) return null;
                        return id.ValueKind switch
                        {
                            JsonValueKind.Number => id.GetRawText(),
                            JsonValueKind.String => id.GetString() is string text && text.Trim().Length > 0 ? text.Trim() : null,
                            _ => null
                        };
                    }
                }
                return null;
            }
            catch (Exception)
            {
                // No connection, a timeout, an odd answer: not found (never a crash).
                return null;
            }
        }
    }
}
