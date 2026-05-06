using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    // -- 工具箱拖拽到画布 --

    private void ToolboxItem_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is FrameworkElement element && element.DataContext is string tool)
            DragDrop.DoDragDrop(element, tool, DragDropEffects.Copy);
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
}
