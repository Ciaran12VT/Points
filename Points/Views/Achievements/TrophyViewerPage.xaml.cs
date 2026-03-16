using Points.Global;
using Points.Models;
using Points.Services.Sqlite.Interfaces;

namespace Points.Views.Achievements;

public partial class TrophyViewerPage : ContentPage
{
    private readonly IDbService _db;
    private readonly TrophyModel _trophy;

    public TrophyViewerPage(TrophyModel trophy, IDbService db)
	{
		InitializeComponent();
        _db = db;
        _trophy = trophy;
        BindingContext = trophy;
    }

    private async void OnShareClicked(object sender, EventArgs e)
    {
        // You need an actual file path for sharing.
        // If ImageSource is already a local path, use it.
        var path = GetLocalPath(_trophy);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            await DisplayAlert("Share", "Couldn't find the image file to share.", "OK");
            return;
        }

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = _trophy.Title,
            File = new ShareFile(path)
        });
    }

    private async void OnSaveAsClicked(object sender, EventArgs e)
    {
        // “Save As” is platform-specific if you want user-visible gallery/downloads.
        // For now: copy into FileSystem.AppDataDirectory/Exports and tell the user where it is.
        // (Later: implement Android MediaStore, iOS Photos, or use a file-saver library.)

        var path = GetLocalPath(_trophy);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            await DisplayAlert("Save As", "Couldn't find the image file to save.", "OK");
            return;
        }

        var exportsDir = Path.Combine(FileSystem.AppDataDirectory, "exports");
        Directory.CreateDirectory(exportsDir);

        var ext = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".png";

        var safeName = MakeSafeFileName(_trophy.Title);
        var destPath = Path.Combine(exportsDir, $"{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");

        File.Copy(path, destPath, overwrite: true);

        await DisplayAlert("Saved", $"Saved to:\n{destPath}", "OK");
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlert(
            "Delete trophy?",
            "This will remove the trophy. Continue?",
            "Delete",
            "Cancel");

        if (!confirm)
            return;

        // 1) Delete from DB
        await _db.DeleteAchievementTrophyAsync(_trophy.Id);

        // 3) Close modal
        await Shell.Current.Navigation.PopModalAsync();
    }

    private static string? GetLocalPath(TrophyModel trophy)
    {
        // Prefer a dedicated LocalPath if you have it.
        // If you only have ImageSource as a string path, use that.
        // Adjust this function to match your actual TrophyItem model.
        var p = trophy.ImageSource;

        // If ImageSource is something like "trophy.png" (app resource),
        // then Share/SaveAs won't work without exporting it first.
        // In that case return null and handle it properly.
        if (string.IsNullOrWhiteSpace(p)) return null;
        if (!Path.IsPathRooted(p)) return null;

        return p;
    }

    private static string MakeSafeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "trophy";
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Trim();
    }
}