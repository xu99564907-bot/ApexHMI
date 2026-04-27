using System.Collections.Generic;

namespace ApexHMI.Models;

public class DesignerProject
{
    public string ProjectName { get; set; } = "PLC HMI Project";
    public List<DesignerPage> Pages { get; set; } = new();
}
