using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Points.Models
{
    public class TatCardModel : ObservableObject, IActiveCardModel
    {
        public string Id { get; } = Guid.NewGuid().ToString();

        private string _title = "TAT Card";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        // Requested shape
        public List<ActivityModel> Activity { get; set; } = new();

        public ValueRateModel? SelectedValueRateModel { get; set; }

        public List<ValueRateModel> ValueRates { get; set; } = new();

        private double _valuePerMinute = 1.0;
        public double ValuePerMinute
        {
            get => _valuePerMinute;
            set => SetProperty(ref _valuePerMinute, value);
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            private set => SetProperty(ref _isActive, value);
        }

        private string _status = "In-Progress";
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        private string _tags = "#Test, #Other";
        public string Tags
        {
            get => _tags;
            set => SetProperty(ref _tags, value);
        }

        private string _description = "";
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public bool HasMultipleValueRates => ValueRates.Count > 0;

        public ICommand ToggleActivityCommand { get; }

        public TatCardModel()
        {
            ToggleActivityCommand = new Command(ToggleActivity);
        }

        private void ToggleActivity()
        {
            var now = DateTime.Now;

            var valueRate = SelectedValueRateModel ?? new ValueRateModel() { RateName = "Base Rate", ValuePerMinute = ValuePerMinute };

            if (!IsActive)
            {
                // Start: store (start, DateTime.MinValue) to mean "open interval"          
                Activity.Add(new ActivityModel(now, DateTime.MinValue, valueRate.RateName, valueRate.ValuePerMinute));          
                IsActive = true;
                RaisePropertyChanged(nameof(Activity));
                return;
            }

            // Stop: close the most recent open interval
            for (int i = Activity.Count - 1; i >= 0; i--)
            {
                if (Activity[i].EndDate == DateTime.MinValue)
                {
                    Activity[i].EndDate = now;
                    IsActive = false;
                    RaisePropertyChanged(nameof(Activity));
                    return;
                }
            }

            // If we got here, state was inconsistent; recover by starting a new interval
            Activity.Add(new ActivityModel(now, DateTime.MinValue, valueRate.RateName, valueRate.ValuePerMinute));
            IsActive = true;
            RaisePropertyChanged(nameof(Activity));
        }

        public void StopActivity()
        {
            if (!IsActive) return;

            var now = DateTime.Now;

            for (int i = Activity.Count - 1; i >= 0; i--)
            {
                if (Activity[i].EndDate == DateTime.MinValue)
                {
                    Activity[i].EndDate = now;
                    IsActive = false;
                    RaisePropertyChanged(nameof(Activity));
                    return;
                }
            }

            // If somehow there was no open interval, just mark inactive.
            IsActive = false;
        }


        public TimeSpan GetActiveTime(DateTime start, DateTime end)
        {
            if (end <= start) return TimeSpan.Zero;

            double totalMinutes = 0;

            foreach (var period in Activity)
            {
                var aStart = period.StartDate;
                var aEnd = period.EndDate == DateTime.MinValue ? Min(end,DateTime.Now) : period.EndDate;

                var overlapStart = aStart > start ? aStart : start;
                var overlapEnd = aEnd < end ? aEnd : end;

                if (overlapEnd > overlapStart)
                    totalMinutes += (overlapEnd - overlapStart).TotalMinutes;
            }

            return TimeSpan.FromMinutes(totalMinutes);
        }

        public virtual double GetValue(DateTime start, DateTime end)
        {
            if (end <= start) return 0;

            double totalValue = 0;

            foreach (var period in Activity)
            {
                var aStart = period.StartDate;
                var aEnd = period.EndDate == DateTime.MinValue ? Min(end, DateTime.Now) : period.EndDate;

                var overlapStart = aStart > start ? aStart : start;
                var overlapEnd = aEnd < end ? aEnd : end;

                if (overlapEnd > overlapStart)
                {
                    var totalMinutes = (overlapEnd - overlapStart).TotalMinutes;

                    double currentRate = period.ValuePerMinute;

                    totalValue += currentRate * totalMinutes;
                }
                    
            }

            return totalValue;

        }

        DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

        public virtual DateTime GetLastActiveTime()
        {
            if (IsActive) return DateTime.Now;

            if (Activity.Count == 0) return DateTime.MinValue;

            return Activity.Select(x => x.EndDate).Max();
        }
    }
}
