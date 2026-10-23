using System;
using System.Collections.Generic;

namespace SalesManagement_SysDev;

public partial class TStock
{
    public int StId { get; set; }

    public int PrId { get; set; }

    public int StQuantity { get; set; }

    public int StFlag { get; set; }

    public virtual MProduct Pr { get; set; } = null!;
}
