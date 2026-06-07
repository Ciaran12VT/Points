namespace Points.Services.Navigation
{
    public sealed class MauiPageService : IAppPageService
    {
        public Page? CurrentPage => Shell.Current?.CurrentPage;
    }
}
