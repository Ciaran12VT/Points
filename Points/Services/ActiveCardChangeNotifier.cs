using Points.Models;

namespace Points.Services
{
    public interface IActiveCardChangeNotifier
    {
        event EventHandler<ActiveCardChangedEventArgs>? ActiveCardChanged;
        event EventHandler<CardDataChangedEventArgs>? CardDataChanged;

        void NotifyActiveCardChanged(long? activePhoneCardId, ToggleActivityModelResult? toggleResult = null);
        void NotifyCardDataChanged(long phoneCardId, string reason);
    }

    public sealed class ActiveCardChangedEventArgs : EventArgs
    {
        public ActiveCardChangedEventArgs(long? activePhoneCardId, ToggleActivityModelResult? toggleResult)
        {
            ActivePhoneCardId = activePhoneCardId;
            ToggleResult = toggleResult;
        }

        public long? ActivePhoneCardId { get; }
        public ToggleActivityModelResult? ToggleResult { get; }
    }

    public sealed class ActiveCardChangeNotifier : IActiveCardChangeNotifier
    {
        public event EventHandler<ActiveCardChangedEventArgs>? ActiveCardChanged;
        public event EventHandler<CardDataChangedEventArgs>? CardDataChanged;

        public void NotifyActiveCardChanged(long? activePhoneCardId, ToggleActivityModelResult? toggleResult = null)
        {
            ActiveCardChanged?.Invoke(this, new ActiveCardChangedEventArgs(activePhoneCardId, toggleResult));
        }

        public void NotifyCardDataChanged(long phoneCardId, string reason)
        {
            CardDataChanged?.Invoke(this, new CardDataChangedEventArgs(phoneCardId, reason));
        }
    }

    public sealed class CardDataChangedEventArgs : EventArgs
    {
        public CardDataChangedEventArgs(long phoneCardId, string reason)
        {
            PhoneCardId = phoneCardId;
            Reason = reason;
        }

        public long PhoneCardId { get; }
        public string Reason { get; }
    }
}
