namespace Points.Services
{
    public interface IActiveCardNotificationNavigationService
    {
        event EventHandler<ActiveCardNotificationNavigationRequestedEventArgs>? NavigationRequested;

        long? PendingCardId { get; }

        void RequestNavigation(long cardId);

        void ClearPending(long cardId);
    }

    public sealed class ActiveCardNotificationNavigationRequestedEventArgs : EventArgs
    {
        public ActiveCardNotificationNavigationRequestedEventArgs(long cardId)
        {
            CardId = cardId;
        }

        public long CardId { get; }
    }

    public sealed class ActiveCardNotificationNavigationService : IActiveCardNotificationNavigationService
    {
        private readonly object _gate = new();
        private long? _pendingCardId;

        public event EventHandler<ActiveCardNotificationNavigationRequestedEventArgs>? NavigationRequested;

        public long? PendingCardId
        {
            get
            {
                lock (_gate)
                    return _pendingCardId;
            }
        }

        public void RequestNavigation(long cardId)
        {
            if (cardId <= 0)
                return;

            lock (_gate)
                _pendingCardId = cardId;

            NavigationRequested?.Invoke(
                this,
                new ActiveCardNotificationNavigationRequestedEventArgs(cardId));
        }

        public void ClearPending(long cardId)
        {
            lock (_gate)
            {
                if (_pendingCardId == cardId)
                    _pendingCardId = null;
            }
        }
    }
}
