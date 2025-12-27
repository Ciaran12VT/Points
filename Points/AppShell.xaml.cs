using Points.Views;

namespace Points
{
    public partial class AppShell : Shell
    {
        public AppShell(IServiceProvider services)
        {
            InitializeComponent();

            Items.Add(new ShellContent
            {
                Route = "HomePage",
                ContentTemplate = new DataTemplate(() => services.GetRequiredService<HomePage>())
            });
        }
    }
}
