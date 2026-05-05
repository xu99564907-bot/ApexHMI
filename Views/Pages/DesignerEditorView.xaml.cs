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
        ToolboxItemsControl.ItemsSource = DesignerEditorViewModel.ToolboxTypes;
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

    private void DesignerCanvas_MouseMoveCoord(object sender, MouseEventArgs e)
    {
        if (sender is Canvas canvas)
        {
            var pt = e.GetPosition(canvas);
            GetViewModel()?.UpdateMouseCoord(pt.X, pt.Y);
        }
    }

    private void DesignerCanvas_BackgroundClick(object sender, MouseButtonEventArgs e)
    {
        // 点击空白处清空选中
        if (e.OriginalSource is Canvas)
        {
            var vm = GetViewModel();
            if (vm is not null)
                vm.SelectedWidget = null;
        }
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
