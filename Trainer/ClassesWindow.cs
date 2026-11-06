using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ATCRoom.Sim;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// The Aircraft classes window (the user, 6 Nov; as Aircraft performance of AuroraPAR v2 Trainer): a row per class. On
    /// the left its performance (all aerodromes, performance.json), on the right its heights at this aerodrome (ALT with
    /// QNH, HGT with QFE): white a value of this aerodrome, grey (empty box) the class's. A box is changed by typing in it
    /// or with the mouse wheel over it. Add class, Duplicate, Remove, Reset to defaults; Save checks every value (the
    /// first wrong one is said, nothing is saved) and gives them back in <see cref="Result"/>.
    /// </summary>
    internal sealed class ClassesWindow : Window
    {
        private static readonly Brush PanelBrush = new SolidColorBrush(Color.FromRgb(0x1A, 0x21, 0x28));
        private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(0xD6, 0xE2, 0xEC));
        private static readonly Brush DimBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x98, 0xA4));
        private static readonly Brush GreyBrush = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90));
        private static readonly Brush WarnBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB0, 0x50));

        /// <summary>The performance columns: header, unit, step of the wheel, limits.</summary>
        private static readonly (string Header, string Unit, double Step, double Low, double High)[] PerformanceColumns =
        [
            ("Min", "kt", 5, 0, 400),
            ("Max", "kt", 5, 40, SimEngine.MaxSpeed),
            ("Initial", "kt", 5, 0, SimEngine.MaxSpeed),
            ("Final", "kt", 5, 0, SimEngine.MaxSpeed),
            ("Climb", "ft/min", 100, 200, 8000),
            ("Descent", "ft/min", 100, 200, 8000),
            ("Bank", "°", 1, 10, 60),
            ("Brake", "kt/s", 0.5, 0.5, 15)
        ];

        /// <summary>The height columns: header, limits (ft above the runway).</summary>
        private static readonly (string Header, double Low, double High)[] HeightColumns =
        [
            ("Conv", 300, 5000), ("Wide", 300, 5000), ("Close", 300, 5000), ("IP", 500, 5000), ("Break", 300, 5000), ("HK", 500, 15000)
        ];

        /// <summary>A row of the table: a class being changed.</summary>
        internal sealed class ClassRow
        {
            /// <summary>The class as it was (its other values kept); its name in the files (null: a new class).</summary>
            public PerformanceClass Source = new();
            public string? OriginalName;
            public TextBox Name = null!;
            public TextBox[] Performance = new TextBox[PerformanceColumns.Length];
            public TextBox[] Heights = new TextBox[HeightColumns.Length];
            public TextBlock[] Greys = new TextBlock[HeightColumns.Length];
        }

        /// <summary>What Save gives back: each class (its performance set; name, name in the files) and its heights at this
        /// aerodrome (ft above the runway, null: the class's).</summary>
        internal sealed record Saved(PerformanceClass Class, string? OriginalName, double?[] Heights, bool[] Editable);

        private readonly List<ClassRow> rows = [];
        private readonly Grid table = new();
        private readonly TextBlock message = new() { Foreground = WarnBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        private readonly double offset;
        private readonly bool heightsHere;
        private ClassRow? chosen;

        public List<Saved>? Result { get; private set; }
        /// <summary>The names of the classes when the window opened (those not saved: removed).</summary>
        public IReadOnlyList<string> OpenedWith { get; }

        /// <param name="classes">The classes now.</param>
        /// <param name="here">The heights of this aerodrome (null: no map open, the heights can't be changed).</param>
        /// <param name="heightOffset">Added to the heights to show them (the elevation with QNH, 0 with QFE).</param>
        /// <param name="word">ALT or HGT.</param>
        public ClassesWindow(IEnumerable<PerformanceClass> classes, AerodromeCircuits? here, double heightOffset, string word)
        {
            Title = "Aircraft classes";
            Width = 1080;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = PanelBrush;
            Foreground = TextBrush;
            offset = heightOffset;
            heightsHere = here != null;
            OpenedWith = classes.Select(c => c.Name).ToList();

            StackPanel panel = new() { Margin = new Thickness(12) };
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) });
            foreach (var _ in PerformanceColumns) table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            foreach (var _ in HeightColumns) table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Place(new TextBlock { Text = "Performance (all aerodromes)", Foreground = DimBrush, Margin = new Thickness(2, 0, 0, 2) }, 0, 1, PerformanceColumns.Length);
            Place(new TextBlock
            {
                Text = here != null ? $"Heights at this aerodrome, {word} ft" : "Heights at this aerodrome: open a map first",
                Foreground = DimBrush, Margin = new Thickness(2, 0, 0, 2)
            }, 0, PerformanceColumns.Length + 2, HeightColumns.Length);
            Place(Header("Class", ""), 1, 0);
            for (int i = 0; i < PerformanceColumns.Length; i++) Place(Header(PerformanceColumns[i].Header, PerformanceColumns[i].Unit), 1, 1 + i);
            for (int i = 0; i < HeightColumns.Length; i++) Place(Header(HeightColumns[i].Header, "ft"), 1, PerformanceColumns.Length + 2 + i);
            foreach (PerformanceClass kind in classes)
            {
                ClassCircuit? heights = here?.Classes.FirstOrDefault(c => c != null && string.Equals((c.Class ?? "").Trim(), kind.Name, StringComparison.OrdinalIgnoreCase));
                AddRow(kind, kind.Name, kind.Name, heights);
            }
            // (Many classes, a small screen: the table scrolls, the buttons stay in sight.)
            panel.Children.Add(new ScrollViewer
            {
                Content = table,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = Math.Max(200, SystemParameters.WorkArea.Height - 300)
            });
            panel.Children.Add(message);

            DockPanel edit = new() { Margin = new Thickness(0, 10, 0, 0), LastChildFill = false };
            Button add = Small("Add class");
            add.ToolTip = "A new class (with the values of the Jet), at the end.";
            add.Click += (_, _) => AddRow(Named(PerformanceClass.Defaults(), "Jet"), null, Unique("New class"), null);
            Button duplicate = Small("Duplicate");
            duplicate.ToolTip = "A copy of the chosen class (click its name first), to change it: e.g. a slower Jet.";
            duplicate.Click += (_, _) =>
            {
                if (chosen == null) { message.Text = "Click the name of a class first."; return; }
                if (Read(chosen) is not PerformanceClass copy) return;
                ClassRow from = chosen;
                AddRow(copy, null, Unique(copy.Name + " 2"), null);
                // (Its heights here too.)
                for (int i = 0; i < HeightColumns.Length; i++) rows[^1].Heights[i].Text = from.Heights[i].IsEnabled ? from.Heights[i].Text : "";
            };
            Button remove = Small("Remove");
            remove.ToolTip = "Removes the chosen class (click its name first); the aircraft of that class get another one.";
            remove.Click += (_, _) =>
            {
                if (chosen == null) message.Text = "Click the name of a class first.";
                else if (rows.Count == 1) message.Text = "At least one class is needed.";
                else RemoveRow(chosen);
            };
            Button reset = Small("Reset to defaults");
            reset.ToolTip = "Light, Turboprop, Jet and Helicopter with their first values (the other classes are removed; the heights of this aerodrome kept).";
            reset.Click += (_, _) =>
            {
                if (MessageBox.Show(this, "Put back Light, Turboprop, Jet and Helicopter with their first values? The other classes are removed.", Title,
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                foreach (ClassRow row in rows.ToList()) RemoveRow(row);
                foreach (PerformanceClass kind in PerformanceClass.Defaults())
                {
                    kind.Normalize();
                    ClassCircuit? heights = here?.Classes.FirstOrDefault(c => c != null && string.Equals((c.Class ?? "").Trim(), kind.Name, StringComparison.OrdinalIgnoreCase));
                    AddRow(kind, OpenedWith.FirstOrDefault(n => string.Equals(n, kind.Name, StringComparison.OrdinalIgnoreCase)), kind.Name, heights);
                }
                message.Text = "";
            };
            edit.Children.Add(add);
            edit.Children.Add(duplicate);
            edit.Children.Add(remove);
            DockPanel.SetDock(reset, Dock.Right);
            reset.Margin = new Thickness(0);
            edit.Children.Add(reset);
            panel.Children.Add(edit);
            panel.Children.Add(new TextBlock
            {
                Text = "Type in a box, or turn the mouse wheel over it (Shift: 5 steps). Heights: grey is the class's value (an empty box); delete a white value to go back to grey. "
                    + "Initial speed: of a new aircraft and on the long final; final speed: near the threshold. Saved in performance.json (the performance) and in the aerodrome (the heights): "
                    + "for the aircraft added from now on, those flying keep theirs.",
                Foreground = DimBrush,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 0)
            });
            StackPanel end = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            Button save = Small("Save");
            save.FontWeight = FontWeights.SemiBold;
            save.IsDefault = true;
            save.Click += (_, _) => Save();
            Button cancel = Small("Cancel");
            cancel.IsCancel = true;
            cancel.Margin = new Thickness(0);
            end.Children.Add(save);
            end.Children.Add(cancel);
            panel.Children.Add(end);
            Content = panel;
        }

        private static PerformanceClass Named(List<PerformanceClass> list, string name)
        {
            PerformanceClass kind = list.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) ?? list[0];
            kind.Normalize();
            return kind;
        }

        private string Unique(string name)
        {
            string unique = name;
            for (int i = 2; rows.Any(r => string.Equals(r.Name.Text.Trim(), unique, StringComparison.OrdinalIgnoreCase)); i++) unique = $"{name} {i}";
            return unique;
        }

        private static Button Small(string text) => new() { Content = text, Padding = new Thickness(12, 3, 12, 3), Margin = new Thickness(0, 0, 6, 0) };

        private static TextBlock Header(string name, string unit) => new()
        {
            Text = unit.Length > 0 ? $"{name}\n{unit}" : name,
            Foreground = DimBrush,
            FontSize = 11,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 3)
        };

        private void Place(UIElement element, int row, int column, int span = 1)
        {
            Grid.SetRow(element, row);
            Grid.SetColumn(element, column);
            Grid.SetColumnSpan(element, span);
            table.Children.Add(element);
        }

        private static string Number(double value) => Math.Round(value, 1).ToString("0.#", CultureInfo.InvariantCulture);

        private static double[] PerformanceOf(PerformanceClass kind) =>
            [kind.MinSpeedKt, kind.MaxSpeedKt, kind.ApproachSpeedKt, kind.FinalSpeedKt, kind.MaxClimbFpm, kind.MaxDescentFpm, kind.MaxBankDegrees, kind.BrakingKtPerSecond];

        /// <summary>The heights of the class itself (ft above the runway): conventional, wide, close, initial, break, High Key.</summary>
        private static double[] ClassHeights(PerformanceClass kind) =>
            [kind.ConventionalHeightFt, kind.WideHeightFt, kind.CloseHeightFt, kind.OverheadHeightFt, kind.BreakHeightFt, kind.HighKeyFt];

        private void AddRow(PerformanceClass kind, string? originalName, string name, ClassCircuit? heights)
        {
            ClassRow row = new() { Source = kind.Copy(), OriginalName = originalName };
            int at = table.RowDefinitions.Count;
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            row.Name = new TextBox { Text = name, Margin = new Thickness(1), FontWeight = FontWeights.SemiBold, ToolTip = "Its name (click it to choose the class for Duplicate or Remove)." };
            row.Name.GotFocus += (_, _) => Choose(row);
            Place(row.Name, at, 0);
            double[] values = PerformanceOf(kind);
            for (int i = 0; i < PerformanceColumns.Length; i++)
            {
                TextBox box = new() { Text = Number(values[i]), Margin = new Thickness(1), TextAlignment = TextAlignment.Right };
                // (A helicopter hovers: its minimum speed is 0.)
                if (i == 0 && kind.CanHover) { box.IsReadOnly = true; box.Foreground = Brushes.Gray; box.ToolTip = "A helicopter: it can hover (0 kt)."; }
                int column = i;
                box.PreviewMouseWheel += (_, e) => { if (!box.IsReadOnly) Wheel(box, PerformanceColumns[column].Step, 0, () => null, e); };
                box.GotFocus += (_, _) => Choose(row);
                row.Performance[i] = box;
                Place(box, at, 1 + i);
            }
            double?[] own = heights == null ? new double?[HeightColumns.Length]
                : [heights.ConventionalHeightFt ?? heights.CircuitHeightFt, heights.WideHeightFt ?? heights.CircuitHeightFt, heights.CloseHeightFt ?? heights.CircuitHeightFt,
                   heights.InitialHeightFt, heights.BreakHeightFt, heights.HighKeyFt];
            for (int i = 0; i < HeightColumns.Length; i++)
            {
                // (A helicopter: no overhead, no SFO.)
                bool off = !heightsHere || (kind.CanHover && i >= 3);
                TextBox box = new()
                {
                    Text = own[i] is double v && !off ? Number(v + offset) : "",
                    Margin = new Thickness(1),
                    TextAlignment = TextAlignment.Right,
                    Background = Brushes.Transparent,
                    IsEnabled = !off
                };
                TextBlock grey = new() { Foreground = GreyBrush, TextAlignment = TextAlignment.Right, Margin = new Thickness(1, 1, 6, 1), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
                Border back = new() { Background = off ? new SolidColorBrush(Color.FromRgb(0x2A, 0x31, 0x38)) : Brushes.White, Margin = new Thickness(1) };
                Grid cell = new();
                cell.Children.Add(back);
                cell.Children.Add(grey);
                cell.Children.Add(box);
                int column = i;
                box.TextChanged += (_, _) => ShowGreys(row);
                box.PreviewMouseWheel += (_, e) => Wheel(box, 100, offset, () => Grey(row, column), e);
                box.GotFocus += (_, _) => Choose(row);
                row.Heights[i] = box;
                row.Greys[i] = grey;
                Place(cell, at, PerformanceColumns.Length + 2 + i);
            }
            rows.Add(row);
            ShowGreys(row);
            message.Text = "";
        }

        private void RemoveRow(ClassRow row)
        {
            int at = Grid.GetRow(row.Name);
            foreach (UIElement element in table.Children.Cast<UIElement>().Where(e => Grid.GetRow(e) == at).ToList()) table.Children.Remove(element);
            // (The rows below one up.)
            foreach (UIElement element in table.Children.Cast<UIElement>().Where(e => Grid.GetRow(e) > at)) Grid.SetRow(element, Grid.GetRow(element) - 1);
            table.RowDefinitions.RemoveAt(table.RowDefinitions.Count - 1);
            rows.Remove(row);
            if (chosen == row) chosen = null;
            message.Text = "";
        }

        private void Choose(ClassRow row)
        {
            if (chosen != null) chosen.Name.Foreground = Brushes.Black;
            chosen = row;
            row.Name.Foreground = Brushes.DarkOrange;
        }

        /// <summary>The value shown grey in an empty height box (as shown: + the offset); null when there is none.</summary>
        private double? Grey(ClassRow row, int column)
        {
            if (!row.Heights[column].IsEnabled) return null;
            // (The break empty: the close height of this aerodrome when given, else the class's.)
            if (column == 4 && Typed(row.Heights[2]) is double close) return close;
            return ClassHeights(row.Source)[column] + offset;
        }

        private static double? Typed(TextBox box) =>
            double.TryParse(box.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : null;

        private void ShowGreys(ClassRow row)
        {
            for (int i = 0; i < HeightColumns.Length; i++)
                row.Greys[i].Text = row.Heights[i].Text.Trim().Length == 0 && Grey(row, i) is double grey ? Number(grey) : "";
        }

        /// <summary>The wheel over a box (empty: from its grey value): one step up or down.</summary>
        private void Wheel(TextBox box, double step, double origin, Func<double?> empty, MouseWheelEventArgs e)
        {
            if (!box.IsEnabled) return;
            double? value = box.Text.Trim().Length == 0 ? empty() : Typed(box);
            if (value is not double v) return;
            double by = step * ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 5 : 1);
            // (Heights: whole hundreds above the runway, whatever the elevation.)
            double next = Math.Round((v - origin + (e.Delta > 0 ? by : -by)) / step) * step + origin;
            box.Text = Number(Math.Max(0, next));
            message.Text = "";
            e.Handled = true;
        }

        /// <summary>The performance of a row read back (the class's other values kept), or null (the problem said).</summary>
        private PerformanceClass? Read(ClassRow row)
        {
            string name = row.Name.Text.Trim();
            if (name.Length == 0) { Say(row, "A class has no name."); return null; }
            double[] values = new double[PerformanceColumns.Length];
            for (int i = 0; i < PerformanceColumns.Length; i++)
            {
                var column = PerformanceColumns[i];
                // (Speeds: at least 30 kt; a helicopter's minimum 0, its initial and final at least 10.)
                double low = i == 0 && row.Source.CanHover ? 0 : i is 0 or 2 or 3 ? (row.Source.CanHover ? 10 : 30) : column.Low;
                double high = i == 0 && row.Source.CanHover ? 0 : column.High;
                if (Typed(row.Performance[i]) is not double value || !double.IsFinite(value) || value < low || value > high)
                {
                    Say(row, string.Format(CultureInfo.InvariantCulture, "{0}: {1} must be a number from {2} to {3} {4}.", name, column.Header.ToLowerInvariant(), low, high, column.Unit));
                    return null;
                }
                values[i] = value;
            }
            (double min, double max, double initial, double final) = (values[0], values[1], values[2], values[3]);
            if (max < min + 10) { Say(row, $"{name}: the max speed must be at least 10 kt more than the min speed."); return null; }
            if (initial < min || initial > max) { Say(row, $"{name}: the initial speed ({initial:0} kt) must be between the min ({min:0}) and the max ({max:0})."); return null; }
            if (final < min || final > initial) { Say(row, $"{name}: the final speed ({final:0} kt) must be between the min ({min:0}) and the initial ({initial:0})."); return null; }
            // (The speeds of its circuits, not in the table, within its speeds: said, not changed silently.)
            PerformanceClass source = row.Source;
            if (final > source.CircuitSpeedKt) { Say(row, $"{name}: the final speed must be at most its circuit speed ({source.CircuitSpeedKt:0} kt, in performance.json)."); return null; }
            double fastest = new[] { source.CircuitSpeedKt, source.OverheadSpeedKt, source.BestGlideKt, source.ArrivalSpeedKt }.Max();
            if (max < fastest) { Say(row, $"{name}: the max speed must be at least {fastest:0} kt (its circuit, overhead, glide or arrival speed, in performance.json)."); return null; }
            PerformanceClass kind = row.Source.Copy();
            kind.Name = name;
            (kind.MinSpeedKt, kind.MaxSpeedKt, kind.ApproachSpeedKt, kind.FinalSpeedKt) = (min, max, initial, final);
            (kind.MaxClimbFpm, kind.MaxDescentFpm, kind.MaxBankDegrees, kind.BrakingKtPerSecond) = (values[4], values[5], values[6], values[7]);
            return kind;
        }

        private void Say(ClassRow row, string text)
        {
            message.Text = text;
            Choose(row);
        }

        /// <summary>Save: every value checked (the first wrong one said, nothing saved), then given back.</summary>
        private void Save()
        {
            if (rows.Count == 0) { message.Text = "At least one class is needed."; return; }
            List<Saved> saved = [];
            foreach (ClassRow row in rows)
            {
                if (Read(row) is not PerformanceClass kind) return;
                if (saved.Any(s => string.Equals(s.Class.Name, kind.Name, StringComparison.OrdinalIgnoreCase))) { Say(row, $"Two classes are named {kind.Name}."); return; }
                double?[] heights = new double?[HeightColumns.Length];
                bool[] editable = row.Heights.Select(b => b.IsEnabled).ToArray();
                for (int i = 0; i < HeightColumns.Length; i++)
                {
                    TextBox box = row.Heights[i];
                    if (!box.IsEnabled || box.Text.Trim().Length == 0) continue;
                    var column = HeightColumns[i];
                    // (Written as shown: altitudes with QNH.)
                    if (Typed(box) is not double shown || shown - offset < column.Low || shown - offset > column.High)
                    {
                        Say(row, string.Format(CultureInfo.InvariantCulture, "{0}: {1} must be a number from {2} to {3} ft{4}.", kind.Name, column.Header,
                            column.Low + offset, column.High + offset, offset != 0 ? " (altitude)" : ""));
                        return;
                    }
                    heights[i] = shown - offset;
                }
                saved.Add(new Saved(kind, row.OriginalName, heights, editable));
            }
            Result = saved;
            DialogResult = true;
        }
    }
}
