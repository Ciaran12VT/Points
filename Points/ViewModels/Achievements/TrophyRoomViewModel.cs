using Points.Global;
using Points.Models;
using Points.Services.Navigation;
using Points.Services.Persistence;
using Points.Services.Time;
using Points.Views.Achievements;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Points.ViewModels.Achievements
{
    public class TrophyRoomViewModel : INotifyPropertyChanged
    {
        private readonly IAchievementService _achievements;
        private readonly IAppNavigationService _navigation;
        private readonly IAppDialogService _dialogs;
        private readonly IClock _clock;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<TrophyModel> Trophies { get; } = new();

        public Task? Initialization { get; private set; }

        public Command<TrophyModel> OpenTrophyCommand { get; }

        public TrophyRoomViewModel(
            IAchievementService achievements,
            IAppNavigationService navigation,
            IAppDialogService dialogs,
            IClock clock)
        {
            _achievements = achievements ?? throw new ArgumentNullException(nameof(achievements));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));

            OpenTrophyCommand = new Command<TrophyModel>(async trophy => await OpenTrophyAsync(trophy));
            Initialization = LoadAsync();

        }

        private async Task OpenTrophyAsync(TrophyModel? trophy)
        {
            if (trophy == null)
                return;

            await _navigation.PushModalAsync(
                new NavigationPage(
                    new TrophyViewerPage(
                        trophy,
                        _achievements,
                        _navigation,
                        _dialogs,
                        _clock)));
        }

        private async Task LoadAsync()
        {
            var trophies = await _achievements.GetTrophyModelsDataAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                foreach (var t in trophies)
                {
                    t.ImageSource = Path.Combine(AppPaths.GetAchievementTrophiesPath(t.AchievementId), t.ImageSource);
                    Trophies.Add(t);
                }
                    
            });
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
