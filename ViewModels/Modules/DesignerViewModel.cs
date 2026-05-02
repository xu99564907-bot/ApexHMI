using System.Collections.ObjectModel;
using ApexHMI.Models;
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
        ResetAutoProgramFlowCommand = new RelayCommand(() => Shell.ResetAutoProgramFlowCommand.Execute(null));
        AddAutoProgramStepCommand = new RelayCommand(() => Shell.AddAutoProgramStepCommand.Execute(null));
        GenerateAutoProgramsCommand = new AsyncRelayCommand(() => Shell.GenerateAutoProgramsCommand.ExecuteAsync(null));
        OpenGeneratedIoFolderCommand = new RelayCommand(() => Shell.OpenGeneratedIoFolderCommand.Execute(null));
        OpenGeneratedAutoFolderCommand = new RelayCommand(() => Shell.OpenGeneratedAutoFolderCommand.Execute(null));
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
    }

    // -- Designer Commands --
    public IAsyncRelayCommand SaveConfigCommand { get; }
    public IAsyncRelayCommand LoadConfigCommand { get; }
    public IAsyncRelayCommand ImportIoTableCommand { get; }
    public IRelayCommand ClearIoTableCommand { get; }
    public IAsyncRelayCommand SaveIoTableToSourceCommand { get; }
    public IAsyncRelayCommand GenerateIoProgramsCommand { get; }
    public IRelayCommand ResetAutoProgramFlowCommand { get; }
    public IRelayCommand AddAutoProgramStepCommand { get; }
    public IAsyncRelayCommand GenerateAutoProgramsCommand { get; }
    public IRelayCommand OpenGeneratedIoFolderCommand { get; }
    public IRelayCommand OpenGeneratedAutoFolderCommand { get; }
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
    public ObservableCollection<GeneratedProgramArtifact> GeneratedAutoPrograms => Shell.GeneratedAutoPrograms;
    public ObservableCollection<AutoProgramFlowNode> AutoProgramFlowNodes => Shell.AutoProgramFlowNodes;
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
    public GeneratedProgramArtifact? SelectedGeneratedAutoProgram
    {
        get => Shell.SelectedGeneratedAutoProgram;
        set => Shell.SelectedGeneratedAutoProgram = value;
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

    // -- IO generation --
    public bool CanSaveIoTable => Shell.CanSaveIoTable;
    public string IoImportSummary => Shell.IoImportSummary;
    public string GeneratedIoOutputDirectory => Shell.GeneratedIoOutputDirectory;
    public string GeneratedAutoOutputDirectory => Shell.GeneratedAutoOutputDirectory;
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
    public string SelectedGeneratedAutoProgramContent => Shell.SelectedGeneratedAutoProgramContent;
    public bool HasGeneratedIoPrograms => Shell.HasGeneratedIoPrograms;
    public bool HasGeneratedAutoPrograms => Shell.HasGeneratedAutoPrograms;

    // -- Auto program --
    public string AutoProgramName
    {
        get => Shell.AutoProgramName;
        set => Shell.AutoProgramName = value;
    }
    public string AutoProgramHeadline => Shell.AutoProgramHeadline;
    public string AutoProgramSummary => Shell.AutoProgramSummary;

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
