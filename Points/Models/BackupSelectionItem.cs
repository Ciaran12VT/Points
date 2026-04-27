using Points.Services.Backup;

namespace Points.Models
{
    public sealed class BackupSelectionItem : ObservableObject
    {
        private bool _isSelected;

        public BackupSelectionItem(BackupResourceOption option, bool isSelected = true)
        {
            Key = option.Key;
            Title = option.Title;
            Description = option.Description;
            IsSelected = isSelected;
        }

        public string Key { get; }
        public string Title { get; }
        public string Description { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
