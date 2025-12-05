using System.Collections.Generic;
using System.Numerics;
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

        /// <summary>
        /// 测试 PanelSharp 矩形面片渲染
        /// </summary>
        private void TestPanelSharp_Rectangle_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🧪 PanelSharp 矩形面片测试");
            sb.AppendLine("");
            sb.AppendLine("✓ 创建了一个在 XY 平面上的蓝色半透明矩形");
            sb.AppendLine("  顶点: (-2, -1, 0), (2, -1, 0), (2, 1, 0), (-2, 1, 0)");
            sb.AppendLine("  颜色: 蓝色 RGB(0, 100, 255) Alpha=0.5");
            sb.AppendLine("");
            sb.AppendLine("操作：");
            sb.AppendLine("• 右键拖动可旋转视图");
            sb.AppendLine("• 滚轮可缩放");
            vm.StatusMessage = sb.ToString();

            // 创建一个矩形面片
            var rectangle = new PanelSharp(
                new List<Vector3>
                {
                    new Vector3(-2, -1, 0),
                    new Vector3(2, -1, 0),
                    new Vector3(2, 1, 0),
                    new Vector3(-2, 1, 0)
                },
                color: System.Windows.Media.Color.FromArgb(128, 0, 100, 255)
            )
            {
                Name = "Rectangle Panel"
            };

            PointCloudViewer.Shapes = new List<BaseSharp> { rectangle };
        }

        /// <summary>
        /// 测试 PanelSharp 三角形面片渲染
        /// </summary>
        private void TestPanelSharp_Triangle_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🧪 PanelSharp 三角形面片测试");
            sb.AppendLine("");
            sb.AppendLine("✓ 创建了三个彩色三角形面片");
            sb.AppendLine("  三角形1: 红色(Z=0)");
            sb.AppendLine("  三角形2: 绿色(Z=1)");
            sb.AppendLine("  三角形3: 黄色(Z=2)");
            sb.AppendLine("");
            sb.AppendLine("操作：");
            sb.AppendLine("• 右键拖动可旋转视图");
            sb.AppendLine("• 中键拖动可平移视图");
            vm.StatusMessage = sb.ToString();

            var triangles = new List<BaseSharp>
            {
                // 红色三角形 - Z=0 平面
                new PanelSharp(
                    new List<Vector3>
                    {
                        new Vector3(-1, -1, 0),
                        new Vector3(1, -1, 0),
                        new Vector3(0, 1, 0)
                    },
                    color: System.Windows.Media.Color.FromArgb(200, 255, 0, 0)
                ) { Name = "Red Triangle" },

                // 绿色三角形 - Z=1 平面
                new PanelSharp(
                    new List<Vector3>
                    {
                        new Vector3(-1, -1, 1),
                        new Vector3(1, -1, 1),
                        new Vector3(0, 1, 1)
                    },
                    color: System.Windows.Media.Color.FromArgb(200, 0, 255, 0)
                ) { Name = "Green Triangle" },

                // 黄色三角形 - Z=2 平面
                new PanelSharp(
                    new List<Vector3>
                    {
                        new Vector3(-1, -1, 2),
                        new Vector3(1, -1, 2),
                        new Vector3(0, 1, 2)
                    },
                    color: System.Windows.Media.Color.FromArgb(200, 255, 255, 0)
                ) { Name = "Yellow Triangle" }
            };

            PointCloudViewer.Shapes = triangles;
        }

        /// <summary>
        /// 测试混合渲染：点云 + 面片
        /// </summary>
        private void TestPanelSharp_Mixed_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🧪 混合渲染测试：点云 + 面片");
            sb.AppendLine("");
            sb.AppendLine("✓ 生成球形点云");
            sb.AppendLine("✓ 添加包围盒面片");
            sb.AppendLine("");
            sb.AppendLine("预期效果：");
            sb.AppendLine("• 白色点云在中心");
            sb.AppendLine("• 彩色半透明包围盒（边界）");
            vm.StatusMessage = sb.ToString();

            // 生成球形点云
            vm.GenerateSamplePointCloudCommand.Execute(null);

            // 创建包围盒
            float boxSize = 1.5f;
            var boundingBox = new List<BaseSharp>
            {
                // 前面（Z=boxSize）
                new PanelSharp(
                    new List<Vector3>
                    {
                        new Vector3(-boxSize, -boxSize, boxSize),
                        new Vector3(boxSize, -boxSize, boxSize),
                        new Vector3(boxSize, boxSize, boxSize),
                        new Vector3(-boxSize, boxSize, boxSize)
                    },
                    color: System.Windows.Media.Color.FromArgb(60, 0, 255, 255)
                ) { Name = "Front" },

                // 后面（Z=-boxSize）
                new PanelSharp(
                    new List<Vector3>
                    {
                        new Vector3(-boxSize, -boxSize, -boxSize),
                        new Vector3(-boxSize, boxSize, -boxSize),
                        new Vector3(boxSize, boxSize, -boxSize),
                        new Vector3(boxSize, -boxSize, -boxSize)
                    },
                    color: System.Windows.Media.Color.FromArgb(60, 255, 0, 255)
                ) { Name = "Back" },

                // 左面（X=-boxSize）
                new PanelSharp(
                    new List<Vector3>
                    {
                        new Vector3(-boxSize, -boxSize, -boxSize),
                        new Vector3(-boxSize, -boxSize, boxSize),
                        new Vector3(-boxSize, boxSize, boxSize),
                        new Vector3(-boxSize, boxSize, -boxSize)
                    },
                    color: System.Windows.Media.Color.FromArgb(60, 255, 255, 0)
                ) { Name = "Left" },

                // 右面（X=boxSize）
                new PanelSharp(
                    new List<Vector3>
                    {
                        new Vector3(boxSize, -boxSize, -boxSize),
                        new Vector3(boxSize, boxSize, -boxSize),
                        new Vector3(boxSize, boxSize, boxSize),
                        new Vector3(boxSize, -boxSize, boxSize)
                    },
                    color: System.Windows.Media.Color.FromArgb(60, 0, 255, 0)
                ) { Name = "Right" },

                // 顶面（Y=boxSize）
                new PanelSharp(
                    new List<Vector3>
                    {
                        new Vector3(-boxSize, boxSize, -boxSize),
                        new Vector3(-boxSize, boxSize, boxSize),
                        new Vector3(boxSize, boxSize, boxSize),
                        new Vector3(boxSize, boxSize, -boxSize)
                    },
                    color: System.Windows.Media.Color.FromArgb(60, 0, 0, 255)
                ) { Name = "Top" },

                // 底面（Y=-boxSize）
                new PanelSharp(
                    new List<Vector3>
                    {
                        new Vector3(-boxSize, -boxSize, -boxSize),
                        new Vector3(boxSize, -boxSize, -boxSize),
                        new Vector3(boxSize, -boxSize, boxSize),
                        new Vector3(-boxSize, -boxSize, boxSize)
                    },
                    color: System.Windows.Media.Color.FromArgb(60, 255, 0, 0)
                ) { Name = "Bottom" }
            };

            PointCloudViewer.Shapes = boundingBox;
        }

        /// <summary>
        /// 测试 LineSharp 坐标轴线条
        /// </summary>
        private void TestLineSharp_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🧪 LineSharp 坐标轴线条测试");
            sb.AppendLine("");
            sb.AppendLine("✓ 创建了三条彩色坐标轴线");
            sb.AppendLine("  X轴：红色 (长度 2.0)");
            sb.AppendLine("  Y轴：绿色 (长度 2.0)");
            sb.AppendLine("  Z轴：蓝色 (长度 2.0)");
            sb.AppendLine("");
            sb.AppendLine("线条宽度: 3.0 像素");
            vm.StatusMessage = sb.ToString();

            var axes = new List<BaseSharp>
            {
                // X轴（红色）
                new LineSharp(
                    new List<Vector3>
                    {
                        Vector3.Zero,
                        new Vector3(2, 0, 0)
                    },
                    color: System.Windows.Media.Color.FromArgb(255, 255, 0, 0),
                    lineWidth: 3.0f
                ) { Name = "X Axis" },

                // Y轴（绿色）
                new LineSharp(
                    new List<Vector3>
                    {
                        Vector3.Zero,
                        new Vector3(0, 2, 0)
                    },
                    color: System.Windows.Media.Color.FromArgb(255, 0, 255, 0),
                    lineWidth: 3.0f
                ) { Name = "Y Axis" },

                // Z轴（蓝色）
                new LineSharp(
                    new List<Vector3>
                    {
                        Vector3.Zero,
                        new Vector3(0, 0, 2)
                    },
                    color: System.Windows.Media.Color.FromArgb(255, 0, 0, 255),
                    lineWidth: 3.0f
                ) { Name = "Z Axis" }
            };

            PointCloudViewer.Shapes = axes;
        }

        /// <summary>
        /// 测试 LineSharp 网格线条
        /// </summary>
        private void TestLineSharp_Wireframe_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🧪 LineSharp 网格线条测试");
            sb.AppendLine("");
            sb.AppendLine("✓ 创建了一个立方体网格（闭合线条）");
            sb.AppendLine("  4条竖线 + 2个矩形边界");
            sb.AppendLine("  颜色：黄色 / 紫色");
            sb.AppendLine("");
            sb.AppendLine("线条宽度: 2.0 像素");
            vm.StatusMessage = sb.ToString();

            float size = 1.0f;
            var wireframe = new List<BaseSharp>
            {
                // 底面矩形
                new LineSharp(
                    new List<Vector3>
                    {
                        new Vector3(-size, -size, -size),
                        new Vector3(size, -size, -size),
                        new Vector3(size, size, -size),
                        new Vector3(-size, size, -size)
                    },
                    color: System.Windows.Media.Color.FromArgb(255, 255, 255, 0),
                    lineWidth: 2.0f,
                    isClosed: true
                ) { Name = "Bottom" },

                // 顶面矩形
                new LineSharp(
                    new List<Vector3>
                    {
                        new Vector3(-size, -size, size),
                        new Vector3(size, -size, size),
                        new Vector3(size, size, size),
                        new Vector3(-size, size, size)
                    },
                    color: System.Windows.Media.Color.FromArgb(255, 255, 0, 255),
                    lineWidth: 2.0f,
                    isClosed: true
                ) { Name = "Top" },

                // 竖线连接
                new LineSharp(
                    new List<Vector3>
                    {
                        new Vector3(-size, -size, -size),
                        new Vector3(-size, -size, size)
                    },
                    color: System.Windows.Media.Color.FromArgb(255, 255, 255, 0),
                    lineWidth: 2.0f
                ),
                new LineSharp(
                    new List<Vector3>
                    {
                        new Vector3(size, -size, -size),
                        new Vector3(size, -size, size)
                    },
                    color: System.Windows.Media.Color.FromArgb(255, 255, 255, 0),
                    lineWidth: 2.0f
                ),
                new LineSharp(
                    new List<Vector3>
                    {
                        new Vector3(size, size, -size),
                        new Vector3(size, size, size)
                    },
                    color: System.Windows.Media.Color.FromArgb(255, 255, 255, 0),
                    lineWidth: 2.0f
                ),
                new LineSharp(
                    new List<Vector3>
                    {
                        new Vector3(-size, size, -size),
                        new Vector3(-size, size, size)
                    },
                    color: System.Windows.Media.Color.FromArgb(255, 255, 255, 0),
                    lineWidth: 2.0f
                )
            };

            PointCloudViewer.Shapes = wireframe;
        }

        /// <summary>
        /// 测试 VolumeSharp 多面体
        /// </summary>
        private void TestVolumeSharp_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🧪 VolumeSharp 多面体测试");
            sb.AppendLine("");
            sb.AppendLine("✓ 创建了一个四棱锥体积");
            sb.AppendLine("  顶点：5个（1个顶点 + 4个底面）");
            sb.AppendLine("  面：4个三角形侧面 + 1个四边形底面");
            sb.AppendLine("");
            sb.AppendLine("颜色：半透明青色");
            sb.AppendLine("边框：启用（紫色）");
            vm.StatusMessage = sb.ToString();

            // 四棱锥体
            var vertices = new List<Vector3>
            {
                // 顶点
                new Vector3(0, 1.5f, 0),
                // 底面四个顶点
                new Vector3(-1, 0, -1),
                new Vector3(1, 0, -1),
                new Vector3(1, 0, 1),
                new Vector3(-1, 0, 1)
            };

            var indices = new List<uint>
            {
                // 四个侧面（三角形）
                0, 1, 2,  // 前面
                0, 2, 3,  // 右面
                0, 3, 4,  // 后面
                0, 4, 1   // 左面
            };

            var pyramid = new VolumeSharp(
                vertices,
                indices,
                color: System.Windows.Media.Color.FromArgb(120, 0, 255, 255),
                drawFill: true,
                drawOutline: true,
                lineWidth: 2.0f
            ) { Name = "Pyramid" };

            PointCloudViewer.Shapes = new List<BaseSharp> { pyramid };
        }

        private void TestSphereSharp_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🧪 SphereSharp 球体测试");
            sb.AppendLine("");
            sb.AppendLine("✓ 创建了一个半径为 1.0 的球体");
            sb.AppendLine("  位置：原点");
            sb.AppendLine("  分段：20x20（Stacks x Slices）");
            sb.AppendLine("");
            sb.AppendLine("颜色：紫色半透明");
            sb.AppendLine("填充：启用");
            vm.StatusMessage = sb.ToString();

            var sphere = new SphereSharp(
                center: Vector3.Zero,
                radius: 1.0f,
                color: System.Windows.Media.Color.FromArgb(150, 150, 100, 200),
                stacks: 20,
                slices: 20,
                drawFill: true,
                drawOutline: false
            ) { Name = "Sphere" };

            PointCloudViewer.Shapes = new List<BaseSharp> { sphere };
        }

        private void TestCylinderSharp_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("🧪 CylinderSharp 圆柱测试");
            sb.AppendLine("");
            sb.AppendLine("✓ 创建了一个圆柱体");
            sb.AppendLine("  位置：原点（底面中心）");
            sb.AppendLine("  半径：0.8, 高度：2.0");
            sb.AppendLine("  分段：24 个");
            sb.AppendLine("");
            sb.AppendLine("颜色：棕色半透明");
            sb.AppendLine("包含盖子：是");
            vm.StatusMessage = sb.ToString();

            var cylinder = new CylinderSharp(
                center: Vector3.Zero,
                radius: 0.8f,
                height: 2.0f,
                color: System.Windows.Media.Color.FromArgb(150, 200, 100, 100),
                slices: 24,
                drawFill: true,
                drawOutline: false,
                includeCaps: true
            ) { Name = "Cylinder" };

            PointCloudViewer.Shapes = new List<BaseSharp> { cylinder };
        }
    }
}
