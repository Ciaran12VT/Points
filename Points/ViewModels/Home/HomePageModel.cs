using System.Collections.ObjectModel;
using Points.Models;

namespace Points.ViewModels.Home
{
    public class HomePageModel : ObservableObject
    {
        public string IconChar { get; set; }
        public Color BackColor { get; set; } = Colors.Black;
        public int FontSize { get; set; } = 14;
        public string Name { get; }
        public ObservableCollection<ICardModel> AllCards { get; } = new();
        public ObservableCollection<ICardModel> VisibleCards { get; } = new();
        public ObservableCollection<DashboardCellModel> DashboardCells { get; } = new();
        public bool IsDashboard => Name == "Dashboard";
        public string EmptyStateText => Name switch
        {
            "Dashboard" => "Create a card, then add a dashboard shortcut.",
            "Main Quest" => "Create a TAT or SC card.",
            "Mission" => "Create a mission card.",
            "Budgets" => "Create a budget card.",
            "Challenges & Pinned Achievements" => "Create or pin an achievement.",
            "Arcs" => "Create a value or event tracker.",
            "Goals" => "Configure goals to see progress here.",
            _ => "Add an item."
        };
        public bool IsCardReorderEnabled =>
            Name == "Main Quest" ||
            Name == "Budgets" ||
            Name == "Challenges & Pinned Achievements" ||
            Name == "Arcs";

        public HomePageModel(string name, IEnumerable<ICardModel> cards, string icon, int fontSize)
        {
            Name = name;
            FontSize = fontSize;

            foreach (var card in cards)
            {
                AllCards.Add(card);
                VisibleCards.Add(card);
            }

            IconChar = icon;
        }

        public void ResetVisible()
        {
            VisibleCards.Clear();
            foreach (var card in AllCards)
                VisibleCards.Add(card);
        }

        public void ApplyFilter(Func<ICardModel, bool> predicate)
        {
            VisibleCards.Clear();
            foreach (var card in AllCards)
            {
                if (predicate(card))
                    VisibleCards.Add(card);
            }
        }

        public void AddCard(ICardModel card)
        {
            if (IsCardReorderEnabled && card.CardID == 0 && card.DisplayOrder == 0 && AllCards.Count > 0)
                card.DisplayOrder = AllCards.Max(c => c.DisplayOrder) + 1;

            AllCards.Add(card);
            VisibleCards.Add(card);
        }

        public void SortCardsByLastActive()
        {
            var sorted = VisibleCards
                .OrderByDescending(x => ((IActiveCardModel)x).GetLastActiveTime())
                .ToArray();

            VisibleCards.Clear();
            foreach (var item in sorted)
                VisibleCards.Add(item);
        }

        public void FilterCardsByTag(string choice)
        {
            var filtered = VisibleCards
                .Where(x => x.Tags.ToLower().Contains(choice.ToLower()))
                .ToArray();

            VisibleCards.Clear();
            foreach (var item in filtered)
                VisibleCards.Add(item);
        }

        public void FilterCardsBySearchTerm(string choice)
        {
            var filtered = VisibleCards
                .Where(x =>
                    x.Tags.ToLower().Contains(choice.ToLower()) ||
                    x.Title.ToLower().Contains(choice.ToLower()))
                .ToArray();

            VisibleCards.Clear();
            foreach (var item in filtered)
                VisibleCards.Add(item);
        }

        internal void RemoveCard(ICardModel card)
        {
            AllCards.Remove(card);
            VisibleCards.Remove(card);
        }

        public bool MoveCard(ICardModel dragged, ICardModel target)
        {
            if (!IsCardReorderEnabled)
                return false;

            if (dragged == null || target == null || ReferenceEquals(dragged, target))
                return false;

            if (!AllCards.Contains(dragged) || !AllCards.Contains(target))
                return false;

            MoveWithin(AllCards, dragged, target);

            if (VisibleCards.Contains(dragged) && VisibleCards.Contains(target))
                MoveWithin(VisibleCards, dragged, target);

            NormalizeDisplayOrder();
            return true;
        }

        public bool MoveCardByOffset(ICardModel card, int offset)
        {
            if (!IsCardReorderEnabled || card == null || offset == 0)
                return false;

            var visibleIndex = VisibleCards.IndexOf(card);
            if (visibleIndex >= 0)
            {
                var visibleTargetIndex = visibleIndex + offset;
                if (visibleTargetIndex < 0 || visibleTargetIndex >= VisibleCards.Count)
                    return false;

                return MoveCard(card, VisibleCards[visibleTargetIndex]);
            }

            var allIndex = AllCards.IndexOf(card);
            var targetIndex = allIndex + offset;
            if (allIndex < 0 || targetIndex < 0 || targetIndex >= AllCards.Count)
                return false;

            return MoveCard(card, AllCards[targetIndex]);
        }

        public void NormalizeDisplayOrder()
        {
            for (var i = 0; i < AllCards.Count; i++)
                AllCards[i].DisplayOrder = i;
        }

        private static void MoveWithin(ObservableCollection<ICardModel> collection, ICardModel dragged, ICardModel target)
        {
            var oldIndex = collection.IndexOf(dragged);
            var newIndex = collection.IndexOf(target);

            if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex)
                return;

            collection.Move(oldIndex, newIndex);
        }
    }
}
