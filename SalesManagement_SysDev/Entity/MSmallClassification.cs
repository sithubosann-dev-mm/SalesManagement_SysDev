using System;
using System.Collections.Generic;

namespace SalesManagement_SysDev;

public partial class MSmallClassification
{
    public int ScId { get; set; }

    public int McId { get; set; }

    public string ScName { get; set; } = null!;

    public int ScFlag { get; set; }

    public string? ScHidden { get; set; }

    public virtual ICollection<MProduct> MProducts { get; set; } = new List<MProduct>();

    public virtual MMajorClassification Mc { get; set; } = null!;
}
