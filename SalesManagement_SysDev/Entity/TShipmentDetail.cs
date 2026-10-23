using System;
using System.Collections.Generic;

namespace SalesManagement_SysDev;

public partial class TShipmentDetail
{
    public int ShDetailId { get; set; }

    public int ShId { get; set; }

    public int PrId { get; set; }

    public int ShQuantity { get; set; }

    public virtual MProduct Pr { get; set; } = null!;

    public virtual TShipment Sh { get; set; } = null!;
}
