using Points.Models;

namespace Points.Global
{
    public sealed record UserMultiplierSnapshot(
        int Id,
        string Name,
        string Code,
        string Description,
        double MultiplyBy);

    public static class MultiplierRuntimeState
    {
        private static readonly object Gate = new();
        private static UserMultiplierSnapshot? _activeMultiplier;

        public static UserMultiplierSnapshot? ActiveMultiplier
        {
            get
            {
                lock (Gate)
                {
                    return _activeMultiplier;
                }
            }
        }

        public static bool HasActiveMultiplier => ActiveMultiplier != null;

        public static string ActiveCode => ActiveMultiplier?.Code ?? string.Empty;

        public static double ActiveMultiplyBy => ActiveMultiplier?.MultiplyBy ?? 1.0d;

        public static void SetActive(UserMultiplierModel? multiplier)
        {
            lock (Gate)
            {
                _activeMultiplier = multiplier == null
                    ? null
                    : new UserMultiplierSnapshot(
                        multiplier.Id,
                        multiplier.Name,
                        multiplier.Code,
                        multiplier.Description,
                        multiplier.MultiplyBy);
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                _activeMultiplier = null;
            }
        }
    }
}
