using Points.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Views.Cards
{
    public class AchievementCardTemplateSelector : DataTemplateSelector
    {
        public DataTemplate AchievementTemplate { get; set; } = null!;

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        {
            if (item is AchievementCardModel)
                return AchievementTemplate;

            throw new InvalidOperationException(
                $"Unsupported item type: {item?.GetType().Name}");
        }
    }
}
