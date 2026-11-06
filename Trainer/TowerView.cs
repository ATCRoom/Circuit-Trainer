using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ATCRoom.Maps;
using ATCRoom.Sim;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// The top view of the aerodrome, north up: the map (its categories turned on), the runway in use with its extended
    /// centreline (a tick every NM), the aircraft with their labels and trails. Wheel: zoom; drag: move; click on an
    /// aircraft: select it. A point can be asked with a click (the threshold of a runway, a helipad, a break point).
    /// Everything in the plane of the engine: NM east and north of the threshold.
    /// </summary>
    internal sealed class TowerView : FrameworkElement
    {
        private static readonly Typeface Face = new(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private static readonly Typeface MapFace = new("Segoe UI");
        private static readonly Brush Background_ = Frozen(new SolidColorBrush(Color.FromRgb(0x0E, 0x13, 0x18)));
        private static readonly Brush AircraftBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xE8, 0xF0, 0xF4)));
        private static readonly Brush SelectedBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x4A)));
        private static readonly Brush GroundBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x9A, 0xA4, 0xAC)));
        private static readonly Brush RunwayBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xB8, 0xC0, 0xC6)));
        private static readonly Brush PromptBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x7D, 0xFF, 0x9A)));
        private static readonly Brush RouteBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x90, 0xFF, 0xD2, 0x4A)));

        private readonly List<(string Category, Pen Pen, Brush? Fill, StreamGeometry Shape)> shapes = [];
        private readonly List<(string Category, double X, double Y, string Text, Brush Brush, bool Ground)> labels = [];
        private readonly List<(string Category, double X, double Y, string Name, Brush Brush)> points = [];
        private readonly Dictionary<string, List<Point>> trails = new(StringComparer.OrdinalIgnoreCase);
        private double trailTime = double.NegativeInfinity;

        private double centreX, centreY;
        /// <summary>Pixels per NM.</summary>
        private double scale = 60;
        private Point? panStart;
        private bool panned;
        private double panX, panY;
        private Action<double, double>? pick;
        /// <summary>The fields and buttons of the label of the selected aircraft, where they were drawn (screen), and the whole label.</summary>
        private readonly List<(Rect Area, string Field)> fields = [];
        private Rect selectedLabel = Rect.Empty;
        /// <summary>The field of the label whose buttons are open under it (CIRC, NEXT, BRK), or null.</summary>
        private string? strip;
        /// <summary>Where each label is, from its aircraft (screen, its top left corner), when the user moved it; and where each was drawn.</summary>
        private readonly Dictionary<string, Vector> labelOffsets = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Rect> labelRects = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>What the left button is dragging: the map, a label, the heading of an aircraft (and which one).</summary>
        private enum Drag { None, Map, Label, Heading }
        private Drag drag;
        private string? dragCallsign;
        private Vector grab;
        private Point dragNow;
        private static readonly Brush FieldBrush = Frozen(new SolidColorBrush(Color.FromArgb(0x60, 0x40, 0x4C, 0x58)));
        private static readonly Brush LabelBack = Frozen(new SolidColorBrush(Color.FromArgb(0xD8, 0x14, 0x1C, 0x24)));

        public TowerView()
        {
            ClipToBounds = true;
            Focusable = true;
        }

        private static T Frozen<T>(T item) where T : Freezable
        {
            item.Freeze();
            return item;
        }

        /// <summary>The runway in use: landing course (degrees true, NaN: none) and length (NM).</summary>
        public double Course { get; set; } = double.NaN;
        public double LengthNM { get; set; }
        public double ElevationFt { get; set; }
        /// <summary>QFE: heights above the runway; else (QNH) altitudes (the user, 6 Nov). What is shown = the height + <see cref="HeightOffset"/>.</summary>
        public bool Qfe { get; set; }
        public double HeightOffset => Qfe ? 0 : ElevationFt;
        /// <summary>Distance of the extended centreline drawn (NM).</summary>
        public double CentrelineNM { get; set; } = 10;
        /// <summary>The entry gates of the aerodrome (the plane, NM).</summary>
        public IReadOnlyList<(string Name, double X, double Y)> Gates { get; set; } = [];
        /// <summary>The runway exits (the plane, NM).</summary>
        public IReadOnlyList<(string Name, double X, double Y)> Exits { get; set; } = [];
        /// <summary>The helipads registered at the aerodrome (the user, 5 Nov), drawn as a small circle with its name.</summary>
        public IReadOnlyList<(string Name, double X, double Y)> Helipads { get; set; } = [];
        /// <summary>The map categories drawn.</summary>
        public HashSet<string> CategoriesOn { get; } = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<AircraftState> Traffic { get; private set; } = [];
        public string? Selected { get; private set; }
        /// <summary>A line of text at the top (PAUSED, the simulation time...).</summary>
        public string Status { get; set; } = "";
        /// <summary>The METAR (whole, or only its wind and QNH), always shown at the top (empty: none).</summary>
        public string Metar { get; set; } = "";
        private static readonly Brush MetarBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xC8, 0xD4, 0xDE)));
        /// <summary>What to do first (no map, no runway), in the middle of the view.</summary>
        public string Hint { get; set; } = "";
        public event Action<string?>? SelectionChanged;
        /// <summary>
        /// The circuit chosen with the wheel on the label and not yet ordered (shown with a "?"), or null.
        /// </summary>
        public CircuitType? PendingCircuit { get; set; }
        /// <summary>The height (ft above the field) and the speed (kt) chosen with the wheel on the label and not yet ordered, or null.</summary>
        public double? PendingHeightFt { get; set; }
        public double? PendingSpeedKt { get; set; }
        /// <summary>The speeds offered under the label's speed field for an aircraft (its class), set by the window.</summary>
        public Func<AircraftState, IReadOnlyList<double>>? SpeedChoices { get; set; }
        /// <summary>The heights offered under the label's height field (ft above the field).</summary>
        public static readonly double[] HeightChoices = [500, 700, 1000, 1500, 2000, 2500, 3000];

        /// <summary>
        /// A button of the label of the selected aircraft clicked (callsign, what: SIDE, BREAKNOW, HOLD, START, ORBIT, GA, or a
        /// value chosen in the buttons opened under a field: CIRC=Close, NEXT=TouchAndGo, BRK=Standby, BRK=Point).
        /// </summary>
        public event Action<string, string>? FieldClicked;
        /// <summary>The wheel on a field of the label (callsign, field, +1 / −1).</summary>
        public event Action<string, string, int>? FieldWheel;
        /// <summary>Right click on an aircraft: its menu of all the commands.</summary>
        public event Action<string>? MenuAsked;
        /// <summary>A heading given by dragging from the aircraft on the map (callsign, heading true).</summary>
        public event Action<string, double>? HeadingDragged;

        /// <summary>The map, in the plane of the engine.</summary>
        public void SetMap(MapFile? map, LocalPlane plane)
        {
            shapes.Clear();
            labels.Clear();
            points.Clear();
            trails.Clear();
            if (map != null)
            {
                foreach (MapCategoryInfo category in MapCategory.All)
                {
                    // (Lines without a category: with the boundaries.)
                    List<MapLine> lines = map.Lines.Where(l => (l.Category == category.Id || (category.Id == MapCategory.Boundaries && MapCategory.Find(l.Category) == null))
                        && l.Points.Count >= 2).ToList();
                    if (lines.Count == 0) continue;
                    StreamGeometry geometry = new();
                    using (StreamGeometryContext context = geometry.Open())
                    {
                        foreach (MapLine line in lines)
                        {
                            Point Plane(double[] p)
                            {
                                (double x, double y) = plane.FromGeo(p[0], p[1]);
                                return new Point(x, -y);
                            }
                            context.BeginFigure(Plane(line.Points[0]), category.Area, category.Area);
                            for (int i = 1; i < line.Points.Count; i++) context.LineTo(Plane(line.Points[i]), true, false);
                        }
                    }
                    geometry.Freeze();
                    Color colour = (Color)ColorConverter.ConvertFromString(category.DefaultColour);
                    Color border = category.Area ? Color.FromRgb((byte)Math.Min(255, colour.R + 50), (byte)Math.Min(255, colour.G + 50), (byte)Math.Min(255, colour.B + 50)) : colour;
                    Brush? fill = category.Area ? Frozen(new SolidColorBrush(colour)) : null;
                    // (The pen is in NM: its width is set when drawing.)
                    shapes.Add((category.Id, Frozen(new Pen(Frozen(new SolidColorBrush(border)), 1)), fill, geometry));
                }
                foreach (MapLabel label in map.Labels)
                {
                    (double x, double y) = plane.FromGeo(label.Latitude, label.Longitude);
                    string category = MapCategory.Find(label.Category)?.Id ?? MapCategory.Airports;
                    labels.Add((category, x, y, label.Text, BrushOf(category), MapCategory.IsGround(category)));
                }
                foreach (MapPoint point in map.Points)
                {
                    (double x, double y) = plane.FromGeo(point.Latitude, point.Longitude);
                    string category = MapCategory.Find(point.Category)?.Id ?? MapCategory.Airports;
                    points.Add((category, x, y, point.Name, BrushOf(category)));
                }
            }
            InvalidateVisual();
        }

        private static Brush BrushOf(string category) =>
            MapCategory.Find(category) is MapCategoryInfo info ? Frozen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(info.DefaultColour))) : Brushes.LightGray;

        /// <summary>The traffic now (a copy from the engine): drawn, and a dot of its trail every 4 s of simulation.</summary>
        public void SetTraffic(IReadOnlyList<AircraftState> traffic, double time)
        {
            Traffic = traffic;
            if (time < trailTime || time - trailTime >= 4)
            {
                trailTime = time;
                foreach (AircraftState a in traffic)
                {
                    if (!trails.TryGetValue(a.Callsign, out List<Point>? trail)) trails[a.Callsign] = trail = [];
                    trail.Add(new Point(a.X, a.Y));
                    if (trail.Count > 8) trail.RemoveAt(0);
                }
                foreach (string gone in trails.Keys.Where(k => !traffic.Any(a => string.Equals(a.Callsign, k, StringComparison.OrdinalIgnoreCase))).ToList()) trails.Remove(gone);
            }
            if (Selected != null && !traffic.Any(a => string.Equals(a.Callsign, Selected, StringComparison.OrdinalIgnoreCase))) Select(null);
            InvalidateVisual();
        }

        public void Select(string? callsign)
        {
            if (string.Equals(Selected, callsign, StringComparison.OrdinalIgnoreCase)) return;
            Selected = callsign;
            strip = null;
            PendingCircuit = null;
            PendingHeightFt = PendingSpeedKt = null;
            SelectionChanged?.Invoke(callsign);
            InvalidateVisual();
        }

        /// <summary>The next click on the map gives a point (the plane, NM) to <paramref name="done"/>; Esc cancels.</summary>
        public void AskPoint(string prompt, Action<double, double> done)
        {
            pick = done;
            Status = prompt;
            Cursor = Cursors.Cross;
            Focus();
            InvalidateVisual();
        }

        public bool Picking => pick != null;

        public void CancelPick()
        {
            pick = null;
            Status = "";
            Cursor = Cursors.Arrow;
            InvalidateVisual();
        }

        /// <summary>Level 1: the aerodrome and its circuits (about 7 NM across). Level 2: the area around it (about 40 NM).</summary>
        /// <summary>The distance of the long final (NM): L2 is centred halfway to it.</summary>
        public double LongFinalNM { get; set; } = 10;

        /// <summary>The default views (the user, 4 Nov): L1 10 NM across the aerodrome, L2 20 NM, halfway to the long final.</summary>
        public void ShowLevel(int level)
        {
            double across = level == 1 ? 10 : 20;
            (double x, double y) = double.IsNaN(Course) ? (0, 0) : Along(level == 1 ? LengthNM / 2 - 0.5 : -LongFinalNM / 2 + 1, 0);
            ShowView(x, y, across);
        }

        /// <summary>A view: its centre (the plane, NM) and how many NM across the shorter side of the map.</summary>
        public void ShowView(double x, double y, double acrossNM)
        {
            (centreX, centreY) = (x, y);
            scale = Math.Max(ActualWidth > 0 ? Math.Min(ActualWidth, ActualHeight) : 700, 100) / Math.Clamp(acrossNM, 0.2, 400);
            InvalidateVisual();
        }

        /// <summary>The view now: its centre (the plane, NM) and how many NM across the shorter side.</summary>
        public (double X, double Y, double AcrossNM) CurrentView => (centreX, centreY, Math.Max(ActualWidth > 0 ? Math.Min(ActualWidth, ActualHeight) : 700, 100) / scale);

        /// <summary>A place of the plane at this distance along the runway (NM from the threshold) and to its right.</summary>
        private (double X, double Y) Along(double along, double right)
        {
            double c = Course * Math.PI / 180;
            return (along * Math.Sin(c) + right * Math.Cos(c), along * Math.Cos(c) - right * Math.Sin(c));
        }

        private Point ToScreen(double x, double y) => new(ActualWidth / 2 + (x - centreX) * scale, ActualHeight / 2 - (y - centreY) * scale);

        private (double X, double Y) ToPlane(Point p) => (centreX + (p.X - ActualWidth / 2) / scale, centreY - (p.Y - ActualHeight / 2) / scale);

        protected override void OnRender(DrawingContext dc)
        {
            double width = ActualWidth, height = ActualHeight;
            dc.DrawRectangle(Background_, null, new Rect(0, 0, width, height));
            if (width < 20 || height < 20) return;
            double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            DrawMap(dc, dip);
            DrawRunway(dc);
            DrawGates(dc, dip);
            DrawExits(dc, dip);
            DrawHelipads(dc, dip);
            DrawTraffic(dc, dip);
            if (Hint.Length > 0)
            {
                FormattedText hint = new(Hint, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, MapFace, 16, GroundBrush, dip)
                {
                    MaxTextWidth = Math.Max(100, width - 80),
                    TextAlignment = TextAlignment.Center
                };
                dc.DrawText(hint, new Point(40, height / 2 - hint.Height / 2));
            }
            double top = 10;
            if (Status.Length > 0)
            {
                FormattedText text = new(Status, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, MapFace, 15, PromptBrush, dip);
                dc.DrawText(text, new Point(12, top));
                top += text.Height + 4;
            }
            if (Metar.Length > 0)
            {
                FormattedText metar = new(Metar, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, MapFace, 13, MetarBrush, dip)
                {
                    MaxTextWidth = Math.Max(100, width - 24)
                };
                dc.DrawRectangle(LabelBack, null, new Rect(8, top - 2, Math.Min(width - 16, metar.WidthIncludingTrailingWhitespace + 8), metar.Height + 4));
                dc.DrawText(metar, new Point(12, top));
            }
            DrawScale(dc, dip);
        }

        private void DrawMap(DrawingContext dc, double dip)
        {
            Matrix matrix = new(scale, 0, 0, scale, ActualWidth / 2 - centreX * scale, ActualHeight / 2 + centreY * scale);
            dc.PushTransform(new MatrixTransform(matrix));
            foreach ((string category, Pen pen, Brush? fill, StreamGeometry shape) in shapes)
            {
                if (!CategoriesOn.Contains(category)) continue;
                dc.DrawGeometry(fill, new Pen(pen.Brush, 1.0 / scale), shape);
            }
            dc.Pop();
            Rect view = new(-40, -20, ActualWidth + 80, ActualHeight + 40);
            // The names of the ground only when near (they would cover the circuits); the others when not too many.
            bool near = scale >= 250;
            List<(Point At, string Text, Brush Brush)> texts = [];
            foreach ((string category, double x, double y, string text, Brush brush, bool ground) in labels)
            {
                if (!CategoriesOn.Contains(category) || (ground && !near)) continue;
                Point at = ToScreen(x, y);
                if (view.Contains(at)) texts.Add((at, text, brush));
            }
            List<(Point At, string Name, Brush Brush)> dots = [];
            foreach ((string category, double x, double y, string name, Brush brush) in points)
            {
                if (!CategoriesOn.Contains(category)) continue;
                Point at = ToScreen(x, y);
                if (view.Contains(at)) dots.Add((at, name, brush));
            }
            bool names = texts.Count + dots.Count <= 600;
            foreach ((Point at, string text, Brush brush) in texts)
            {
                if (!names) break;
                FormattedText formatted = new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, MapFace, 10, brush, dip);
                dc.DrawText(formatted, new Point(at.X - formatted.Width / 2, at.Y - formatted.Height / 2));
            }
            foreach ((Point at, string name, Brush brush) in dots)
            {
                dc.DrawEllipse(brush, null, at, 2, 2);
                if (names && name.Length > 0) dc.DrawText(new FormattedText(name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, MapFace, 10, brush, dip), new Point(at.X + 4, at.Y - 6));
            }
        }

        private void DrawRunway(DrawingContext dc)
        {
            if (double.IsNaN(Course)) return;
            (double ex, double ey) = Along(LengthNM, 0);
            Pen runway = new(RunwayBrush, Math.Max(3, 0.03 * scale)) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
            dc.DrawLine(runway, ToScreen(0, 0), ToScreen(ex, ey));
            // The extended centreline before the threshold, a tick every NM (longer every 5).
            (double fx, double fy) = Along(-CentrelineNM, 0);
            Pen line = new(GroundBrush, 1) { DashStyle = new DashStyle([6, 6], 0) };
            dc.DrawLine(line, ToScreen(0, 0), ToScreen(fx, fy));
            Pen tick = new(GroundBrush, 1);
            for (int n = 1; n <= CentrelineNM; n++)
            {
                double half = n % 5 == 0 ? 0.2 : 0.1;
                (double ax, double ay) = Along(-n, -half);
                (double bx, double by) = Along(-n, half);
                dc.DrawLine(tick, ToScreen(ax, ay), ToScreen(bx, by));
            }
        }

        private static readonly Brush GateBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x5D, 0xCA, 0xA5)));

        /// <summary>The entry gates: a triangle and the name.</summary>
        private void DrawGates(DrawingContext dc, double dip)
        {
            Pen pen = new(GateBrush, 1.4);
            foreach ((string name, double x, double y) in Gates)
            {
                Point at = ToScreen(x, y);
                StreamGeometry triangle = new();
                using (StreamGeometryContext c = triangle.Open())
                {
                    c.BeginFigure(new Point(at.X, at.Y - 7), false, true);
                    c.LineTo(new Point(at.X + 6, at.Y + 4), true, false);
                    c.LineTo(new Point(at.X - 6, at.Y + 4), true, false);
                }
                dc.DrawGeometry(null, pen, triangle);
                dc.DrawText(new FormattedText(name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, MapFace, 12, GateBrush, dip), new Point(at.X + 8, at.Y - 8));
            }
        }

        /// <summary>The runway exits: a short bar across the runway edge and the name.</summary>
        private void DrawExits(DrawingContext dc, double dip)
        {
            Pen pen = new(PromptBrush, 2);
            double c = double.IsNaN(Course) ? 0 : Course * Math.PI / 180;
            Vector across = new(Math.Cos(c) * 5, Math.Sin(c) * 5);
            foreach ((string name, double x, double y) in Exits)
            {
                Point at = ToScreen(x, y);
                dc.DrawLine(pen, at - across, at + across);
                dc.DrawText(new FormattedText(name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, MapFace, 11, PromptBrush, dip), new Point(at.X + 6, at.Y + 2));
            }
        }

        private void DrawHelipads(DrawingContext dc, double dip)
        {
            Pen pen = new(PromptBrush, 1.5);
            foreach ((string name, double x, double y) in Helipads)
            {
                Point at = ToScreen(x, y);
                dc.DrawEllipse(null, pen, at, 6, 6);
                dc.DrawText(new FormattedText(name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, MapFace, 11, PromptBrush, dip), new Point(at.X + 8, at.Y - 6));
            }
        }

        /// <summary>The name of the map point (VRP, FIX...) nearest a place of the plane, within 15 pixels, or null.</summary>
        public string? MapPointNear(double x, double y)
        {
            Point at = ToScreen(x, y);
            (string Name, double Distance) best = ("", double.MaxValue);
            foreach ((string category, double px, double py, string name, _) in points)
            {
                if (name.Length == 0 || !CategoriesOn.Contains(category)) continue;
                double d = (ToScreen(px, py) - at).Length;
                if (d < best.Distance) best = (name, d);
            }
            return best.Distance <= 15 ? best.Name : null;
        }

        private void DrawTraffic(DrawingContext dc, double dip)
        {
            fields.Clear();
            selectedLabel = Rect.Empty;
            labelRects.Clear();
            // The route of the selected aircraft still to fly (its circuit), faint, as it will fly it: the 180° turns as
            // half circles, the overhead's initial to the break, the close pattern's climb to the end of the runway.
            if (Traffic.FirstOrDefault(a => a.Callsign == Selected) is AircraftState chosen && chosen.Route is { } route && chosen.RouteNext >= 0 && route.Count > 0)
            {
                Pen pen = new(RouteBrush, 1) { DashStyle = new DashStyle([3, 4], 0) };
                StreamGeometry path = new();
                using (StreamGeometryContext c = path.Open())
                {
                    c.BeginFigure(ToScreen(chosen.X, chosen.Y), false, false);
                    Point from = ToScreen(chosen.X, chosen.Y);
                    for (int i = chosen.RouteNext; i < route.Count; i++)
                    {
                        RoutePoint point = route[i];
                        (double x, double y) = (point.X, point.Y);
                        if (!double.IsNaN(Course) && chosen.Circuit is CircuitType type)
                        {
                            if ((point.Name == "INITIAL" && type == CircuitType.Overhead) || (point.Name is "UPWIND" or "GO AROUND" && type == CircuitType.Close))
                            {
                                // (The close pattern turns to its downwind at its break point too, the user 5 Nov.)
                                double length = Math.Max(LengthNM, 0.3);
                                double breakAt = chosen.Break switch
                                {
                                    BreakPoint.Beginning => 0.05,
                                    BreakPoint.End => length,
                                    BreakPoint.Standby => length + 1,
                                    _ => length / 2
                                };
                                (x, y) = Along(breakAt, 0);
                            }
                            else if (point.Name is "UPWIND" or "GO AROUND" && type is CircuitType.Close or CircuitType.Overhead or CircuitType.Sfo)
                            {
                                (x, y) = Along(Math.Max(LengthNM, 0.3), 0);
                            }
                        }
                        Point to = ToScreen(x, y);
                        double half = (to - from).Length / 2;
                        if (point.Turn180 && half > 1)
                        {
                            c.ArcTo(to, new Size(half, half), 0, false,
                                chosen.CircuitSide == CircuitSide.Left ? SweepDirection.Counterclockwise : SweepDirection.Clockwise, true, false);
                        }
                        else
                        {
                            c.LineTo(to, true, false);
                        }
                        from = to;
                    }
                }
                dc.DrawGeometry(null, pen, path);
            }
            // (The selected one last: its label on top.)
            foreach (AircraftState a in Traffic.OrderBy(t => t.Callsign == Selected))
            {
                bool selected = a.Callsign == Selected;
                bool onGround = a.Contact != GroundContact.Airborne;
                Brush brush = selected ? SelectedBrush : onGround ? GroundBrush : AircraftBrush;
                if (trails.TryGetValue(a.Callsign, out List<Point>? trail))
                {
                    foreach (Point p in trail) dc.DrawEllipse(brush, null, ToScreen(p.X, p.Y), 1.3, 1.3);
                }
                Point at = ToScreen(a.X, a.Y);
                // (A formation: a larger symbol, the user 4 Nov.)
                bool formation = a.Formation is { Count: > 1 };
                double size = formation ? 8 : 4;
                Pen pen = new(brush, formation ? 2.2 : 1.4);
                if (a.PerformanceClass.Equals("Helicopter", StringComparison.OrdinalIgnoreCase))
                {
                    StreamGeometry diamond = new();
                    using (StreamGeometryContext c = diamond.Open())
                    {
                        c.BeginFigure(new Point(at.X, at.Y - size - 1), false, true);
                        c.LineTo(new Point(at.X + size + 1, at.Y), true, false);
                        c.LineTo(new Point(at.X, at.Y + size + 1), true, false);
                        c.LineTo(new Point(at.X - size - 1, at.Y), true, false);
                    }
                    dc.DrawGeometry(null, pen, diamond);
                }
                else
                {
                    dc.DrawEllipse(null, pen, at, size, size);
                }
                // Where it will be in 30 s (heading and speed over the ground).
                double vector = a.GroundSpeedKt / 3600 * 30;
                double track = a.TrackTrue * Math.PI / 180;
                dc.DrawLine(pen, at, ToScreen(a.X + vector * Math.Sin(track), a.Y + vector * Math.Cos(track)));
                string line1 = a.Formation is { Count: > 1 } members ? $"{a.Callsign} F{members.Count}" : a.Callsign;
                string line2 = $"{Math.Max(0, Math.Round((a.AltitudeFt - ElevationFt + HeightOffset) / 100)):000} {Math.Round(a.GroundSpeedKt):0}";
                if (selected)
                {
                    DrawSelectedLabel(dc, dip, a, at, line2);
                    continue;
                }
                string line3 = LegText(a);
                FormattedText text = new($"{line1}\n{line2}\n{line3}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, 12, brush, dip);
                Point corner = at + (labelOffsets.TryGetValue(a.Callsign, out Vector moved) ? moved : new Vector(10, -text.Height - 4));
                Rect area = new(corner, new Size(text.Width, text.Height));
                labelRects[a.Callsign] = area;
                dc.DrawLine(new Pen(brush, 0.6), at, Nearest(area, at));
                dc.DrawText(text, corner);
            }
            // A heading being dragged: the line from the aircraft to the mouse, with the heading.
            if (drag == Drag.Heading && panned && dragCallsign != null && Traffic.FirstOrDefault(t => t.Callsign == dragCallsign) is AircraftState dragged)
            {
                Point from = ToScreen(dragged.X, dragged.Y);
                dc.DrawLine(new Pen(PromptBrush, 1.5), from, dragNow);
                FormattedText heading = new($"{DraggedHeading(from, dragNow):000}", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, 14, PromptBrush, dip);
                dc.DrawText(heading, new Point(dragNow.X + 10, dragNow.Y - 18));
            }
        }

        /// <summary>How a piece of the selected label is drawn (the user, 4 Nov: as in the panel, yellow = chosen now).</summary>
        private enum Look { Text, Tag, Option, On, Command, Off, Thick }

        private static readonly Brush OnText = Frozen(new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2A)));
        private static readonly Brush CommandBack = Frozen(new SolidColorBrush(Color.FromRgb(0x2F, 0x6F, 0x8F)));
        private static readonly Brush CommandEdge = Frozen(new SolidColorBrush(Color.FromRgb(0x5D, 0x9A, 0xC0)));
        private static readonly Brush OffText = Frozen(new SolidColorBrush(Color.FromRgb(0x5F, 0x6B, 0x75)));
        private static readonly Brush OffEdge = Frozen(new SolidColorBrush(Color.FromRgb(0x3B, 0x46, 0x4F)));

        /// <summary>
        /// The label of the selected aircraft (the user, 4 Nov: larger, easy to understand): the callsign and the leg, then a
        /// row for each choice, its name small on the left (HEIGHT / SPEED, PATTERN, SIDE, BREAK, LAND, END): the value now
        /// yellow, the others outlined (a click: that one; the wheel: the next one); under a line the row NOW: the orders
        /// at once, blue (grey: not where it is now). ORB: Shift + click orbits until clicked again (a thick border).
        /// </summary>
        private void DrawSelectedLabel(DrawingContext dc, double dip, AircraftState a, Point at, string heightSpeed)
        {
            fields.Clear();
            List<List<(string Field, string Text, Look Look)>> rows = [];
            void Choices<T>(string tag, string name, IEnumerable<T> values, T now, Func<T, string> text) where T : notnull =>
                rows.Add([("", tag, Look.Tag), .. values.Select(v => ($"{name}={v}", text(v), EqualityComparer<T>.Default.Equals(v, now) ? Look.On : Look.Option))]);
            bool airborne = a.Contact == GroundContact.Airborne;
            string leg = a.Leg == "LANDED" ? $"LANDED{(a.ExpediteTaxi ? " EXP" : "")}{(a.Exit.Length > 0 ? $" EXIT {a.Exit}" : "")}"
                : a.Leg == "MANUAL" || a.Leg.Length == 0 ? $"{(a.Leg.Length > 0 ? a.Leg : "HDG")} {Math.Round(a.TargetHeading) % 360:000}°" : a.Leg;
            string title = a.Formation is { Count: > 1 } team ? $"{a.Callsign} F{team.Count}  {a.PerformanceClass}" : $"{a.Callsign}  {a.PerformanceClass}";
            List<(string Field, string Text, Look Look)> now = [];
            if (a.Leg == "LANDED")
            {
                // On the runway: its speed, EXPEDITE TAXI.
                rows.Add([("", "SPEED", Look.Tag), ("", $"{Math.Round(a.GroundSpeedKt):000}", Look.Option)]);
                now.Add(("EXPTAXI", "EXP TAXI", a.ExpediteTaxi ? Look.On : Look.Command));
            }
            else
            {
                rows.Add([("", Qfe ? "HGT" : "ALT", Look.Tag), ("ALT", HeightText(a), Look.Option), ("", "SPEED", Look.Tag), ("SPD", SpeedText(a), Look.Option)]);
                if (strip == "ALT")
                {
                    rows.Add([("", "", Look.Tag), ("ALT=AUTO", "AUTO", !a.RouteAltitude && a.Route != null && a.Navigation == NavigationMode.Route ? Look.Command : Look.Off),
                        .. HeightChoices.Select(h => ($"ALT={h:0}", $"{h:0}", Look.Option))]);
                }
                if (strip == "SPD")
                {
                    rows.Add([("", "", Look.Tag), ("SPD=AUTO", "AUTO", !a.RouteSpeed && a.Route != null && a.Navigation == NavigationMode.Route ? Look.Command : Look.Off),
                        .. (SpeedChoices?.Invoke(a) ?? []).Select(v => ($"SPD={v:0}", $"{v:0}", Look.Option))]);
                }
                bool manual = a.Leg is "MANUAL" or "GO AROUND" || (a.Circuit == null && !a.Transit);
                if (a.Circuit is CircuitType type && !a.Transit)
                {
                    CircuitType shown = PendingCircuit ?? a.NextCircuit ?? type;
                    // (On the ground and taking off: the conventional or the wide, the user 5 Nov; the others once it flies.)
                    rows.Add([("", "PATTERN", Look.Tag), .. Enum.GetValues<CircuitType>().Select(c => ($"CIRC={c}", Short(c) + (PendingCircuit == c ? "?" : ""),
                        c == shown ? Look.On : airborne || c is CircuitType.Conventional or CircuitType.Wide ? Look.Option : Look.Off))]);
                    Choices("SIDE", "SIDE", new[] { CircuitSide.Left, CircuitSide.Right }, a.NextCircuit != null ? a.NextSide : a.CircuitSide, s => s == CircuitSide.Left ? "L" : "R");
                    if (shown is CircuitType.Overhead or CircuitType.Close) Choices("BREAK", "BRK", Enum.GetValues<BreakPoint>(), a.Break, BreakShort);
                    if (a.Formation is { Count: > 1 } crew)
                    {
                        // Yellow: they land together; the others go around (any of them; from a circuit at most 2 land).
                        rows.Add([("", "LAND", Look.Tag), .. crew.Select((m, i) => ($"LAND={i}", Number(m), a.Lands == null || i >= a.Lands.Count || a.Lands[i] ? Look.On : Look.Option))]);
                    }
                    rows.Add([("", "END", Look.Tag), .. Enum.GetValues<EndAction>().Select(e => ($"NEXT={e}", EndShort(e), e == a.EndAction && !a.HasPad && !a.WantsPad ? Look.On : Look.Option)),
                        // (A helicopter, the user 5 Nov: its final on a helipad.)
                        .. (a.CanHover ? [("PAD=END", a.WantsPad && !a.HasPad ? "HPAD?" : "HPAD", a.HasPad || a.WantsPad ? Look.On : Look.Option)] : new List<(string, string, Look)>()),
                        .. (a.LapsLeft > 1 ? [("", $"{a.LapsLeft} left", Look.Tag)] : new List<(string, string, Look)>())]);
                    // DCT TO (the user, 5 Nov): at once to the downwind, the base, the initial point, the High Key; in flight.
                    if (airborne) rows.Add([("", "DCT TO", Look.Tag), ("DCT=Downwind", "DW", Look.Command), ("DCT=Base", "BASE", Look.Command),
                        ("DCT=InitialPoint", "IP", Look.Command), ("DCT=HighKey", "HK", Look.Command),
                        .. (a.CanHover ? [("PAD=NOW", "HPAD", Look.Command)] : new List<(string, string, Look)>())]);
                    // The orders of where it is now.
                    if ((shown == CircuitType.Overhead && a.Leg == "INITIAL") || (type == CircuitType.Close && a.Leg is "UPWIND" or "GO AROUND")) now.Add(("BREAKNOW", "BREAK NOW", Look.Command));
                    if (type == CircuitType.Sfo && a.Leg is "CLIMB" or "HOLD" or "HIGH KEY") now.Add(a.SfoHold ? ("START", "START SFO", Look.Command) : ("HOLD", "HOLD", Look.Command));
                    if (manual) now.Add(("REJOIN", "REJOIN", Look.Command));
                    now.Add(("EXTEND", "EXT DWN", a.Leg == "DOWNWIND" ? Look.Command : Look.Off));
                    now.Add(("BASE", "BASE", a.Leg is "DOWNWIND" or "EXT DOWNWIND" || (a.Leg == "JOIN" && type is CircuitType.Conventional or CircuitType.Wide or CircuitType.Close) ? Look.Command : Look.Off));
                    bool orbit = airborne && !manual && a.Leg is "DOWNWIND" or "EXT DOWNWIND" or "UPWIND" or "CROSSWIND" or "JOIN" or "CIRCLING";
                    now.Add(a.OrbitForever ? ("CONTINUE", "ORB", Look.Thick) : ("ORBIT", "ORB", orbit ? Look.Command : Look.Off));
                    now.Add(("GA", "G/A", a.Leg == "FINAL" ? Look.Command : Look.Off));
                }
                else
                {
                    // Crossing, or not in a circuit: its orbits, back onto its way.
                    now.Add(a.OrbitForever ? ("CONTINUE", "ORB", Look.Thick) : ("ORBIT", "ORB", a.Leg == "TRANSIT" ? Look.Command : Look.Off));
                    now.Add(("REJOIN", "REJOIN", a.Leg is "MANUAL" or "" ? Look.Command : Look.Off));
                }
                if (a.Formation is { Count: > 1 }) now.Add(("SPLIT", "SPLIT", a.SplitAllowed ? Look.Command : Look.Off));
            }
            const double pad = 6, gap = 4, lineGap = 4, tagWidth = 50;
            FormattedText Text(string text, Look look, double size = 12) => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, look == Look.Tag ? 10 : size,
                look switch
                {
                    Look.Tag => GroundBrush,
                    Look.On => OnText,
                    Look.Command or Look.Thick => Brushes.White,
                    Look.Off => OffText,
                    Look.Text => SelectedBrush,
                    _ => AircraftBrush
                }, dip);
            FormattedText titleText = Text(title, Look.Text, 13), legText = Text(leg, Look.Text, 13);
            double PieceWidth((string Field, string Text, Look Look) piece, FormattedText text) => piece.Look == Look.Tag ? Math.Max(tagWidth, text.Width + 4) : text.Width + 10;
            List<List<((string Field, string Text, Look Look) Piece, FormattedText Text)>> made = rows.Select(r => r.Select(p => (p, Text(p.Text, p.Look))).ToList()).ToList();
            List<((string Field, string Text, Look Look) Piece, FormattedText Text)> nowMade = now.Select(p => (p, Text(p.Text, p.Look))).ToList();
            double RowWidth(IEnumerable<((string Field, string Text, Look Look) Piece, FormattedText Text)> line) =>
                line.Sum(t => PieceWidth(t.Piece, t.Text)) + gap * Math.Max(0, line.Count() - 1);
            double width = Math.Max(titleText.Width + 16 + legText.Width, Math.Max(made.Count > 0 ? made.Max(RowWidth) : 0, nowMade.Count > 0 ? RowWidth(nowMade) + tagWidth + gap : 0));
            double rowHeight = Text("X", Look.Option).Height + 2;
            double height = pad + titleText.Height + lineGap + made.Count * (rowHeight + lineGap) + (nowMade.Count > 0 ? 6 + rowHeight + lineGap : 0) + pad - lineGap;
            width += 2 * pad;
            Point corner = at + (labelOffsets.TryGetValue(a.Callsign, out Vector moved) ? moved : new Vector(14, -height - 8));
            selectedLabel = new Rect(corner, new Size(width, height));
            labelRects[a.Callsign] = selectedLabel;
            dc.DrawLine(new Pen(SelectedBrush, 0.8), at, Nearest(selectedLabel, at));
            dc.DrawRectangle(LabelBack, new Pen(SelectedBrush, 1), selectedLabel);
            double y = corner.Y + pad;
            dc.DrawText(titleText, new Point(corner.X + pad, y));
            dc.DrawText(legText, new Point(corner.X + width - pad - legText.Width, y));
            y += titleText.Height + lineGap;
            void Draw(List<((string Field, string Text, Look Look) Piece, FormattedText Text)> line, double x)
            {
                foreach (((string field, string _, Look look), FormattedText text) in line)
                {
                    double w = look == Look.Tag ? Math.Max(tagWidth, text.Width + 4) : text.Width + 10;
                    Rect area = new(x, y, w, rowHeight);
                    switch (look)
                    {
                        case Look.Tag:
                        case Look.Text:
                            dc.DrawText(text, new Point(x, y + (rowHeight - text.Height) / 2));
                            break;
                        default:
                            Brush? fill = look switch { Look.On => SelectedBrush, Look.Command or Look.Thick => CommandBack, _ => null };
                            Pen edge = look switch
                            {
                                Look.On => new Pen(SelectedBrush, 1),
                                Look.Command => new Pen(CommandEdge, 1),
                                Look.Thick => new Pen(SelectedBrush, 2.5),
                                Look.Off => new Pen(OffEdge, 1),
                                _ => new Pen(GroundBrush, 0.8)
                            };
                            dc.DrawRectangle(fill, edge, area);
                            dc.DrawText(text, new Point(x + 5, y + (rowHeight - text.Height) / 2));
                            // (Grey orders are not where it is now: no click.)
                            if (field.Length > 0 && look != Look.Off) fields.Add((area, field));
                            break;
                    }
                    x += w + gap;
                }
                y += rowHeight + lineGap;
            }
            foreach (var line in made) Draw(line, corner.X + pad);
            if (nowMade.Count > 0)
            {
                y += 2;
                dc.DrawLine(new Pen(OffEdge, 1), new Point(corner.X + pad, y - 3), new Point(corner.X + width - pad, y - 3));
                y += 4;
                Draw([(("", "NOW", Look.Tag), Text("NOW", Look.Tag)), .. nowMade], corner.X + pad);
            }
        }

        /// <summary>
        /// The height in hundreds of feet above the field; with the height given by the instructor and not yet reached (or
        /// chosen with the wheel, with a "?"): 015↑020.
        /// </summary>
        private string HeightText(AircraftState a)
        {
            double now = Math.Max(0, Math.Round((a.AltitudeFt - ElevationFt + HeightOffset) / 100));
            string text = $"{now:000}";
            double? target = PendingHeightFt ?? (!a.RouteAltitude && a.VerticalMode == VerticalMode.Altitude ? a.TargetAltitudeFt - ElevationFt : null);
            if (target is double t && (PendingHeightFt != null || Math.Abs(t - (a.AltitudeFt - ElevationFt)) > 50))
            {
                text += $"{(t > a.AltitudeFt - ElevationFt ? "↑" : "↓")}{Math.Max(0, Math.Round((t + HeightOffset) / 100)):000}{(PendingHeightFt != null ? "?" : "")}";
            }
            return text;
        }

        /// <summary>The ground speed; with the speed given by the instructor and not yet reached (or chosen with the wheel, "?"): 090→110.</summary>
        private string SpeedText(AircraftState a)
        {
            string text = $"{Math.Round(a.GroundSpeedKt):0}";
            double? target = PendingSpeedKt ?? (!a.RouteSpeed ? a.TargetSpeedKt : null);
            if (target is double t && (PendingSpeedKt != null || Math.Abs(t - a.SpeedKt) > 3)) text += $"→{t:0}{(PendingSpeedKt != null ? "?" : "")}";
            return text;
        }

        /// <summary>The number at the end of a callsign (VIPER03: 03), else the callsign.</summary>
        private static string Number(string callsign)
        {
            int digits = 0;
            while (digits < callsign.Length && char.IsAsciiDigit(callsign[callsign.Length - 1 - digits])) digits++;
            return digits > 0 ? callsign[^digits..] : callsign;
        }

        private static string Short(CircuitType type) => type switch
        {
            CircuitType.Close => "CLS",
            CircuitType.Wide => "WIDE",
            CircuitType.Overhead => "OVH",
            CircuitType.Sfo => "SFO",
            _ => "CONV"
        };

        private static string EndShort(EndAction end) => end switch
        {
            EndAction.TouchAndGo => "T/G",
            EndAction.GoAround => "L/A",
            EndAction.LowPass => "L/P",
            EndAction.Departure => "DEP",
            _ => "F/S"
        };

        private static string BreakShort(BreakPoint where) => where switch
        {
            BreakPoint.Beginning => "BEG",
            BreakPoint.End => "END",
            BreakPoint.Point => "PNT",
            BreakPoint.Standby => "STBY",
            _ => "MID"
        };

        private string? FieldAt(Point p) => fields.FirstOrDefault(f => f.Area.Contains(p)).Field;

        /// <summary>The point of a label nearest to its aircraft (where the leader line ends).</summary>
        private static Point Nearest(Rect area, Point at) => new(Math.Clamp(at.X, area.Left, area.Right), Math.Clamp(at.Y, area.Top, area.Bottom));

        /// <summary>The heading (degrees true) from the aircraft to the mouse on the screen (north up).</summary>
        private static double DraggedHeading(Point from, Point to) => SimEngine.Normalize(Math.Round(Math.Atan2(to.X - from.X, from.Y - to.Y) * 180 / Math.PI));

        /// <summary>The aircraft whose symbol is under a point of the screen, or null.</summary>
        private string? SymbolAt(Point p)
        {
            foreach (AircraftState a in Traffic.Reverse())
            {
                if ((p - ToScreen(a.X, a.Y)).Length <= 9) return a.Callsign;
            }
            return null;
        }

        /// <summary>The aircraft whose label is under a point of the screen, or null.</summary>
        private string? LabelAt(Point p) => labelRects.Where(r => r.Value.Contains(p)).Select(r => r.Key).LastOrDefault();

        /// <summary>The third line of the label: the leg, the circuit and its side, what it does at the end of the final.</summary>
        public static string LegText(AircraftState a)
        {
            if (a.Transit) return a.Leg;
            if (a.Leg == "LANDED" && a.Exit.Length > 0) return $"LANDED EXIT {a.Exit}";
            if (a.Circuit is not CircuitType type) return "";
            string circuit = type switch
            {
                CircuitType.Close => "CLS",
                CircuitType.Wide => "WIDE",
                CircuitType.Overhead => "OVH",
                CircuitType.Sfo => "SFO",
                _ => "CONV"
            };
            string end = a.EndAction switch
            {
                EndAction.TouchAndGo => "T/G",
                EndAction.GoAround => "L/A",
                EndAction.LowPass => "L/P",
                EndAction.Departure => "DEP",
                _ => "F/S"
            };
            string next = a.NextCircuit is CircuitType n ? $">{n.ToString().ToUpperInvariant()[..Math.Min(3, n.ToString().Length)]}" : "";
            return $"{a.Leg} {circuit} {(a.CircuitSide == CircuitSide.Left ? "L" : "R")} {end}{next}".Trim();
        }

        private void DrawScale(DrawingContext dc, double dip)
        {
            // 1 NM (or 5 NM when far) at the bottom left.
            double nm = scale >= 40 ? 1 : 5;
            double length = nm * scale;
            Point a = new(14, ActualHeight - 16), b = new(14 + length, ActualHeight - 16);
            Pen pen = new(GroundBrush, 1);
            dc.DrawLine(pen, a, b);
            dc.DrawLine(pen, a, new Point(a.X, a.Y - 5));
            dc.DrawLine(pen, b, new Point(b.X, b.Y - 5));
            dc.DrawText(new FormattedText($"{nm} NM", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, MapFace, 11, GroundBrush, dip), new Point(b.X + 6, a.Y - 9));
        }

        /// <summary>The aircraft under a point of the screen (its symbol or its label), or null.</summary>
        private string? AircraftAt(Point p)
        {
            if (Selected != null && selectedLabel.Contains(p)) return Selected;
            return SymbolAt(p) ?? LabelAt(p);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            Point at = e.GetPosition(this);
            if (Selected != null && FieldAt(at) is string field && (field is "ALT" or "SPD" || field.StartsWith("LAND=", StringComparison.Ordinal)
                || field.StartsWith("CIRC=", StringComparison.Ordinal) || field.StartsWith("NEXT=", StringComparison.Ordinal) || field.StartsWith("BRK=", StringComparison.Ordinal)))
            {
                // (The wheel on a row of choices: the next one; on the height, the speed, LAND: as before.)
                string key = field.StartsWith("LAND=", StringComparison.Ordinal) ? field : field.Split('=')[0];
                FieldWheel?.Invoke(Selected, key, e.Delta < 0 ? 1 : -1);
                e.Handled = true;
                return;
            }
            (double x, double y) = ToPlane(at);
            scale = Math.Clamp(scale * Math.Pow(1.2, e.Delta / 120.0), 2, 20000);
            // The point under the mouse stays under it.
            centreX = x - (at.X - ActualWidth / 2) / scale;
            centreY = y + (at.Y - ActualHeight / 2) / scale;
            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            Focus();
            Point at = e.GetPosition(this);
            panStart = at;
            panned = false;
            (panX, panY) = (centreX, centreY);
            // What a drag would move: the heading (from the symbol), the label (not on its buttons), else the map.
            dragCallsign = null;
            drag = Drag.Map;
            if (pick == null && (Selected == null || FieldAt(at) == null))
            {
                if (SymbolAt(at) is string symbol)
                {
                    drag = Drag.Heading;
                    dragCallsign = symbol;
                }
                else if (LabelAt(at) is string label)
                {
                    drag = Drag.Label;
                    dragCallsign = label;
                    grab = at - labelRects[label].TopLeft;
                }
            }
            CaptureMouse();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (panStart is not Point start || e.LeftButton != MouseButtonState.Pressed) return;
            Point at = e.GetPosition(this);
            if (!panned && (at - start).Length < 5) return;
            panned = true;
            dragNow = at;
            if (drag == Drag.Label && dragCallsign != null && Traffic.FirstOrDefault(t => t.Callsign == dragCallsign) is AircraftState owner)
            {
                // The label where the mouse takes it, kept from its aircraft.
                labelOffsets[owner.Callsign] = (at - grab) - ToScreen(owner.X, owner.Y);
            }
            else if (drag == Drag.Map)
            {
                centreX = panX - (at.X - start.X) / scale;
                centreY = panY + (at.Y - start.Y) / scale;
            }
            InvalidateVisual();
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            Point at = e.GetPosition(this);
            bool click = panStart != null && !panned;
            panStart = null;
            ReleaseMouseCapture();
            Drag was = drag;
            drag = Drag.None;
            if (!click)
            {
                // A heading dragged from an aircraft: given on release.
                if (was == Drag.Heading && dragCallsign != null && Traffic.FirstOrDefault(t => t.Callsign == dragCallsign) is AircraftState dragged)
                {
                    HeadingDragged?.Invoke(dragged.Callsign, DraggedHeading(ToScreen(dragged.X, dragged.Y), at));
                }
                InvalidateVisual();
                return;
            }
            // A click (not a drag): the point asked, else the aircraft under it.
            if (pick is Action<double, double> done)
            {
                (double x, double y) = ToPlane(at);
                CancelPick();
                done(x, y);
                return;
            }
            if (Selected != null && FieldAt(at) is string clicked)
            {
                if (clicked is "ALT" or "SPD")
                {
                    // Its buttons open under the label (closed by a second click).
                    strip = strip == clicked ? null : clicked;
                    InvalidateVisual();
                    return;
                }
                if (clicked.Contains('=')) strip = null;
                FieldClicked?.Invoke(Selected, clicked);
                InvalidateVisual();
                return;
            }
            Select(AircraftAt(at));
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            if (AircraftAt(e.GetPosition(this)) is string callsign)
            {
                Select(callsign);
                MenuAsked?.Invoke(callsign);
                e.Handled = true;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape && Picking)
            {
                CancelPick();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && drag == Drag.Heading)
            {
                // The heading being dragged: not given.
                drag = Drag.None;
                panStart = null;
                ReleaseMouseCapture();
                InvalidateVisual();
                e.Handled = true;
            }
        }
    }
}
