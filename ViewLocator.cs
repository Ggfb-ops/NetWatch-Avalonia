using Avalonia.Controls;
using Avalonia.Controls.Templates;
using NetWatch.ViewModels;
using NetWatch.Views;

namespace NetWatch;

public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is MainWindowViewModel)
            return new MainWindow();
        return new TextBlock { Text = "Not Found" };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
