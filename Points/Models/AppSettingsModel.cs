namespace Points.Models
{
    public sealed class AppSettingsModel
    {
        public int SettingsID { get; set; } = 1;

        public bool HardModeEnabled { get; set; }
        public double HardModeDamagePerMinuteValue { get; set; } // negative

        public bool StatusConditionsEnabled { get; set; }
        public int? CurrentlyAppliedStatusConditionID { get; set; }

        public int? SelectedThemeID { get; set; }
    }
}
