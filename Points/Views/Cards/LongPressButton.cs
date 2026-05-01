using System.Windows.Input;

namespace Points.Views.Cards;

internal sealed class LongPressButton : Button
{
    private const int DefaultLongPressMilliseconds = 600;

    public static readonly BindableProperty TapCommandProperty = BindableProperty.Create(
        nameof(TapCommand),
        typeof(ICommand),
        typeof(LongPressButton));

    public static readonly BindableProperty LongPressCommandProperty = BindableProperty.Create(
        nameof(LongPressCommand),
        typeof(ICommand),
        typeof(LongPressButton));

    public static readonly BindableProperty LongPressMillisecondsProperty = BindableProperty.Create(
        nameof(LongPressMilliseconds),
        typeof(int),
        typeof(LongPressButton),
        DefaultLongPressMilliseconds);

    private CancellationTokenSource? _longPressCts;
    private bool _longPressFired;

    public LongPressButton()
    {
        Clicked += OnClicked;
        Pressed += OnPressed;
        Released += OnReleased;
        Unfocused += OnUnfocused;
        Unloaded += OnUnloaded;
    }

    public ICommand? TapCommand
    {
        get => (ICommand?)GetValue(TapCommandProperty);
        set => SetValue(TapCommandProperty, value);
    }

    public ICommand? LongPressCommand
    {
        get => (ICommand?)GetValue(LongPressCommandProperty);
        set => SetValue(LongPressCommandProperty, value);
    }

    public int LongPressMilliseconds
    {
        get => (int)GetValue(LongPressMillisecondsProperty);
        set => SetValue(LongPressMillisecondsProperty, value);
    }

    private void OnPressed(object? sender, EventArgs e)
    {
        CancelPendingLongPress();
        _longPressFired = false;

        var cts = new CancellationTokenSource();
        _longPressCts = cts;
        var parameter = ResolveCommandParameter();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(LongPressMilliseconds, cts.Token);
                if (cts.Token.IsCancellationRequested)
                    return;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!ReferenceEquals(_longPressCts, cts))
                        return;

                    if (LongPressCommand?.CanExecute(parameter) == true)
                    {
                        _longPressFired = true;
                        LongPressCommand.Execute(parameter);
                    }
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

    private void OnReleased(object? sender, EventArgs e)
    {
        CancelPendingLongPress();
    }

    private void OnUnfocused(object? sender, FocusEventArgs e)
    {
        CancelPendingLongPress();
    }

    private void OnClicked(object? sender, EventArgs e)
    {
        if (_longPressFired)
        {
            _longPressFired = false;
            return;
        }

        var parameter = ResolveCommandParameter();
        if (TapCommand?.CanExecute(parameter) == true)
            TapCommand.Execute(parameter);
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        CancelPendingLongPress();
    }

    private object? ResolveCommandParameter()
    {
        return CommandParameter ?? BindingContext;
    }

    private void CancelPendingLongPress()
    {
        _longPressCts?.Cancel();
        _longPressCts?.Dispose();
        _longPressCts = null;
    }
}
