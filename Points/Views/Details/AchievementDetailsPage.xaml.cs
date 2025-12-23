using Points.Models;
using Points.ViewModels;
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
       var achievementPicker = this.FindByName<Picker>("AchievementPicker");


        _allTags = allTags?.Distinct().OrderBy(x => x).ToList() ?? new List<string>();
       _stepNames = stepNames?.Distinct().OrderBy(x => x).ToList() ?? new List<string>();
       _achievementTitles = achievementTitles?.Distinct().OrderBy(x => x).ToList() ?? new List<string>();

       BindingContext = new AchievementDetailsViewModel(model, onSaved);

       // Populate pickers
       goalTypePicker.ItemsSource = Enum.GetValues(typeof(AchievementGoalType)).Cast<AchievementGoalType>().ToList();
       completionTypePicker.ItemsSource = Enum.GetValues(typeof(AchievementCompletionType)).Cast<AchievementCompletionType>().ToList();
       rangeUnitPicker.ItemsSource = Enum.GetValues(typeof(AchievementRangeUnit)).Cast<AchievementRangeUnit>().ToList();

       stepPicker.ItemsSource = _stepNames;
       achievementPicker.ItemsSource = _achievementTitles;

       // Tap-to-pick tags
       tagEntry.Focused += async (_, __) =>
       {
           // immediately unfocus so keyboard doesn't show
           tagEntry.Unfocus();
           await PickTagsAsync();
       };
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
    
}