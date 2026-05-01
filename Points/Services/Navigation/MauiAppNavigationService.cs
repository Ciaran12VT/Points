namespace Points.Services.Navigation
{
    public sealed class MauiAppNavigationService : IAppNavigationService
    {
        public Task PushAsync(Page page)
        {
            if (page == null)
                throw new ArgumentNullException(nameof(page));

            var navigation = Shell.Current?.Navigation
                ?? throw new InvalidOperationException("Shell navigation is not available.");

            return navigation.PushAsync(page);
        }

        public Task PushModalAsync(Page page)
        {
            if (page == null)
                throw new ArgumentNullException(nameof(page));

            var navigation = Shell.Current?.Navigation
                ?? throw new InvalidOperationException("Shell navigation is not available.");

            return navigation.PushModalAsync(page);
        }

        public Task PopAsync()
        {
            var navigation = Shell.Current?.Navigation
                ?? throw new InvalidOperationException("Shell navigation is not available.");

            return navigation.PopAsync();
        }

        public Task PopModalAsync()
        {
            var navigation = Shell.Current?.Navigation
                ?? throw new InvalidOperationException("Shell navigation is not available.");

            return navigation.PopModalAsync();
        }
    }
}
