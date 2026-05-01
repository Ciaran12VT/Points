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
    }
}
