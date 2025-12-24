using Points.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Points.ViewModels
{
    public class TrophyRoomViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<TrophyModel> Trophies { get; } = new();

        public TrophyRoomViewModel()
        {
            // Dummy data
            Trophies.Add(new TrophyModel
            {
                Title = "Super Nerd Trophy",
                EarnedOn = DateTime.Today.AddDays(-2),
                ImageSource = "trophy.png"
            });

            Trophies.Add(new TrophyModel
            {
                Title = "Gym Rat Trophy",
                EarnedOn = DateTime.Today.AddDays(-7),
                ImageSource = "trophy.png"
            });

            Trophies.Add(new TrophyModel
            {
                Title = "Consistency Trophy",
                EarnedOn = DateTime.Today.AddDays(-14),
                ImageSource = "trophy.png"
            });

            Trophies.Add(new TrophyModel
            {
                Title = "Early Bird Trophy",
                EarnedOn = DateTime.Today.AddDays(-30),
                ImageSource = "trophy.png"
            });
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
