using Microsoft.Maui.ApplicationModel;
using Points.Models;
using Points.Services.Backup;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.ViewModels.Settings;
using Points.Views.Schedules;

namespace Points.Views.Settings;

public partial class DatabaseSettingsPage : ContentPage
{
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;
    private readonly IClock _clock;
    private readonly ITimeZoneService _timeZoneService;
    private readonly IScheduledBackupLogStore _scheduledBackupLogStore;

    public Command ExportDatabaseCommand { get; }
    public Command ImportDatabaseCommand { get; }
    public Command ConfigureAutomaticExportCommand { get; }
    public Command ToggleAutomaticExportCommand { get; }
    public Command ReconnectAutomaticExportCommand { get; }
    public Command ViewAutomaticExportHistoryCommand { get; }

    public DatabaseSettingsPage(
        IDatabaseMaintenanceService databaseMaintenance,
        IDatabaseInitializationService databaseLifecycle,
        IBackupFileStorageService backupFileStorage,
        IScheduledBackupConfigStore scheduledBackupConfigStore,
        IScheduledBackupLogStore scheduledBackupLogStore,
        IGoogleDriveBackupConnector googleDriveBackupConnector,
        IScheduledBackupWorkScheduler scheduledBackupWorkScheduler,
        IAppNavigationService navigation,
        IAppDialogService dialogs,
        IClock clock,
        ITimeZoneService timeZoneService)
    {
        ExportDatabaseCommand = new Command(async () => await ExportDatabaseAsync());
        ImportDatabaseCommand = new Command(async () => await ImportDatabaseAsync());
        ConfigureAutomaticExportCommand = new Command(async () => await ConfigureAutomaticExportAsync());
        ToggleAutomaticExportCommand = new Command(async () => await ToggleAutomaticExportAsync());
        ReconnectAutomaticExportCommand = new Command(async () => await ReconnectAutomaticExportAsync());
        ViewAutomaticExportHistoryCommand = new Command(async () => await ViewAutomaticExportHistoryAsync());

        InitializeComponent();
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _timeZoneService = timeZoneService ?? throw new ArgumentNullException(nameof(timeZoneService));
        _scheduledBackupLogStore = scheduledBackupLogStore ?? throw new ArgumentNullException(nameof(scheduledBackupLogStore));
        BindingContext = new DatabaseSettingsViewModel(
            databaseMaintenance,
            databaseLifecycle,
            clock,
            _dialogs,
            backupFileStorage,
            scheduledBackupConfigStore,
            scheduledBackupLogStore,
            googleDriveBackupConnector,
            scheduledBackupWorkScheduler);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is DatabaseSettingsViewModel vm)
            await vm.LoadAutomaticExportConfigAsync();
    }

    private async Task ExportDatabaseAsync()
    {
        if (BindingContext is not DatabaseSettingsViewModel vm)
            return;

        var selectionPage = new BackupSelectionPage(
            "Export",
            "Choose what to include in the Points backup package.",
            "Export",
            vm.GetExportableItems(),
            _navigation,
            _dialogs);

        await _navigation.PushModalAsync(selectionPage);
        var selectedKeys = await selectionPage.SelectionTask;

        if (selectedKeys == null)
            return;

        var targetLocation = await SelectStorageLocationAsync(
            "Export To",
            vm.GetExportLocations());

        if (targetLocation == null)
            return;

        try
        {
            var result = await vm.ExportDatabaseAsync(selectedKeys, targetLocation.Value);

            if (result != null)
                await _dialogs.DisplayAlertAsync("Export Complete", FormatExportCompleteMessage(result), "OK");
        }
        catch (Exception ex)
        {
            await _dialogs.DisplayAlertAsync("Export Failed", ex.Message, "OK");
        }
    }

    private async Task ImportDatabaseAsync()
    {
        if (BindingContext is not DatabaseSettingsViewModel vm)
            return;

        BackupImportPlan? importPlan = null;

#if ANDROID
        var sourceLocation = await SelectStorageLocationAsync(
            "Import From",
            vm.GetFileImportLocations());

        if (sourceLocation == null)
            return;

        try
        {
            importPlan = await vm.PickImportFileAsync(sourceLocation.Value);
            await ImportSelectedItemsAsync(vm, importPlan);
        }
        catch (Exception ex)
        {
            await _dialogs.DisplayAlertAsync("Import Failed", ex.Message, "OK");
        }
        finally
        {
            importPlan?.Dispose();
        }
#else
        var source = await _dialogs.DisplayActionSheetAsync(
            "Import",
            "Cancel",
            null,
            "Device storage",
            "Google Drive",
            "Backup folder");

        if (string.IsNullOrWhiteSpace(source) || source == "Cancel")
            return;

        try
        {
            importPlan = source == "Backup folder"
                ? await vm.PickImportFolderAsync()
                : await vm.PickImportFileAsync(
                    source == "Google Drive"
                        ? BackupStorageLocation.GoogleDrive
                        : BackupStorageLocation.DeviceStorage);

            await ImportSelectedItemsAsync(vm, importPlan);
        }
        catch (Exception ex)
        {
            await _dialogs.DisplayAlertAsync("Import Failed", ex.Message, "OK");
        }
        finally
        {
            importPlan?.Dispose();
        }
#endif
    }

    private async Task ImportSelectedItemsAsync(DatabaseSettingsViewModel vm, BackupImportPlan? importPlan)
    {
        if (importPlan == null)
            return;

        var selectionPage = new BackupSelectionPage(
            "Import",
            "Choose what to restore. Selected folders will replace the existing app folders.",
            "Import",
            importPlan.Resources,
            _navigation,
            _dialogs);

        await _navigation.PushModalAsync(selectionPage);
        var selectedKeys = await selectionPage.SelectionTask;

        if (selectedKeys == null)
            return;

        var confirm = await _dialogs.DisplayAlertAsync(
            "Import",
            "Selected data will replace the current data in the app. Continue?",
            "Import",
            "Cancel");

        if (!confirm)
            return;

        await vm.ImportDatabaseAsync(importPlan, selectedKeys);
        await _dialogs.DisplayAlertAsync("Import Complete", "Selected backup items were restored.", "OK");
    }

    private async Task ConfigureAutomaticExportAsync()
    {
        if (BindingContext is not DatabaseSettingsViewModel vm)
            return;

        var draft = vm.GetAutomaticExportDraft();

        var selectedKeys = await SelectAutomaticExportResourcesAsync(vm, draft.ResourceKeys);
        if (selectedKeys == null)
            return;

        draft.ResourceKeys = selectedKeys.ToList();

        var schedule = await EditAutomaticExportScheduleAsync(draft.Schedule);
        if (schedule == null)
            return;

        draft.Schedule = schedule;
        draft.IsEnabled = schedule.IsEnabled;

        var destination = await SelectAutomaticExportDestinationAsync(vm, draft.Destination);
        if (destination == null)
            return;

        draft.Destination = destination;

        var retentionCount = await PromptRetentionCountAsync(draft.RetentionCount);
        if (retentionCount == null)
            return;

        draft.RetentionCount = retentionCount.Value;

        await vm.SaveAutomaticExportConfigAsync(draft);
        await _dialogs.DisplayAlertAsync("Automatic Export", "Automatic export settings were saved.", "OK");
    }

    private async Task ToggleAutomaticExportAsync()
    {
        if (BindingContext is not DatabaseSettingsViewModel vm)
            return;

        await vm.ToggleAutomaticExportAsync();
    }

    private async Task ReconnectAutomaticExportAsync()
    {
        if (BindingContext is not DatabaseSettingsViewModel vm)
            return;

        try
        {
            await vm.ReconnectAutomaticExportGoogleDriveAsync(PresentGoogleDriveAuthorizationAsync);
            await _dialogs.DisplayAlertAsync("Google Drive", "Google Drive was reconnected.", "OK");
        }
        catch (OperationCanceledException)
        {
        }
        catch (ScheduledBackupRequiresUserActionException ex)
        {
            await _dialogs.DisplayAlertAsync("Google Drive", ex.Message, "OK");
        }
        catch (Exception ex)
        {
            await _dialogs.DisplayAlertAsync("Google Drive", $"Could not reconnect Google Drive: {ex.Message}", "OK");
        }
    }

    private async Task ViewAutomaticExportHistoryAsync()
    {
        await _navigation.PushAsync(new ScheduledBackupHistoryPage(
            _scheduledBackupLogStore,
            _timeZoneService));
    }

    private async Task<IReadOnlyList<string>?> SelectAutomaticExportResourcesAsync(
        DatabaseSettingsViewModel vm,
        IReadOnlyList<string> selectedKeys)
    {
        var selectionPage = new BackupSelectionPage(
            "Automatic Export",
            "Choose what to include in scheduled backup packages.",
            "Next",
            vm.GetExportableItems(),
            _navigation,
            _dialogs,
            selectedKeys);

        await _navigation.PushModalAsync(selectionPage);
        return await selectionPage.SelectionTask;
    }

    private async Task<ScheduledBackupSchedule?> EditAutomaticExportScheduleAsync(ScheduledBackupSchedule schedule)
    {
        var completion = new TaskCompletionSource<ScheduledBackupSchedule?>();
        var draft = CloneSchedule(schedule);

        Task OnSaved(IScheduleModel saved)
        {
            completion.TrySetResult(CloneSchedule((ScheduledBackupSchedule)saved));
            return Task.CompletedTask;
        }

        var page = new ScheduleEditPage(
            draft,
            OnSaved,
            _navigation,
            _clock,
            () => completion.TrySetResult(null));

        await _navigation.PushModalAsync(page);
        return await completion.Task;
    }

    private async Task<ScheduledBackupDestinationConfig?> SelectAutomaticExportDestinationAsync(
        DatabaseSettingsViewModel vm,
        ScheduledBackupDestinationConfig current)
    {
        var selected = await _dialogs.DisplayActionSheetAsync(
            "Automatic Export Destination",
            "Cancel",
            null,
            "Device storage",
            "Google Drive");

        if (string.IsNullOrWhiteSpace(selected) || selected == "Cancel")
            return null;

        if (selected == "Google Drive")
        {
            try
            {
                return await vm.ConnectGoogleDriveDestinationAsync(PresentGoogleDriveAuthorizationAsync);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (ScheduledBackupRequiresUserActionException ex)
            {
                await _dialogs.DisplayAlertAsync("Google Drive", ex.Message, "OK");
                return null;
            }
            catch (Exception ex)
            {
                await _dialogs.DisplayAlertAsync("Google Drive", $"Could not connect Google Drive: {ex.Message}", "OK");
                return null;
            }
        }

        return new ScheduledBackupDestinationConfig
        {
            Type = ScheduledBackupDestinationType.DeviceStorage,
            DisplayName = "App exports folder",
            DeviceFolderPath = current.Type == ScheduledBackupDestinationType.DeviceStorage
                ? current.DeviceFolderPath
                : null,
            DeviceFolderUri = current.Type == ScheduledBackupDestinationType.DeviceStorage
                ? current.DeviceFolderUri
                : null
        };
    }

    private async Task PresentGoogleDriveAuthorizationAsync(GoogleDriveDeviceAuthorizationInfo authorization)
    {
        var open = await _dialogs.DisplayAlertAsync(
            "Connect Google Drive",
            $"Google will ask you to enter this code:\n\n{authorization.UserCode}\n\nURL:\n{authorization.VerificationUrl}",
            "Open Google",
            "Cancel");

        if (!open)
            throw new OperationCanceledException();

        await Browser.Default.OpenAsync(new Uri(authorization.VerificationUrl), BrowserLaunchMode.SystemPreferred);

        await _dialogs.DisplayAlertAsync(
            "Connect Google Drive",
            "After granting access in Google, return to Points. Points will finish the connection now.",
            "Continue");
    }

    private async Task<int?> PromptRetentionCountAsync(int currentRetentionCount)
    {
        while (true)
        {
            var input = await _dialogs.DisplayPromptAsync(
                "Retention",
                "Number of backup files to keep.",
                "Save",
                "Cancel",
                keyboard: Keyboard.Numeric,
                initialValue: Math.Max(1, currentRetentionCount).ToString());

            if (input == null)
                return null;

            if (int.TryParse(input.Trim(), out var retentionCount) && retentionCount > 0)
                return retentionCount;

            await _dialogs.DisplayAlertAsync("Retention", "Enter a positive whole number.", "OK");
        }
    }

    private async Task<BackupStorageLocation?> SelectStorageLocationAsync(
        string title,
        IReadOnlyList<BackupStorageLocationOption> locations)
    {
        var choices = locations
            .Select(x => x.Title)
            .ToArray();

        var selected = await _dialogs.DisplayActionSheetAsync(
            title,
            "Cancel",
            null,
            choices);

        if (string.IsNullOrWhiteSpace(selected) || selected == "Cancel")
            return null;

        return locations.First(x => x.Title == selected).Location;
    }

    private static string FormatExportCompleteMessage(BackupExportResult result)
    {
        if (string.IsNullOrWhiteSpace(result.FilePath))
            return $"Saved backup to {result.DisplayLocation}.";

        return result.Location == BackupStorageLocation.GoogleDrive
            ? $"Saved backup using the Google Drive location:\n{result.FilePath}"
            : $"Saved backup to:\n{result.FilePath}";
    }

    private static ScheduledBackupSchedule CloneSchedule(ScheduledBackupSchedule schedule)
    {
        return new ScheduledBackupSchedule
        {
            FrequencyType = schedule.FrequencyType,
            FrequencyValue = schedule.FrequencyValue,
            FromDateTime = schedule.FromDateTime,
            ToDateTime = schedule.ToDateTime,
            IsEnabled = schedule.IsEnabled,
            Note = schedule.Note
        };
    }
}
