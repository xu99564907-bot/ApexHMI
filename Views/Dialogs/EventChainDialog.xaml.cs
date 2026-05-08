using System.Collections.Generic;
using System.Windows;
using ApexHMI.ViewModels.Modules;

namespace ApexHMI.Views.Dialogs;

public partial class EventChainDialog : Window
{
    public EventChainDialog() => InitializeComponent();

    public sealed class EventChainViewModel
    {
        public string Keyword { get; set; } = string.Empty;
        public List<EventChainEntry> Entries { get; set; } = new();
    }
}
