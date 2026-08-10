using AppBridge.Launcher.ViewModels;
using Microsoft.UI.Xaml;

namespace AppBridge.Launcher;

public sealed partial class MainWindow : Window
{
    public ApplicationListViewModel ViewModel { get; }

    public MainWindow(ApplicationListViewModel viewModel)
    {
        ViewModel = viewModel;
        this.InitializeComponent();
        this.ExtendsContentIntoTitleBar = true;
        this.Title = "AppBridge Launcher";

        Loaded += (s, e) =>
        {
            ViewModel.LoadApplicationsCommand.Execute(null);
        };
    }
}
