using Points.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.ViewModels
{
    public class MultiSelectPickerViewModel : ObservableObject
    {
        private readonly TaskCompletionSource<IReadOnlyList<string>> _tcs;
        private readonly HashSet<string> _selected = new();

        public string TitleText { get; }
        public List<string> Items { get; }

        private string _selectedText = "";
        public string SelectedText
        {
            get => _selectedText;
            private set => SetProperty(ref _selectedText, value);
        }

        public IReadOnlyList<string> GetSelected() => _selected.OrderBy(x => x).ToList();


        public Command<string> AddCommand { get; }
        public Command ClearCommand { get; }

        public MultiSelectPickerViewModel(
            string title,
            IEnumerable<string> items,
            IEnumerable<string>? initial,
            TaskCompletionSource<IReadOnlyList<string>> tcs)
        {
            TitleText = title;
            Items = items.OrderBy(x => x).ToList();
            _tcs = tcs;

            if (initial != null)
                foreach (var i in initial) _selected.Add(i);

            AddCommand = new Command<string>(Add);
            ClearCommand = new Command(Clear);

            RefreshText();
        }

        private void Add(string item)
        {
            _selected.Add(item);
            RefreshText();
        }

        private void Clear()
        {
            _selected.Clear();
            RefreshText();
        }

        private void RefreshText()
            => SelectedText = string.Join(", ", _selected);

        public void Commit()
            => _tcs.TrySetResult(_selected.ToList());
    }
}
