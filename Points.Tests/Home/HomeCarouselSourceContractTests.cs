namespace Points.Tests.Home;

using Xunit;

public sealed class HomeCarouselSourceContractTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void HomeCarousel_UsesOneObjectSelectionBindingAndDoesNotLoop()
    {
        var xaml = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Points",
            "Views",
            "Home",
            "HomePage.xaml"));

        Assert.Contains("CurrentItem=\"{Binding SelectedPage, Mode=TwoWay}\"", xaml);
        Assert.Contains("Loop=\"False\"", xaml);
        Assert.Contains("ItemsUpdatingScrollMode=\"KeepScrollOffset\"", xaml);
        Assert.DoesNotContain("Position=\"{Binding Position", xaml);
    }

    [Fact]
    public void HomePageCodeBehind_DoesNotCompeteWithBoundCarouselSelection()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Points",
            "Views",
            "Home",
            "HomePage.xaml.cs"));

        Assert.DoesNotContain("MainCarousel.Position =", source);
        Assert.DoesNotContain("MainCarousel.CurrentItem =", source);
        Assert.DoesNotContain("MaterializeCarouselPosition", source);
        Assert.DoesNotContain("vm.Position =", source);
        Assert.Contains("vm.SelectedPage = targetPage", source);
        Assert.DoesNotContain("JumpToPageRequested", source);
    }

    [Fact]
    public void HomePageReconciliation_DoesNotResetOuterPagesCollection()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "Points",
            "ViewModels",
            "Home",
            "HomePageStateCoordinator.cs"));

        Assert.DoesNotContain("_pages.Clear()", source);
        Assert.Contains("_pages.Move(", source);
        Assert.Contains("_pages.Insert(", source);
        Assert.Contains("_pages.RemoveAt(", source);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Points.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
