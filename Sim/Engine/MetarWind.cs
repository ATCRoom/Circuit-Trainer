using System.Globalization;
using System.Text.RegularExpressions;

namespace ATCRoom.Sim
{
    /// <summary>The wind group of a METAR: 27015KT, 27015G25KT, VRB03KT, 27008MPS, 27030KMH.</summary>
    public static class MetarWind
    {
        private static readonly Regex Group = new(@"\b(\d{3}|VRB)(\d{2,3})(?:G(\d{2,3}))?(KT|MPS|KMH)\b", RegexOptions.CultureInvariant);

        /// <summary>
        /// Reads the wind of a METAR: direction it blows from (degrees true; NaN when variable), speed and gusts above
        /// it (kt). False when the METAR has no wind group.
        /// </summary>
        public static bool TryParse(string? metar, out double fromTrue, out double speedKt, out double gustKt)
        {
            fromTrue = double.NaN;
            speedKt = 0;
            gustKt = 0;
            if (string.IsNullOrEmpty(metar)) return false;
            Match match = Group.Match(metar);
            if (!match.Success) return false;
            double factor = match.Groups[4].Value switch { "MPS" => 1.94384, "KMH" => 0.539957, _ => 1 };
            if (match.Groups[1].Value != "VRB") fromTrue = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) % 360;
            speedKt = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) * factor;
            if (match.Groups[3].Success) gustKt = Math.Max(0, double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) * factor - speedKt);
            return true;
        }

        private static readonly Regex Qnh = new(@"\b(?:Q\d{4}|A\d{4})\b", RegexOptions.CultureInvariant);
        private static readonly Regex Time = new(@"\b\d{6}Z\b", RegexOptions.CultureInvariant);

        /// <summary>
        /// The METAR with only the wind and the QNH (the user, 3 Nov): the station, the time, the wind group (with its
        /// variable directions, 240V300) and the QNH, the rest left out. "" for an empty one.
        /// </summary>
        public static string WindAndQnh(string? metar)
        {
            string text = (metar ?? "").Trim();
            if (text.StartsWith("METAR ", StringComparison.Ordinal) || text.StartsWith("SPECI ", StringComparison.Ordinal)) text = text[6..].Trim();
            if (text.Length == 0) return "";
            List<string> parts = [text.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]];
            if (Time.Match(text) is { Success: true } time) parts.Add(time.Value);
            if (Group.Match(text) is { Success: true } wind)
            {
                parts.Add(wind.Value);
                if (Regex.Match(text[(wind.Index + wind.Length)..], @"^\s+(\d{3}V\d{3})\b") is { Success: true } varying) parts.Add(varying.Groups[1].Value);
            }
            if (Qnh.Match(text) is { Success: true } qnh) parts.Add(qnh.Value);
            return string.Join(' ', parts);
        }
    }
}
