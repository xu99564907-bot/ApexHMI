namespace ApexHMI.Models.Sfc;

public sealed record SfcActionOption(string Key, string Label)
{
    public override string ToString() => Label;
}
