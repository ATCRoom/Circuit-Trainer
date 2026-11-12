using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ATCRoom.Maps;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// An aircraft as the student sees it (the user, 6 and 9 Nov): callsign, ICAO type, flight rules (well visible), where it
    /// is, altitude, speed and track; nothing of the instructor's (leg, circuit, orders).
    /// </summary>
    internal sealed record StudentTarget(string Callsign, string Type, bool Ifr, double X, double Y, double AltitudeFt, double GroundSpeedKt,
        double Track, bool OnGround, bool Helicopter, int Formation);

    /// <summary>
    /// The "photo" of the lesson made each second (the user, 6 Nov): what the student sees. Used by the student console on
    /// the same PC, sent on the link between two PCs; later written in the recording.
    /// </summary>
    internal sealed record LessonSnapshot(double Time, DateTime Utc, bool Paused, string Aerodrome, string Runway, string Metar,
        IReadOnlyList<StudentTarget> Traffic,
        IReadOnlyList<(string Callsign, double X, double Y, double AltitudeFt, double SpeedKt, double Track)> Network,
        List<StripData> Coordination, List<StripData> Contact);

    /// <summary>A named point of the aerodrome (gate, exit, helipad) in the plane (NM).</summary>
    internal sealed record LessonPoint(string Name, double X, double Y);

    /// <summary>
    /// The aerodrome as the student's map needs it: which map (its id), the plane (the threshold of the runway in use and its
    /// course), the runway, QNH / QFE, the extended centreline, gates, exits, helipads and the categories of the map shown.
    /// </summary>
    internal sealed class LessonAerodrome
    {
        public string MapId { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Heading { get; set; }
        public double Course { get; set; } = double.NaN;
        public double Length { get; set; }
        public double Elevation { get; set; }
        public bool Qfe { get; set; }
        public double Centreline { get; set; } = 10;
        public double LongFinal { get; set; } = 10;
        public List<LessonPoint> Gates { get; set; } = [];
        public List<LessonPoint> Exits { get; set; } = [];
        public List<LessonPoint> Helipads { get; set; } = [];
        public List<string> Categories { get; set; } = [];

        /// <summary>What changes the map drawn (another map, or another plane).</summary>
        [JsonIgnore]
        public string MapKey => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{MapId}|{Latitude:F7}|{Longitude:F7}|{Heading:F4}");
    }

    /// <summary>A strip on the link: its key as text, and the key of its plan (its notes go with it when it starts).</summary>
    internal sealed record LessonStrip(string Key, string Callsign, string Type, bool Ifr, StripKind Kind, string Entry, string Altitude, string? From);

    /// <summary>The instructor's message of each second (T = "snap").</summary>
    internal sealed class InstructorMessage
    {
        public string T { get; set; } = "snap";
        public double Time { get; set; }
        public DateTime Utc { get; set; }
        public bool Paused { get; set; }
        public string Aerodrome { get; set; } = "";
        public string Runway { get; set; } = "";
        public string Metar { get; set; } = "";
        public LessonAerodrome Ad { get; set; } = new();
        public List<StudentTarget> Traffic { get; set; } = [];
        public List<LessonStrip> Coordination { get; set; } = [];
        public List<LessonStrip> Contact { get; set; } = [];
        /// <summary>Strips whose plan changed a moment ago: [old key, new key] (the student's notes follow them).</summary>
        public List<string[]> Rekeys { get; set; } = [];
        /// <summary>A piece of the map, inside the photo while the student has not got it all (the user, 10 Nov).</summary>
        public MapPiece? Piece { get; set; }
    }

    /// <summary>A piece of the map (T = "map"): the map compressed, in pieces, sent until the student has it all.</summary>
    internal sealed class MapPiece
    {
        public string T { get; set; } = "map";
        public string Id { get; set; } = "";
        public int Index { get; set; }
        public int Count { get; set; }
        public string Data { get; set; } = "";
    }

    /// <summary>The student's message of each second (T = "s"): the map he has, his callsign and VID (for the log).</summary>
    internal sealed class StudentMessage
    {
        public string T { get; set; } = "s";
        public string Map { get; set; } = "";
        public string Callsign { get; set; } = "";
        public string Vid { get; set; } = "";
    }

    /// <summary>The messages of the lesson as JSON, and the map in pieces (the user, 6 Nov: kept in memory only, never saved).</summary>
    internal static class LessonMessages
    {
        public static readonly JsonSerializerOptions Json = new()
        {
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>The size of a piece of the map (characters): small enough for the public relays, two photos a second.</summary>
        private const int PieceSize = 32_000;

        public static string Write<T>(T message) => JsonSerializer.Serialize(message, Json);

        /// <summary>The kind of a message ("snap", "map", "s"), or "" when it cannot be read.</summary>
        public static string KindOf(string json)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                return document.RootElement.TryGetProperty("T", out JsonElement t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
            }
            catch (JsonException)
            {
                return "";
            }
        }

        public static T? Read<T>(string json) where T : class
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json, Json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static LessonStrip ToLink(StripData s) =>
            new(StripBay.KeyText(s.Key), s.Callsign, s.Type, s.Ifr, s.Kind, s.Entry, s.Altitude, s.From != null ? StripBay.KeyText(s.From) : null);

        public static StripData FromLink(LessonStrip s) => new(s.Key, s.Callsign, s.Type, s.Ifr, s.Kind, s.Entry, s.Altitude, s.From);

        /// <summary>A map as pieces to send: its id (from its content) and the pieces (JSON, compressed, as text).</summary>
        public static (string Id, List<string> Pieces) Pack(MapFile map)
        {
            byte[] json = Encoding.UTF8.GetBytes(map.ToJson());
            string id = Convert.ToHexString(SHA256.HashData(json))[..12].ToLowerInvariant();
            using MemoryStream packed = new();
            using (GZipStream zip = new(packed, CompressionLevel.SmallestSize, leaveOpen: true)) zip.Write(json);
            string text = Convert.ToBase64String(packed.ToArray());
            List<string> pieces = [];
            for (int at = 0; at < text.Length; at += PieceSize) pieces.Add(text.Substring(at, Math.Min(PieceSize, text.Length - at)));
            if (pieces.Count == 0) pieces.Add("");
            return (id, pieces);
        }

        /// <summary>The map from all its pieces (null when it cannot be read).</summary>
        public static MapFile? Unpack(IEnumerable<string> pieces)
        {
            try
            {
                byte[] packed = Convert.FromBase64String(string.Concat(pieces));
                using GZipStream zip = new(new MemoryStream(packed), CompressionMode.Decompress);
                using StreamReader reader = new(zip, Encoding.UTF8);
                return MapFile.Parse(reader.ReadToEnd());
            }
            catch (Exception ex) when (ex is FormatException or InvalidDataException or IOException or JsonException)
            {
                return null;
            }
        }
    }
}
