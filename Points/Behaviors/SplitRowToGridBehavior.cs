using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Behaviors
{
    public class SplitRowToGridBehavior : Behavior<Grid>
    {
        public string Separator { get; set; } = "|";

        protected override void OnAttachedTo(Grid grid)
        {
            base.OnAttachedTo(grid);

            grid.BindingContextChanged += (_, _) =>
            {
                grid.Children.Clear();

                if (grid.BindingContext is not string row)
                    return;

                var cells = row.Split(Separator)
                               .Select(s => s.Trim())
                               .ToArray();

                for (int i = 0; i < cells.Length; i++)
                {
                    var label = new Label
                    {
                        Text = cells[i],
                        LineBreakMode = LineBreakMode.NoWrap,
                        VerticalTextAlignment = TextAlignment.Center,
                        FontSize = 13
                    };

                    Grid.SetColumn(label, i);
                    grid.Children.Add(label);
                }
            };
        }
    }
}
