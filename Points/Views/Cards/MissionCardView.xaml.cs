namespace Points.Views.Cards;

public partial class MissionCardView : ContentView
{
    private readonly ActiveCardLongPressForwarder _longPress = new();

    public MissionCardView()
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
