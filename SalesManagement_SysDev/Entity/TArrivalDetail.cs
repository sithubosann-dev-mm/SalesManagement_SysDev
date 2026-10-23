using System;
using System.Collections.Generic;

namespace SalesManagement_SysDev;

public partial class TArrivalDetail
{
    public int ArDetailId { get; set; }

    public int? ArId { get; set; }

    public int? PrId { get; set; }

    public int? ArQuantity { get; set; }

    public virtual TArrival? Ar { get; set; }

    public virtual MProduct? Pr { get; set; }
}
