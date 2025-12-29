using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.ViewModels
{
    public sealed class EditActiveTimeRow
    {
        public DateTime Start { get; private set; }
        public DateTime End { get; private set; }

        public string StartText => Start.ToString("yyyy-MM-dd HH:mm");
        public string EndText => End.ToString("yyyy-MM-dd HH:mm");

        public EditActiveTimeRow(DateTime start, DateTime end)
        {
            Start = start;
            End = end;
        }

        public void SetStart(DateTime dt) => Start = dt;
        public void SetEnd(DateTime dt) => End = dt;
    }

    public sealed class EditActiveTimeViewModel : BindableObject
    {
        private readonly Func<DateTime, Task<DateTime?>> _pickDateTime;
        private readonly Action<List<Tuple<DateTime, DateTime>>> _onSave;

        public ObservableCollection<EditActiveTimeRow> Rows { get; } = new();

        public Command<EditActiveTimeRow> EditStartCommand { get; }
        public Command<EditActiveTimeRow> EditEndCommand { get; }
        public Command SaveCommand { get; }

        public EditActiveTimeViewModel(
            List<Tuple<DateTime, DateTime>> activity,
            Action<List<Tuple<DateTime, DateTime>>> onSave,
            Func<DateTime, Task<DateTime?>> pickDateTime)
        {
            _onSave = onSave;
            _pickDateTime = pickDateTime;

            // Sort: most recent first, using Item1
            foreach (var t in activity
                .OrderByDescending(x => x.Item1))
            {
                Rows.Add(new EditActiveTimeRow(t.Item1, t.Item2));
            }

            EditStartCommand = new Command<EditActiveTimeRow>(async row =>
            {
                if (row is null) return;

                var chosen = await _pickDateTime(row.Start);
                if (chosen is null) return;

                row.SetStart(chosen.Value);

                // Keep ordering correct after edits
                Resort();
            });

            EditEndCommand = new Command<EditActiveTimeRow>(async row =>
            {
                if (row is null) return;

                var chosen = await _pickDateTime(row.End);
                if (chosen is null) return;

                row.SetEnd(chosen.Value);

                Resort();
            });

            SaveCommand = new Command(() =>
            {
                var edited = Rows
                    .OrderByDescending(r => r.Start)
                    .Select(r => Tuple.Create(r.Start, r.End))
                    .ToList();

                _onSave(edited);
            });
        }

        private void Resort()
        {
            var sorted = Rows.OrderByDescending(r => r.Start).ToList();
            Rows.Clear();
            foreach (var r in sorted) Rows.Add(r);

            // Force UI refresh of computed text properties
            OnPropertyChanged(nameof(Rows));
        }
    }
}
