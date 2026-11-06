using System.Globalization;
using System.IO;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// The runway file of AuroraPAR (runways.par, the user 6 Nov): one line per approach, fields separated by ";": aerodrome
    /// (ICAO), runway (with the glide path after a space, e.g. "14 3.0"), true heading, threshold elevation (ft), threshold
    /// latitude and longitude (degrees), length (m), width, glide path, TCH, MDH, distance. Only what places a runway is read.
    /// </summary>
    internal static class RunwaysPar
    {
        internal sealed record Threshold(string Runway, double Latitude, double Longitude, double HeadingTrue, double ElevationFt, double LengthM);

        /// <summary>The thresholds of an aerodrome (each runway once: the first line of it), in the order of the file.</summary>
        public static List<Threshold> Read(string path, string icao)
        {
            List<Threshold> found = [];
            foreach (string line in File.ReadLines(path))
            {
                string[] f = line.Split(';', StringSplitOptions.TrimEntries);
                if (f.Length < 7 || !string.Equals(f[0], icao, StringComparison.OrdinalIgnoreCase)) continue;
                string runway = f[1].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToUpperInvariant() ?? "";
                if (runway.Length == 0 || found.Any(t => t.Runway == runway)) continue;
                if (Number(f[2]) is double heading && Number(f[3]) is double elevation && Number(f[4]) is double latitude
                    && Number(f[5]) is double longitude && Number(f[6]) is double length && Math.Abs(latitude) <= 90 && Math.Abs(longitude) <= 180)
                    found.Add(new Threshold(runway, latitude, longitude, heading, elevation, length));
            }
            return found;
        }

        private static double? Number(string text) =>
            double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value) ? value : null;
    }
}
