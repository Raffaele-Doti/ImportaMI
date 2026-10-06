using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ImportaMI.App.Services;
using ImportaMI.App.ViewModels;
using ImportaMI.App.Views;

namespace ImportaMI.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            window.DataContext = new MainViewModel(new FolderPicker(() => window));
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}