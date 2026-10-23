using System;
using System.Collections.Generic;

namespace SalesManagement_SysDev;

public partial class MMajorClassification
{
    public int McId { get; set; }

    public string McName { get; set; } = null!;

    public int McFlag { get; set; }

    public string? McHidden { get; set; }

    public virtual ICollection<MSmallClassification> MSmallClassifications { get; set; } = new List<MSmallClassification>();
}
