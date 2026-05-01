namespace Points.Services.Navigation
{
    public interface IAppNavigationService
    {
        Task PushAsync(Page page);

        Task PushModalAsync(Page page);

        Task PopAsync();

        Task PopModalAsync();
    }
}
