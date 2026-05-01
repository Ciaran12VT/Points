#if ANDROID
using Android.Content;
using Android.Runtime;
using AndroidX.Work;
using Points.Helpers;
using Points.Services.Backup;

namespace Points.Platforms.Android
{
    [Register("com.companyname.points.ScheduledBackupWorker")]
    public sealed class ScheduledBackupWorker : Worker
    {
        public ScheduledBackupWorker(Context context, WorkerParameters workerParameters)
            : base(context, workerParameters)
        {
        }

        public override Result DoWork()
        {
            try
            {
                var runner = ServiceHelper.GetService<IScheduledBackupRunner>();
                var outcome = runner.RunDueAsync().GetAwaiter().GetResult();

                return outcome.Result == ScheduledBackupRunResult.Failed
                    ? Result.InvokeRetry()
                    : Result.InvokeSuccess();
            }
            catch (OperationCanceledException)
            {
                return Result.InvokeRetry();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Scheduled backup worker failed: {ex}");
                return Result.InvokeRetry();
            }
        }
    }
}
#endif
