#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using Points.Models;
using Points.Platforms.Android;
using Points.Services;
using Points.Services.Time;
using System.Text.Json;
using aa = Android.App;

namespace Points.Platforms.Android
{
    public sealed class ActiveCardNotificationService : IActiveCardNotificationPresenter
    {
        public Task PresentAsync(
            ActiveCardNotificationRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            var context = aa.Application.Context;

            if (request.Mode == ActiveCardNotificationMode.None)
            {
                StopForegroundService(context);
                return Task.CompletedTask;
            }

            var intent = new Intent(context, typeof(ActiveCardForegroundService));
            intent.PutExtra(
                ActiveCardForegroundService.ExtraNotificationMode,
                (int)request.Mode);

            switch (request.Mode)
            {
                case ActiveCardNotificationMode.ActiveCard:
                    PutActiveCardExtras(intent, request.ActiveCard!);
                    break;

                case ActiveCardNotificationMode.DeadAir:
                    intent.PutExtra(
                        ActiveCardForegroundService.ExtraDeadAirStartedAtUtc,
                        StrictTimeSerializer.SerializeUtcInstant(request.DeadAirStartedAtUtc!.Value));
                    intent.PutExtra(
                        ActiveCardForegroundService.ExtraDeadAirAlertNoiseRequested,
                        request.AlertNoiseRequested);
                    break;

                default:
                    StopForegroundService(context);
                    throw new ArgumentOutOfRangeException(
                        nameof(request),
                        request.Mode,
                        "Unsupported active-card notification mode.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                context.StartForegroundService(intent);
            else
                context.StartService(intent);

            return Task.CompletedTask;
        }

        private static void StopForegroundService(Context context)
        {
            var stopIntent = new Intent(context, typeof(ActiveCardForegroundService));
            context.StopService(stopIntent);
        }

        private static void PutActiveCardExtras(Intent intent, IActiveCardModel cardModel)
        {
            JsonElement cardJson = JsonSerializer.SerializeToElement(cardModel, cardModel.GetType());
            intent.PutExtra(
                ActiveCardForegroundService.ExtraCardJson,
                JsonSerializer.Serialize(
                    new ActiveCardModelWrapper
                    {
                        Type = cardModel.GetType().AssemblyQualifiedName,
                        Data = cardJson
                    }));

            var prefs = aa.Application.Context.GetSharedPreferences(
                "points_service_prefs",
                FileCreationMode.Private);
            var sessionId = prefs.GetString("active_card_service_session_id", null);

            // Null is expected on the first run; the service treats it as a new session.
            intent.PutExtra(ActiveCardForegroundService.ExtraSessionId, sessionId);
        }
    }
}
#endif
