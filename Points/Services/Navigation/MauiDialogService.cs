namespace Points.Services.Navigation
{
    public sealed class MauiDialogService : IAppDialogService
    {
        public Task<string?> DisplayActionSheetAsync(
            string title,
            string cancel,
            string? destruction,
            params string[] buttons)
        {
            var page = Shell.Current?.CurrentPage;
            if (page == null)
                return Task.FromResult<string?>(null);

            return page.DisplayActionSheet(title, cancel, destruction, buttons);
        }

        public Task DisplayAlertAsync(string title, string message, string cancel)
        {
            var page = Shell.Current?.CurrentPage;
            return page?.DisplayAlert(title, message, cancel) ?? Task.CompletedTask;
        }

        public Task<bool> DisplayAlertAsync(string title, string message, string accept, string cancel)
        {
            var page = Shell.Current?.CurrentPage;
            return page?.DisplayAlert(title, message, accept, cancel) ?? Task.FromResult(false);
        }

        public async Task<string?> DisplayPromptAsync(
            string title,
            string message,
            string accept = "OK",
            string cancel = "Cancel",
            string? placeholder = null,
            int maxLength = -1,
            Keyboard? keyboard = null,
            string initialValue = "")
        {
            var page = Shell.Current?.CurrentPage;
            if (page == null)
                return null;

            return await page.DisplayPromptAsync(
                title,
                message,
                accept,
                cancel,
                placeholder,
                maxLength,
                keyboard,
                initialValue);
        }
    }
}
