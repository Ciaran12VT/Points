using Points.ViewModels.Reports;

namespace Points.Views.Reports
{
    public partial class ReportDetailsPage : ContentPage
    {
        public ReportDetailsPage(ReportDetailsViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;

            HookResults(vm);
            RebuildResultsGrid();
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();

            if (BindingContext is ReportDetailsViewModel vm)
            {
                HookResults(vm);
                RebuildResultsGrid();
            }
        }

        private void HookResults(ReportDetailsViewModel vm)
        {
            if (vm == null)
                return;

            // Detach first so we don't double-subscribe
            vm.ResultsUpdated -= Vm_ResultsUpdated;
            vm.ResultsUpdated += Vm_ResultsUpdated;
        }

        private void Vm_ResultsUpdated()
        {
            RebuildResultsGrid();
        }

        private void RebuildResultsGrid()
        {
            if (BindingContext is not ReportDetailsViewModel vm)
                return;

            var results = vm.Results;
            ResultsGrid.RowDefinitions.Clear();
            ResultsGrid.ColumnDefinitions.Clear();
            ResultsGrid.Children.Clear();

            if (results == null || results.Count == 0)
            {
                ResultsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                ResultsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                ResultsGrid.Add(new Label
                {
                    Text = GetEmptyResultsText(vm),
                    TextColor = Colors.Gray,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center,
                    Padding = new Thickness(12)
                }, 0, 0);
                return;
            }

            // Assume each row is "col1|col2|col3|..."
            var firstSplit = (results[0] ?? string.Empty).Split('|');
            int columnCount = firstSplit.Length;

            // Shared column definitions
            for (int c = 0; c < columnCount; c++)
            {
                ResultsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            }

            // Build rows
            for (int r = 0; r < results.Count; r++)
            {
                ResultsGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

                var cells = (results[r] ?? string.Empty).Split('|');

                for (int c = 0; c < columnCount; c++)
                {
                    string text = c < cells.Length ? cells[c] : string.Empty;

                    var label = new Label
                    {
                        Text = text,
                        FontFamily = "Courier New",
                        FontSize = 12,
                        LineBreakMode = LineBreakMode.NoWrap,
                        Padding = new Thickness(4, 2),
                        HorizontalOptions = LayoutOptions.Start,
                        VerticalOptions = LayoutOptions.Center
                    };

                    if (r == 0)
                    {
                        label.FontAttributes = FontAttributes.Bold; // header row
                    }

                    ResultsGrid.Add(label, c, r);
                }
            }
        }

        private static string GetEmptyResultsText(ReportDetailsViewModel vm)
        {
            var message = vm.ResultsMessage ?? string.Empty;

            if (message.StartsWith("0 rows returned", StringComparison.OrdinalIgnoreCase))
                return "No rows returned.";

            if (message.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase))
                return "Fix the query and execute again.";

            return "Execute a SELECT query to see results.";
        }
    }
}
