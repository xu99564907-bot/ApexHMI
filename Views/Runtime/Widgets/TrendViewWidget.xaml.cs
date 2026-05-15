#nullable enable
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ApexHMI.ViewModels.Runtime;
using OxyPlot;
using OxyPlot.Axes;

namespace ApexHMI.Views.Runtime.Widgets;

public partial class TrendViewWidget : UserControl
{
    public TrendViewWidget()
    {
        InitializeComponent();
    }

    /// <summary>M4.1: 把鼠标在 PlotView 上的位置传给 VM 计算 Ruler 数据。</summary>
    private void OnPlotMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not TrendViewWidgetViewModel vm) return;
        if (!vm.ShowRuler) return;

        var pos = e.GetPosition(Plot);
        // 命中测试 → 屏幕点转数据坐标
        try
        {
            var screenPoint = new ScreenPoint(pos.X, pos.Y);
            var xAxis = vm.PlotModel.DefaultXAxis;
            if (xAxis is null) return;
            // 用 X 轴 InverseTransform 拿到鼠标时间戳（DateTime as double）
            var xData = xAxis.InverseTransform(pos.X);
            vm.UpdateRuler(xData, pos.X, pos.Y);

            // popup 跟随鼠标右下
            RulerPopup.Margin = new Thickness(Math.Min(pos.X + 14, ActualWidth - 200),
                                              Math.Max(pos.Y - 10, 0), 0, 0);
        }
        catch
        {
            // ignore — Plot 尚未初始化等
        }
    }

    private void OnPlotMouseLeave(object sender, MouseEventArgs e)
    {
        if (DataContext is TrendViewWidgetViewModel vm) vm.HideRuler();
    }
}
