#if ANDROID
using Android.App;
using Android.Content;
using Android.Gms.Extensions;
using Android.Gms.Wearable;
using Android.Runtime;
using Android.Util;
using Points.Helpers;
using Points.Services.Watch;

namespace Points.Platforms.Android;

[Service(Exported = true)]
[IntentFilter(
    new[] { "com.google.android.gms.wearable.MESSAGE_RECEIVED", "com.google.android.gms.wearable.DATA_CHANGED" },
    DataScheme = "wear",
    DataHost = "*",
    DataPathPrefix = "/points")]
public sealed class PointsWearableListenerService : WearableListenerService
{
    public PointsWearableListenerService()
    {
    }

    public PointsWearableListenerService(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    public override void OnMessageReceived(IMessageEvent messageEvent)
    {
        base.OnMessageReceived(messageEvent);

        if (messageEvent.Path != WatchConstants.CommandPath)
            return;

        var json = System.Text.Encoding.UTF8.GetString(messageEvent.GetData());
        Log.Info(Tag, $"Received watch command message: {messageEvent.Path}");
        _ = Task.Run(async () =>
        {
            try
            {
                var result = await ServiceHelper
                    .GetService<IWatchCommandProcessor>()
                    .ProcessCommandJsonAsync(json);

                Log.Info(Tag, $"Processed watch command message. Accepted={result.Accepted}; Duplicate={result.Duplicate}; Message={result.Message}");
            }
            catch (Exception ex)
            {
                Log.Error(Tag, $"Watch command message processing failed: {ex}");
            }
        });
    }

    public override void OnDataChanged(DataEventBuffer dataEvents)
    {
        base.OnDataChanged(dataEvents);

        try
        {
            foreach (var item in dataEvents)
            {
                if (item?.Type != DataEvent.TypeChanged)
                    continue;

                var dataItem = item.DataItem;
                var path = dataItem?.Uri?.Path ?? "";
                if (!path.StartsWith(WatchConstants.EventPathPrefix, StringComparison.Ordinal))
                    continue;

                var dataMap = DataMapItem.FromDataItem(dataItem).DataMap;
                var json = dataMap.GetString(WatchConstants.EventJsonKey);
                if (string.IsNullOrWhiteSpace(json))
                    continue;

                var uri = dataItem!.Uri;
                Log.Info(Tag, $"Received watch event DataItem: {path}");
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var result = await ServiceHelper
                            .GetService<IWatchCommandProcessor>()
                            .ProcessCommandJsonAsync(json);

                        Log.Info(Tag, $"Processed watch event DataItem. Accepted={result.Accepted}; Duplicate={result.Duplicate}; Message={result.Message}");

                        await global::Android.Gms.Wearable.WearableClass
                            .GetDataClient(ApplicationContext)
                            .DeleteDataItems(uri);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(Tag, $"Watch event DataItem processing failed: {ex}");
                    }
                });
            }
        }
        finally
        {
            dataEvents.Release();
        }
    }

    private const string Tag = "PointsWearableSvc";
}
#endif
