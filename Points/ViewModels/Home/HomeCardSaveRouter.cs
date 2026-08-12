using Points.Models;
using Points.Services.Persistence;

namespace Points.ViewModels.Home;

internal static class HomeCardSaveRouter
{
    public static Task SaveAsync(
        ICardModel card,
        ICardWriteService cardWriter,
        IBudgetService budgets,
        ITrackerService trackers,
        ITatCardService tats)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(cardWriter);
        ArgumentNullException.ThrowIfNull(budgets);
        ArgumentNullException.ThrowIfNull(trackers);
        ArgumentNullException.ThrowIfNull(tats);

        return card switch
        {
            // ScCardModel derives from TatCardModel, so it must be matched first.
            ScCardModel sc => cardWriter.SaveCardModelAsync(sc),
            TatCardModel tat when tat.CardID > 0 =>
                tats.SaveTatModelDataAsync(tat, tat.CardID),
            BudgetCardModel budget when budget.CardID > 0 =>
                budgets.SaveBudgetCardModelDataAsync(budget, budget.CardID),
            ValueTrackerCardModel valueTracker when valueTracker.CardID > 0 =>
                trackers.SaveValueTrackerCardModelDataAsync(valueTracker, valueTracker.CardID),
            EventTrackerCardModel eventTracker when eventTracker.CardID > 0 =>
                trackers.SaveEventTrackerCardModelDataAsync(eventTracker, eventTracker.CardID),
            _ => cardWriter.SaveCardModelAsync(card)
        };
    }
}
