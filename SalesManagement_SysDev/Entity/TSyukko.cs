using System;
using System.Collections.Generic;

namespace SalesManagement_SysDev;

public partial class TSyukko
{
    public int SyId { get; set; }

    public int? EmId { get; set; }

    public int ClId { get; set; }

    public int SoId { get; set; }

    public int OrId { get; set; }

    public DateTime? SyDate { get; set; }

    public int? SyStateFlag { get; set; }

    public int SyFlag { get; set; }

    public string? SyHidden { get; set; }

    public virtual MClient Cl { get; set; } = null!;

    public virtual MEmployee? Em { get; set; }

    public virtual TOrder Or { get; set; } = null!;

    public virtual MSalesOffice So { get; set; } = null!;

    public virtual ICollection<TSyukkoDetail> TSyukkoDetails { get; set; } = new List<TSyukkoDetail>();
}
