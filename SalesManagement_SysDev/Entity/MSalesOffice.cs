using System;
using System.Collections.Generic;

namespace SalesManagement_SysDev;

public partial class MSalesOffice
{
    public int SoId { get; set; }

    public string SoName { get; set; } = null!;

    public string SoAddress { get; set; } = null!;

    public string SoPhone { get; set; } = null!;

    public string SoPostal { get; set; } = null!;

    public string SoFax { get; set; } = null!;

    public int SoFlag { get; set; }

    public string? SoHidden { get; set; }

    public virtual ICollection<MClient> MClients { get; set; } = new List<MClient>();

    public virtual ICollection<MEmployee> MEmployees { get; set; } = new List<MEmployee>();

    public virtual ICollection<TArrival> TArrivals { get; set; } = new List<TArrival>();

    public virtual ICollection<TChumon> TChumons { get; set; } = new List<TChumon>();

    public virtual ICollection<TOrder> TOrders { get; set; } = new List<TOrder>();

    public virtual ICollection<TSale> TSales { get; set; } = new List<TSale>();

    public virtual ICollection<TShipment> TShipments { get; set; } = new List<TShipment>();

    public virtual ICollection<TSyukko> TSyukkos { get; set; } = new List<TSyukko>();
}
