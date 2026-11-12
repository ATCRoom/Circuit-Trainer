using System.Net.Http;
using System.Text.RegularExpressions;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// The METAR of an airport from the internet (as in AuroraPAR v2 Trainer): the public data service of the Aviation
    /// Weather Center (NOAA, https://aviationweather.gov/data/api/), free and without a key, worldwide by ICAO code. Asked
    /// only when the instructor presses Get METAR (never by itself).
    /// </summary>
    internal static class OnlineMetar
    {
        private const string Address = "https://aviationweather.gov/api/data/metar?format=raw&ids=";
        private static readonly Regex Time = new(@"\b(\d{6}Z)\b", RegexOptions.CultureInvariant);

        /// <summary>The newest METAR of the airport (its text), or null (no internet, no answer, no METAR for it).</summary>
        public static async Task<string?> FetchAsync(string icao, string version)
        {
            string code = (icao ?? "").Trim().ToUpperInvariant();
            if (code.Length != 4 || !code.All(char.IsAsciiLetterOrDigit)) return null;
            try
            {
                using HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("CircuitTrainer/" + (version.Length > 0 ? version.Replace(' ', '-') : "0"));
                string text = await http.GetStringAsync(Address + code);
                // One METAR a line (the newest first); "METAR " / "SPECI " in front on some.
                string? line = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
                if (line == null) return null;
                if (line.StartsWith("METAR ", StringComparison.Ordinal) || line.StartsWith("SPECI ", StringComparison.Ordinal)) line = line[6..];
                return line.StartsWith(code, StringComparison.Ordinal) ? line : null;
            }
            catch (Exception)
            {
                // No connection, a timeout, a refused or odd answer: no METAR (never a crash).
                return null;
            }
        }

        /// <summary>The time of a METAR (e.g. 241650Z), or "".</summary>
        public static string TimeOf(string? metar) => metar != null && Time.Match(metar) is { Success: true } m ? m.Groups[1].Value : "";
    }
}
