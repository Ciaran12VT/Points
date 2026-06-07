using Points.Global;

namespace Points.Models
{
    public static class MultiplierValueCalculator
    {
        public static double GetValue(ICardModel card, DateTime start, DateTime end)
        {
            if (card == null)
                return 0d;

            return ApplyToCard(card, card.GetValue(start, end));
        }

        public static double ApplyToCard(ICardModel card, double value)
        {
            if (card == null || !IsAffectedCard(card))
                return value;

            return value * MultiplierRuntimeState.ActiveMultiplyBy;
        }

        public static bool IsAffectedCard(ICardModel card)
        {
            return card is MissionCardModel or TatCardModel;
        }
    }
}
