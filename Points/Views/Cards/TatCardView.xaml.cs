namespace Points.Views.Cards;

public partial class TatCardView : ContentView
{
    private readonly ActiveCardLongPressForwarder _longPress = new();

    public TatCardView()
    {
        InitializeComponent();
        Unloaded += (_, __) => _longPress.Dispose();
    }

    private void ActivityToggleButton_Pressed(object sender, EventArgs e)
    {
        _longPress.Pressed(sender);
    }

    private void ActivityToggleButton_Released(object sender, EventArgs e)
    {
        _longPress.Released();
    }
}
