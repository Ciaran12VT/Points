using Points.ViewModels;

namespace Points.Views.Details
{
    public partial class ReportDetailsPage : ContentPage
    {
        public ReportDetailsPage(ReportDetailsViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;

            HookResults(vm);
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();

            if (BindingContext is ReportDetailsViewModel vm)
            {
                HookResults(vm);
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
                return;

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
    }
}
