using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// A row of buttons each on (yellow) or off, any number on (the members of a formation that land: the user, 4 Nov).
    /// </summary>
    internal sealed class ToggleRow : WrapPanel
    {
        private static readonly Brush OnBack = new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x4A));
        private static readonly Brush OnText = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2A));
        private static readonly Brush OffBack = new SolidColorBrush(Color.FromRgb(0x2A, 0x33, 0x3C));
        private static readonly Brush OffText = new SolidColorBrush(Color.FromRgb(0xD6, 0xE2, 0xEC));
        private static readonly Brush Edge = new SolidColorBrush(Color.FromRgb(0x5F, 0x6B, 0x75));

        private readonly List<Button> buttons = [];
        private readonly List<bool> values = [];
        private readonly Func<int, string>? tip;

        public ToggleRow(Func<int, string>? tip = null)
        {
            this.tip = tip;
            Margin = new Thickness(0, 1, 0, 1);
        }

        /// <summary>Which are on.</summary>
        public IReadOnlyList<bool> Values => values;

        /// <summary>The buttons again: these texts, all on (or as given).</summary>
        public void Set(IReadOnlyList<string> texts, IReadOnlyList<bool>? on = null)
        {
            Children.Clear();
            buttons.Clear();
            values.Clear();
            for (int i = 0; i < texts.Count; i++)
            {
                int index = i;
                Button button = new()
                {
                    Content = texts[i],
                    Margin = new Thickness(0, 0, 4, 4),
                    Padding = new Thickness(7, 2, 7, 2),
                    MinWidth = 34,
                    BorderBrush = Edge,
                    Focusable = false,
                    ToolTip = tip?.Invoke(i)
                };
                button.Click += (_, _) =>
                {
                    values[index] = !values[index];
                    Paint();
                };
                buttons.Add(button);
                values.Add(on == null || i >= on.Count || on[i]);
                Children.Add(button);
            }
            Paint();
        }

        private void Paint()
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                buttons[i].Background = values[i] ? OnBack : OffBack;
                buttons[i].Foreground = values[i] ? OnText : OffText;
            }
        }
    }
}
