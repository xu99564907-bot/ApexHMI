using System.Collections.Generic;
using System.Collections.ObjectModel;
using ApexHMI.Models;
using ApexHMI.Models.Sfc;
using CommunityToolkit.Mvvm.Input;

namespace ApexHMI.ViewModels.Modules;

public sealed class DesignerViewModel : ModuleViewModelBase
{
    public DesignerViewModel(MainViewModel shell)
        : base(shell, "设计器")
    {
        // Designer commands
        SaveConfigCommand = new AsyncRelayCommand(() => Shell.SaveConfigCommand.ExecuteAsync(null));
        LoadConfigCommand = new AsyncRelayCommand(() => Shell.LoadConfigCommand.ExecuteAsync(null));
        ImportIoTableCommand = new AsyncRelayCommand(() => Shell.ImportIoTableCommand.ExecuteAsync(null));
        ClearIoTableCommand = new RelayCommand(() => Shell.ClearIoTableCommand.Execute(null));
        SaveIoTableToSourceCommand = new AsyncRelayCommand(() => Shell.SaveIoTableToSourceCommand.ExecuteAsync(null));
        GenerateIoProgramsCommand = new AsyncRelayCommand(() => Shell.GenerateIoProgramsCommand.ExecuteAsync(null));
        OpenGeneratedIoFolderCommand = new RelayCommand(() => Shell.OpenGeneratedIoFolderCommand.Execute(null));
        OpenGeneratedIoFileCommand = new RelayCommand<GeneratedProgramArtifact?>(a => Shell.OpenGeneratedIoFileCommand.Execute(a));
        ApplyRuntimeTemplateCommand = new RelayCommand<string?>(t => Shell.ApplyRuntimeTemplateCommand.Execute(t));
        AddDesignerElementCommand = new RelayCommand<string?>(e => Shell.AddDesignerElementCommand.Execute(e));
        AddDesignerElementAtDropCommand = new RelayCommand<string?>(p => Shell.AddDesignerElementAtDropCommand.Execute(p));
        StartToolboxDragCommand = new RelayCommand<string?>(t => Shell.StartToolboxDragCommand.Execute(t));
        RemoveSelectedDesignerElementCommand = new RelayCommand(() => Shell.RemoveSelectedDesignerElementCommand.Execute(null));
        CopySelectedDesignerElementCommand = new RelayCommand(() => Shell.CopySelectedDesignerElementCommand.Execute(null));
        PasteDesignerElementCommand = new RelayCommand(() => Shell.PasteDesignerElementCommand.Execute(null));
        MoveSelectedElementCommand = new RelayCommand<string?>(d => Shell.MoveSelectedElementCommand.Execute(d));

        // GitPull commands
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

    // -- Designer Commands --
    public IAsyncRelayCommand SaveConfigCommand { get; }
    public IAsyncRelayCommand LoadConfigCommand { get; }
    public IAsyncRelayCommand ImportIoTableCommand { get; }
    public IRelayCommand ClearIoTableCommand { get; }
    public IAsyncRelayCommand SaveIoTableToSourceCommand { get; }
    public IAsyncRelayCommand GenerateIoProgramsCommand { get; }
    public IRelayCommand OpenGeneratedIoFolderCommand { get; }
    public IRelayCommand<GeneratedProgramArtifact?> OpenGeneratedIoFileCommand { get; }
    public IRelayCommand<string?> ApplyRuntimeTemplateCommand { get; }
    public IRelayCommand<string?> AddDesignerElementCommand { get; }
    public IRelayCommand<string?> AddDesignerElementAtDropCommand { get; }
    public IRelayCommand<string?> StartToolboxDragCommand { get; }
    public IRelayCommand RemoveSelectedDesignerElementCommand { get; }
    public IRelayCommand CopySelectedDesignerElementCommand { get; }
    public IRelayCommand PasteDesignerElementCommand { get; }
    public IRelayCommand<string?> MoveSelectedElementCommand { get; }

    // -- GitPull Commands --
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

    // -- Navigation/Sub-section --
    public string CurrentSubSection
    {
        get => Shell.CurrentDesignerSubSection;
        set => Shell.CurrentDesignerSubSection = value;
    }
    public string CurrentDesignerTitle => Shell.CurrentDesignerTitle;
    public bool IsDesignMode => Shell.IsDesignMode;
    public bool IsRuntimeMode => Shell.IsRuntimeMode;
    public bool IsDesignerCanvasPageVisible => Shell.IsDesignerCanvasPageVisible;
    public bool IsDesignerIoProgramPageVisible => Shell.IsDesignerIoProgramPageVisible;
    public bool IsDesignerAutoProgramPageVisible => Shell.IsDesignerAutoProgramPageVisible;

    // -- Collections --
    public ObservableCollection<DesignerPage> Pages => Shell.DesignerPages;
    public ObservableCollection<DesignerElement> Elements => Shell.DesignerElements;
    public ObservableCollection<IoTableRow> IoTableRows => Shell.IoTableRows;
    public ObservableCollection<GeneratedProgramArtifact> GeneratedIoPrograms => Shell.GeneratedIoPrograms;
    public ObservableCollection<TagItem> Tags => Shell.Tags;
    public ObservableCollection<string> DesignerActionOptions => Shell.DesignerActionOptions;

    // -- Selected items --
    public DesignerElement? SelectedDesignerElement
    {
        get => Shell.SelectedDesignerElement;
        set => Shell.SelectedDesignerElement = value;
    }
    public DesignerPage? SelectedDesignerPage
    {
        get => Shell.SelectedDesignerPage;
        set => Shell.SelectedDesignerPage = value;
    }
    public GeneratedProgramArtifact? SelectedGeneratedIoProgram
    {
        get => Shell.SelectedGeneratedIoProgram;
        set => Shell.SelectedGeneratedIoProgram = value;
    }

    // -- Selected element helpers --
    public bool IsSelectedDesignerElementButtonLike => Shell.IsSelectedDesignerElementButtonLike;
    public bool IsSelectedDesignerElementTagBindable => Shell.IsSelectedDesignerElementTagBindable;
    public bool IsSelectedDesignerElementNavigationAction => Shell.IsSelectedDesignerElementNavigationAction;
    public bool HasClipboard => Shell.HasClipboard;

    // -- Designer settings --
    public string SelectedToolboxItem
    {
        get => Shell.SelectedToolboxItem;
        set => Shell.SelectedToolboxItem = value;
    }
    public double DesignerCanvasWidth
    {
        get => Shell.DesignerCanvasWidth;
        set => Shell.DesignerCanvasWidth = value;
    }
    public double DesignerCanvasHeight
    {
        get => Shell.DesignerCanvasHeight;
        set => Shell.DesignerCanvasHeight = value;
    }
    public string DesignerPageName
    {
        get => Shell.DesignerPageName;
        set => Shell.DesignerPageName = value;
    }
    public string DesignerProjectName
    {
        get => Shell.DesignerProjectName;
        set => Shell.DesignerProjectName = value;
    }
    public string DragToolboxItem
    {
        get => Shell.DragToolboxItem;
        set => Shell.DragToolboxItem = value;
    }
    public bool EnableGridSnap
    {
        get => Shell.EnableGridSnap;
        set => Shell.EnableGridSnap = value;
    }
    public int GridSize
    {
        get => Shell.GridSize;
        set => Shell.GridSize = value;
    }
    public string SelectedRuntimeTemplate
    {
        get => Shell.SelectedRuntimeTemplate;
        set => Shell.SelectedRuntimeTemplate = value;
    }

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

    public bool IsDesignerInitProgramPageVisible => Shell.IsDesignerInitProgramPageVisible;
    public IEnumerable<SfcDeviceOption> SfcCylinderOptions => Shell.SfcCylinderOptions;
    public IEnumerable<SfcDeviceOption> SfcAxisOptions => Shell.SfcAxisOptions;
    public IEnumerable<SfcDeviceOption> SfcVacuumOptions => Shell.SfcVacuumOptions;

    // -- IO generation --
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

    // -- GitPull properties --
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
