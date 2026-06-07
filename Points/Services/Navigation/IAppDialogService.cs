namespace Points.Services.Navigation
{
    public interface IAppDialogService
    {
        Task<string?> DisplayActionSheetAsync(
            string title,
            string cancel,
            string? destruction,
            params string[] buttons);

        Task DisplayAlertAsync(string title, string message, string cancel);

        Task<bool> DisplayAlertAsync(string title, string message, string accept, string cancel);

        Task<string?> DisplayPromptAsync(
            string title,
            string message,
            string accept = "OK",
            string cancel = "Cancel",
            string? placeholder = null,
            int maxLength = -1,
            Keyboard? keyboard = null,
            string initialValue = "");
    }
}
