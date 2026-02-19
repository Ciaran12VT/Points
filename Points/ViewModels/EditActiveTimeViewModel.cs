using Points.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.ViewModels
{
    public sealed class EditActiveTimeRow : BindableObject
    {
        private DateTime _start;
        private DateTime? _end;
        private string _rateName;
        private double _valuePerMinute;

        public DateTime Start
        {
            get => _start;
            private set
            {
                if (_start == value) return;
                _start = value;
                OnPropertyChanged(nameof(Start));
                OnPropertyChanged(nameof(StartText));
            }
        }

        public DateTime? End
        {
            get => _end;
            private set
            {
                if (_end == value) return;
                _end = value;
                OnPropertyChanged(nameof(End));
                OnPropertyChanged(nameof(EndText));
            }
        }

        public string StartText => Start.ToString("yyyy-MM-dd HH:mm:ss");
        public string EndText => End.HasValue ? End.Value.ToString("yyyy-MM-dd HH:mm:ss") : "∞";

        public string RateName => _rateName;
        public double ValuePerMinute => _valuePerMinute;

        // NEW: duration in hours
        public double Hours => (End.Value - Start).TotalHours;

        // Optional: nice formatted text for binding
        public string HoursText => $"{Hours:F2} h";

        public EditActiveTimeRow(DateTime start, DateTime? end, string rateName, double valuePerMinute)
        {
            _start = start;
            _end = end;
            _rateName = rateName;
            _valuePerMinute = valuePerMinute;
        }

        public void SetStart(DateTime dt) => Start = dt;
        public void SetEnd(DateTime dt) => End = dt;
    }

    public sealed class EditActiveTimeViewModel : BindableObject
    {
        private readonly Func<DateTime, Task<DateTime?>> _pickDateTime;
        private readonly Action<List<ActivityModel>> _onSave;

        public ObservableCollection<EditActiveTimeRow> Rows { get; } = new();

        public Command<EditActiveTimeRow> EditStartCommand { get; }
        public Command<EditActiveTimeRow> EditEndCommand { get; }
        public Command SaveCommand { get; }

        public EditActiveTimeViewModel(List<ActivityModel> activity, Action<List<ActivityModel>> onSave, Func<DateTime, Task<DateTime?>> pickDateTime)
        {
            _onSave = onSave;
            _pickDateTime = pickDateTime;

            // Sort: most recent first, using Item1
            foreach (var t in activity.OrderByDescending(x => x.StartDate))
            {
                Rows.Add(new EditActiveTimeRow(t.StartDate, t.EndDate, t.RateName, t.ValuePerMinute));
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

                var chosen = await _pickDateTime(row.End.Value);
                if (chosen is null) return;

                row.SetEnd(chosen.Value);

                Resort();
            });

            SaveCommand = new Command(() =>
            {
                var edited = Rows
                    .OrderByDescending(r => r.Start)
                    .Select(r => new ActivityModel(r.Start, r.End, r.RateName, r.ValuePerMinute))
                    .ToList();

                _onSave(edited);
            });
        }

        private void Resort()
        {
            var sorted = Rows.OrderByDescending(r => r.Start).ToList();
            Rows.Clear();
            foreach (var r in sorted) Rows.Add(r);
        }

    }
}
