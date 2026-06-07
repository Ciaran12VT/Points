using System.Diagnostics.CodeAnalysis;
using Points.ViewModels.Shortcuts;
using Points.Models;

namespace Points.ViewModels.Home
{
    internal static class HomeDashboardShortcutCoordinator
    {
        private const int DashboardColumnCount = 4;

        public static void RebuildDashboardCells(HomePageModel dashboardPage, IEnumerable<ShortcutModel> shortcuts)
        {
            dashboardPage.DashboardCells.Clear();

            var orderedGroups = shortcuts
                .Where(s => s.Group != null)
                .GroupBy(s => s.Group!.ShortcutGroupId)
                .Select(g => new
                {
                    Group = g.First().Group!,
                    Items = g.OrderBy(s => s.ShortcutOrder).ToList()
                })
                .OrderBy(x => x.Group.ShortcutGroupOrder)
                .ToList();

            foreach (var group in orderedGroups)
            {
                foreach (var shortcut in group.Items)
                {
                    dashboardPage.DashboardCells.Add(new DashboardCellModel
                    {
                        IsPlaceholder = false,
                        Shortcut = shortcut
                    });
                }

                PadDashboardRow(dashboardPage);
            }
        }

        public static Dictionary<TargetCardType, List<CardOption>> BuildShortcutOptionsByType(
            IEnumerable<HomePageModel> pages)
        {
            var dict = new Dictionary<TargetCardType, List<CardOption>>();

            AddOptions(dict, TargetCardType.MainQuest, pages.FirstOrDefault(p => p.Name == "Main Quest"));
            AddOptions(dict, TargetCardType.Mission, pages.FirstOrDefault(p => p.Name == "Mission"));
            AddOptions(dict, TargetCardType.Budget, pages.FirstOrDefault(p => p.Name == "Budgets"));
            AddOptions(dict, TargetCardType.Achievement, pages.FirstOrDefault(p => p.Name == "Challenges & Pinned Achievements"));
            AddOptions(dict, TargetCardType.Arc, pages.FirstOrDefault(p => p.Name == "Arcs"));
            AddOptions(dict, TargetCardType.Goal, pages.FirstOrDefault(p => p.Name == "Goals"));

            foreach (TargetCardType type in Enum.GetValues(typeof(TargetCardType)))
            {
                if (!dict.ContainsKey(type))
                    dict[type] = new List<CardOption>();
            }

            return dict;
        }

        public static CardOption? FindDefaultTarget(Dictionary<TargetCardType, List<CardOption>> optionsByType)
        {
            return (optionsByType.TryGetValue(TargetCardType.MainQuest, out var mainQuest)
                    ? mainQuest.FirstOrDefault()
                    : null)
                ?? optionsByType.SelectMany(kvp => kvp.Value).FirstOrDefault();
        }

        public static int GetNextShortcutOrder(IReadOnlyCollection<ShortcutModel> shortcuts)
        {
            return shortcuts.Count == 0 ? 1 : shortcuts.Max(s => s.ShortcutOrder) + 1;
        }

        public static ShortcutModel CreateNewShortcut(CardOption defaultTarget, int shortcutOrder)
        {
            return new ShortcutModel
            {
                ShortcutId = 0,
                IconChar = "★",
                TargetCardId = defaultTarget.CardId,
                ShortcutOrder = shortcutOrder,
                ShortcutGroupId = 0,
                Group = CreateEmptyGroup()
            };
        }

        public static bool TryPrepareShortcutForEdit(
            ShortcutModel shortcut,
            Dictionary<TargetCardType, List<CardOption>> optionsByType,
            [NotNullWhen(true)] out ShortcutModel? model,
            out TargetCardType defaultType)
        {
            model = null;
            defaultType = ResolveTargetType(shortcut.TargetCardId, optionsByType) ?? TargetCardType.MainQuest;

            var targetStillExists = optionsByType
                .SelectMany(kvp => kvp.Value)
                .Any(o => o.CardId == shortcut.TargetCardId);

            if (!targetStillExists)
            {
                var fallbackTarget = FindDefaultTarget(optionsByType);
                if (fallbackTarget == null || fallbackTarget.CardId <= 0)
                    return false;

                shortcut.TargetCardId = fallbackTarget.CardId;
                defaultType = ResolveTargetType(fallbackTarget.CardId, optionsByType) ?? TargetCardType.MainQuest;
            }

            model = CloneShortcut(shortcut);
            return true;
        }

        private static void AddOptions(
            Dictionary<TargetCardType, List<CardOption>> dict,
            TargetCardType type,
            HomePageModel? page)
        {
            if (page != null)
                dict[type] = ToOptions(page.AllCards);
        }

        private static List<CardOption> ToOptions(IEnumerable<ICardModel> cards)
        {
            return cards
                .Where(c => c.CardID > 0)
                .Where(c => !IsHeaderCard(c))
                .Select(c => new CardOption { CardId = c.CardID, Title = c.Title ?? "" })
                .Where(o => !string.IsNullOrWhiteSpace(o.Title))
                .OrderBy(o => o.Title)
                .ToList();
        }

        private static bool IsHeaderCard(ICardModel card)
        {
            return card is DateHeaderCardModel || card is TimeScopeHeaderCardModel;
        }

        private static TargetCardType? ResolveTargetType(
            long cardId,
            Dictionary<TargetCardType, List<CardOption>> optionsByType)
        {
            var match = optionsByType.FirstOrDefault(kvp => kvp.Value.Any(o => o.CardId == cardId));
            return match.Equals(default(KeyValuePair<TargetCardType, List<CardOption>>))
                ? null
                : match.Key;
        }

        private static ShortcutModel CloneShortcut(ShortcutModel shortcut)
        {
            return new ShortcutModel
            {
                ShortcutId = shortcut.ShortcutId,
                IconChar = shortcut.IconChar,
                TargetCardId = shortcut.TargetCardId,
                ShortcutOrder = shortcut.ShortcutOrder,
                ShortcutGroupId = shortcut.ShortcutGroupId,
                Group = shortcut.Group == null
                    ? CreateEmptyGroup()
                    : new ShortcutGroupModel
                    {
                        ShortcutGroupId = shortcut.Group.ShortcutGroupId,
                        Name = shortcut.Group.Name,
                        Color = shortcut.Group.Color,
                        ShortcutGroupOrder = shortcut.Group.ShortcutGroupOrder
                    }
            };
        }

        private static ShortcutGroupModel CreateEmptyGroup()
        {
            return new ShortcutGroupModel
            {
                ShortcutGroupId = 0,
                Name = "",
                Color = Colors.Black,
                ShortcutGroupOrder = 0
            };
        }

        private static void PadDashboardRow(HomePageModel dashboardPage)
        {
            var remainder = dashboardPage.DashboardCells.Count % DashboardColumnCount;
            if (remainder == 0)
                return;

            var pad = DashboardColumnCount - remainder;
            for (var i = 0; i < pad; i++)
            {
                dashboardPage.DashboardCells.Add(new DashboardCellModel
                {
                    IsPlaceholder = true,
                    Shortcut = null
                });
            }
        }
    }
}
