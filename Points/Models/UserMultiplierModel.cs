using System.Globalization;

namespace Points.Models
{
    public sealed class UserMultiplierModel : ObservableObject
    {
        public int Id { get; set; }

        private string _name = "";
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value ?? "");
        }

        private string _code = "";
        public string Code
        {
            get => _code;
            set => SetProperty(ref _code, value ?? "");
        }

        private string _description = "";
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value ?? "");
        }

        private double _multiplyBy = 1.0d;
        public double MultiplyBy
        {
            get => _multiplyBy;
            set => SetProperty(ref _multiplyBy, value);
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public string MultiplyByText => MultiplyBy.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
