#if ANDROID
using Android.Content;
using Points.Models;
using Points.Services.Scheduling;
using aa = Android.App;

namespace Points.Platforms.Android
{
    public sealed class AndroidScheduleNotificationPresenter : IScheduleNotificationPresenter
    {
        private readonly Context _context;

        public AndroidScheduleNotificationPresenter()
        {
            _context = aa.Application.Context;
        }

        public Task ShowScheduleFiredAsync(CardSchedule schedule, string? title, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
                return Task.FromCanceled(ct);

            ScheduleNotificationHelper.ShowScheduleFired(_context, schedule, title);
            return Task.CompletedTask;
        }
    }
}
#endif
