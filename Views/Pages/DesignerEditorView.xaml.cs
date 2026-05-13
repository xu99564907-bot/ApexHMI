using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ApexHMI.Models;
using ApexHMI.Models.RuntimeUi;
using ApexHMI.ViewModels.Modules;

namespace ApexHMI.Views.Pages;

public partial class DesignerEditorView : UserControl
{
    private bool _isDragging;
    private Point _dragStartPoint;
    private double _widgetStartX, _widgetStartY;

    public DesignerEditorView()
    {
        InitializeComponent();
        ToolboxItemsControl.ItemsSource = DesignerEditorViewModel.ToolboxGroups;
        Focusable = true;
        Loaded += (_, _) => Focus();
        PreviewKeyDown += DesignerEditorView_PreviewKeyDown;
    }

    private void DesignerEditorView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var vm = GetViewModel();
        if (vm is null) return;

        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        if (ctrl && e.Key == Key.C) { vm.CopySelectedCommand.Execute(null); e.Handled = true; }
        else if (ctrl && e.Key == Key.V) { vm.PasteClipboardCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Delete) { vm.DeleteSelectedWidgetsCommand.Execute(null); e.Handled = true; }
        else if (ctrl && e.Key == Key.Z) { vm.UndoCommand.Execute(null); e.Handled = true; }
        else if (ctrl && e.Key == Key.Y) { vm.RedoCommand.Execute(null); e.Handled = true; }
    }

    private DesignerEditorViewModel? GetViewModel()
        => DataContext as DesignerEditorViewModel;

    private void FitToWindow_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetViewModel();
        if (vm?.SelectedPage is null) return;
        if (vm.IsFitToWindow) { vm.RestoreFromFit(); return; }
        var sv = DesignerCanvasScrollViewer;
        var cw = vm.SelectedPage.CanvasWidth;
        var ch = vm.SelectedPage.CanvasHeight;
        if (cw <= 0 || ch <= 0 || sv.ViewportWidth <= 0 || sv.ViewportHeight <= 0) return;
        const double pad = 16.0;
        var z = Math.Min((sv.ViewportWidth - pad) / cw, (sv.ViewportHeight - pad) / ch);
        if (z <= 0) return;
        z = Math.Max(0.1, Math.Min(4.0, z));
        vm.ApplyFitToWindow(z);
    }

    // ===== 工具栏悬浮 / 固定 =====
    private bool _isToolboxFloating;
    private Panel? _toolboxOriginalParent;
    private int _toolboxOriginalIndex;

    private void ToolboxFloatToggle_Click(object sender, RoutedEventArgs e)
    {
        if (!_isToolboxFloating)
        {
            if (ToolboxPanel.Parent is not Panel parent) return;
            _toolboxOriginalParent = parent;
            _toolboxOriginalIndex = parent.Children.IndexOf(ToolboxPanel);
            parent.Children.Remove(ToolboxPanel);

            ToolboxPanel.BorderThickness = new Thickness(1);
            ToolboxPanel.Effect = new System.Windows.Media.Effects.DropShadowEffect
            { ShadowDepth = 2, BlurRadius = 8, Opacity = 0.35 };
            ToolboxDragHandle.Visibility = Visibility.Visible;
            ToolboxFloatIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Pin;

            Canvas.SetLeft(ToolboxPanel, 20);
            Canvas.SetTop(ToolboxPanel, 12);
            OverlayLayer.Children.Add(ToolboxPanel);
            _isToolboxFloating = true;
        }
        else
        {
            OverlayLayer.Children.Remove(ToolboxPanel);
            ToolboxPanel.ClearValue(Canvas.LeftProperty);
            ToolboxPanel.ClearValue(Canvas.TopProperty);
            ToolboxPanel.BorderThickness = new Thickness(0);
            ToolboxPanel.Effect = null;
            ToolboxDragHandle.Visibility = Visibility.Collapsed;
            ToolboxFloatIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.PinOutline;

            _toolboxOriginalParent?.Children.Insert(
                Math.Min(_toolboxOriginalIndex, _toolboxOriginalParent.Children.Count),
                ToolboxPanel);
            _isToolboxFloating = false;
        }
    }

    private void ToolboxDragHandle_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (!_isToolboxFloating) return;
        var left = Canvas.GetLeft(ToolboxPanel);
        var top = Canvas.GetTop(ToolboxPanel);
        if (double.IsNaN(left)) left = 0;
        if (double.IsNaN(top)) top = 0;
        Canvas.SetLeft(ToolboxPanel, Math.Max(0, left + e.HorizontalChange));
        Canvas.SetTop(ToolboxPanel, Math.Max(0, top + e.VerticalChange));
    }

    // ===== 设计器弹出到独立窗口（包含工具栏 + 页面树 + 画布 + 属性编辑器）=====
    private Window? _canvasPopupWindow;
    private UIElement? _designerOriginalRoot;

    private void PopOutCanvas()
    {
        if (_canvasPopupWindow != null) { _canvasPopupWindow.Activate(); return; }
        if (this.Content is not UIElement root) return;

        _designerOriginalRoot = root;
        this.Content = null;

        var placeholder = new Border
        {
            Background = System.Windows.Media.Brushes.WhiteSmoke,
            BorderBrush = System.Windows.Media.Brushes.LightGray,
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = "设计器已弹出到独立窗口\n关闭窗口以恢复",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.Gray,
                TextAlignment = TextAlignment.Center,
                FontSize = 14
            }
        };
        this.Content = placeholder;

        _canvasPopupWindow = new Window
        {
            Title = "设计器 - 独立窗口（关闭以恢复）",
            Width = 1400,
            Height = 880,
            ShowInTaskbar = true,
            DataContext = this.DataContext,
            Content = root
        };
        // 主窗口关闭时同步关闭弹窗，避免遗留进程
        var mainWindow = Window.GetWindow(this);
        if (mainWindow != null)
        {
            EventHandler? closeHandler = null;
            closeHandler = (_, _) =>
            {
                mainWindow.Closed -= closeHandler;
                _canvasPopupWindow?.Close();
            };
            mainWindow.Closed += closeHandler;
        }
        // 把 Delete / Ctrl+C/V/Z/Y 等快捷键挂到弹出窗口
        _canvasPopupWindow.PreviewKeyDown += DesignerEditorView_PreviewKeyDown;
        _canvasPopupWindow.Closed += (_, _) => RestoreCanvas();
        _canvasPopupWindow.Show();
    }

    private void RestoreCanvas()
    {
        if (_canvasPopupWindow is null) return;
        var content = _canvasPopupWindow.Content as UIElement;
        _canvasPopupWindow.PreviewKeyDown -= DesignerEditorView_PreviewKeyDown;
        _canvasPopupWindow.Content = null;
        _canvasPopupWindow = null;
        this.Content = null;
        if (content is not null) this.Content = content;
        else if (_designerOriginalRoot is not null) this.Content = _designerOriginalRoot;
        _designerOriginalRoot = null;
    }

    // -- 工具箱拖拽到画布 --
    // 修复：单次按下既触发 Click(AddWidgetCommand) 又触发 DoDragDrop 会重复添加。
    // 通过记录按下点 + 阈值后再触发 DoDragDrop，并在 PreviewMouseLeftButtonUp 中
    // 当已发生拖拽时把事件 Handled=true，从而抑制 Button.Click 触发 Command。
    private bool _toolboxDragging;
    private Point _toolboxPressPoint;

    private void ToolboxItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _toolboxDragging = false;
        _toolboxPressPoint = e.GetPosition(null);
    }

    private void ToolboxItem_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _toolboxDragging) return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _toolboxPressPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _toolboxPressPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;
        if (sender is FrameworkElement element)
        {
            string? typeId = element.DataContext switch
            {
                DesignerEditorViewModel.ToolboxItem ti => ti.TypeId,
                string s => s, // 兼容旧绑定
                _ => null
            };
            if (string.IsNullOrEmpty(typeId)) return;
            _toolboxDragging = true;
            DragDrop.DoDragDrop(element, typeId, DragDropEffects.Copy);
        }
    }

    private void ToolboxItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_toolboxDragging)
        {
            e.Handled = true;
            _toolboxDragging = false;
        }
    }

    // ===== 框选状态 =====
    private bool _isMarqueeing;
    private Point _marqueeStart;

    private void DesignerCanvas_MouseMoveCoord(object sender, MouseEventArgs e)
    {
        if (sender is not Canvas canvas) return;
        var pt = e.GetPosition(canvas);
        var vm = GetViewModel();
        if (vm is null) return;

        vm.UpdateMouseCoord(pt.X, pt.Y);

        // 进行中的框选 → 更新矩形可视化
        if (_isMarqueeing && e.LeftButton == MouseButtonState.Pressed)
        {
            var x = System.Math.Min(_marqueeStart.X, pt.X);
            var y = System.Math.Min(_marqueeStart.Y, pt.Y);
            vm.MarqueeX = x;
            vm.MarqueeY = y;
            vm.MarqueeWidth = System.Math.Abs(pt.X - _marqueeStart.X);
            vm.MarqueeHeight = System.Math.Abs(pt.Y - _marqueeStart.Y);
            vm.IsMarqueeActive = vm.MarqueeWidth > 2 && vm.MarqueeHeight > 2;
        }
    }

    private void DesignerCanvas_BackgroundClick(object sender, MouseButtonEventArgs e)
    {
        // 在画布空白处按下：开始框选 + 清空当前选中
        if (sender is not Canvas canvas) return;

        // 仅当原始源是 Canvas 自身或其网格背景 Rectangle 时才视作"空白处"
        var orig = e.OriginalSource;
        if (orig is not Canvas && orig is not System.Windows.Shapes.Rectangle) return;

        // 双击空白处：弹出画布到独立窗口
        if (e.ClickCount >= 2)
        {
            if (_isMarqueeing) { canvas.ReleaseMouseCapture(); _isMarqueeing = false; }
            var vm0 = GetViewModel();
            if (vm0 is not null) { vm0.IsMarqueeActive = false; vm0.MarqueeWidth = 0; vm0.MarqueeHeight = 0; }
            PopOutCanvas();
            e.Handled = true;
            return;
        }

        var vm = GetViewModel();
        if (vm is null) return;

        vm.SelectSingleWidget(null);

        _isMarqueeing = true;
        _marqueeStart = e.GetPosition(canvas);
        vm.MarqueeX = _marqueeStart.X;
        vm.MarqueeY = _marqueeStart.Y;
        vm.MarqueeWidth = 0;
        vm.MarqueeHeight = 0;
        canvas.CaptureMouse();
        e.Handled = true;
    }

    private void DesignerCanvas_BackgroundMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isMarqueeing) return;
        if (sender is not Canvas canvas) return;

        var vm = GetViewModel();
        canvas.ReleaseMouseCapture();
        _isMarqueeing = false;

        if (vm is not null && vm.IsMarqueeActive)
        {
            var endX = vm.MarqueeX + vm.MarqueeWidth;
            var endY = vm.MarqueeY + vm.MarqueeHeight;
            vm.SelectInRectangle(vm.MarqueeX, vm.MarqueeY, endX, endY);
        }
        if (vm is not null)
        {
            vm.IsMarqueeActive = false;
            vm.MarqueeWidth = 0;
            vm.MarqueeHeight = 0;
        }
        e.Handled = true;
    }

    private void DesignerCanvas_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.StringFormat)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void DesignerCanvas_Drop(object sender, DragEventArgs e)
    {
        var vm = GetViewModel();
        if (vm is null || sender is not Canvas canvas || !e.Data.GetDataPresent(DataFormats.StringFormat))
            return;

        var typeId = e.Data.GetData(DataFormats.StringFormat)?.ToString();
        if (string.IsNullOrWhiteSpace(typeId)) return;

        var position = e.GetPosition(canvas);
        vm.AddWidgetAtDropCommand.Execute($"{typeId}|{position.X}|{position.Y}");
    }

    // -- 画布控件：选中 + 拖拽移位 --

    private void Widget_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element) return;

        // DataContext 可能是 DesignerWidgetItem (新 WYSIWYG 模式) 或直接 WidgetInstance (兼容旧)
        var widget = element.DataContext switch
        {
            DesignerWidgetItem item => item.Model,
            WidgetInstance w => w,
            _ => null
        };
        if (widget is null) return;

        var vm = GetViewModel();
        if (vm is null) return;

        // Ctrl+点击 → 多选切换；其它情况 → 单选
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            vm.ToggleWidgetSelection(widget);
        }
        else
        {
            vm.SelectSingleWidget(widget);
        }

        _isDragging = true;
        _widgetStartX = widget.X;
        _widgetStartY = widget.Y;
        _dragStartPoint = e.GetPosition(DesignerCanvas);
        element.CaptureMouse();
        e.Handled = true;
    }

    private void Widget_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(DesignerCanvas);
        GetViewModel()?.MoveSelectedWidget(
            Math.Max(0, _widgetStartX + pos.X - _dragStartPoint.X),
            Math.Max(0, _widgetStartY + pos.Y - _dragStartPoint.Y));
    }

    private void Widget_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;

        if (e.MouseDevice.Captured is FrameworkElement captured)
            captured.ReleaseMouseCapture();

        var vm = GetViewModel();
        if (vm?.SelectedWidget is WidgetInstance w &&
            (Math.Abs(w.X - _widgetStartX) > 0.5 || Math.Abs(w.Y - _widgetStartY) > 0.5))
        {
            vm.CommitMove(_widgetStartX, _widgetStartY, w.X, w.Y);
        }

        e.Handled = true;
    }

    // ===== 控件尺寸调整手柄（8 个，按 Tag 标识方向） =====
    private bool _isResizing;
    private string? _resizeHandle;
    private Point _resizeStartPoint;
    private double _resizeStartX, _resizeStartY, _resizeStartW, _resizeStartH;

    private static WidgetInstance? ResolveWidget(object? dataContext) => dataContext switch
    {
        DesignerWidgetItem item => item.Model,
        WidgetInstance w => w,
        _ => null
    };

    private void ResizeHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        var widget = ResolveWidget(fe.DataContext);
        if (widget is null) return;

        var vm = GetViewModel();
        if (vm is null) return;

        // 选中此控件，确保 SelectedWidget 是手柄所属的那个
        vm.SelectSingleWidget(widget);

        _isResizing = true;
        _resizeHandle = fe.Tag as string;
        _resizeStartPoint = e.GetPosition(DesignerCanvas);
        _resizeStartX = widget.X;
        _resizeStartY = widget.Y;
        _resizeStartW = widget.Width;
        _resizeStartH = widget.Height;
        fe.CaptureMouse();
        e.Handled = true;
    }

    private void ResizeHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isResizing || e.LeftButton != MouseButtonState.Pressed || _resizeHandle is null) return;

        var vm = GetViewModel();
        if (vm?.SelectedWidget is null) return;

        var pos = e.GetPosition(DesignerCanvas);
        var dx = pos.X - _resizeStartPoint.X;
        var dy = pos.Y - _resizeStartPoint.Y;

        double newX = _resizeStartX, newY = _resizeStartY;
        double newW = _resizeStartW, newH = _resizeStartH;

        if (_resizeHandle.Contains("W")) { newX = _resizeStartX + dx; newW = _resizeStartW - dx; }
        if (_resizeHandle.Contains("E")) { newW = _resizeStartW + dx; }
        if (_resizeHandle.Contains("N")) { newY = _resizeStartY + dy; newH = _resizeStartH - dy; }
        if (_resizeHandle.Contains("S")) { newH = _resizeStartH + dy; }

        // 强制最小尺寸（与 WidgetEditorService 内部 10 一致，但放宽到 20 更易拖）
        const double minSize = 20;
        if (newW < minSize)
        {
            newW = minSize;
            if (_resizeHandle.Contains("W")) newX = _resizeStartX + _resizeStartW - minSize;
        }
        if (newH < minSize)
        {
            newH = minSize;
            if (_resizeHandle.Contains("N")) newY = _resizeStartY + _resizeStartH - minSize;
        }
        if (newX < 0) { newW += newX; newX = 0; }
        if (newY < 0) { newH += newY; newY = 0; }

        vm.MoveAndResizeSelectedWidget(newX, newY, newW, newH);
    }

    private void ResizeHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isResizing) return;
        _isResizing = false;
        _resizeHandle = null;

        if (e.MouseDevice.Captured is FrameworkElement captured)
            captured.ReleaseMouseCapture();

        var vm = GetViewModel();
        if (vm?.SelectedWidget is WidgetInstance w &&
            (Math.Abs(w.X - _resizeStartX) > 0.5 || Math.Abs(w.Y - _resizeStartY) > 0.5
             || Math.Abs(w.Width - _resizeStartW) > 0.5 || Math.Abs(w.Height - _resizeStartH) > 0.5))
        {
            vm.CommitResize(_resizeStartX, _resizeStartY, _resizeStartW, _resizeStartH,
                            w.X, w.Y, w.Width, w.Height);
        }

        e.Handled = true;
    }

}
