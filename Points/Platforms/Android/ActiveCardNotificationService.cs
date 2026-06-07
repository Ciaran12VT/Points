#if ANDROID
using Android.App;
using Android.Content;
using Points.Models;
using Points.Platforms.Android;
using Points.Services;
using System.Text.Json;
using aa = Android.App;

namespace Points.Platforms.Android
{
    public class ActiveCardNotificationService : IActiveCardNotificationService
    {
        public void UpdateActiveCardNotification(IActiveCardModel? cardModel)
        {
            var context = aa.Application.Context;

            if (cardModel is null)
            {
                // Stop the foreground service = remove persistent notification
                var stopIntent = new Intent(context, typeof(ActiveCardForegroundService));
                context.StopService(stopIntent);
            }
            else
            {
                // Start/update the foreground service with the new title
                var intent = new Intent(context, typeof(ActiveCardForegroundService));
                JsonElement cardJson = JsonSerializer.SerializeToElement(cardModel, cardModel.GetType());
                intent.PutExtra(
                    ActiveCardForegroundService.ExtraCardJson, 
                    JsonSerializer.Serialize(
                        new ActiveCardModelWrapper() 
                        { 
                            Type = cardModel.GetType().AssemblyQualifiedName, 
                            Data = cardJson
                        }
                     )
                );

                var prefs = aa.Application.Context.GetSharedPreferences("points_service_prefs", FileCreationMode.Private);
                var sessionId = prefs.GetString("active_card_service_session_id", null);

                // include it (null is fine on first ever run; service will treat as mismatch and seed)
                intent.PutExtra("EXTRA_SERVICE_SESSION_ID", sessionId);


                context.StartForegroundService(intent);
            }
        }
    }
}
#endif
