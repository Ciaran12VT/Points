using Points.Models;
using Points.ViewModels;
using System;
using System.Formats.Tar;

namespace Points.Views.Details;

public partial class AchievementDetailsPage : ContentPage
{
   private readonly List<string> _allTags;
   private readonly List<string> _stepNames;
   private readonly List<string> _reportNames;
   private readonly List<string> _achievementTitles;

    public AchievementDetailsPage(
        AchievementCardModel model,
        IEnumerable<string> allTags,
        IEnumerable<string> stepNames,
        IEnumerable<string> achievementTitles,
        Func<AchievementCardModel, Task> onSaved,
        Action<AchievementCardModel> onDelete)
    {
        InitializeComponent();

        _allTags = allTags?.Distinct().OrderBy(x => x).ToList() ?? new List<string>();
        _stepNames = stepNames?.Distinct().OrderBy(x => x).ToList() ?? new List<string>();
        _reportNames = new List<string> { "Report 1", "Report 2" };
        _achievementTitles = achievementTitles?.Distinct().OrderBy(x => x).ToList() ?? new List<string>();

        BindingContext = new AchievementDetailsViewModel(model, onSaved, onDelete);

        var tagEntry = this.FindByName<Entry>("TagsEntry");
        //var difficultyLevelPicker = this.FindByName<Picker>("DifficultyLevelPicker");
        //var targetTypePicker = this.FindByName<Picker>("TargetTypePicker");
        //var completionTypePicker = this.FindByName<Picker>("CompletionTypePicker");
        //var rangeUnitPicker = this.FindByName<Picker>("RangeUnitPicker");
        var stepPicker = this.FindByName<Picker>("StepPicker");
        var reportPicker = this.FindByName<Picker>("CustomReportPicker");

        //targetTypePicker.ItemsSource = Enum.GetValues(typeof(AchievementTargetType)).Cast<AchievementTargetType>().ToList();
        //completionTypePicker.ItemsSource = Enum.GetValues(typeof(AchievementCompletionType)).Cast<AchievementCompletionType>().ToList();
        //rangeUnitPicker.ItemsSource = Enum.GetValues(typeof(AchievementRangeUnit)).Cast<AchievementRangeUnit>().ToList();
        //difficultyLevelPicker.ItemsSource = Enum.GetValues(typeof(AchievementDifficultyLevels)).Cast<AchievementDifficultyLevels>().ToList();

        stepPicker.ItemsSource = _stepNames;
        reportPicker.ItemsSource = _reportNames;

        tagEntry.Focused += async (_, __) =>
        {
            tagEntry.Unfocus();
            await PickTagsAsync();
        };
    }

    private async void OnEditActiveTimeTargetClicked(object sender, EventArgs e)
    {
        if (BindingContext is not AchievementDetailsViewModel typedVm)
            return;

        if (!typedVm.CanEdit)
            return;

        var page = new DurationPickerPage(
            typedVm.ActiveTimeTarget
        );

        await Shell.Current.Navigation.PushAsync(page);

        var result = await page.Result;
        if (result is null) return;

        var totalHours = (int)result.Value.TotalHours;
        var formatted = $"{(totalHours < 10 ? "0" : "")}{totalHours}:{result.Value.Minutes:D2}:{result.Value.Seconds:D2}";

        typedVm.ActiveTimeTarget = result.Value;
        typedVm.ActiveTimeTargetText = formatted;
    }

    private async void OnEditAchievementsClicked(object sender, EventArgs e)
    {
        if (BindingContext is not AchievementDetailsViewModel vm)
            return;

        if (!vm.CanEdit)
            return;

        var initial = (vm.AchievementTitle ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var page = new MultiSelectPickerPage(
            "Select Achievements",
            _achievementTitles,
            initial,
            true
        );

        await Shell.Current.Navigation.PushAsync(page);

        var result = await page.Result;
        if (result == null)
            return;

        vm.AchievementTitle = string.Join(", ", result);
    }


    private void OnClearAchievementsClicked(object sender, EventArgs e)
    {
        if (BindingContext is AchievementDetailsViewModel vm && vm.CanEdit)
            vm.AchievementTitle = "";
    }

    private async void OnEditTagsClicked(object sender, EventArgs e)
    {
        if (BindingContext is not AchievementDetailsViewModel vm)
            return;

        if (!vm.CanEdit)
            return;

        var initial = (vm.Tags ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var page = new MultiSelectPickerPage(
            "Select Tags",
            _allTags,
            initial,
            true
        );

        await Shell.Current.Navigation.PushAsync(page);

        var result = await page.Result;
        if (result == null)
            return;

        vm.Tags = string.Join(", ", result);
    }

    private void OnClearTagsClicked(object sender, EventArgs e)
    {
        if (BindingContext is AchievementDetailsViewModel vm && vm.CanEdit)
            vm.Tags = "";
    }

    private async void OnAddTrophyPhotoClicked(object sender, EventArgs e)
    {
        if (BindingContext is not AchievementDetailsViewModel vm)
            return;

        if (!vm.CanEdit)
            return;

        var isRange = vm.CompletionType == AchievementCompletionType.Range;

        try
        {
            if (isRange)
            {
                var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
                {
                    PickerTitle = "Pick photos (trophies)",
                    FileTypes = FilePickerFileType.Images
                });

                foreach (var r in results ?? Enumerable.Empty<FileResult>())
                {
                    vm.TrophiesToAdd.Add(r.FullPath);
                }
            }
            else
            {
                var r = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Pick a photo (trophy)",
                    FileTypes = FilePickerFileType.Images
                });

                if (r == null) return;

                vm.TrophiesToAdd.Clear();
                vm.TrophiesToAdd.Add(r.FullPath);
            }
        }
        catch (TaskCanceledException)
        {
        }
    }

    private async void OnAddTrophyFileClicked(object sender, EventArgs e)
    {
        if (BindingContext is not AchievementDetailsViewModel vm)
            return;

        if (!vm.CanEdit)
            return;

        var isRange = vm.CompletionType == AchievementCompletionType.Range;

        try
        {
            if (isRange)
            {
                var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
                {
                    PickerTitle = "Pick files (trophies)"
                });

                foreach (var r in results ?? Enumerable.Empty<FileResult>())
                    vm.TrophiesToAdd.Add(r.FullPath);
            }
            else
            {
                var r = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Pick a file (trophy)"
                });

                if (r == null) return;

                vm.TrophiesToAdd.Clear();
                vm.TrophiesToAdd.Add(r.FullPath);
            }
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void OnClearTrophiesClicked(object sender, EventArgs e)
    {
        if (BindingContext is AchievementDetailsViewModel vm && vm.CanEdit)
        {
            vm.TrophiesToAdd.Clear();
            vm.Trophies.Clear();
        }
    }

    private async Task PickTagsAsync()
    {
        if (BindingContext is not AchievementDetailsViewModel vm)
            return;

        if (!vm.CanEdit)
            return;

        if (_allTags.Count == 0)
        {
            await DisplayAlert("Tags", "No tags available yet.", "OK");
            return;
        }

        var choice = await DisplayActionSheet("Pick a tag", "Done", null, _allTags.ToArray());
        if (string.IsNullOrWhiteSpace(choice) || choice == "Done")
            return;

        var current = (vm.Tags ?? "").Trim();
        var set = new HashSet<string>(
            current.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase
        );

        set.Add(choice);

        vm.Tags = string.Join(", ", set.OrderBy(x => x));
    }


    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is AchievementDetailsViewModel vm)
            vm.StopTimer();
    }
}