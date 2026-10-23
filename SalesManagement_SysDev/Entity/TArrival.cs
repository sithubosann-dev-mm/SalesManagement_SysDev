using System;
using System.Collections.Generic;

namespace SalesManagement_SysDev;

public partial class TArrival
{
    public int ArId { get; set; }

    public int SoId { get; set; }

    public int? EmId { get; set; }

    public int ClId { get; set; }

    public int OrId { get; set; }

    public DateTime? ArDate { get; set; }

    public int? ArStateFlag { get; set; }

    public int ArFlag { get; set; }

    public string? ArHidden { get; set; }

    public virtual MClient Cl { get; set; } = null!;

    public virtual MEmployee? Em { get; set; }

    public virtual TOrder Or { get; set; } = null!;

    public virtual MSalesOffice So { get; set; } = null!;

    public virtual ICollection<TArrivalDetail> TArrivalDetails { get; set; } = new List<TArrivalDetail>();
}
