using System;
using System.Collections.Generic;

namespace SalesManagement_SysDev;

public partial class TSyukkoDetail
{
    public int SyDetailId { get; set; }

    public int SyId { get; set; }

    public int PrId { get; set; }

    public int SyQuantity { get; set; }

    public virtual MProduct Pr { get; set; } = null!;

    public virtual TSyukko Sy { get; set; } = null!;
}
