using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ApexHMI.Interfaces;
using ApexHMI.Models;
using ApexHMI.Views.Dialogs;
using CommunityToolkit.Mvvm.Input;

namespace ApexHMI.ViewModels.Modules;

public sealed class ParameterViewModel : ModuleViewModelBase
{
    private readonly IParameterService _parameterService;

    public ParameterViewModel(MainViewModel shell, IParameterService parameterService)
        : base(shell, "参数设定")
    {
        _parameterService = parameterService;
        LoadParametersCommand = new AsyncRelayCommand(LoadParametersAsync);
        SaveParametersCommand = new AsyncRelayCommand(SaveParametersAsync);
        ConnectCommand = new AsyncRelayCommand(() => Shell.ConnectCommand.ExecuteAsync(null));
        DisconnectCommand = new AsyncRelayCommand(() => Shell.DisconnectCommand.ExecuteAsync(null));
        OpenCommunicationConfigCommand = new RelayCommand(OpenCommunicationConfig);
    }

    public IAsyncRelayCommand LoadParametersCommand { get; }
    public IAsyncRelayCommand SaveParametersCommand { get; }
    public IAsyncRelayCommand ConnectCommand { get; }
    public IAsyncRelayCommand DisconnectCommand { get; }
    public IRelayCommand OpenCommunicationConfigCommand { get; }

    private void OpenCommunicationConfig()
    {
        var owner = Application.Current?.MainWindow;
        var window = new CommunicationConfigWindow
        {
            Owner = owner,
            DataContext = Shell
        };
        window.ShowDialog();
    }
    public string CurrentSubSection => Shell.CurrentParameterSubSection;
    public string Title => Shell.CurrentParameterTitle;
    public ObservableCollection<ParameterItem> Parameters => Shell.Parameters;
    public ICollectionView ParametersView => Shell.ParametersView;
    public bool CanEditParameters => Shell.CanEditParameters;

    public async Task SaveParametersAsync()
    {
        if (!CanEditParameters)
        {
            Shell.SystemMessage = "当前权限不足，无法修改参数";
            return;
        }

        var illegal = Parameters.FirstOrDefault(parameter => !Shell.CanEditParameter(parameter));
        if (illegal is not null)
        {
            Shell.SystemMessage = $"存在超权限参数：{illegal.Name}";
            return;
        }

        var path = Path.Combine(Shell.GetProjectRoot(), "config", "parameters.json");
        await _parameterService.SaveAsync(path, Parameters);
        Shell.SystemMessage = $"参数已保存：{path}";
        Shell.AddLog("参数", Shell.SystemMessage, "Info");
    }

    public async Task LoadParametersAsync()
    {
        var path = Path.Combine(Shell.GetProjectRoot(), "config", "parameters.json");
        var items = await _parameterService.LoadAsync(path);
        if (items.Count == 0)
        {
            Shell.RefreshParameterPermissions();
            Shell.SystemMessage = "未找到参数文件，已保留当前示例参数";
            return;
        }

        Parameters.Clear();
        foreach (var item in items)
        {
            Parameters.Add(item);
        }

        Shell.RefreshParameterPermissions();
        Shell.SystemMessage = "参数加载完成";
        Shell.AddLog("参数", Shell.SystemMessage, "Info");
    }
}
