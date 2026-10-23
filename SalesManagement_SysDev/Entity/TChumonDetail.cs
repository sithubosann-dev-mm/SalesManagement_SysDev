using System;
using System.Collections.Generic;

namespace SalesManagement_SysDev;

public partial class TChumonDetail
{
    public int ChDetailId { get; set; }

    public int ChId { get; set; }

    public int PrId { get; set; }

    public int ChQuantity { get; set; }

    public virtual TChumon Ch { get; set; } = null!;

    public virtual MProduct Pr { get; set; } = null!;
}
