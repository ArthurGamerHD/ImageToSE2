using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Threading;

namespace Img2SE2.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        MessageBox.TextChanged += (_, _) => Dispatcher.UIThread.Post(() =>
            MessageBox.GetTemplateChildren().OfType<ScrollViewer>().FirstOrDefault()?.ScrollToEnd());
    }
}