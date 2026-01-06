namespace Points.Models
{
    public sealed class ThemeConfigurationModel
    {
        public int ThemesConfigID { get; set; }
        public int ThemeID { get; set; }

        // Main page
        public string MainPageBackColor { get; set; } = "";
        public string MainPageForeColor { get; set; } = "";
        public string GlobalFontFamily { get; set; } = "";

        // Cards
        public string ScCardBackColor { get; set; } = "";
        public string ScCardForeColor { get; set; } = "";
        public string TatCardBackColor { get; set; } = "";
        public string TatCardForeColor { get; set; } = "";
        public string MissionCardBackColor { get; set; } = "";
        public string MissionCardForeColor { get; set; } = "";
        public string BudgetCardBackColor { get; set; } = "";
        public string BudgetCardForeColor { get; set; } = "";

        // Labels
        public string DueInLabelForeColor { get; set; } = "";

        // Negative button
        public string NegativeButtonBackColor { get; set; } = "";
        public string NegativeButtonForeColor { get; set; } = "";
        public string NegativeButtonStyle { get; set; } = "RoundedEdges";
        public double NegativeButtonBorderThickness { get; set; }
        public string NegativeButtonBorderColor { get; set; } = "";

        // Positive button
        public string PositiveButtonBackColor { get; set; } = "";
        public string PositiveButtonForeColor { get; set; } = "";
        public string PositiveButtonStyle { get; set; } = "RoundedEdges";
        public double PositiveButtonBorderThickness { get; set; }
        public string PositiveButtonBorderColor { get; set; } = "";

        // Active toggle ON
        public string ActiveToggleOnBackColor { get; set; } = "";
        public string ActiveToggleOnForeColor { get; set; } = "";
        public string ActiveToggleOnButtonStyle { get; set; } = "RoundedEdges";
        public double ActiveToggleOnButtonBorderThickness { get; set; }
        public string ActiveToggleOnButtonBorderColor { get; set; } = "";

        // Active toggle OFF
        public string ActiveToggleOffBackColor { get; set; } = "";
        public string ActiveToggleOffForeColor { get; set; } = "";
        public string ActiveToggleOffButtonStyle { get; set; } = "RoundedEdges";
        public double ActiveToggleOffButtonBorderThickness { get; set; }
        public string ActiveToggleOffButtonBorderColor { get; set; } = "";

        // Global value thresholds
        public string GlobalValueBelowZeroThresholdForeColor { get; set; } = "";
        public string GlobalValueNonZeroBelowThresholdForeColor { get; set; } = "";
        public string GlobalValueAboveThresholdForeColor { get; set; } = "";
        public string GlobalValueAboveSecondaryThresholdForeColor { get; set; } = "";

        // Card border
        public string CardBorderStyle { get; set; } = "Rounded";
        public double CardBorderThickness { get; set; }
        public string CardBorderColor { get; set; } = "";

        // Section display names
        public string MainQuestSectionDisplayName { get; set; } = "";
        public string MissionSectionDisplayName { get; set; } = "";
        public string BudgetSectionDisplayName { get; set; } = "";
        public string ArcsSectionDisplayName { get; set; } = "";
        public string PinnedAchievementsSectionDisplayName { get; set; } = "";
    }
}
