using Points.Models;
using Points.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Views.Cards
{
    public class CardTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? TatTemplate { get; set; }
        public DataTemplate? ScTemplate { get; set; }
        public DataTemplate? MissionTemplate { get; set; }
        public DataTemplate? BudgetTemplate { get; set; }
        public DataTemplate? DateHeaderCardTemplate { get; set; }
        public DataTemplate? AchievementTemplate { get; set; }
        public DataTemplate? TrackerTemplate { get; set; }
        public DataTemplate? GoalTemplate { get; set; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        {
            return item switch
            {
                BudgetCardModel => BudgetTemplate ?? TatTemplate!,
                MissionCardModel => MissionTemplate ?? TatTemplate!,
                ScCardModel => ScTemplate ?? TatTemplate!,
                TatCardModel => TatTemplate!,
                DateHeaderCardModel => DateHeaderCardTemplate!,
                AchievementCardModel => AchievementTemplate!,
                ValueTrackerCardModel => TrackerTemplate!,
                EventTrackerCardModel => TrackerTemplate!,
                GoalProgressRowVm => GoalTemplate!,
                _ => TatTemplate!
            };
        }
    }

    public class HomePaneTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? DefaultPaneTemplate { get; set; }
        public DataTemplate? DashboardPaneTemplate { get; set; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        {
            if (item is HomePageModel page)
            {
                if (page.IsDashboard)
                    return DashboardPaneTemplate ?? DefaultPaneTemplate!;
            }

            return DefaultPaneTemplate!;
        }
    }
}
