using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ATCRoom.TwrTrainer
{
    /// <summary>The kind of flight of a strip (its colour): departure, arrival, local or transit (the user, 6 Nov).</summary>
    internal enum StripKind { Departure, Arrival, Local }

    /// <summary>
    /// What a strip shows (the user, 6 Nov): line 1 callsign, ICAO type, rules V / I, entry (gate, LF, RWY...; the exit of a
    /// departure) and altitude in hundreds of feet. <see cref="Key"/>: the plan (Coordination) or the callsign (In contact).
    /// </summary>
    /// <param name="From">The key of the plan whose aircraft this is (its notes and its place go with it when it starts), or null.</param>
    internal sealed record StripData(object Key, string Callsign, string Type, bool Ifr, StripKind Kind, string Entry, string Altitude, object? From = null);

    /// <summary>
    /// The strip bay (the user, 6 Nov; here on the instructor's screen to see how it behaves, then the student's): two sections,
    /// Coordination (a strip for each plan of the Traffic List waiting, as if coordinated) and In contact (the aircraft
    /// flying, moved there when they start). The student's notes: a strip chosen with a click, then the quick buttons or the
    /// free note; a click on a note removes it; the last one highlighted. Nothing of the instructor's actions on the strips.
    /// </summary>
    internal sealed class StripBay : StackPanel
    {
        private static readonly string[] Quick = ["DW", "BASE", "FINAL", "OVH", "BRK", "SFO", "ORBIT", "T/G", "LAND", "L/A", "L/P", "G/A", "TKOF", "FRQ CNG"];
        private static readonly FontFamily StripFont = new("Consolas");
        private static readonly Brush StripText = Frozen(Color.FromRgb(0x22, 0x22, 0x22));
        private static readonly Brush NoteText = Frozen(Color.FromRgb(0x1D, 0x3F, 0x8A));
        private static readonly Brush ChosenEdge = Frozen(Color.FromRgb(0xD9, 0x48, 0x0F));

        private readonly StackPanel coordination = new(), contact = new();
        private readonly TextBlock coordinationTitle = new(), contactTitle = new();
        private readonly TextBox noteBox = new() { MinWidth = 90, Margin = new Thickness(0, 0, 4, 0), CharacterCasing = CharacterCasing.Upper, MaxLength = 24 };
        /// <summary>The notes of each strip (its key), in order.</summary>
        private readonly Dictionary<object, List<string>> notes = [];
        private List<StripData> waiting = [], flying = [];
        private string shown = "";
        private object? chosen;
        /// <summary>The order of the strips (the user, 9 Nov: moved by hand, dragging them); a new strip goes last.</summary>
        private readonly List<object> order = [];
        /// <summary>A strip being dragged: not drawn again meanwhile.</summary>
        private bool dragging;
        /// <summary>What is dragged: the key of a strip.</summary>
        private sealed record Moving(object Key);
        private static readonly Brush VfrText = Frozen(Color.FromRgb(0x1A, 0x7A, 0x35));
        private static readonly Brush IfrBack = Frozen(Color.FromRgb(0x1D, 0x3F, 0x8A));

        /// <summary>The colour of a strip of each kind (from the settings).</summary>
        public Func<StripKind, Brush> ColourOf { get; set; } = _ => Brushes.White;

        public StripBay()
        {
            Children.Add(Title(coordinationTitle));
            Children.Add(coordination);
            Children.Add(Title(contactTitle));
            Children.Add(contact);
            WrapPanel buttons = new() { Margin = new Thickness(0, 8, 0, 0) };
            foreach (string note in Quick)
            {
                Button button = new() { Content = note, Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(0, 0, 3, 3), FontSize = 11 };
                button.Click += (_, _) => AddNote(note);
                buttons.Children.Add(button);
            }
            Children.Add(buttons);
            DockPanel free = new() { Margin = new Thickness(0, 2, 0, 0) };
            Button add = new() { Content = "ADD", Padding = new Thickness(8, 1, 8, 1) };
            add.Click += (_, _) => AddFreeNote();
            noteBox.KeyDown += (_, e) => { if (e.Key == Key.Enter) { AddFreeNote(); e.Handled = true; } };
            noteBox.ToolTip = "A free note on the strip chosen (Enter or ADD).";
            DockPanel.SetDock(add, Dock.Right);
            free.Children.Add(add);
            free.Children.Add(noteBox);
            Children.Add(free);
            Children.Add(new TextBlock
            {
                Text = "Click a strip to choose it, then a button or a free note; click a note to remove it. Drag a strip to change its place.",
                Foreground = Theme.Dim, FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0)
            });
            Draw();
        }

        private static Brush Frozen(Color colour)
        {
            SolidColorBrush brush = new(colour);
            brush.Freeze();
            return brush;
        }

        private static TextBlock Title(TextBlock title)
        {
            title.Foreground = Theme.Dim;
            title.FontSize = 11;
            title.Margin = new Thickness(0, 4, 0, 2);
            return title;
        }

        /// <summary>The strips now (called each second): drawn again only when something changed.</summary>
        public void Show(List<StripData> coordinationStrips, List<StripData> contactStrips)
        {
            waiting = coordinationStrips.ToList();
            flying = contactStrips.ToList();
            // (A plan started: its notes and its place go to its aircraft, the first one of a formation.)
            foreach (StripData strip in flying)
                if (strip.From != null && !order.Any(key => Equals(key, strip.Key)) && order.Any(key => Equals(key, strip.From))) Rekey(strip.From, strip.Key);
            // (The notes of strips gone are forgotten, and their places.)
            foreach (object key in notes.Keys.ToList())
                if (!waiting.Any(s => Equals(s.Key, key)) && !flying.Any(s => Equals(s.Key, key))) notes.Remove(key);
            order.RemoveAll(key => !waiting.Any(s => Equals(s.Key, key)) && !flying.Any(s => Equals(s.Key, key)));
            foreach (StripData strip in waiting.Concat(flying)) if (!order.Any(key => Equals(key, strip.Key))) order.Add(strip.Key);
            Sort();
            if (chosen != null && !waiting.Any(s => Equals(s.Key, chosen)) && !flying.Any(s => Equals(s.Key, chosen))) chosen = null;
            if (dragging || Text() == shown) return;
            Draw();
        }

        /// <summary>
        /// A strip has a new key: a plan started (its notes go with its first aircraft, its strip to In contact), or a plan
        /// changed (UPDATE: the new plan in the place of the old one).
        /// </summary>
        public void Rekey(object old, object now)
        {
            if (ReferenceEquals(old, now) || Equals(old, now)) return;
            if (notes.Remove(old, out List<string>? kept)) notes[now] = kept;
            if (Equals(chosen, old)) chosen = now;
            int at = order.FindIndex(key => Equals(key, old));
            if (at >= 0) order[at] = now;
        }

        /// <summary>Each section in the order of the strips.</summary>
        private void Sort()
        {
            int Place(StripData strip)
            {
                int at = order.FindIndex(key => Equals(key, strip.Key));
                return at < 0 ? int.MaxValue : at;
            }
            waiting = waiting.OrderBy(Place).ToList();
            flying = flying.OrderBy(Place).ToList();
        }

        /// <summary>Whether two strips are in the same section (a strip moves only inside its own).</summary>
        private bool SameSection(object a, object b) =>
            (waiting.Any(s => Equals(s.Key, a)) && waiting.Any(s => Equals(s.Key, b))) || (flying.Any(s => Equals(s.Key, a)) && flying.Any(s => Equals(s.Key, b)));

        /// <summary>A strip dropped on another: before it (or after it, dropped on its lower half).</summary>
        private void Move(object key, object onto, bool after)
        {
            if (Equals(key, onto) || !SameSection(key, onto)) return;
            int from = order.FindIndex(k => Equals(k, key));
            if (from < 0) return;
            object moved = order[from];
            order.RemoveAt(from);
            int at = order.FindIndex(k => Equals(k, onto));
            if (at < 0)
            {
                order.Insert(from, moved);
                return;
            }
            order.Insert(after ? at + 1 : at, moved);
            Sort();
            Draw();
        }

        /// <summary>A key as text: the callsign, or the plan itself (not its content: two plans alike are two strips).</summary>
        public static string KeyText(object key) => key is string text ? text : $"#{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(key)}";

        /// <summary>All that is drawn, as text: drawn again only when it changes.</summary>
        private string Text() =>
            string.Join("|", waiting.Concat(flying).Select(s => $"{KeyText(s.Key)}:{s.Callsign}:{s.Type}:{s.Ifr}:{s.Kind}:{s.Entry}:{s.Altitude}")) + "#" + Signature();

        /// <summary>The colours changed: drawn again.</summary>
        public void Redraw() => Draw();

        private string Signature() => string.Join(";", notes.Select(n => $"{KeyText(n.Key)}:{string.Join(",", n.Value)}")) + $"@{(chosen != null ? KeyText(chosen) : "")}";

        private void Draw()
        {
            shown = Text();
            coordinationTitle.Text = $"Coordination · {waiting.Count}";
            contactTitle.Text = $"In contact · {flying.Count}";
            Fill(coordination, waiting, true);
            Fill(contact, flying, false);
        }

        private void Fill(StackPanel into, List<StripData> strips, bool faint)
        {
            into.Children.Clear();
            if (strips.Count == 0)
            {
                into.Children.Add(new TextBlock { Text = "none", Foreground = Theme.Dim, FontSize = 10, Margin = new Thickness(2, 0, 0, 2) });
                return;
            }
            foreach (StripData strip in strips) into.Children.Add(Strip(strip, faint));
        }

        private static readonly Brush DropLine = Frozen(Color.FromRgb(0xF0, 0x7A, 0x1E));

        private Grid Strip(StripData strip, bool faint)
        {
            bool isChosen = Equals(strip.Key, chosen);
            Grid first = new();
            first.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            first.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock callsign = new() { Text = strip.Callsign, FontWeight = FontWeights.Bold, Foreground = StripText, FontFamily = StripFont, FontSize = 12 };
            string right = $"{strip.Type} {strip.Entry} {strip.Altitude}".Replace("  ", " ").Trim();
            TextBlock rest = new() { Text = right, Foreground = StripText, FontFamily = StripFont, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(6, 0, 0, 0) };
            Grid.SetColumn(rest, 1);
            first.Children.Add(callsign);
            first.Children.Add(rest);
            WrapPanel second = new() { MinHeight = 13 };
            List<string> written = notes.TryGetValue(strip.Key, out List<string>? list) ? list : [];
            for (int i = 0; i < written.Count; i++)
            {
                int index = i;
                bool last = i == written.Count - 1;
                TextBlock note = new()
                {
                    Text = written[i],
                    FontFamily = StripFont,
                    FontSize = 10,
                    Foreground = last ? Brushes.White : NoteText,
                    Background = last ? NoteText : Brushes.Transparent,
                    Padding = new Thickness(2, 0, 2, 0),
                    Margin = new Thickness(0, 0, 4, 0),
                    Cursor = Cursors.Hand,
                    ToolTip = "Click: remove this note."
                };
                note.MouseLeftButtonUp += (_, e) =>
                {
                    if (notes.TryGetValue(strip.Key, out List<string>? those) && index < those.Count) those.RemoveAt(index);
                    e.Handled = true;
                    Draw();
                };
                second.Children.Add(note);
            }
            StackPanel lines = new() { Margin = new Thickness(4, 1, 0, 1) };
            lines.Children.Add(first);
            lines.Children.Add(second);
            Brush colour = ColourOf(strip.Kind);
            // (The flight rules well visible, the user 9 Nov: a square on the left, V green on white, I white on blue.)
            Border rules = new()
            {
                Width = 20,
                Background = strip.Ifr ? IfrBack : Brushes.White,
                BorderBrush = strip.Ifr ? IfrBack : VfrText,
                BorderThickness = new Thickness(strip.Ifr ? 0 : 2),
                Child = new TextBlock
                {
                    Text = strip.Ifr ? "I" : "V",
                    FontFamily = StripFont,
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Foreground = strip.Ifr ? Brushes.White : VfrText,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                ToolTip = strip.Ifr ? "IFR" : "VFR"
            };
            DockPanel inside = new();
            DockPanel.SetDock(rules, Dock.Left);
            inside.Children.Add(rules);
            inside.Children.Add(lines);
            Border border = new()
            {
                Child = inside,
                Background = colour,
                BorderBrush = isChosen ? ChosenEdge : Darker(colour),
                BorderThickness = new Thickness(isChosen ? 2 : 1),
                Padding = new Thickness(0, 0, 4, 0),
                Margin = new Thickness(0, 0, 0, 2),
                Opacity = faint ? 0.8 : 1,
                Cursor = Cursors.Hand,
                ToolTip = "Click: choose this strip for the notes. Drag: change its place."
            };
            // (Moved by hand, the user 9 Nov: dragged onto another strip of its section.)
            Point? down = null;
            border.PreviewMouseLeftButtonDown += (_, e) => down = e.GetPosition(border);
            border.MouseMove += (_, e) =>
            {
                if (down is not Point start || e.LeftButton != MouseButtonState.Pressed) return;
                if ((e.GetPosition(border) - start).Length < 5) return;
                down = null;
                dragging = true;
                try
                {
                    DragDrop.DoDragDrop(border, new DataObject(typeof(Moving), new Moving(strip.Key)), DragDropEffects.Move);
                }
                finally
                {
                    dragging = false;
                }
                if (Text() != shown) Draw();
            };
            // (Where it will go, the user 9 Nov: an orange line above or below the strip under the mouse; none where it cannot.)
            System.Windows.Shapes.Rectangle above = Line(VerticalAlignment.Top), below = Line(VerticalAlignment.Bottom);
            // (The whole place of the strip takes the drop, the gap under it too; the review of 9 Nov.)
            Grid holder = new() { Background = Brushes.Transparent, AllowDrop = true };
            void ShowLine(bool? after)
            {
                above.Visibility = after == false ? Visibility.Visible : Visibility.Hidden;
                below.Visibility = after == true ? Visibility.Visible : Visibility.Hidden;
            }
            holder.DragOver += (_, e) =>
            {
                bool allowed = e.Data.GetData(typeof(Moving)) is Moving m && SameSection(m.Key, strip.Key);
                e.Effects = allowed ? DragDropEffects.Move : DragDropEffects.None;
                bool own = e.Data.GetData(typeof(Moving)) is Moving self && Equals(self.Key, strip.Key);
                ShowLine(allowed && !own ? e.GetPosition(holder).Y > holder.ActualHeight / 2 : null);
                e.Handled = true;
            };
            holder.DragLeave += (_, e) =>
            {
                // (Only when the mouse is really out, not passing over a part of the strip.)
                Point at = e.GetPosition(holder);
                if (at.X < 0 || at.Y < 0 || at.X >= holder.ActualWidth || at.Y >= holder.ActualHeight) ShowLine(null);
            };
            holder.Drop += (_, e) =>
            {
                ShowLine(null);
                if (e.Data.GetData(typeof(Moving)) is Moving m)
                {
                    bool after = e.GetPosition(holder).Y > holder.ActualHeight / 2;
                    dragging = false;
                    Move(m.Key, strip.Key, after);
                }
                e.Handled = true;
            };
            border.MouseLeftButtonUp += (_, _) =>
            {
                down = null;
                chosen = Equals(chosen, strip.Key) ? null : strip.Key;
                Draw();
            };
            holder.Children.Add(border);
            holder.Children.Add(above);
            holder.Children.Add(below);
            return holder;
        }

        /// <summary>The line where a dragged strip will go, on the top or the bottom edge of a strip; hidden at first.</summary>
        private static System.Windows.Shapes.Rectangle Line(VerticalAlignment edge) => new()
        {
            Height = 3,
            Fill = DropLine,
            VerticalAlignment = edge,
            Margin = edge == VerticalAlignment.Top ? new Thickness(0, -2, 0, 0) : new Thickness(0, 0, 0, 0),
            IsHitTestVisible = false,
            Visibility = Visibility.Hidden
        };

        /// <summary>The left edge of a strip: its colour, darker.</summary>
        private static Brush Darker(Brush brush)
        {
            Color c = brush is SolidColorBrush solid ? solid.Color : Colors.White;
            return Frozen(Color.FromRgb((byte)(c.R * 0.6), (byte)(c.G * 0.6), (byte)(c.B * 0.6)));
        }

        private void AddNote(string note)
        {
            if (chosen == null) return;
            if (!notes.TryGetValue(chosen, out List<string>? list)) notes[chosen] = list = [];
            list.Add(note);
            Draw();
        }

        private void AddFreeNote()
        {
            string text = noteBox.Text.Trim();
            if (text.Length == 0 || chosen == null) return;
            AddNote(text);
            noteBox.Text = "";
        }
    }
}
