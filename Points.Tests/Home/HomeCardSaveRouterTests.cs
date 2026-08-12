using Points.Models;
using Points.Services.Persistence;
using Points.ViewModels.Home;
using Xunit;

namespace Points.Tests.Home;

public sealed class HomeCardSaveRouterTests
{
    [Fact]
    public async Task SaveAsync_ExistingScCardUsesCardWriterInsteadOfTatService()
    {
        var cardWriter = new RecordingCardWriter();
        var tats = new RecordingTatCardService();
        var sc = new ScCardModel
        {
            Id = 7,
            CardID = 42,
            Title = "Persist my points"
        };

        await HomeCardSaveRouter.SaveAsync(
            sc,
            cardWriter,
            new UnexpectedBudgetService(),
            new UnexpectedTrackerService(),
            tats);

        Assert.Same(sc, Assert.Single(cardWriter.SavedCards));
        Assert.Empty(tats.SavedCards);
    }

    [Fact]
    public async Task SaveAsync_ExistingTatCardStillUsesTatService()
    {
        var cardWriter = new RecordingCardWriter();
        var tats = new RecordingTatCardService();
        var tat = new TatCardModel
        {
            Id = 8,
            CardID = 43,
            Title = "Persist my time"
        };

        await HomeCardSaveRouter.SaveAsync(
            tat,
            cardWriter,
            new UnexpectedBudgetService(),
            new UnexpectedTrackerService(),
            tats);

        Assert.Empty(cardWriter.SavedCards);
        var saved = Assert.Single(tats.SavedCards);
        Assert.Same(tat, saved.Model);
        Assert.Equal(tat.CardID, saved.CardId);
    }

    private sealed class RecordingCardWriter : ICardWriteService
    {
        public List<ICardModel> SavedCards { get; } = new();

        public Task SaveCardModelAsync(ICardModel model)
        {
            SavedCards.Add(model);
            return Task.CompletedTask;
        }

        public Task SaveCardDisplayOrderAsync(IReadOnlyList<ICardModel> orderedCards) =>
            throw new NotSupportedException();

        public Task<bool> WouldArchiveCardModelOnDeleteAsync(ICardModel model) =>
            throw new NotSupportedException();

        public Task DeleteCardModelAsync(ICardModel model) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingTatCardService : ITatCardService
    {
        public List<(TatCardModel Model, long CardId)> SavedCards { get; } = new();

        public Task<TatCardModel> GetTatModelDataAsync(int id) =>
            throw new NotSupportedException();

        public Task<List<TatCardModel>> GetTatModelsDataAsync(DateTime rangeStart, DateTime rangeEnd) =>
            throw new NotSupportedException();

        public Task SaveTatModelDataAsync(TatCardModel model, long cardId)
        {
            SavedCards.Add((model, cardId));
            return Task.CompletedTask;
        }
    }

    private sealed class UnexpectedBudgetService : IBudgetService
    {
        public Task<BudgetCardModel> GetBudgetCardModelDataAsync(int id) =>
            throw new NotSupportedException();

        public Task<List<BudgetCardModel>> GetBudgetCardModelsDataAsync(string? whereClause = null) =>
            throw new NotSupportedException();

        public Task SaveBudgetCardModelDataAsync(BudgetCardModel model, long cardId) =>
            throw new InvalidOperationException("The SC/TAT routing tests must not use budget persistence.");
    }

    private sealed class UnexpectedTrackerService : ITrackerService
    {
        public Task<ValueTrackerCardModel> GetValueTrackerCardModelDataAsync(int id) =>
            throw new NotSupportedException();

        public Task<List<ValueTrackerCardModel>> GetValueTrackerCardModelsDataAsync(string? whereClause = null) =>
            throw new NotSupportedException();

        public Task<EventTrackerCardModel> GetEventTrackerCardModelDataAsync(int id) =>
            throw new NotSupportedException();

        public Task<List<EventTrackerCardModel>> GetEventTrackerCardModelsDataAsync(string? whereClause = null) =>
            throw new NotSupportedException();

        public Task SaveValueTrackerCardModelDataAsync(ValueTrackerCardModel model, long cardId) =>
            throw new InvalidOperationException("The SC/TAT routing tests must not use tracker persistence.");

        public Task SaveEventTrackerCardModelDataAsync(EventTrackerCardModel model, long cardId) =>
            throw new InvalidOperationException("The SC/TAT routing tests must not use tracker persistence.");
    }
}
