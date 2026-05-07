using System.Windows;
using WpfFeatureFlagsDemo.ViewModels;

namespace WpfFeatureFlagsDemo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = MainViewModel.CreateDefault();
    }
}
