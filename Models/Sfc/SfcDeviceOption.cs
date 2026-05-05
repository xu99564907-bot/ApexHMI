namespace ApexHMI.Models.Sfc;

public sealed record SfcDeviceOption(string DeviceType, int Index, string DisplayName, string WorkLabel = "", string HomeLabel = "")
{
    public override string ToString() => $"[{DeviceType}{Index:00}] {DisplayName}";
}
