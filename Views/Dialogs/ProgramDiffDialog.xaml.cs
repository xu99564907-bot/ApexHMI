using System.Windows;

namespace ApexHMI.Views.Dialogs;

public partial class ProgramDiffDialog : Window
{
    public ProgramDiffDialog() => InitializeComponent();

    public sealed class DiffViewModel
    {
        public string LeftPath { get; set; } = string.Empty;
        public string LeftContent { get; set; } = string.Empty;
        public string RightPath { get; set; } = string.Empty;
        public string RightContent { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }
}
