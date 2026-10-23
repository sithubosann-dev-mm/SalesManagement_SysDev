using System;
using System.Collections.Generic;

namespace SalesManagement_SysDev;

public partial class TShipment
{
    public int ShId { get; set; }

    public int ClId { get; set; }

    public int? EmId { get; set; }

    public int SoId { get; set; }

    public int OrId { get; set; }

    public int? ShStateFlag { get; set; }

    public DateTime? ShFinishDate { get; set; }

    public int ShFlag { get; set; }

    public string? ShHidden { get; set; }

    public virtual MClient Cl { get; set; } = null!;

    public virtual MEmployee? Em { get; set; }

    public virtual TOrder Or { get; set; } = null!;

    public virtual MSalesOffice So { get; set; } = null!;

    public virtual ICollection<TShipmentDetail> TShipmentDetails { get; set; } = new List<TShipmentDetail>();
}
