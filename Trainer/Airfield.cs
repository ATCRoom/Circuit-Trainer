using System.IO;
using ATCRoom.Maps;
using ATCRoom.Sim;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// The aerodrome of the session: the map, the file of its circuits (runways, sides, heights) and the runway in use.
    /// Everything is drawn and flown in the flat plane of the engine (NM east and north) centred on the threshold of the
    /// runway in use (without a runway: on the middle of the map).
    /// </summary>
    internal sealed class Airfield
    {
        public MapFile? Map { get; private set; }
        public string MapPath { get; private set; } = "";
        /// <summary>The ICAO code (from the map, else its file name): the name of the aerodrome file.</summary>
        public string Code { get; private set; } = "";
        public AerodromeCircuits Circuits { get; private set; } = new();
        public RunwayCircuit? Runway { get; private set; }
        public LocalPlane Plane { get; private set; } = new(45, 10, 0);
        /// <summary>The runway in use: course of the landing (degrees true in the plane) and length (NM); NaN course: none.</summary>
        public double Course { get; private set; } = double.NaN;
        public double LengthNM { get; private set; }
        public double ElevationFt { get; private set; }

        public string AerodromePath => Path.Combine(Paths.AerodromesFolder, (Code.Length > 0 ? Code : "AERODROME") + ".json");

        /// <summary>Opens a map (and the file of its aerodrome, when there is one). Throws with a readable message.</summary>
        public void OpenMap(string path)
        {
            MapFile map = MapFile.Load(path);
            Map = map;
            MapPath = path;
            string code = (map.Airport ?? "").Trim();
            if (code.Length == 0) code = Path.GetFileNameWithoutExtension(path);
            Code = new string(code.ToUpperInvariant().Where(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_').ToArray());
            Circuits = new AerodromeCircuits { Airport = Code };
            if (File.Exists(AerodromePath))
            {
                try
                {
                    Circuits = AerodromeCircuits.Load(AerodromePath);
                }
                catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
                {
                    // A damaged file is kept aside, a new one is started.
                    try { File.Copy(AerodromePath, AerodromePath + ".bad", overwrite: true); } catch (Exception) { }
                    Circuits = new AerodromeCircuits { Airport = Code };
                }
            }
            SetRunway(null);
        }

        /// <summary>The runways whose place is known.</summary>
        public IReadOnlyList<RunwayCircuit> PlacedRunways => Circuits.Runways.Where(r => r.HasPlace).ToList();

        /// <summary>The runway in use (null: none): the plane is centred on its threshold.</summary>
        public void SetRunway(string? name)
        {
            Runway = PlacedRunways.FirstOrDefault(r => string.Equals(r.Runway, name, StringComparison.OrdinalIgnoreCase));
            if (Runway == null)
            {
                Course = double.NaN;
                LengthNM = 0;
                ElevationFt = 0;
                (double latitude, double longitude) = MapCentre();
                Plane = new LocalPlane(latitude, longitude, 0);
                return;
            }
            double lat = Runway.ThresholdLatitude!.Value, lon = Runway.ThresholdLongitude!.Value;
            (double x, double y) = new LocalPlane(lat, lon, 0).FromGeo(Runway.EndLatitude!.Value, Runway.EndLongitude!.Value);
            Course = SimEngine.Normalize(Math.Atan2(x, y) * 180 / Math.PI);
            Plane = new LocalPlane(lat, lon, Course);
            (x, y) = Plane.FromGeo(Runway.EndLatitude!.Value, Runway.EndLongitude!.Value);
            LengthNM = Math.Sqrt(x * x + y * y);
            ElevationFt = Runway.ElevationFt ?? 0;
        }

        /// <summary>
        /// A new runway (or its place given again) from two points of the plane (the threshold, the end), with its elevation,
        /// and the same runway the other way when its name is a runway number and it is not there yet. Saved at once.
        /// </summary>
        public void PlaceRunway(string name, (double X, double Y) threshold, (double X, double Y) end, double elevationFt, double? backElevationFt = null)
        {
            (double tLat, double tLon) = Plane.ToGeo(threshold.X, threshold.Y);
            (double eLat, double eLon) = Plane.ToGeo(end.X, end.Y);
            RunwayCircuit runway = Find(name) ?? Added(name);
            runway.ThresholdLatitude = tLat;
            runway.ThresholdLongitude = tLon;
            runway.EndLatitude = eLat;
            runway.EndLongitude = eLon;
            runway.ElevationFt = elevationFt;
            if (RunwayCircuit.Reciprocal(name) is string other)
            {
                // The same runway the other way: always at the same place (its side is kept when it was there).
                bool existed = Find(other) != null;
                RunwayCircuit back = Find(other) ?? Added(other);
                back.ThresholdLatitude = eLat;
                back.ThresholdLongitude = eLon;
                back.EndLatitude = tLat;
                back.EndLongitude = tLon;
                // (Its own elevation when known: runways.par gives each threshold its own.)
                back.ElevationFt = backElevationFt ?? elevationFt;
                if (!existed)
                {
                    back.Side = runway.Side;
                    back.ConventionalSide = runway.ConventionalSide;
                    back.JetSide = runway.JetSide;
                    back.SfoSide = runway.SfoSide;
                }
            }
        }

        /// <summary>The entry gates in the plane (NM east, north of the threshold in use).</summary>
        public IReadOnlyList<(string Name, double X, double Y)> Gates => Circuits.Gates.Select(g =>
        {
            (double x, double y) = Plane.FromGeo(g.Latitude, g.Longitude);
            return (g.Name, x, y);
        }).ToList();

        /// <summary>A new entry gate at a point of the plane (another one with that name: moved there). Not saved yet.</summary>
        public void AddGate(string name, double x, double y)
        {
            (double lat, double lon) = Plane.ToGeo(x, y);
            Circuits.Gates.RemoveAll(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
            Circuits.Gates.Add(new EntryGate { Name = name, Latitude = lat, Longitude = lon });
        }

        public void RemoveGate(string name) => Circuits.Gates.RemoveAll(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>The runway exits in the plane (the user, 4 Nov: where the landing aircraft leave the runway).</summary>
        public IReadOnlyList<(string Name, double X, double Y)> Exits => (Circuits.Exits ?? []).Select(g =>
        {
            (double x, double y) = Plane.FromGeo(g.Latitude, g.Longitude);
            return (g.Name, x, y);
        }).ToList();

        /// <summary>A new runway exit at a point of the plane (another one with that name: moved there). Not saved yet.</summary>
        public void AddExit(string name, double x, double y)
        {
            (double lat, double lon) = Plane.ToGeo(x, y);
            Circuits.Exits ??= [];
            Circuits.Exits.RemoveAll(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
            Circuits.Exits.Add(new EntryGate { Name = name, Latitude = lat, Longitude = lon });
        }

        /// <summary>The helipads registered at this aerodrome, in the plane (the user, 5 Nov).</summary>
        public IReadOnlyList<(string Name, double X, double Y)> Helipads => (Circuits.Helipads ?? []).Select(g =>
        {
            (double x, double y) = Plane.FromGeo(g.Latitude, g.Longitude);
            return (g.Name, x, y);
        }).ToList();

        public void AddHelipad(string name, double x, double y)
        {
            (double lat, double lon) = Plane.ToGeo(x, y);
            Circuits.Helipads ??= [];
            Circuits.Helipads.RemoveAll(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
            Circuits.Helipads.Add(new EntryGate { Name = name, Latitude = lat, Longitude = lon });
        }

        public void RemoveHelipad(string name) => (Circuits.Helipads ?? []).RemoveAll(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));

        public void RemoveExit(string name) => (Circuits.Exits ?? []).RemoveAll(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Distance (NM) of a point of the plane from the middle of the runway in use (else from the origin).</summary>
        public double FromField(double x, double y)
        {
            double c = double.IsNaN(Course) ? 0 : Course * Math.PI / 180;
            double half = double.IsNaN(Course) ? 0 : LengthNM / 2;
            double mx = half * Math.Sin(c), my = half * Math.Cos(c);
            return Math.Sqrt((x - mx) * (x - mx) + (y - my) * (y - my));
        }

        /// <summary>Removes a runway and the same runway the other way.</summary>
        public void RemoveRunway(string name)
        {
            string? other = RunwayCircuit.Reciprocal(name);
            Circuits.Runways.RemoveAll(r => string.Equals(r.Runway, name, StringComparison.OrdinalIgnoreCase)
                || (other != null && string.Equals(r.Runway, other, StringComparison.OrdinalIgnoreCase)));
        }

        private RunwayCircuit? Find(string name) =>
            Circuits.Runways.FirstOrDefault(r => string.Equals(r.Runway, name, StringComparison.OrdinalIgnoreCase));

        private RunwayCircuit Added(string name)
        {
            RunwayCircuit runway = new() { Runway = name.Trim().ToUpperInvariant() };
            Circuits.Runways.Add(runway);
            return runway;
        }

        /// <summary>Writes the file of the aerodrome. Returns an error message, or null.</summary>
        public string? Save()
        {
            try
            {
                Circuits.Airport = Code;
                Circuits.Save(AerodromePath);
                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return $"The aerodrome file could not be saved: {ex.Message}";
            }
        }

        private (double Latitude, double Longitude) MapCentre()
        {
            List<double[]> all = (Map?.Lines.SelectMany(l => l.Points) ?? [])
                .Concat(Map?.Labels.Select(l => new[] { l.Latitude, l.Longitude }) ?? [])
                .Concat(Map?.Points.Select(p => new[] { p.Latitude, p.Longitude }) ?? []).ToList();
            // The ground of the aerodrome when there is one (the map may reach far away).
            List<double[]> ground = (Map?.Lines.Where(l => MapCategory.IsGround(l.Category)).SelectMany(l => l.Points) ?? []).ToList();
            if (ground.Count == 0 && Map?.Points.FirstOrDefault(p => string.Equals(p.Name, Code, StringComparison.OrdinalIgnoreCase)) is MapPoint airport)
            {
                return (airport.Latitude, airport.Longitude);
            }
            List<double[]> use = ground.Count > 0 ? ground : all;
            if (use.Count == 0) return (45, 10);
            return ((use.Min(p => p[0]) + use.Max(p => p[0])) / 2, (use.Min(p => p[1]) + use.Max(p => p[1])) / 2);
        }
    }
}
