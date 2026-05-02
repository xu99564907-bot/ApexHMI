using System;

namespace ApexHMI.ViewModels;

public partial class MainViewModel
{
    public void Dispose()
    {
        _subscriptionTimer.Stop();
        _subscriptionTimer.Tick -= SubscriptionTimer_Tick;

        _opcUaBrowserRefreshTimer.Stop();
        _opcUaBrowserRefreshTimer.Tick -= OpcUaBrowserRefreshTimer_Tick;

        _opcUaService.TagValueChanged -= OpcUaService_TagValueChanged;
        GC.SuppressFinalize(this);
    }
}
