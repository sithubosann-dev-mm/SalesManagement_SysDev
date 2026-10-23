using System;
using System.Collections.Generic;

namespace SalesManagement_SysDev;

public partial class TWarehousingDetail
{
    public int WaDetailId { get; set; }

    public int WaId { get; set; }

    public int PrId { get; set; }

    public int WaQuantity { get; set; }

    public virtual MProduct Pr { get; set; } = null!;

    public virtual TWarehousing Wa { get; set; } = null!;
}
