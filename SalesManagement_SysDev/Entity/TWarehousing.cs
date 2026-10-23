using System;
using System.Collections.Generic;

namespace SalesManagement_SysDev;

public partial class TWarehousing
{
    public int WaId { get; set; }

    public int HaId { get; set; }

    public int? EmId { get; set; }

    public DateTime WaDate { get; set; }

    public int? WaShelfFlag { get; set; }

    public string? WaHidden { get; set; }

    public int WaFlag { get; set; }

    public virtual MEmployee Em { get; set; } = null!;

    public virtual THattyu Ha { get; set; } = null!;

    public virtual ICollection<TWarehousingDetail> TWarehousingDetails { get; set; } = new List<TWarehousingDetail>();
}
