using Points.Models;
namespace Points.Services
{
    public interface IActiveCardNotificationService
    {
        /// <summary>
        /// Reconciles the ongoing activity notification with the current active card
        /// and the persisted Dead Air notification preference.
        /// </summary>
        Task ReconcileAsync(
            IActiveCardModel? activeCard,
            CancellationToken cancellationToken = default);
    }
}
