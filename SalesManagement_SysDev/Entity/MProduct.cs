using System;
using System.Collections.Generic;

namespace SalesManagement_SysDev;

public partial class MProduct
{
    public int PrId { get; set; }

    public int MaId { get; set; }

    public string PrName { get; set; } = null!;

    public decimal Price { get; set; }

    public string? PrJcode { get; set; }

    public int PrSafetyStock { get; set; }

    public int ScId { get; set; }

    public string PrModelNumber { get; set; } = null!;

    public string PrColor { get; set; } = null!;

    public DateTime PrReleaseDate { get; set; }

    public int PrFlag { get; set; }

    public string? PrHidden { get; set; }

    public virtual MSmallClassification Sc { get; set; } = null!;

    public virtual MMaker Ma { get; set; } = null!;

    public virtual ICollection<TArrivalDetail> TArrivalDetails { get; set; } = new List<TArrivalDetail>();

    public virtual ICollection<TChumonDetail> TChumonDetails { get; set; } = new List<TChumonDetail>();

    public virtual ICollection<THattyuDetail> THattyuDetails { get; set; } = new List<THattyuDetail>();

    public virtual ICollection<TOrderDetail> TOrderDetails { get; set; } = new List<TOrderDetail>();

    public virtual ICollection<TSaleDetail> TSaleDetails { get; set; } = new List<TSaleDetail>();

    public virtual ICollection<TShipmentDetail> TShipmentDetails { get; set; } = new List<TShipmentDetail>();

    public virtual ICollection<TStock> TStocks { get; set; } = new List<TStock>();

    public virtual ICollection<TSyukkoDetail> TSyukkoDetails { get; set; } = new List<TSyukkoDetail>();

    public virtual ICollection<TWarehousingDetail> TWarehousingDetails { get; set; } = new List<TWarehousingDetail>();
}
