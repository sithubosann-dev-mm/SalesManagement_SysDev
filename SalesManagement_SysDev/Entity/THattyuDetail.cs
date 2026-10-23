using System;
using System.Collections.Generic;

namespace SalesManagement_SysDev;

public partial class THattyuDetail
{
    public int HaDetailId { get; set; }

    public int HaId { get; set; }

    public int PrId { get; set; }

    public int HaQuantity { get; set; }

    public virtual THattyu Ha { get; set; } = null!;

    public virtual MProduct Pr { get; set; } = null!;
}
