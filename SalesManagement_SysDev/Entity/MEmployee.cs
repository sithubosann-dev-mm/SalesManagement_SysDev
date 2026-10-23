using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace SalesManagement_SysDev;

public partial class MEmployee
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int EmId { get; set; }

    public string EmName { get; set; } = null!;

    public int SoId { get; set; }

    public int PoId { get; set; }

    public DateTime EmHiredate { get; set; }

    public string EmPassword { get; set; } = null!;

    public string EmPhone { get; set; } = null!;

    public int EmFlag { get; set; }

    public string? EmHidden { get; set; }

    public virtual MPosition Po { get; set; } = null!;

    public virtual MSalesOffice So { get; set; } = null!;

    public virtual ICollection<TArrival> TArrivals { get; set; } = new List<TArrival>();

    public virtual ICollection<TChumon> TChumons { get; set; } = new List<TChumon>();

    public virtual ICollection<THattyu> THattyus { get; set; } = new List<THattyu>();

    public virtual ICollection<TOrder> TOrders { get; set; } = new List<TOrder>();

    public virtual ICollection<TSale> TSales { get; set; } = new List<TSale>();

    public virtual ICollection<TShipment> TShipments { get; set; } = new List<TShipment>();

    public virtual ICollection<TSyukko> TSyukkos { get; set; } = new List<TSyukko>();

    public virtual ICollection<TWarehousing> TWarehousings { get; set; } = new List<TWarehousing>();
}
