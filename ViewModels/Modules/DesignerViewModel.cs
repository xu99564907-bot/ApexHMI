using System.Collections.Generic;
using System.Collections.ObjectModel;
using ApexHMI.Models;
using ApexHMI.Models.Sfc;
using CommunityToolkit.Mvvm.Input;

namespace ApexHMI.ViewModels.Modules;

/// <summary>
/// Tab 8 "程序生成" 视图模型：透传 Shell 的 IO 程序生成 / SFC 自动 / SFC 初始化 / GitPull 命令与状态。
///
/// 已移除 V1 画布相关命令与属性（DesignerElements/DesignerPages/...）；
/// 画布设计已由 V2 的 <see cref="DesignerEditorViewModel"/> 接管（Tab 9）。
/// V1 模型 (DesignerElement/DesignerPage/DesignerProject) 仍保留供 V1ProjectMigrator 使用。
/// </summary>
public sealed class DesignerViewModel : ModuleViewModelBase
{
    public DesignerViewModel(MainViewModel shell)
        : base(shell, "设计器")
    {
        // IO 程序生成命令
        SaveConfigCommand = new AsyncRelayCommand(() => Shell.SaveConfigCommand.ExecuteAsync(null));
        LoadConfigCommand = new AsyncRelayCommand(() => Shell.LoadConfigCommand.ExecuteAsync(null));
        ImportIoTableCommand = new AsyncRelayCommand(() => Shell.ImportIoTableCommand.ExecuteAsync(null));
        ClearIoTableCommand = new RelayCommand(() => Shell.ClearIoTableCommand.Execute(null));
        SaveIoTableToSourceCommand = new AsyncRelayCommand(() => Shell.SaveIoTableToSourceCommand.ExecuteAsync(null));
        GenerateIoProgramsCommand = new AsyncRelayCommand(() => Shell.GenerateIoProgramsCommand.ExecuteAsync(null));
        OpenGeneratedIoFolderCommand = new RelayCommand(() => Shell.OpenGeneratedIoFolderCommand.Execute(null));
        OpenGeneratedIoFileCommand = new RelayCommand<GeneratedProgramArtifact?>(a => Shell.OpenGeneratedIoFileCommand.Execute(a));
        ApplyRuntimeTemplateCommand = new RelayCommand<string?>(t => Shell.ApplyRuntimeTemplateCommand.Execute(t));

        // GitPull 命令
        BrowseGitTargetFolderCommand = new RelayCommand(() => Shell.BrowseGitTargetFolderCommand.Execute(null));
        PullGitRepositoryCommand = new AsyncRelayCommand(() => Shell.PullGitRepositoryCommand.ExecuteAsync(null));
        OpenGitTargetFolderCommand = new RelayCommand(() => Shell.OpenGitTargetFolderCommand.Execute(null));

        // SFC 自动程序命令
        AddSfcStepCommand = new RelayCommand(() => Shell.AddSfcStepCommand.Execute(null));
        DeleteSfcStepCommand = new RelayCommand(() => Shell.DeleteSfcStepCommand.Execute(null));
        MoveSfcStepUpCommand = new RelayCommand(() => Shell.MoveSfcStepUpCommand.Execute(null));
        MoveSfcStepDownCommand = new RelayCommand(() => Shell.MoveSfcStepDownCommand.Execute(null));
        AddSfcActionCommand = new RelayCommand(() => Shell.AddSfcActionCommand.Execute(null));
        DeleteSfcActionCommand = new RelayCommand<SfcStepAction?>(a => Shell.DeleteSfcActionCommand.Execute(a));
        AddSfcBranchCommand = new RelayCommand(() => Shell.AddSfcBranchCommand.Execute(null));
        DeleteSfcBranchCommand = new RelayCommand<SfcStepBranch?>(b => Shell.DeleteSfcBranchCommand.Execute(b));
        AutoFillSelectedSfcStepCommand = new RelayCommand(() => Shell.AutoFillSelectedSfcStepCommand.Execute(null));
        AddSfcAlarmCommand = new RelayCommand(() => Shell.AddSfcAlarmCommand.Execute(null));
        DeleteSfcAlarmCommand = new RelayCommand<SfcStepAlarm?>(a => Shell.DeleteSfcAlarmCommand.Execute(a));
        GenerateSfcCodeCommand = new AsyncRelayCommand(() => Shell.GenerateSfcCodeCommand.ExecuteAsync(null));
        CopySfcCodeCommand = new RelayCommand(() => Shell.CopySfcCodeCommand.Execute(null));
        SaveSfcCodeToFileCommand = new RelayCommand(() => Shell.SaveSfcCodeToFileCommand.Execute(null));

        // SFC 初始化程序命令
        AddSfcInitStepCommand = new RelayCommand(() => Shell.AddSfcInitStepCommand.Execute(null));
        DeleteSfcInitStepCommand = new RelayCommand(() => Shell.DeleteSfcInitStepCommand.Execute(null));
        MoveSfcInitStepUpCommand = new RelayCommand(() => Shell.MoveSfcInitStepUpCommand.Execute(null));
        MoveSfcInitStepDownCommand = new RelayCommand(() => Shell.MoveSfcInitStepDownCommand.Execute(null));
        AddSfcInitActionCommand = new RelayCommand(() => Shell.AddSfcInitActionCommand.Execute(null));
        DeleteSfcInitActionCommand = new RelayCommand<SfcStepAction?>(a => Shell.DeleteSfcInitActionCommand.Execute(a));
        AddSfcInitBranchCommand = new RelayCommand(() => Shell.AddSfcInitBranchCommand.Execute(null));
        DeleteSfcInitBranchCommand = new RelayCommand<SfcStepBranch?>(b => Shell.DeleteSfcInitBranchCommand.Execute(b));
        AutoFillSelectedSfcInitStepCommand = new RelayCommand(() => Shell.AutoFillSelectedSfcInitStepCommand.Execute(null));
        AddSfcInitAlarmCommand = new RelayCommand(() => Shell.AddSfcInitAlarmCommand.Execute(null));
        DeleteSfcInitAlarmCommand = new RelayCommand<SfcStepAlarm?>(a => Shell.DeleteSfcInitAlarmCommand.Execute(a));
        GenerateSfcInitCodeCommand = new AsyncRelayCommand(() => Shell.GenerateSfcInitCodeCommand.ExecuteAsync(null));
        CopySfcInitCodeCommand = new RelayCommand(() => Shell.CopySfcInitCodeCommand.Execute(null));
        SaveSfcInitCodeToFileCommand = new RelayCommand(() => Shell.SaveSfcInitCodeToFileCommand.Execute(null));
    }

    // -- IO 程序生成命令 --
    public IAsyncRelayCommand SaveConfigCommand { get; }
    public IAsyncRelayCommand LoadConfigCommand { get; }
    public IAsyncRelayCommand ImportIoTableCommand { get; }
    public IRelayCommand ClearIoTableCommand { get; }
    public IAsyncRelayCommand SaveIoTableToSourceCommand { get; }
    public IAsyncRelayCommand GenerateIoProgramsCommand { get; }
    public IRelayCommand OpenGeneratedIoFolderCommand { get; }
    public IRelayCommand<GeneratedProgramArtifact?> OpenGeneratedIoFileCommand { get; }
    public IRelayCommand<string?> ApplyRuntimeTemplateCommand { get; }

    // -- GitPull 命令 --
    public IRelayCommand BrowseGitTargetFolderCommand { get; }
    public IAsyncRelayCommand PullGitRepositoryCommand { get; }
    public IRelayCommand OpenGitTargetFolderCommand { get; }

    // -- SFC 自动程序命令 --
    public IRelayCommand AddSfcStepCommand { get; }
    public IRelayCommand DeleteSfcStepCommand { get; }
    public IRelayCommand MoveSfcStepUpCommand { get; }
    public IRelayCommand MoveSfcStepDownCommand { get; }
    public IRelayCommand AddSfcActionCommand { get; }
    public IRelayCommand<SfcStepAction?> DeleteSfcActionCommand { get; }
    public IRelayCommand AddSfcBranchCommand { get; }
    public IRelayCommand<SfcStepBranch?> DeleteSfcBranchCommand { get; }
    public IRelayCommand AutoFillSelectedSfcStepCommand { get; }
    public IRelayCommand AddSfcAlarmCommand { get; }
    public IRelayCommand<SfcStepAlarm?> DeleteSfcAlarmCommand { get; }
    public IAsyncRelayCommand GenerateSfcCodeCommand { get; }
    public IRelayCommand CopySfcCodeCommand { get; }
    public IRelayCommand SaveSfcCodeToFileCommand { get; }

    // -- SFC 初始化程序命令 --
    public IRelayCommand AddSfcInitStepCommand { get; }
    public IRelayCommand DeleteSfcInitStepCommand { get; }
    public IRelayCommand MoveSfcInitStepUpCommand { get; }
    public IRelayCommand MoveSfcInitStepDownCommand { get; }
    public IRelayCommand AddSfcInitActionCommand { get; }
    public IRelayCommand<SfcStepAction?> DeleteSfcInitActionCommand { get; }
    public IRelayCommand AddSfcInitBranchCommand { get; }
    public IRelayCommand<SfcStepBranch?> DeleteSfcInitBranchCommand { get; }
    public IRelayCommand AutoFillSelectedSfcInitStepCommand { get; }
    public IRelayCommand AddSfcInitAlarmCommand { get; }
    public IRelayCommand<SfcStepAlarm?> DeleteSfcInitAlarmCommand { get; }
    public IAsyncRelayCommand GenerateSfcInitCodeCommand { get; }
    public IRelayCommand CopySfcInitCodeCommand { get; }
    public IRelayCommand SaveSfcInitCodeToFileCommand { get; }

    // -- 导航 / 子页面切换 --
    public string CurrentSubSection
    {
        get => Shell.CurrentDesignerSubSection;
        set => Shell.CurrentDesignerSubSection = value;
    }
    public string CurrentDesignerTitle => Shell.CurrentDesignerTitle;
    public bool IsDesignMode => Shell.IsDesignMode;
    public bool IsRuntimeMode => Shell.IsRuntimeMode;
    public bool IsDesignerIoProgramPageVisible => Shell.IsDesignerIoProgramPageVisible;
    public bool IsDesignerAutoProgramPageVisible => Shell.IsDesignerAutoProgramPageVisible;
    public bool IsDesignerInitProgramPageVisible => Shell.IsDesignerInitProgramPageVisible;

    // -- 数据集合（IO/SFC/Tag）--
    public ObservableCollection<IoTableRow> IoTableRows => Shell.IoTableRows;
    public ObservableCollection<GeneratedProgramArtifact> GeneratedIoPrograms => Shell.GeneratedIoPrograms;
    public ObservableCollection<TagItem> Tags => Shell.Tags;

    public GeneratedProgramArtifact? SelectedGeneratedIoProgram
    {
        get => Shell.SelectedGeneratedIoProgram;
        set => Shell.SelectedGeneratedIoProgram = value;
    }

    // -- IO 生成相关属性 --
    public bool CanSaveIoTable => Shell.CanSaveIoTable;
    public string IoImportSummary => Shell.IoImportSummary;
    public string GeneratedIoOutputDirectory => Shell.GeneratedIoOutputDirectory;
    public string SelectedIoPlcTemplate
    {
        get => Shell.SelectedIoPlcTemplate;
        set => Shell.SelectedIoPlcTemplate = value;
    }
    public string IoOperationNumber
    {
        get => Shell.IoOperationNumber;
        set => Shell.IoOperationNumber = value;
    }
    public string SelectedGeneratedIoProgramContent => Shell.SelectedGeneratedIoProgramContent;
    public bool HasGeneratedIoPrograms => Shell.HasGeneratedIoPrograms;

    // -- SFC 自动程序属性 --
    public ObservableCollection<SfcStep> SfcSteps => Shell.SfcSteps;
    public SfcStep? SelectedSfcStep
    {
        get => Shell.SelectedSfcStep;
        set => Shell.SelectedSfcStep = value;
    }
    public string SfcProgramName
    {
        get => Shell.SfcProgramName;
        set => Shell.SfcProgramName = value;
    }
    public string SfcStationNo
    {
        get => Shell.SfcStationNo;
        set => Shell.SfcStationNo = value;
    }
    public string SfcGeneratedCode
    {
        get => Shell.SfcGeneratedCode;
        set => Shell.SfcGeneratedCode = value;
    }

    // -- SFC 初始化程序属性 --
    public ObservableCollection<SfcStep> SfcInitSteps => Shell.SfcInitSteps;
    public SfcStep? SelectedSfcInitStep
    {
        get => Shell.SelectedSfcInitStep;
        set => Shell.SelectedSfcInitStep = value;
    }
    public string SfcInitProgramName
    {
        get => Shell.SfcInitProgramName;
        set => Shell.SfcInitProgramName = value;
    }
    public string SfcInitStationNo
    {
        get => Shell.SfcInitStationNo;
        set => Shell.SfcInitStationNo = value;
    }
    public string SfcInitGeneratedCode
    {
        get => Shell.SfcInitGeneratedCode;
        set => Shell.SfcInitGeneratedCode = value;
    }

    public IEnumerable<SfcDeviceOption> SfcCylinderOptions => Shell.SfcCylinderOptions;
    public IEnumerable<SfcDeviceOption> SfcAxisOptions => Shell.SfcAxisOptions;
    public IEnumerable<SfcDeviceOption> SfcVacuumOptions => Shell.SfcVacuumOptions;

    // -- GitPull 属性 --
    public string GitRepositoryUrl
    {
        get => Shell.GitRepositoryUrl;
        set => Shell.GitRepositoryUrl = value;
    }
    public string GitBranch
    {
        get => Shell.GitBranch;
        set => Shell.GitBranch = value;
    }
    public string GitTargetFolder
    {
        get => Shell.GitTargetFolder;
        set => Shell.GitTargetFolder = value;
    }
    public string GitProjectFolderName
    {
        get => Shell.GitProjectFolderName;
        set => Shell.GitProjectFolderName = value;
    }
    public string GitUsername
    {
        get => Shell.GitUsername;
        set => Shell.GitUsername = value;
    }
    public string GitAccessToken
    {
        get => Shell.GitAccessToken;
        set => Shell.GitAccessToken = value;
    }
    public string GitPullStatus => Shell.GitPullStatus;
    public string GitPullLog => Shell.GitPullLog;
    public bool IsGitPullRunning => Shell.IsGitPullRunning;
    public bool IsSyncGeneratedToGitEnabled
    {
        get => Shell.IsSyncGeneratedToGitEnabled;
        set => Shell.IsSyncGeneratedToGitEnabled = value;
    }
    public bool IsIncludeProjectFilesOnPullEnabled
    {
        get => Shell.IsIncludeProjectFilesOnPullEnabled;
        set => Shell.IsIncludeProjectFilesOnPullEnabled = value;
    }
    public bool IsForceResetLocalEnabled
    {
        get => Shell.IsForceResetLocalEnabled;
        set => Shell.IsForceResetLocalEnabled = value;
    }
    public bool IsPushProjectBranchToRemoteEnabled
    {
        get => Shell.IsPushProjectBranchToRemoteEnabled;
        set => Shell.IsPushProjectBranchToRemoteEnabled = value;
    }
    public bool IsCommitAndPushAfterGenerateEnabled
    {
        get => Shell.IsCommitAndPushAfterGenerateEnabled;
        set => Shell.IsCommitAndPushAfterGenerateEnabled = value;
    }
    public string GitAutoCommitMessageTemplate
    {
        get => Shell.GitAutoCommitMessageTemplate;
        set => Shell.GitAutoCommitMessageTemplate = value;
    }
}
