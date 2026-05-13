#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ApexHMI.Models.RuntimeUi;

/// <summary>P7: Faceplate 接口属性类型。</summary>
public enum FaceplatePropertyType
{
    String,     // 文本
    Number,     // 数字
    Boolean,    // 布尔
    TagAddress, // OPC UA Tag 地址
    Color,      // 颜色
    PageRoute,  // 页面 RouteKey
}

/// <summary>P7: Faceplate 接口属性定义（暴露给实例化时配置的参数）。</summary>
public partial class FaceplateProperty : ObservableObject
{
    [ObservableProperty] private string _key = "";
    [ObservableProperty] private string _displayName = "";
    [ObservableProperty] private FaceplatePropertyType _type = FaceplatePropertyType.String;
    [ObservableProperty] private string _defaultValue = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private bool _required;
}

/// <summary>P7: Faceplate 定义（可被多次实例化的复合控件模板）。</summary>
public partial class Faceplate : ObservableObject
{
    [ObservableProperty] private string _id = Guid.NewGuid().ToString("N");
    [ObservableProperty] private string _name = "未命名 Faceplate";
    [ObservableProperty] private string _version = "1.0.0";
    [ObservableProperty] private string _category = "通用";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private double _defaultWidth = 200;
    [ObservableProperty] private double _defaultHeight = 120;
    [ObservableProperty] private string _iconKind = "ViewModule";
    [ObservableProperty] private bool _isBuiltIn;

    /// <summary>接口属性（实例化时配置的参数）。</summary>
    public ObservableCollection<FaceplateProperty> InterfaceProperties { get; set; } = new();

    /// <summary>内部画面：复合控件的内部 widget 树。</summary>
    public PageDefinition InnerScreen { get; set; } = new() { Title = "内部画面", CanvasWidth = 200, CanvasHeight = 120 };
}

/// <summary>P7: 项目级 Faceplate 库。</summary>
public partial class FaceplateLibrary : ObservableObject
{
    public ObservableCollection<Faceplate> Faceplates { get; set; } = new();
}
