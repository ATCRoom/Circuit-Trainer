using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// One look for every button of the program (the user, 4 Nov, version B): Windows' own template painted a very light
    /// colour under the mouse, and the light text could not be read. Here, under the mouse the background stays, only the
    /// text and the border turn white (a chosen one, yellow with dark text, Tag "on": only the border); pressed a little
    /// darker; not usable: faded.
    /// </summary>
    internal static class ButtonStyle
    {
        private static readonly Brush OnText = Frozen(new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2A)));

        public static Style Make()
        {
            ControlTemplate template = new(typeof(Button));
            FrameworkElementFactory border = new(typeof(Border), "bd");
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            border.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
            FrameworkElementFactory content = new(typeof(ContentPresenter), "cp");
            content.SetValue(FrameworkElement.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
            content.SetValue(FrameworkElement.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            content.SetValue(TextElement.ForegroundProperty, new TemplateBindingExtension(Control.ForegroundProperty));
            content.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            border.AppendChild(content);
            template.VisualTree = border;

            Trigger over = new() { Property = UIElement.IsMouseOverProperty, Value = true };
            over.Setters.Add(new Setter(Border.BorderBrushProperty, Brushes.White, "bd"));
            over.Setters.Add(new Setter(TextElement.ForegroundProperty, Brushes.White, "cp"));
            template.Triggers.Add(over);
            MultiTrigger chosenOver = new();
            chosenOver.Conditions.Add(new Condition(UIElement.IsMouseOverProperty, true));
            chosenOver.Conditions.Add(new Condition(FrameworkElement.TagProperty, "on"));
            chosenOver.Setters.Add(new Setter(TextElement.ForegroundProperty, OnText, "cp"));
            template.Triggers.Add(chosenOver);
            Trigger pressed = new() { Property = ButtonBase.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 0.8, "bd"));
            template.Triggers.Add(pressed);
            Trigger disabled = new() { Property = UIElement.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45, "bd"));
            template.Triggers.Add(disabled);

            Style style = new(typeof(Button));
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            style.Setters.Add(new Setter(Control.BackgroundProperty, Frozen(new SolidColorBrush(Color.FromRgb(0x2A, 0x33, 0x3C)))));
            style.Setters.Add(new Setter(Control.ForegroundProperty, Frozen(new SolidColorBrush(Color.FromRgb(0xC8, 0xD4, 0xDE)))));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, Frozen(new SolidColorBrush(Color.FromRgb(0x5F, 0x6B, 0x75)))));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 1, 6, 1)));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            return style;
        }

        private static T Frozen<T>(T item) where T : Freezable
        {
            item.Freeze();
            return item;
        }
    }
}
