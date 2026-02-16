using Points.Global;
using Points.Models;
using Points.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Points.ViewModels
{
    public class TrophyRoomViewModel : INotifyPropertyChanged
    {
        private IDbService _db;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<TrophyModel> Trophies { get; } = new();

        public Task? Initialization { get; private set; }

        public Command OpenTrophyCommand { get; }

        public TrophyRoomViewModel(Services.IDbService db)
        {
            _db = db;

            Initialization = LoadAsync();

        }

        private async Task LoadAsync()
        {
            var trophies = await _db.GetTrophyModelsDataAsync();

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
