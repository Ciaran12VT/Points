using Points.Global;
using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;

namespace Points.ViewModels.Achievements;

public sealed class TrophyViewerViewModel
{
    private readonly TrophyModel _trophy;
    private readonly IAchievementService _achievements;
    private readonly IAppNavigationService _navigation;
    private readonly IAppDialogService _dialogs;
    private readonly IClock _clock;

    public Command SaveAsCommand { get; }

    public Command ShareCommand { get; }

    public Command DeleteCommand { get; }

    public string ImageSource => _trophy.ImageSource;

    public TrophyViewerViewModel(
        TrophyModel trophy,
        IAchievementService achievements,
        IAppNavigationService navigation,
        IAppDialogService dialogs,
        IClock clock)
    {
        _trophy = trophy ?? throw new ArgumentNullException(nameof(trophy));
        _achievements = achievements ?? throw new ArgumentNullException(nameof(achievements));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

        SaveAsCommand = new Command(async () => await SaveAsAsync());
        ShareCommand = new Command(async () => await ShareAsync());
        DeleteCommand = new Command(async () => await DeleteAsync());
    }

    private async Task ShareAsync()
    {
        var path = GetLocalPath(_trophy);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            await _dialogs.DisplayAlertAsync("Share", "Couldn't find the image file to share.", "OK");
            return;
        }

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = _trophy.Title,
            File = new ShareFile(path)
        });
    }

    private async Task SaveAsAsync()
    {
        var path = GetLocalPath(_trophy);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            await _dialogs.DisplayAlertAsync("Save As", "Couldn't find the image file to save.", "OK");
            return;
        }

        var exportsDir = AppPaths.ExportsFolder;

        var extension = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".png";

        var safeName = MakeSafeFileName(_trophy.Title);
        var exportTimestamp = _clock.LocalNow.ToString("yyyyMMdd_HHmmss");
        var destinationPath = Path.Combine(exportsDir, $"{safeName}_{exportTimestamp}{extension}");

        File.Copy(path, destinationPath, overwrite: true);

        await _dialogs.DisplayAlertAsync("Saved", $"Saved to:\n{destinationPath}", "OK");
    }

    private async Task DeleteAsync()
    {
        var confirm = await _dialogs.DisplayAlertAsync(
            "Delete trophy?",
            "This will remove the trophy. Continue?",
            "Delete",
            "Cancel");

        if (!confirm)
            return;

        await _achievements.DeleteAchievementTrophyAsync(_trophy.Id);
        await _navigation.PopModalAsync();
    }

    private static string? GetLocalPath(TrophyModel trophy)
    {
        var path = trophy.ImageSource;

        if (string.IsNullOrWhiteSpace(path))
            return null;

        return Path.IsPathRooted(path) ? path : null;
    }

    private static string MakeSafeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "trophy";

        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return string.IsNullOrWhiteSpace(name.Trim()) ? "trophy" : name.Trim();
    }
}
