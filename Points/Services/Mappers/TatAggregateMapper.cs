using Points.Models;
using Points.Models.DbModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Points.Services.Mappers
{
    /// <summary>
    /// Maps a TAT aggregate across:
    /// - CardDbModel (shared card fields)
    /// - TatCardDbModel (tat-specific fields)
    /// - ActivityDbModel (CardID-linked)
    /// - ValueRateDbModel (TatCardID-linked)
    /// </summary>
    public sealed class TatAggregateMapper
    {
        // -----------------------
        // Db -> Business
        // -----------------------
        public TatCardModel MapToModel(
            TatCardDbModel tatDb,
            CardDbModel? cardDb,
            IEnumerable<ActivityDbModel>? activityDbRows,
            IEnumerable<ValueRateDbModel>? valueRateDbRows)
        {
            if (tatDb == null) throw new ArgumentNullException(nameof(tatDb));

            var model = new TatCardModel
            {
                // IMPORTANT: business Id maps to TatCardID (not CardID)
                Id = tatDb.TatCardID,

                // shared card fields from Card table
                Title = cardDb?.Title ?? "TAT Card",
                Tags = cardDb?.Tags ?? "",

                // tat fields
                Status = tatDb.Status ?? "",
                Description = tatDb.Description ?? "",
                ValuePerMinute = tatDb.ValuePerMinute,
            };

            // Activities (CardID-linked)
            model.Activity = (activityDbRows ?? Enumerable.Empty<ActivityDbModel>())
                .OrderBy(a => a.Start)
                .Select(a => new ActivityModel(
                    a.Start,
                    a.End,
                    rate: "Base Rate",
                    value: tatDb.ValuePerMinute))
                .ToList();

            // Value rates (TatCardID-linked)
            var rates = (valueRateDbRows ?? Enumerable.Empty<ValueRateDbModel>())
                .Select(r => new ValueRateModel
                {
                    RateName = r.RateName ?? "",
                    ValuePerMinute = r.ValuePerMinute
                })
                .ToList();

            model.ValueRates = rates;
            model.SelectedValueRateModel = rates.FirstOrDefault();

            return model;
        }

        public List<TatCardModel> MapToModels(
            IEnumerable<TatCardDbModel> tatRows,
            IEnumerable<CardDbModel> cardRows,
            IEnumerable<ActivityDbModel> activityRows,
            IEnumerable<ValueRateDbModel> valueRateRows)
        {
            tatRows ??= Enumerable.Empty<TatCardDbModel>();
            cardRows ??= Enumerable.Empty<CardDbModel>();
            activityRows ??= Enumerable.Empty<ActivityDbModel>();
            valueRateRows ??= Enumerable.Empty<ValueRateDbModel>();

            var cardsById = cardRows.ToDictionary(c => c.CardID);
            var activitiesByCardId = activityRows
                .GroupBy(a => a.CardID)
                .ToDictionary(g => g.Key, g => g.AsEnumerable());

            var ratesByTatId = valueRateRows
                .GroupBy(r => r.TatCardID)
                .ToDictionary(g => g.Key, g => g.AsEnumerable());

            var result = new List<TatCardModel>();

            foreach (var tat in tatRows)
            {
                cardsById.TryGetValue(tat.CardID, out var card);
                activitiesByCardId.TryGetValue(tat.CardID, out var acts);
                ratesByTatId.TryGetValue(tat.TatCardID, out var rates);

                result.Add(MapToModel(tat, card, acts, rates));
            }

            return result;
        }

        // -----------------------
        // Business -> Db
        // -----------------------
        public CardDbModel MapToCardDb(TatCardModel model, int cardId, string? existingCardStringId = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            return new CardDbModel
            {
                CardID = cardId,
                Title = model.Title ?? "",
                Tags = model.Tags ?? "",
                // keep existing if present; otherwise give something stable
                Id = string.IsNullOrWhiteSpace(existingCardStringId)
                    ? Guid.NewGuid().ToString("N")
                    : existingCardStringId
            };
        }

        public TatCardDbModel MapToTatDb(TatCardModel model, int tatCardId, int cardId)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            return new TatCardDbModel
            {
                TatCardID = tatCardId,
                CardID = cardId,
                ValuePerMinute = model.ValuePerMinute,
                Status = model.Status ?? "",
                Description = model.Description ?? ""
            };
        }

        public List<ActivityDbModel> MapToActivityDbRows(TatCardModel model, int cardId)
        {
            model.Activity ??= new List<ActivityModel>();

            return model.Activity
                .Where(a => a != null)
                .Select(a => new ActivityDbModel
                {
                    // ActivityID assigned by persistence layer
                    CardID = cardId,
                    Start = a.StartDate,
                    End = a.EndDate
                })
                .ToList();
        }

        public List<ValueRateDbModel> MapToValueRateDbRows(TatCardModel model, int tatCardId)
        {
            model.ValueRates ??= new List<ValueRateModel>();

            return model.ValueRates
                .Where(r => r != null)
                .Select(r => new ValueRateDbModel
                {
                    // TatCardValueRateID assigned by persistence layer
                    TatCardID = tatCardId,
                    RateName = r.RateName ?? "",
                    ValuePerMinute = r.ValuePerMinute
                })
                .ToList();
        }
    }

    public record TatAggregateDb(
        CardDbModel Card,
        TatCardDbModel Tat,
        List<ActivityDbModel> Activities,
        List<ValueRateDbModel> ValueRates);
}
