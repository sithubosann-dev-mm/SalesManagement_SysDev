using System;
using System.Collections.Generic;

namespace SalesManagement_SysDev;

public partial class THattyu
{
    public int HaId { get; set; }

    public int MaId { get; set; }

    public int EmId { get; set; }

    public DateTime HaDate { get; set; }

    public int? WaWarehouseFlag { get; set; }

    public int HaFlag { get; set; }

    public string? HaHidden { get; set; }

    public virtual MEmployee Em { get; set; } = null!;

    public virtual MMaker Ma { get; set; } = null!;

    public virtual ICollection<THattyuDetail> THattyuDetails { get; set; } = new List<THattyuDetail>();

    public virtual ICollection<TWarehousing> TWarehousings { get; set; } = new List<TWarehousing>();
}
