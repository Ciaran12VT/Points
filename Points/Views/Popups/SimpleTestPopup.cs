using CommunityToolkit.Maui.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Views.Popups
{
    public sealed class SimpleTestPopup : Popup
    {
        public SimpleTestPopup(string message = "Long-press detected ✅")
        {
            CanBeDismissedByTappingOutsideOfPopup = true;

            Content = new Frame
            {
                Padding = 16,
                CornerRadius = 12,
                HasShadow = true,
                Content = new VerticalStackLayout
                {
                    Spacing = 12,
                    Children =
                {
                    new Label
                    {
                        Text = message,
                        FontSize = 16
                    },
                    new Button
                    {
                        Text = "Close",
                        Command = new Command(() => Close())
                    }
                }
                }
            };
        }
    }
}
