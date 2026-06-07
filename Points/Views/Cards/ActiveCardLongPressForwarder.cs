using Points.Models;

namespace Points.Views.Cards;

internal sealed class ActiveCardLongPressForwarder : IDisposable
{
    private const int LongPressMs = 600;

    private CancellationTokenSource? _longPressCts;

    public void Pressed(object? sender)
    {
        CancelPending();

        var cts = new CancellationTokenSource();
        _longPressCts = cts;

        if (sender is not Button button)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(LongPressMs, cts.Token);
                if (cts.Token.IsCancellationRequested)
                    return;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!ReferenceEquals(_longPressCts, cts))
                        return;

                    if (button.BindingContext is IActiveCardModel card)
                        card.FireLongPressRequested(card);
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }, cts.Token);
    }

    public void Released()
    {
        _longPressCts?.Cancel();
    }

    public void Dispose()
    {
        CancelPending();
    }

    private void CancelPending()
    {
        _longPressCts?.Cancel();
        _longPressCts?.Dispose();
        _longPressCts = null;
    }
}
