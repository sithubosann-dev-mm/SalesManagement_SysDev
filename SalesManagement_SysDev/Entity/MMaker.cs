using System;
using System.Collections.Generic;

namespace SalesManagement_SysDev;

public partial class MMaker
{
    public int MaId { get; set; }

    public string MaName { get; set; } = null!;

    public string MaAddress { get; set; } = null!;

    public string MaPhone { get; set; } = null!;

    public string MaPostal { get; set; } = null!;

    public string MaFax { get; set; } = null!;

    public int MaFlag { get; set; }

    public string? MaHidden { get; set; }

    public virtual ICollection<MProduct> MProducts { get; set; } = new List<MProduct>();

    public virtual ICollection<THattyu> THattyus { get; set; } = new List<THattyu>();
}
