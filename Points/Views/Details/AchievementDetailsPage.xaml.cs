using Points.Models;
using Points.ViewModels;
using System;
using System.Formats.Tar;

namespace Points.Views.Details;

public partial class AchievementDetailsPage : ContentPage
{
   private readonly List<string> _allTags;
   private readonly List<string> _stepNames;
   private readonly List<string> _achievementTitles;

   public AchievementDetailsPage(AchievementCardModel model, IEnumerable<string> allTags, IEnumerable<string> stepNames, IEnumerable<string> achievementTitles, Action<AchievementCardModel> onSaved)
   {
       InitializeComponent();

       var tagEntry = this.FindByName<Entry>("TagsEntry");
       var goalTypePicker = this.FindByName<Picker>("GoalTypePicker");
       var completionTypePicker = this.FindByName<Picker>("CompletionTypePicker");
       var rangeUnitPicker = this.FindByName<Picker>("RangeUnitPicker");
       var stepPicker = this.FindByName<Picker>("StepPicker");
       var activeTimeEntry = this.FindByName<Entry>("ActiveTimeEntry");


        _allTags = allTags?.Distinct().OrderBy(x => x).ToList() ?? new List<string>();
       _stepNames = stepNames?.Distinct().OrderBy(x => x).ToList() ?? new List<string>();
       _achievementTitles = achievementTitles?.Distinct().OrderBy(x => x).ToList() ?? new List<string>();

       BindingContext = new AchievementDetailsViewModel(model, onSaved);

       // Populate pickers
       goalTypePicker.ItemsSource = Enum.GetValues(typeof(AchievementGoalType)).Cast<AchievementGoalType>().ToList();
       completionTypePicker.ItemsSource = Enum.GetValues(typeof(AchievementCompletionType)).Cast<AchievementCompletionType>().ToList();
       rangeUnitPicker.ItemsSource = Enum.GetValues(typeof(AchievementRangeUnit)).Cast<AchievementRangeUnit>().ToList();

       stepPicker.ItemsSource = _stepNames;

       // Tap-to-pick tags
       tagEntry.Focused += async (_, __) =>
       {
           // immediately unfocus so keyboard doesn't show
           tagEntry.Unfocus();
           await PickTagsAsync();
       };

    }

    private async void OnEditActiveTimeTargetClicked(object sender, EventArgs e)
    {
        // 1) Start values (parse from the current display if you want)
        // If you already store a TimeSpan on the VM, use that instead.
        var vm = BindingContext; // cast to your AchievementDetailsViewModel if you want

        // 2) Push your picker page
        // This assumes your DurationPickerPage returns a TimeSpan (or null if cancelled).
        var page = new DurationPickerPage(
        /* pass current duration here if your ctor needs it */
        );

        // OPTION A: if DurationPickerPage exposes a TaskCompletionSource result
        await Shell.Current.Navigation.PushAsync(page);

        var result = await page.Result; // e.g. Task<TimeSpan?>
        if (result is null) return;

        // 3) Write back to VM
        // Replace with your real VM property
        if (BindingContext is AchievementDetailsViewModel typedVm)
        {
            var totalHours = (int)result.Value.TotalHours;
            var formatted = $"{totalHours}:{result.Value.Minutes:D2}:{result.Value.Seconds:D2}";

            typedVm.ActiveTimeTarget = result.Value;          // if you store TimeSpan
            typedVm.ActiveTimeTargetText = formatted; // if you store string
        }
    }

    private async void OnEditAchievementsClicked(object sender, EventArgs e)
    {
        if (BindingContext is not AchievementDetailsViewModel vm)
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
            return; // Cancelled

        vm.AchievementTitle = string.Join(", ", result);
    }


    private void OnClearAchievementsClicked(object sender, EventArgs e)
    {
        if (BindingContext is AchievementDetailsViewModel vm)
            vm.AchievementTitle = "";
    }

    private async void OnEditTagsClicked(object sender, EventArgs e)
    {
        if (BindingContext is not AchievementDetailsViewModel vm)
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
            return; // cancelled

        vm.Tags = string.Join(", ", result);
    }

    private void OnClearTagsClicked(object sender, EventArgs e)
    {
        if (BindingContext is AchievementDetailsViewModel vm)
            vm.Tags = "";
    }

    private async void OnAddTrophyPhotoClicked(object sender, EventArgs e)
    {
        if (BindingContext is not AchievementDetailsViewModel vm)
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
                    vm.Model.Trophies.Add(r.FileName);
            }
            else
            {
                var r = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Pick a photo (trophy)",
                    FileTypes = FilePickerFileType.Images
                });

                if (r == null) return;

                vm.Model.Trophies.Clear(); // deadline => single trophy
                vm.Model.Trophies.Add(r.FileName);
            }
        }
        catch (TaskCanceledException)
        {
            // user cancelled
        }
    }

    private async void OnAddTrophyFileClicked(object sender, EventArgs e)
    {
        if (BindingContext is not AchievementDetailsViewModel vm)
            return;

        var isRange = vm.CompletionType == AchievementCompletionType.Range;

        try
        {
            if (isRange)
            {
                var results = await FilePicker.Default.PickMultipleAsync(new PickOptions
                {
                    PickerTitle = "Pick files (trophies)"
                    // no FileTypes => any
                });

                foreach (var r in results ?? Enumerable.Empty<FileResult>())
                    vm.Model.Trophies.Add(r.FileName);
            }
            else
            {
                var r = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Pick a file (trophy)"
                });

                if (r == null) return;

                vm.Model.Trophies.Clear(); // deadline => single trophy
                vm.Model.Trophies.Add(r.FileName);
            }
        }
        catch (TaskCanceledException)
        {
            // user cancelled
        }
    }

    private void OnClearTrophiesClicked(object sender, EventArgs e)
    {
        if (BindingContext is AchievementDetailsViewModel vm)
            vm.Model.Trophies.Clear();
    }


   private async Task PickTagsAsync()
   {
       if (BindingContext is not AchievementDetailsViewModel vm)
           return;

       if (_allTags.Count == 0)
       {
           await DisplayAlert("Tags", "No tags available yet.", "OK");
           return;
       }

       // Minimal approach: pick one tag per tap, with actions.
       // (Later we can switch to multi-select UI.)
       var choice = await DisplayActionSheet("Pick a tag", "Done", null, _allTags.ToArray());
       if (string.IsNullOrWhiteSpace(choice) || choice == "Done")
           return;

       var current = (vm.Tags ?? "").Trim();
       var set = new HashSet<string>(
           current.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
           StringComparer.OrdinalIgnoreCase
       );

       // store tags like "#Test" etc
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