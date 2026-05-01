using CommunityToolkit.Maui.Views;

namespace Points.Services.Navigation
{
    public sealed class MauiPopupService : IPopupService
    {
        public Task<object?> ShowPopupAsync(Popup popup)
        {
            if (popup == null)
                throw new ArgumentNullException(nameof(popup));

            var page = Shell.Current?.CurrentPage;
            return page == null
                ? Task.FromResult<object?>(null)
                : page.ShowPopupAsync(popup);
        }
    }
}
