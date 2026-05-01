using Points.Services.Persistence;

namespace Points
{
    public partial class App : Application
    {
        public App(IDatabaseInitializationService db, AppShell shell)
        {
            InitializeComponent();

            MainPage = shell;

            // Kick off init (don’t block UI thread).
            Points.Services.Diagnostics.TaskSupervisor.Forget(
                db.InitializeAsync(),
                "Database initialization");
        }
    }
}
