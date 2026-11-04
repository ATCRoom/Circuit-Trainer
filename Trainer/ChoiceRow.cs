using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// A row of buttons, one per value, the value chosen in yellow (instead of a drop-down list: one click). A click
    /// chooses the value and tells it (<see cref="Picked"/>); the value shown can also be set from outside (the state of
    /// the selected aircraft), but not for a few seconds after a click (so a chained click uses the value just chosen).
    /// </summary>
    internal sealed class ChoiceRow<T> : WrapPanel
    {
        private static readonly Brush OnBack = new SolidColorBrush(Color.FromRgb(0xFF, 0xD2, 0x4A));
        private static readonly Brush OnText = new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2A));
        private static readonly Brush OffBack = new SolidColorBrush(Color.FromRgb(0x2A, 0x33, 0x3C));
        private static readonly Brush OffText = new SolidColorBrush(Color.FromRgb(0xA9, 0xB6, 0xC0));
        private static readonly Brush Edge = new SolidColorBrush(Color.FromRgb(0x5F, 0x6B, 0x75));

        private readonly List<(T Value, Button Button)> buttons = [];
        private DateTime clicked = DateTime.MinValue;

        public T? Value { get; private set; }
        public bool HasValue { get; private set; }
        public event Action<T>? Picked;

        public ChoiceRow(IEnumerable<(T Value, string Text)> items, Func<T, string, string?>? tip = null)
        {
            Margin = new Thickness(0, 1, 0, 1);
            foreach ((T value, string text) in items)
            {
                Button button = new()
                {
                    Content = text,
                    Margin = new Thickness(0, 0, 4, 4),
                    Padding = new Thickness(7, 2, 7, 2),
                    MinWidth = 34,
                    Background = OffBack,
                    Foreground = OffText,
                    BorderBrush = Edge,
                    Focusable = false,
                    ToolTip = tip?.Invoke(value, text)
                };
                ToolTipService.SetShowOnDisabled(button, true);
                button.Click += (_, _) =>
                {
                    clicked = DateTime.UtcNow;
                    Show(value);
                    Picked?.Invoke(value);
                };
                buttons.Add((value, button));
                Children.Add(button);
            }
        }

        /// <summary>The button of a value (null: none).</summary>
        public Button? ButtonOf(T value) => buttons.Where(b => EqualityComparer<T>.Default.Equals(b.Value, value)).Select(b => b.Button).FirstOrDefault();

        /// <summary>One button usable or grey (it does not apply now).</summary>
        public void SetEnabled(T value, bool enabled)
        {
            foreach ((T v, Button b) in buttons) if (EqualityComparer<T>.Default.Equals(v, value)) b.IsEnabled = enabled;
        }

        /// <summary>Shows this value as chosen.</summary>
        public void Show(T value)
        {
            Value = value;
            HasValue = true;
            foreach ((T v, Button b) in buttons)
            {
                bool on = EqualityComparer<T>.Default.Equals(v, value);
                b.Background = on ? OnBack : OffBack;
                b.Foreground = on ? OnText : OffText;
                // (Under the mouse: the chosen one keeps its dark text, see ButtonStyle.)
                b.Tag = on ? "on" : null;
                b.BorderBrush = on ? OnBack : Edge;
            }
        }

        /// <summary>The value of the aircraft now, unless a button was clicked a moment ago (the order may not be carried out yet).</summary>
        public void Refresh(T value)
        {
            if ((DateTime.UtcNow - clicked).TotalSeconds > 3) Show(value);
        }
    }
}
