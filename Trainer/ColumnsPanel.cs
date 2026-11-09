using System.Windows;
using System.Windows.Controls;

namespace ATCRoom.TwrTrainer
{
    /// <summary>
    /// Buttons in columns as wide as each other, as many columns as fit (the user, 9 Nov: the panel can be resized, its
    /// buttons stay lined up). <see cref="MinItemWidth"/> 0: a plain wrap, each one as wide as it wants.
    /// </summary>
    internal class ColumnsPanel : Panel
    {
        /// <summary>The narrowest a column may be (0: a plain wrap).</summary>
        public double MinItemWidth { get; set; }

        private int Columns(double width) => Math.Max(1, (int)(width / MinItemWidth));

        protected override Size MeasureOverride(Size available)
        {
            bool columns = MinItemWidth > 0 && double.IsFinite(available.Width) && available.Width > 0;
            double width = double.IsFinite(available.Width) ? available.Width : double.PositiveInfinity;
            double x = 0, y = 0, rowHeight = 0, widest = 0;
            int count = 0, perRow = columns ? Columns(width) : 0;
            double cell = columns ? width / perRow : 0;
            foreach (UIElement child in InternalChildren)
            {
                if (child.Visibility == Visibility.Collapsed) continue;
                child.Measure(new Size(columns ? cell : width, double.PositiveInfinity));
                Size size = child.DesiredSize;
                if (columns)
                {
                    if (count > 0 && count % perRow == 0) { y += rowHeight; rowHeight = 0; }
                    rowHeight = Math.Max(rowHeight, size.Height);
                    count++;
                    continue;
                }
                if (x > 0 && x + size.Width > width) { y += rowHeight; x = 0; rowHeight = 0; }
                x += size.Width;
                widest = Math.Max(widest, x);
                rowHeight = Math.Max(rowHeight, size.Height);
            }
            return new Size(columns ? width : widest, y + rowHeight);
        }

        protected override Size ArrangeOverride(Size final)
        {
            bool columns = MinItemWidth > 0 && final.Width > 0;
            int perRow = columns ? Columns(final.Width) : 0;
            double cell = columns ? final.Width / perRow : 0;
            double x = 0, y = 0, rowHeight = 0;
            int count = 0;
            foreach (UIElement child in InternalChildren)
            {
                if (child.Visibility == Visibility.Collapsed) continue;
                Size size = child.DesiredSize;
                if (columns)
                {
                    if (count > 0 && count % perRow == 0) { y += rowHeight; rowHeight = 0; }
                    child.Arrange(new Rect((count % perRow) * cell, y, cell, size.Height));
                    rowHeight = Math.Max(rowHeight, size.Height);
                    count++;
                    continue;
                }
                if (x > 0 && x + size.Width > final.Width) { y += rowHeight; x = 0; rowHeight = 0; }
                child.Arrange(new Rect(x, y, size.Width, size.Height));
                x += size.Width;
                rowHeight = Math.Max(rowHeight, size.Height);
            }
            return final;
        }
    }
}
