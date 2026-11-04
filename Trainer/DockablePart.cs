using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ATCRoom.TwrTrainer
{
    /// <summary>Where a part of the panel is: in the column or floating over the map (its place and size), open or closed to its bar.</summary>
    internal sealed class PartPlacement
    {
        public bool Floating { get; set; }
        public double? Left { get; set; }
        public double? Top { get; set; }
        public double Width { get; set; } = 340;
        public double Height { get; set; } = 420;
        public bool Open { get; set; } = true;
        /// <summary>A height set by hand with the grip (else: as tall as its content, the user 4 Nov).</summary>
        public bool ManualHeight { get; set; }
    }

    /// <summary>
    /// A part of the panel (the user, 3 Nov: the panels detachable and resizable): in the column on the right, or taken
    /// out (⇱) to float over the map: moved by its bar, resized by the grip in its bottom right corner, back to the
    /// column with ⇲ or a double click on its bar; ▴ / ▾ closes it to its bar. Always inside the view; a click brings it
    /// to the front. Its place, size and state are kept in a <see cref="PartPlacement"/> (saved by the owner).
    /// </summary>
    internal sealed class DockablePart
    {
        private static readonly Brush BarBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x24, 0x31, 0x3D)));
        private static readonly Brush BackBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x1A, 0x21, 0x28)));
        private static readonly Brush EdgeBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x5D, 0x7A, 0x99)));
        private static readonly Brush TextBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xD6, 0xE2, 0xEC)));
        private static readonly Brush DimBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x8A, 0x98, 0xA4)));
        private static int topZ;
        private const double MinWidth = 260, MinHeight = 120;

        private readonly FrameworkElement content;
        private readonly Canvas layer;
        private readonly PartPlacement place;
        private readonly Action changed;
        private readonly Border docked;
        private readonly ContentControl dockedHost = new();
        private readonly Border floating;
        private readonly Border floatBar;
        private readonly ScrollViewer floatHost;
        private readonly TextBlock dockedSummary = Summary(), floatSummary = Summary();
        private readonly Button dockedToggle, floatToggle;
        private Point? dragStart;
        private Point dragFrom;

        public DockablePart(string title, FrameworkElement content, Panel column, Canvas layer, PartPlacement place, Action changed)
        {
            this.content = content;
            this.layer = layer;
            this.place = place;
            this.changed = changed;

            // In the column: its bar (title, summary, take out, close) and its content.
            dockedToggle = BarButton("▴", "Close to the bar / open.", () => SetOpen(!place.Open));
            Button takeOut = BarButton("⇱", "Take it out of the column: it floats over the map (move it by its bar, resize it by its corner).", Float);
            DockPanel dockedBar = Bar(title, dockedSummary, takeOut, dockedToggle);
            dockedBar.Margin = new Thickness(0, 0, 0, 2);
            StackPanel dockedLayout = new();
            dockedLayout.Children.Add(new Border { Background = BarBrush, CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 3, 4, 3), Child = dockedBar });
            dockedLayout.Children.Add(dockedHost);
            docked = new Border { Child = dockedLayout, Margin = new Thickness(0, 8, 0, 2) };
            column.Children.Add(docked);

            // Floating: its bar (moved by it; double click: back to the column) and its content, which scrolls, and the grip.
            floatToggle = BarButton("▴", "Close to the bar / open.", () => SetOpen(!place.Open));
            Button back = BarButton("⇲", "Back into the column on the right.", Dock);
            floatBar = new Border
            {
                Background = BarBrush,
                CornerRadius = new CornerRadius(3, 3, 0, 0),
                Padding = new Thickness(6, 3, 4, 3),
                MinHeight = 24,
                Cursor = Cursors.SizeAll,
                Child = Bar("⋮⋮ " + title, floatSummary, back, floatToggle),
                ToolTip = "Drag to move it; double click: back into the column."
            };
            floatBar.MouseLeftButtonDown += BarDown;
            floatBar.MouseMove += BarMove;
            floatBar.MouseLeftButtonUp += BarUp;
            floatBar.LostMouseCapture += (_, _) =>
            {
                if (dragStart == null) return;
                dragStart = null;
                changed();
            };
            floatHost = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(8, 4, 8, 8)
            };
            Thumb grip = new()
            {
                Width = 14,
                Height = 14,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Cursor = Cursors.SizeNWSE,
                Background = EdgeBrush,
                Opacity = 0.8,
                ToolTip = "Drag to change the size."
            };
            grip.DragDelta += (_, e) =>
            {
                // (The height by hand from now on: kept until a double click on the grip or docking it again.)
                if (!place.ManualHeight)
                {
                    place.ManualHeight = true;
                    place.Height = Math.Max(MinHeight, floatHost.ActualHeight);
                }
                place.Width = Math.Max(MinWidth, place.Width + e.HorizontalChange);
                place.Height = Math.Max(MinHeight, place.Height + e.VerticalChange);
                Fit();
            };
            grip.MouseDoubleClick += (_, e) =>
            {
                place.ManualHeight = false;
                Fit();
                changed();
                e.Handled = true;
            };
            grip.ToolTip = "Drag to change the size; double click: as tall as its content again.";
            grip.DragCompleted += (_, _) => changed();
            Grid body = new();
            body.Children.Add(floatHost);
            body.Children.Add(grip);
            DockPanel floatLayout = new();
            System.Windows.Controls.DockPanel.SetDock(floatBar, System.Windows.Controls.Dock.Top);
            floatLayout.Children.Add(floatBar);
            floatLayout.Children.Add(body);
            floating = new Border
            {
                Background = BackBrush,
                BorderBrush = EdgeBrush,
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(4),
                Child = floatLayout,
                Visibility = Visibility.Collapsed
            };
            floating.PreviewMouseDown += (_, _) => System.Windows.Controls.Panel.SetZIndex(floating, ++topZ);
            layer.Children.Add(floating);
            layer.SizeChanged += (_, _) => Fit();
            Show();
        }

        private string summary = "";
        private DateTime flashUntil = DateTime.MinValue;

        /// <summary>The text on its bar (the state of what it holds); a message flashed there a moment ago first.</summary>
        public string SummaryText
        {
            set
            {
                summary = value;
                if (DateTime.UtcNow >= flashUntil) dockedSummary.Text = floatSummary.Text = value;
            }
        }

        /// <summary>Floating over the map (not in the column).</summary>
        public bool IsFloating => place.Floating;

        /// <summary>Open (not closed to its bar).</summary>
        public bool IsOpen => place.Open;

        /// <summary>A message on its bar for a few seconds (the part closed: the user, 3 Nov), then its summary again.</summary>
        public void Flash(string text)
        {
            flashUntil = DateTime.UtcNow.AddSeconds(6);
            dockedSummary.Text = floatSummary.Text = text;
            System.Windows.Threading.DispatcherTimer back = new() { Interval = TimeSpan.FromSeconds(6.2) };
            back.Tick += (_, _) =>
            {
                back.Stop();
                if (DateTime.UtcNow >= flashUntil) dockedSummary.Text = floatSummary.Text = summary;
            };
            back.Start();
        }

        private static TextBlock Summary() => new() { Foreground = DimBrush, FontSize = 11, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };

        private static DockPanel Bar(string title, TextBlock summary, params Button[] buttons)
        {
            DockPanel bar = new() { LastChildFill = true };
            foreach (Button button in Enumerable.Reverse(buttons))
            {
                System.Windows.Controls.DockPanel.SetDock(button, System.Windows.Controls.Dock.Right);
                bar.Children.Add(button);
            }
            StackPanel name = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            name.Children.Add(new TextBlock { Text = title, Foreground = TextBrush, FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            name.Children.Add(summary);
            bar.Children.Add(name);
            return bar;
        }

        private static Button BarButton(string text, string tip, Action action)
        {
            Button button = new()
            {
                Content = text,
                FontSize = 11,
                Width = 22,
                Height = 18,
                Padding = new Thickness(0),
                Margin = new Thickness(4, 0, 0, 0),
                Focusable = false,
                Foreground = Brushes.White,
                Background = BarBrush,
                BorderBrush = DimBrush,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = tip
            };
            button.Click += (_, _) => action();
            return button;
        }

        private void Float()
        {
            place.Floating = true;
            Show();
            changed();
        }

        private void Dock()
        {
            place.Floating = false;
            place.ManualHeight = false;
            Show();
            changed();
        }

        private void SetOpen(bool open)
        {
            place.Open = open;
            Show();
            changed();
        }

        /// <summary>The content where the part is now (in the column or floating), open or closed.</summary>
        private void Show()
        {
            dockedHost.Content = null;
            floatHost.Content = null;
            if (place.Floating) floatHost.Content = content; else dockedHost.Content = content;
            docked.Visibility = place.Floating ? Visibility.Collapsed : Visibility.Visible;
            floating.Visibility = place.Floating ? Visibility.Visible : Visibility.Collapsed;
            dockedHost.Visibility = place.Open ? Visibility.Visible : Visibility.Collapsed;
            floatHost.Visibility = place.Open ? Visibility.Visible : Visibility.Collapsed;
            dockedToggle.Content = floatToggle.Content = place.Open ? "▴" : "▾";
            if (place.Floating) System.Windows.Controls.Panel.SetZIndex(floating, ++topZ);
            Fit();
        }

        private void BarDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
            {
                Dock();
                e.Handled = true;
                return;
            }
            dragStart = e.GetPosition(layer);
            dragFrom = new Point(Canvas.GetLeft(floating) is double l && double.IsFinite(l) ? l : 0, Canvas.GetTop(floating) is double t && double.IsFinite(t) ? t : 0);
            floatBar.CaptureMouse();
            e.Handled = true;
        }

        private void BarMove(object sender, MouseEventArgs e)
        {
            if (dragStart is not Point start) return;
            Vector moved = e.GetPosition(layer) - start;
            place.Left = dragFrom.X + moved.X;
            place.Top = dragFrom.Y + moved.Y;
            Fit();
        }

        private void BarUp(object sender, MouseButtonEventArgs e)
        {
            if (dragStart == null) return;
            dragStart = null;
            floatBar.ReleaseMouseCapture();
            changed();
            e.Handled = true;
        }

        /// <summary>Floating: inside the view (the whole bar at least), at its size (smaller when the view is).</summary>
        private void Fit()
        {
            if (!place.Floating || layer.ActualWidth <= 0 || layer.ActualHeight <= 0) return;
            double width = Math.Min(Math.Max(MinWidth, place.Width), Math.Max(MinWidth, layer.ActualWidth - 8));
            floating.Width = width;
            double left = Math.Clamp(place.Left ?? 20, 0, Math.Max(0, layer.ActualWidth - width));
            double top = Math.Clamp(place.Top ?? 20, 0, Math.Max(0, layer.ActualHeight - 28));
            Canvas.SetLeft(floating, left);
            Canvas.SetTop(floating, top);
            double room = Math.Max(40, layer.ActualHeight - top - 30);
            if (place.ManualHeight)
            {
                floatHost.MaxHeight = double.PositiveInfinity;
                floatHost.Height = Math.Max(40, Math.Min(Math.Max(MinHeight, place.Height), room));
            }
            else
            {
                // As tall as its content (an expander opened, rows added), as far as the window allows, then it scrolls.
                floatHost.Height = double.NaN;
                floatHost.MaxHeight = room;
            }
        }

        private static T Freeze<T>(T item) where T : Freezable
        {
            item.Freeze();
            return item;
        }
    }
}
