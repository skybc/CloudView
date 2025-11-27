using System.Windows;
using CloudView.Controls;
using CloudView.ViewModels;

namespace CloudView
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // 订阅 ROI 选择事件
            PointCloudViewer.RoiSelected += OnRoiSelected;
        }

        private void OnRoiSelected(object? sender, RoiSelectionEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.OnRoiSelected(e);
            }
        }

        private void ResetView_Click(object sender, RoutedEventArgs e)
        {
            PointCloudViewer.ResetView();
        }

        private void FitToView_Click(object sender, RoutedEventArgs e)
        {
            PointCloudViewer.FitToView();
        }
    }
}