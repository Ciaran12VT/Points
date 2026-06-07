using Points.Models;
using System.Collections.ObjectModel;

namespace Points.ViewModels.Shared
{
    public class MultiSelectPickerViewModel : ObservableObject
    {
        private readonly TaskCompletionSource<IReadOnlyList<string>?> _tcs;
        private readonly HashSet<string> _selected = new();
        private readonly List<string> _allItems;

        public string TitleText { get; }

        public bool IsReadOnly { get; }

        public bool IsSingleTag { get; }

        public ObservableCollection<string> FilteredItems { get; } = new();

        public string EmptyStateText => string.IsNullOrWhiteSpace(SearchText)
            ? IsReadOnly
                ? "No options available yet."
                : "No saved options yet. Type one below."
            : "No matching options.";

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyFilter();
                    RaisePropertyChanged(nameof(EmptyStateText));
                }
            }
        }

        private string _selectedText = "";
        public string SelectedText
        {
            get => _selectedText;
            set => SetProperty(ref _selectedText, value);
        }

        public IReadOnlyList<string> GetSelected() => _selected.OrderBy(x => x).ToList();

        public Command<string> AddCommand { get; }
        public Command ClearCommand { get; }

        public MultiSelectPickerViewModel(string title, IEnumerable<string> items, IEnumerable<string>? initial, TaskCompletionSource<IReadOnlyList<string>?> tcs, bool isReadOnly, bool isSingleTag)
        {
            TitleText = title;
            _allItems = items.OrderBy(x => x).ToList();
            _tcs = tcs;
            IsReadOnly = isReadOnly;
            IsSingleTag = isSingleTag;

            if (initial != null)
                foreach (var i in initial) _selected.Add(i);

            AddCommand = new Command<string>(Add);
            ClearCommand = new Command(Clear);

            ApplyFilter();
            RefreshText();
        }

        private void ApplyFilter()
        {
            var q = (SearchText ?? "").Trim();

            var results = string.IsNullOrWhiteSpace(q)
                ? _allItems
                : _allItems.Where(x => x?.Contains(q, StringComparison.OrdinalIgnoreCase) == true).ToList();

            FilteredItems.Clear();
            foreach (var item in results)
                FilteredItems.Add(item);
        }

        private void Add(string item)
        {
            if (string.IsNullOrWhiteSpace(item))
                return;

            _selected.Add(item);
            RefreshText();
        }

        public void Clear()
        {
            _selected.Clear();
            RefreshText();
        }

        private void RefreshText() => SelectedText = string.Join(", ", _selected.OrderBy(x => x));

        // Called by the page when editable and the user types in the SelectedText box.
        public void SetSelectedFromText(string? text)
        {
            _selected.Clear();

            var parts = (text ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var p in parts)
            {
                if (!string.IsNullOrWhiteSpace(p))
                    _selected.Add(p);
            }

            // Keep SelectedText normalized (trimmed + consistent separators)
            RefreshText();
        }

        public void Commit() => _tcs.TrySetResult(_selected.ToList());

        internal bool HasMultipleValues()
        {
            return SelectedText.Contains(' ');
        }
    }
}
