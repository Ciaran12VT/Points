using Points.Services.Sqlite.Interfaces;

namespace Points
{
    public partial class App : Application
    {
        public App(IDbService db, AppShell shell)
        {
            InitializeComponent();

            MainPage = shell;

            // Kick off init (don’t block UI thread).
            _ = db.InitializeAsync();
        }
    }
}
