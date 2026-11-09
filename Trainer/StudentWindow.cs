using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// The student console on the same PC (the user, 9 Nov: a second window, for a second monitor, no network): the top view
    /// of the aerodrome with the traffic (callsign, ICAO type, flight rules, altitude, speed), the runway, UTC time and the
    /// METAR line; the student's own strip bay (his notes: the instructor does not see them). No commands to the aircraft.
    /// </summary>
    internal sealed class StudentWindow : Window
    {
        private readonly ScrollViewer column;
        private readonly DockablePart bayPart;

        /// <summary>The student's view (zoom and moves his own).</summary>
        public TowerView View { get; } = new() { Student = true };

        /// <summary>The student's strip bay (his notes, his order of the strips).</summary>
        public StripBay Bay { get; } = new() { Margin = new Thickness(4) };

        /// <summary>The map shown (to set it again only when another one is opened).</summary>
        public object? MapShown { get; set; }

        public StudentWindow(PartPlacement place, Action saved, Func<StripKind, Brush> colours)
        {
            Title = "TWR Trainer - Student";
            Width = 1200;
            Height = 800;
            Background = Theme.Panel;
            Foreground = Theme.Text;
            if (Application.Current?.MainWindow?.Icon is ImageSource icon) Icon = icon;

            StackPanel panel = new() { Margin = new Thickness(10) };
            column = new ScrollViewer { Content = panel, Width = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Panel };
            DockPanel root = new();
            DockPanel.SetDock(column, Dock.Right);
            root.Children.Add(column);
            Grid map = new();
            map.Children.Add(View);
            Canvas layer = new();
            map.Children.Add(layer);
            root.Children.Add(map);
            Content = root;

            Bay.ColourOf = colours;
            // (Resizable by its corner, closed to a small box with its symbol, or in the column: as the instructor's parts.)
            bayPart = new DockablePart("Strip bay", "▤", Bay, panel, layer, place, () =>
            {
                saved();
                ShowColumn();
            });
            ShowColumn();
            Loaded += (_, _) => View.ShowLevel(1);
            KeyDown += (_, e) =>
            {
                if (e.Key is Key.F1 or Key.F2)
                {
                    View.ShowLevel(e.Key == Key.F1 ? 1 : 2);
                    e.Handled = true;
                }
            };
        }

        /// <summary>The column on the right only when the strip bay is in it.</summary>
        private void ShowColumn() => column.Visibility = bayPart.IsFloating ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>The photo of the lesson of this second.</summary>
        public void Show(LessonSnapshot s)
        {
            View.Status = $"{s.Aerodrome}  RWY {(s.Runway.Length > 0 ? s.Runway : "-")}   UTC {s.Utc:HH:mm:ss}" + (s.Paused ? "   PAUSED" : "");
            View.Metar = s.Metar;
            View.Network = s.Network;
            View.SetTargets(s.Traffic, s.Time);
            Bay.Show(s.Coordination, s.Contact);
            bayPart.SummaryText = $"{s.Coordination.Count} + {s.Contact.Count}";
            ShowColumn();
        }
    }
}
