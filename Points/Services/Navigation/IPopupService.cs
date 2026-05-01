using CommunityToolkit.Maui.Views;

namespace Points.Services.Navigation
{
    public interface IPopupService
    {
        Task<object?> ShowPopupAsync(Popup popup);
    }
}
