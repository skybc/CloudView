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

        private void TestRangeFilter_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm != null)
            {
                // 执行命令生成点云
                vm.TestRangeFilterCommand.Execute(null);
                
                // 设置坐标范围限制：仅显示上半部分
                // 点云范围: X[10,20], Y[5,15], Z[-5,5]
                // 限制范围: X[10,20], Y[10,15], Z[-5,5]
                PointCloudViewer.MinX = 10;
                PointCloudViewer.MaxX = 20;
                PointCloudViewer.MinY = 10;
                PointCloudViewer.MaxY = 15;
                PointCloudViewer.MinZ = -5;
                PointCloudViewer.MaxZ = 5;
            }
        }

        private void TestRangeFilter2_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm != null)
            {
                // 执行命令生成点云
                vm.TestRangeFilter2Command.Execute(null);
                
                // 设置坐标范围限制：仅显示中心立方体部分
                // 点云范围: X[10,20], Y[5,15], Z[-5,5]
                // 限制范围: X[12,18], Y[7,13], Z[-3,3]
                PointCloudViewer.MinX = 12;
                PointCloudViewer.MaxX = 18;
                PointCloudViewer.MinY = 7;
                PointCloudViewer.MaxY = 13;
                PointCloudViewer.MinZ = -3;
                PointCloudViewer.MaxZ = 3;
            }
        }

        private void ResetRangeFilter_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm != null)
            {
                // 执行命令显示状态
                vm.ResetRangeFilterCommand.Execute(null);
                
                // 重置坐标范围限制到无限制
                PointCloudViewer.MinX = float.MinValue;
                PointCloudViewer.MaxX = float.MaxValue;
                PointCloudViewer.MinY = float.MinValue;
                PointCloudViewer.MaxY = float.MaxValue;
                PointCloudViewer.MinZ = float.MinValue;
                PointCloudViewer.MaxZ = float.MaxValue;
            }
        }

        /// <summary>
        /// 测试 Visibility 从 Collapsed 切换到 Visible 的场景
        /// 验证修复是否有效：首次显示时是否能正确显示点云
        /// </summary>
        private void TestVisibilityToggle_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🧪 Visibility 切换测试启动");
            sb.AppendLine("");
            sb.AppendLine("步骤 1: 隐藏 PointCloudViewer (Visibility.Collapsed)");
            
            vm.StatusMessage = sb.ToString();

            // 隐藏点云查看器
            PointCloudViewer.Visibility = Visibility.Collapsed;

            // 延迟 1 秒后执行后续操作
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            int step = 1;
            
            timer.Tick += (s, args) =>
            {
                step++;
                if (step == 2)
                {
                    sb.AppendLine("✓ 步骤 1 完成：PointCloudViewer 已隐藏");
                    sb.AppendLine("");
                    sb.AppendLine("步骤 2: 生成点云数据（PointCloudViewer 仍隐藏）");
                    vm.StatusMessage = sb.ToString();

                    // 生成点云数据
                    vm.GenerateSamplePointCloudCommand.Execute(null);
                }
                else if (step == 3)
                {
                    sb.AppendLine("✓ 步骤 2 完成：点云数据已生成");
                    sb.AppendLine("");
                    sb.AppendLine("步骤 3: 显示 PointCloudViewer (Visibility.Visible)");
                    vm.StatusMessage = sb.ToString();

                    // 显示点云查看器
                    PointCloudViewer.Visibility = Visibility.Visible;
                }
                else if (step == 4)
                {
                    sb.AppendLine("✓ 步骤 3 完成：PointCloudViewer 已显示");
                    sb.AppendLine("");
                    sb.AppendLine("🎯 测试结果:");
                    sb.AppendLine("✅ 点云应该直接显示（无需第二次赋值）");
                    sb.AppendLine("✅ 坐标轴应该可见");
                    sb.AppendLine("✅ XY 平面网格应该可见");
                    vm.StatusMessage = sb.ToString();

                    timer.Stop();
                }
            };

            timer.Start();
        }
    }
}
