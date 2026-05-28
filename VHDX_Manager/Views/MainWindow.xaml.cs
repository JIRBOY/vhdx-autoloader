using System.Windows;
using VHDX_Manager.ViewModels;

namespace VHDX_Manager.Views
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var vm = new MainViewModel();
            DataContext = vm;
            Loaded += async (s, e) => await vm.InitializeOnLoadAsync();
        }
    }
}