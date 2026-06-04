using Points.Services.Backup;
using Points.Services.Diagnostics;
using Points.Services.Persistence;

namespace Points
{
    public partial class App : Application
    {
        public App(
            IDatabaseInitializationService db,
            IScheduledBackupWorkScheduler scheduledBackupWorkScheduler,
            AppShell shell)
        {
            InitializeComponent();

            UserAppTheme = AppTheme.Dark;
            MainPage = shell;

            TaskSupervisor.Forget(
                db.InitializeAsync(),
                "Database initialization");

            TaskSupervisor.Forget(
                BackupFeatureFlags.AutomaticExportRuntimeEnabled
                    ? scheduledBackupWorkScheduler.SyncAsync()
                    : scheduledBackupWorkScheduler.CancelAsync(),
                "Scheduled automatic export worker sync");
        }
    }
}
