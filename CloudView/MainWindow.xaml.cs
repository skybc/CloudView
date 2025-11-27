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

        private void TestRotationCenter_Click(object sender, RoutedEventArgs e)
        {
            TestRotationCenter();
        }

        /// <summary>
        /// 测试旋转中心
        /// 当加载点云后，右键拖动旋转，摄像机应该围绕点云中心旋转而不是原点
        /// 可以通过观察旋转后的视图是否保持点云在中心来验证
        /// </summary>
        private void TestRotationCenter()
        {
            var vm = DataContext as MainViewModel;
            if (vm?.Points != null && vm.Points.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("✅ 旋转中心测试方法：");
                sb.AppendLine("1. 已加载点云");
                sb.AppendLine("2. 使用右键拖动旋转视图");
                sb.AppendLine("3. 观察点云是否始终在视野中心旋转");
                sb.AppendLine("4. 如果是，表示旋转中心正确");
                sb.AppendLine("");
                sb.AppendLine("理论：");
                sb.AppendLine("- 摄像机目标点(_cameraTarget)应设置为点云中心");
                sb.AppendLine("- 摄像机位置应围绕目标点旋转");
                sb.AppendLine("- 旋转角度存储在 _rotationX 和 _rotationY 中");
                
                vm.StatusMessage = sb.ToString();
            }
            else
            {
                var vm2 = DataContext as MainViewModel;
                if (vm2 != null)
                {
                    vm2.StatusMessage = "❌ 请先生成点云数据";
                }
            }
        }
    }
}