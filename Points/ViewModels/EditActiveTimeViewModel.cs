using Points.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Points.Views.Details.EditActiveTimePage;

namespace Points.ViewModels
{
    public sealed class EditActiveTimeRow : BindableObject
    {
        public int Id { get; }
        public long CardID { get; }

        private DateTime _start;
        private DateTime? _end;
        private readonly string _rateName;
        private readonly double _valuePerMinute;

        public DateTime Start
        {
            get => _start;
            private set
            {
                if (_start == value) return;
                _start = value;
                OnPropertyChanged(nameof(Start));
                OnPropertyChanged(nameof(StartText));
                OnPropertyChanged(nameof(HoursText));
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
                OnPropertyChanged(nameof(HoursText));
            }
        }

        // Display formatting per spec
        public string StartText => Start.ToString("MMM-dd HH:mm");

        public string EndText =>
            End.HasValue
                ? End.Value.ToString("MMM-dd HH:mm")
                : "∞";

        public string HoursText
        {
            get
            {
                if (!End.HasValue) return "";

                var hours = (End.Value - Start).TotalHours;
                return $"{hours:F1}h";
            }
        }

        public string RateName => _rateName;
        public double ValuePerMinute => _valuePerMinute;

        public EditActiveTimeRow(ActivityModel model)
        {
            Id = model.Id;
            CardID = model.CardID;
            _start = model.StartDate;
            _end = model.EndDate;
            _rateName = model.RateName;
            _valuePerMinute = model.ValuePerMinute;
        }

        public void SetStart(DateTime dt)
        {
            Start = dt;
        }

        public void SetEnd(DateTime? dt)
        {
            End = dt;
        }

        public ActivityModel ToModel()
        {
            return new ActivityModel
            {
                Id = Id,
                CardID = CardID,
                StartDate = Start,
                EndDate = End,
                RateName = RateName,
                ValuePerMinute = ValuePerMinute
            };
        }
    }

    public sealed class EditActiveTimeViewModel : BindableObject
    {
        private readonly Func<EditActiveTimeRow, ActiveBoundary, Task<DateTime?>> _pickDateTime;
        private readonly Func<string, string, Task<bool>> _confirmDelete;
        private readonly Action<List<ActivityModel>> _onSave;

        public ObservableCollection<EditActiveTimeRow> Rows { get; } = new();

        public Command<EditActiveTimeRow> EditStartCommand { get; }
        public Command<EditActiveTimeRow> EditEndCommand { get; }
        public Command<EditActiveTimeRow> DeleteRowCommand { get; }
        public Command SaveCommand { get; }

        public EditActiveTimeViewModel(List<ActivityModel> activity, Action<List<ActivityModel>> onSave, Func<EditActiveTimeRow, ActiveBoundary, Task<DateTime?>> pickDateTime, Func<string, string, Task<bool>> confirmDelete)
        {
            _onSave = onSave ?? throw new ArgumentNullException(nameof(onSave));
            _pickDateTime = pickDateTime ?? throw new ArgumentNullException(nameof(pickDateTime));
            _confirmDelete = confirmDelete ?? throw new ArgumentNullException(nameof(confirmDelete));

            if (activity is null) throw new ArgumentNullException(nameof(activity));

            foreach (var model in activity.OrderByDescending(x => x.StartDate))
                Rows.Add(new EditActiveTimeRow(model));

            EditStartCommand = new Command<EditActiveTimeRow>(async row =>
            {
                if (row is null) return;

                var chosen = await _pickDateTime(row, ActiveBoundary.Start);
                if (chosen is null) return;

                row.SetStart(chosen.Value);
                Resort();
            });

            EditEndCommand = new Command<EditActiveTimeRow>(async row =>
            {
                if (row is null) return;

                // If you don't want to allow editing End when it's open-ended, keep this guard.
                // Otherwise remove it and let the picker + validator handle it.
                if (!row.End.HasValue) return;

                var chosen = await _pickDateTime(row, ActiveBoundary.End);
                if (chosen is null) return;

                row.SetEnd(chosen.Value);
                Resort();
            });

            DeleteRowCommand = new Command<EditActiveTimeRow>(async row =>
            {
                if (row is null) return;

                var confirm = await _confirmDelete(
                    "Delete time block?",
                    $"Delete {row.StartText} → {row.EndText} ?");

                if (!confirm) return;

                Rows.Remove(row);
            });

            SaveCommand = new Command(() =>
            {
                var edited = Rows
                    .OrderByDescending(r => r.Start)
                    .Select(r => r.ToModel())
                    .ToList();

                _onSave(edited);
            });
        }

        private void Resort()
        {
            var sorted = Rows
                .OrderByDescending(r => r.Start)
                .ToList();

            Rows.Clear();
            foreach (var r in sorted)
                Rows.Add(r);
        }
    }
}
