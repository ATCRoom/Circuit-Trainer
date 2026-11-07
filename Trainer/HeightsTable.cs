using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ATCRoom.Sim;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// The heights of the classes at this aerodrome, in Settings › Circuits (the user, 7 Nov; before in the Aircraft classes
    /// window): a row per class, a column per height (Conv, Wide, Close, IP, Break, HK). Shown as altitudes (QNH) or heights
    /// above the runway (QFE). White: a value of this aerodrome (kept in its file above the threshold); grey (an empty box):
    /// the class's altitude here. A box is changed by typing in it (saved when left, or with Enter) or with the mouse wheel
    /// over it (Shift: 5 steps); a white value deleted: the grey one again.
    /// </summary>
    internal sealed class HeightsTable : Grid
    {
        private static readonly Brush GreyBrush = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90));

        /// <summary>The columns: header, tooltip, limits (ft above the runway).</summary>
        private static readonly (string Header, string Tip, double Low, double High)[] Columns =
        [
            ("Conv", "The conventional circuit.", 300, 5000),
            ("Wide", "The wide circuit.", 300, 5000),
            ("Close", "The close pattern (also the overhead's downwind after the break).", 300, 5000),
            ("IP", "The overhead: the initial, from the initial point to the break.", 500, 5000),
            ("Break", "The overhead: the height at the break (empty: the close pattern's).", 300, 5000),
            ("HK", "The High Key of the SFO.", 500, 15000)
        ];

        private sealed class Line
        {
            public PerformanceClass Kind = new();
            public TextBox[] Boxes = new TextBox[Columns.Length];
            public TextBlock[] Greys = new TextBlock[Columns.Length];
            /// <summary>The texts as last saved (a wrong value goes back to it).</summary>
            public string[] Saved = new string[Columns.Length];
        }

        private readonly List<Line> lines = [];
        private AerodromeCircuits? here;
        private double elevation, offset;
        private bool qfeShown;
        private bool filling;

        /// <summary>The heights changed (the aerodrome's file to save): what to say (an error, or null).</summary>
        public Func<string?>? Save { get; set; }
        /// <summary>A message for the panel (null: none).</summary>
        public Action<string?>? Say { get; set; }

        /// <summary>Every line written and not saved yet: saved now (before QNH / QFE changes, the review of 6 Nov).</summary>
        public void CommitAll()
        {
            foreach (Line line in lines.ToList()) Commit(line);
        }

        /// <summary>The table again: the classes, the aerodrome (null: no map), the elevation of the runway in use, QFE.</summary>
        public void Show(IReadOnlyList<PerformanceClass> classes, AerodromeCircuits? aerodrome, double elevationFt, bool qfe)
        {
            filling = true;
            here = aerodrome;
            elevation = double.IsFinite(elevationFt) ? elevationFt : 0;
            offset = qfe ? 0 : elevation;
            qfeShown = qfe;
            Children.Clear();
            RowDefinitions.Clear();
            ColumnDefinitions.Clear();
            lines.Clear();
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            foreach (var _ in Columns) ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Place(new TextBlock { Text = qfe ? "HGT ft" : "ALT ft", Foreground = Theme.Dim, FontSize = 11, VerticalAlignment = VerticalAlignment.Bottom }, 0, 0);
            for (int i = 0; i < Columns.Length; i++)
                Place(new TextBlock { Text = Columns[i].Header, Foreground = Theme.Dim, FontSize = 11, TextAlignment = TextAlignment.Center, ToolTip = Columns[i].Tip }, 0, 1 + i);
            foreach (PerformanceClass kind in classes) AddLine(kind);
            filling = false;
        }

        private void Place(UIElement element, int row, int column)
        {
            SetRow(element, row);
            SetColumn(element, column);
            Children.Add(element);
        }

        private static string Number(double value) => Math.Round(value).ToString("0", CultureInfo.InvariantCulture);

        private ClassCircuit? EntryOf(PerformanceClass kind) =>
            here?.Classes.FirstOrDefault(c => c != null && string.Equals((c.Class ?? "").Trim(), kind.Name, StringComparison.OrdinalIgnoreCase));

        private void AddLine(PerformanceClass kind)
        {
            Line line = new() { Kind = kind.Copy() };
            int at = RowDefinitions.Count;
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Place(new TextBlock { Text = kind.Name, Foreground = Theme.Text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = kind.Name }, at, 0);
            ClassCircuit? entry = EntryOf(kind);
            double?[] own = entry == null ? new double?[Columns.Length]
                : [entry.ConventionalHeightFt ?? entry.CircuitHeightFt, entry.WideHeightFt ?? entry.CircuitHeightFt, entry.CloseHeightFt ?? entry.CircuitHeightFt,
                   entry.InitialHeightFt, entry.BreakHeightFt, entry.HighKeyFt];
            for (int i = 0; i < Columns.Length; i++)
            {
                // (A helicopter: no overhead, no SFO. No map: nothing to change.)
                bool off = here == null || (kind.CanHover && i >= 3);
                TextBox box = new()
                {
                    Text = own[i] is double v && !off ? Number(v + offset) : "",
                    Margin = new Thickness(1),
                    FontSize = 11,
                    TextAlignment = TextAlignment.Right,
                    Background = Brushes.Transparent,
                    Foreground = Brushes.Black,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0, 1, 2, 1),
                    IsEnabled = !off,
                    ToolTip = $"{kind.Name}, {Columns[i].Tip} Empty: the class's (grey). Mouse wheel: ±100 ft (Shift: 500)."
                };
                TextBlock grey = new() { Foreground = GreyBrush, FontSize = 11, TextAlignment = TextAlignment.Right, Margin = new Thickness(1, 1, 4, 1), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
                Border back = new() { Background = off ? Theme.OffBack : Brushes.White, Margin = new Thickness(1) };
                Grid cell = new();
                cell.Children.Add(back);
                cell.Children.Add(grey);
                cell.Children.Add(box);
                int column = i;
                box.TextChanged += (_, _) => ShowGreys(line);
                box.LostFocus += (_, _) => Commit(line);
                box.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Commit(line); e.Handled = true; } };
                box.PreviewMouseWheel += (_, e) => Wheel(line, column, e);
                line.Boxes[i] = box;
                line.Greys[i] = grey;
                line.Saved[i] = box.Text;
                Place(cell, at, 1 + i);
            }
            lines.Add(line);
            ShowGreys(line);
        }

        private static double? Typed(TextBox box) =>
            double.TryParse(box.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value) ? value : null;

        /// <summary>The value flown when a box is empty, as shown (null: none): the class's altitude here (the break: the close of this aerodrome when written).</summary>
        private double? Grey(Line line, int column)
        {
            if (!line.Boxes[column].IsEnabled) return null;
            if (column == 4 && Typed(line.Boxes[2]) is double close) return close;
            PerformanceClass flown = new AerodromeCircuits().Apply(line.Kind, elevation);
            double[] heights = [flown.ConventionalHeightFt, flown.WideHeightFt, flown.CloseHeightFt, flown.OverheadHeightFt, flown.BreakHeightFt, flown.HighKeyFt];
            return heights[column] + offset;
        }

        private void ShowGreys(Line line)
        {
            for (int i = 0; i < Columns.Length; i++)
                line.Greys[i].Text = line.Boxes[i].Text.Trim().Length == 0 && Grey(line, i) is double grey ? Number(grey) : "";
        }

        /// <summary>The wheel over a box (empty: from its grey value): 100 ft up or down (Shift: 500), whole hundreds above the runway; saved.</summary>
        private void Wheel(Line line, int column, MouseWheelEventArgs e)
        {
            TextBox box = line.Boxes[column];
            if (!box.IsEnabled) return;
            double? value = box.Text.Trim().Length == 0 ? Grey(line, column) : Typed(box);
            if (value is not double v) return;
            double by = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 500 : 100;
            double next = Math.Round((v - offset + (e.Delta > 0 ? by : -by)) / 100) * 100;
            next = Math.Clamp(next, Columns[column].Low, Columns[column].High);
            box.Text = Number(next + offset);
            e.Handled = true;
            Commit(line);
        }

        /// <summary>A line written into the aerodrome (every box checked first: a wrong one said and put back), then saved.</summary>
        private void Commit(Line line)
        {
            if (filling || here == null) return;
            if (line.Boxes.Select(b => b.Text).SequenceEqual(line.Saved)) return;
            double?[] heights = new double?[Columns.Length];
            for (int i = 0; i < Columns.Length; i++)
            {
                TextBox box = line.Boxes[i];
                if (!box.IsEnabled || box.Text.Trim().Length == 0) continue;
                var column = Columns[i];
                // (Written as shown: altitudes with QNH.)
                if (Typed(box) is not double shown || shown - offset < column.Low || shown - offset > column.High)
                {
                    Say?.Invoke(string.Format(CultureInfo.InvariantCulture, "{0} {1}: a number from {2:0} to {3:0} ft{4}.", line.Kind.Name, column.Header,
                        column.Low + offset, column.High + offset, qfeShown ? " (height above the runway, QFE)" : " (altitude, QNH)"));
                    box.Text = line.Saved[i];
                    return;
                }
                heights[i] = Math.Round(shown - offset);
            }
            ClassCircuit entry = EntryOf(line.Kind) ?? new ClassCircuit { Class = line.Kind.Name };
            if (!here.Classes.Contains(entry)) here.Classes.Add(entry);
            // (Only the boxes that can be changed: a helicopter's overhead, SFO and Low Key kept.)
            entry.CircuitHeightFt = null;
            entry.ConventionalHeightFt = heights[0];
            entry.WideHeightFt = heights[1];
            entry.CloseHeightFt = heights[2];
            if (line.Boxes[3].IsEnabled) entry.InitialHeightFt = heights[3];
            if (line.Boxes[4].IsEnabled) entry.BreakHeightFt = heights[4];
            if (line.Boxes[5].IsEnabled) entry.HighKeyFt = heights[5];
            if (entry is { CircuitHeightFt: null, ConventionalHeightFt: null, WideHeightFt: null, CloseHeightFt: null, InitialHeightFt: null, BreakHeightFt: null,
                HighKeyFt: null, LowKeyFt: null, Extra: null }) here.Classes.Remove(entry);
            for (int i = 0; i < Columns.Length; i++) line.Saved[i] = line.Boxes[i].Text;
            string? error = Save?.Invoke();
            Say?.Invoke(error ?? $"{line.Kind.Name}: the heights of this aerodrome saved (for the aircraft added from now on).");
        }
    }
}
