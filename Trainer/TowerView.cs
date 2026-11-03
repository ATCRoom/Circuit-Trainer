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
        /// <summary>Distance of the extended centreline drawn (NM).</summary>
        public double CentrelineNM { get; set; } = 10;
        /// <summary>The entry gates of the aerodrome (the plane, NM).</summary>
        public IReadOnlyList<(string Name, double X, double Y)> Gates { get; set; } = [];
        /// <summary>The map categories drawn.</summary>
        public HashSet<string> CategoriesOn { get; } = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<AircraftState> Traffic { get; private set; } = [];
        public string? Selected { get; private set; }
        /// <summary>A line of text at the top (PAUSED, the simulation time...).</summary>
        public string Status { get; set; } = "";
        /// <summary>What to do first (no map, no runway), in the middle of the view.</summary>
        public string Hint { get; set; } = "";
        public event Action<string?>? SelectionChanged;
        /// <summary>
        /// The circuit chosen with the wheel on the label and not yet ordered (shown with a "?"), or null.
        /// </summary>
        public CircuitType? PendingCircuit { get; set; }

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
        public void ShowLevel(int level)
        {
            double across = level == 1 ? 7 : 40;
            (centreX, centreY) = double.IsNaN(Course) ? (0, 0) : Along(level == 1 ? LengthNM / 2 - 0.5 : -3, 0);
            scale = Math.Max(ActualWidth > 0 ? Math.Min(ActualWidth, ActualHeight) : 700, 100) / across;
            InvalidateVisual();
        }

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
            if (Status.Length > 0)
            {
                FormattedText text = new(Status, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, MapFace, 15, PromptBrush, dip);
                dc.DrawText(text, new Point(12, 10));
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
                            if (point.Name == "INITIAL" && type == CircuitType.Overhead)
                            {
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
                Pen pen = new(brush, 1.4);
                if (a.PerformanceClass.Equals("Helicopter", StringComparison.OrdinalIgnoreCase))
                {
                    StreamGeometry diamond = new();
                    using (StreamGeometryContext c = diamond.Open())
                    {
                        c.BeginFigure(new Point(at.X, at.Y - 5), false, true);
                        c.LineTo(new Point(at.X + 5, at.Y), true, false);
                        c.LineTo(new Point(at.X, at.Y + 5), true, false);
                        c.LineTo(new Point(at.X - 5, at.Y), true, false);
                    }
                    dc.DrawGeometry(null, pen, diamond);
                }
                else
                {
                    dc.DrawEllipse(null, pen, at, 4, 4);
                }
                // Where it will be in 30 s (heading and speed over the ground).
                double vector = a.GroundSpeedKt / 3600 * 30;
                double track = a.TrackTrue * Math.PI / 180;
                dc.DrawLine(pen, at, ToScreen(a.X + vector * Math.Sin(track), a.Y + vector * Math.Cos(track)));
                string line1 = a.Callsign;
                string line2 = $"{Math.Max(0, Math.Round((a.AltitudeFt - ElevationFt) / 100)):000} {Math.Round(a.GroundSpeedKt):0}";
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

        /// <summary>
        /// The label of the selected aircraft, larger, with its commands (step 4b): the fields CCT (the circuit: click = list),
        /// side (click = the other side), NEXT (the end of the final: click = list, wheel = the next one), BRK (overhead: where
        /// it breaks: list, wheel), and the buttons BREAK NOW (overhead), HOLD / START (SFO), ORB (orbit), G/A (go around).
        /// </summary>
        private void DrawSelectedLabel(DrawingContext dc, double dip, AircraftState a, Point at, string heightSpeed)
        {
            fields.Clear();
            FormattedText Text(string text, Brush brush) => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, 13, brush, dip);
            string next = a.NextCircuit is CircuitType n ? $"  >{Short(n)}" : "";
            List<List<(string Field, string Text, bool Button)>> rows =
            [
                [("", $"{a.Callsign}  {a.PerformanceClass}", false)],
                [("", $"{heightSpeed}  {a.Leg}{next}", false)]
            ];
            if (a.Circuit is CircuitType type)
            {
                string circuit = PendingCircuit is CircuitType pending ? $"Ⓒ {Short(pending)}?" : $"Ⓒ {Short(type)}";
                rows.Add([("CIRC", circuit, false), ("SIDE", a.CircuitSide == CircuitSide.Left ? "L" : "R", false), ("NEXT", $"NEXT {EndShort(a.EndAction)}", false)]);
                List<(string, string, bool)> buttons = [];
                if (type == CircuitType.Overhead)
                {
                    buttons.Add(("BRK", $"BRK {BreakShort(a.Break)}", false));
                    buttons.Add(("BREAKNOW", "BREAK NOW", true));
                }
                if (type == CircuitType.Sfo) buttons.Add(a.SfoHold ? ("START", "START", true) : ("HOLD", "HOLD", true));
                if (a.Leg == "DOWNWIND") buttons.Add(("EXTEND", "EXT DWN", true));
                if (a.Leg is "DOWNWIND" or "EXT DOWNWIND") buttons.Add(("BASE", "BASE", true));
                if (a.OrbitForever) buttons.Add(("CONTINUE", "CONT", true));
                else if (a.Leg != "FINAL") buttons.Add(("ORBIT", "ORB", true));
                if (a.Leg == "FINAL") buttons.Add(("GA", "G/A", true));
                rows.Add(buttons);
                // The buttons of the field clicked, under the label: the value now outlined, the others to click.
                List<(string, string, bool)> choices = strip switch
                {
                    "CIRC" => Enum.GetValues<CircuitType>().Select(c => ($"CIRC={c}", Short(c), c != (a.NextCircuit ?? type))).ToList(),
                    "NEXT" => Enum.GetValues<EndAction>().Select(e => ($"NEXT={e}", EndShort(e), e != a.EndAction)).ToList(),
                    "BRK" when type == CircuitType.Overhead => Enum.GetValues<BreakPoint>().Select(b => ($"BRK={b}", BreakShort(b), b != a.Break)).ToList(),
                    _ => []
                };
                if (choices.Count > 0) rows.Add(choices);
            }
            const double pad = 4, gap = 6, lineGap = 3;
            double width = 0, height = pad;
            List<List<(string Field, FormattedText Text, bool Button)>> made = [];
            foreach (var row in rows)
            {
                List<(string, FormattedText, bool)> line = row.Select(r => (r.Field, Text(r.Text, r.Button ? SelectedBrush : r.Field.Length > 0 ? AircraftBrush : SelectedBrush), r.Button)).ToList();
                made.Add(line);
                width = Math.Max(width, line.Sum(t => t.Item2.Width + (t.Item1.Length > 0 ? 8 : 0)) + gap * (line.Count - 1));
                height += line.Max(t => t.Item2.Height) + lineGap;
            }
            width += 2 * pad;
            height += pad - lineGap;
            Point corner = at + (labelOffsets.TryGetValue(a.Callsign, out Vector moved) ? moved : new Vector(14, -height - 8));
            selectedLabel = new Rect(corner, new Size(width, height));
            labelRects[a.Callsign] = selectedLabel;
            dc.DrawLine(new Pen(SelectedBrush, 0.8), at, Nearest(selectedLabel, at));
            dc.DrawRectangle(LabelBack, new Pen(SelectedBrush, 1), selectedLabel);
            double y = corner.Y + pad;
            foreach (var line in made)
            {
                double x = corner.X + pad;
                double lineHeight = line.Max(t => t.Text.Height);
                foreach ((string field, FormattedText text, bool button) in line)
                {
                    if (field.Length > 0)
                    {
                        // A field or button: a box around it, kept for the clicks.
                        Rect area = new(x, y - 1, text.Width + 8, lineHeight + 2);
                        dc.DrawRectangle(button ? FieldBrush : null, new Pen(button ? SelectedBrush : GroundBrush, 0.8), area);
                        dc.DrawText(text, new Point(x + 4, y));
                        fields.Add((area, field));
                        x += area.Width + gap;
                    }
                    else
                    {
                        dc.DrawText(text, new Point(x, y));
                        x += text.Width + gap;
                    }
                }
                y += lineHeight + lineGap;
            }
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
            EndAction.GoAround => "G/A",
            EndAction.LowPass => "LOW",
            EndAction.Departure => "DEP",
            _ => "FS"
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
            if (a.Circuit is not CircuitType type) return "";
            string circuit = type switch
            {
                CircuitType.Close => "CLS",
                CircuitType.Wide => "WIDE",
                CircuitType.Overhead => "OVH",
                CircuitType.Sfo => "SFO",
                _ => "CCT"
            };
            string end = a.EndAction switch
            {
                EndAction.TouchAndGo => "T/G",
                EndAction.GoAround => "G/A",
                EndAction.LowPass => "LOW",
                EndAction.Departure => "DEP",
                _ => "FS"
            };
            string next = a.NextCircuit is CircuitType n ? $">{n.ToString().ToUpperInvariant()[..Math.Min(3, n.ToString().Length)]}" : "";
            return $"{a.Leg} {circuit}{(a.CircuitSide == CircuitSide.Left ? "L" : "R")} {end}{next}".Trim();
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
            if (Selected != null && FieldAt(at) is ("NEXT" or "BRK" or "CIRC") and string field)
            {
                FieldWheel?.Invoke(Selected, field, e.Delta < 0 ? 1 : -1);
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
                if (clicked is "CIRC" or "NEXT" or "BRK")
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
