using System;
using System.Collections.Generic;

namespace SalesManagement_SysDev;

public partial class MPosition
{
    public int PoId { get; set; }

    public string PoName { get; set; } = null!;

    public int PoFlag { get; set; }

    public string? PoHidden { get; set; }

    public virtual ICollection<MEmployee> MEmployees { get; set; } = new List<MEmployee>();
}
