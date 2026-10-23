using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.EntityFrameworkCore;

namespace SalesManagement_SysDev;

public partial class SalesManagementContext : DbContext
{
    public SalesManagementContext()
    {
    }

    public SalesManagementContext(DbContextOptions<SalesManagementContext> options)
        : base(options)
    {
    }

    public virtual DbSet<MClient> MClients { get; set; }

    public virtual DbSet<MEmployee> MEmployees { get; set; }

    public virtual DbSet<MMajorClassification> MMajorClassifications { get; set; }

    public virtual DbSet<MMaker> MMakers { get; set; }

    public virtual DbSet<MPosition> MPositions { get; set; }

    public virtual DbSet<MProduct> MProducts { get; set; }

    public virtual DbSet<MSalesOffice> MSalesOffices { get; set; }

    public virtual DbSet<MSmallClassification> MSmallClassifications { get; set; }

    public virtual DbSet<TArrival> TArrivals { get; set; }

    public virtual DbSet<TArrivalDetail> TArrivalDetails { get; set; }

    public virtual DbSet<TChumon> TChumons { get; set; }

    public virtual DbSet<TChumonDetail> TChumonDetails { get; set; }

    public virtual DbSet<THattyu> THattyus { get; set; }

    public virtual DbSet<THattyuDetail> THattyuDetails { get; set; }

    public virtual DbSet<TOrder> TOrders { get; set; }

    public virtual DbSet<TOrderDetail> TOrderDetails { get; set; }

    public virtual DbSet<TSale> TSales { get; set; }

    public virtual DbSet<TSaleDetail> TSaleDetails { get; set; }

    public virtual DbSet<TShipment> TShipments { get; set; }

    public virtual DbSet<TShipmentDetail> TShipmentDetails { get; set; }

    public virtual DbSet<TStock> TStocks { get; set; }

    public virtual DbSet<TSyukko> TSyukkos { get; set; }

    public virtual DbSet<TSyukkoDetail> TSyukkoDetails { get; set; }

    public virtual DbSet<TWarehousing> TWarehousings { get; set; }

    public virtual DbSet<TWarehousingDetail> TWarehousingDetails { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer($"Data Source=(LocalDB)\\MSSQLLocalDB;Initial Catalog=SalesManagement;Integrated Security=True");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MClient>(entity =>
        {
            entity.HasKey(e => e.ClId).HasName("PK__M_Client__B1FCF8D58C35E4D2");

            entity.ToTable("M_Client");

            entity.Property(e => e.ClId).HasColumnName("ClID");
            entity.Property(e => e.ClAddress).HasMaxLength(50);
            entity.Property(e => e.ClFax)
                .HasMaxLength(13)
                .HasColumnName("ClFAX");
            entity.Property(e => e.ClName).HasMaxLength(50);
            entity.Property(e => e.ClPhone).HasMaxLength(13);
            entity.Property(e => e.ClPostal).HasMaxLength(7);
            entity.Property(e => e.SoId).HasColumnName("SoID");

            entity.HasOne(d => d.So).WithMany(p => p.MClients)
                .HasForeignKey(d => d.SoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_M_Client_ToM_SalesOffice");
        });

        modelBuilder.Entity<MEmployee>(entity =>
        {
            entity.HasKey(e => e.EmId).HasName("PK__M_Employ__DCB5BEC24482B998");

            entity.ToTable("M_Employee");

            entity.Property(e => e.EmId).HasColumnName("EmID");
            entity.Property(e => e.EmHiredate).HasColumnType("date");
            entity.Property(e => e.EmName).HasMaxLength(50);
            entity.Property(e => e.EmPassword).HasMaxLength(10);
            entity.Property(e => e.EmPhone).HasMaxLength(13);
            entity.Property(e => e.PoId).HasColumnName("PoID");
            entity.Property(e => e.SoId).HasColumnName("SoID");

            entity.HasOne(d => d.Po).WithMany(p => p.MEmployees)
                .HasForeignKey(d => d.PoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_M_Employee_ToM_Position");

            entity.HasOne(d => d.So).WithMany(p => p.MEmployees)
                .HasForeignKey(d => d.SoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_M_Employee_ToM_SalesOffice");
        });

        modelBuilder.Entity<MMajorClassification>(entity =>
        {
            entity.HasKey(e => e.McId).HasName("PK__M_MajorC__27B1E232B6CE2E6D");

            entity.ToTable("M_MajorClassification");

            entity.Property(e => e.McId).HasColumnName("McID");
            entity.Property(e => e.McName).HasMaxLength(50);
        });

        modelBuilder.Entity<MMaker>(entity =>
        {
            entity.HasKey(e => e.MaId).HasName("PK__M_Maker__2725BF40FEBFC979");

            entity.ToTable("M_Maker");

            entity.Property(e => e.MaId).HasColumnName("MaID");
            entity.Property(e => e.MaAddress).HasMaxLength(50);
            entity.Property(e => e.MaFax)
                .HasMaxLength(13)
                .HasColumnName("MaFAX");
            entity.Property(e => e.MaName).HasMaxLength(50);
            entity.Property(e => e.MaPhone).HasMaxLength(13);
            entity.Property(e => e.MaPostal).HasMaxLength(7);
        });

        modelBuilder.Entity<MPosition>(entity =>
        {
            entity.HasKey(e => e.PoId).HasName("PK__M_Positi__A4C01F9E8A03A2CA");

            entity.ToTable("M_Position");

            entity.Property(e => e.PoId).HasColumnName("PoID");
            entity.Property(e => e.PoName).HasMaxLength(50);
        });

        modelBuilder.Entity<MProduct>(entity =>
        {
            entity.HasKey(e => e.PrId).HasName("PK__M_Produc__A5021A4FEB48A98E");

            entity.ToTable("M_Product");

            entity.Property(e => e.PrId).HasColumnName("PrID");
            entity.Property(e => e.MaId).HasColumnName("MaID");
            entity.Property(e => e.PrColor).HasMaxLength(20);
            entity.Property(e => e.PrJcode)
                .HasMaxLength(13)
                .HasColumnName("PrJCode");
            entity.Property(e => e.PrModelNumber).HasMaxLength(20);
            entity.Property(e => e.PrName).HasMaxLength(50);
            entity.Property(e => e.PrReleaseDate).HasColumnType("date");
            entity.Property(e => e.Price).HasColumnType("decimal(9, 0)");
            entity.Property(e => e.ScId).HasColumnName("ScID");

            entity.HasOne(d => d.Sc).WithMany(p => p.MProducts)
                .HasForeignKey(d => d.ScId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_M_Product_ToM_SmallClassification");

            entity.HasOne(d => d.Ma).WithMany(p => p.MProducts)
                .HasForeignKey(d => d.MaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_M_Product_ToM_Maker");
        });

        modelBuilder.Entity<MSalesOffice>(entity =>
        {
            entity.HasKey(e => e.SoId).HasName("PK__M_SalesO__BC3C9374243FA851");

            entity.ToTable("M_SalesOffice");

            entity.Property(e => e.SoId).HasColumnName("SoID");
            entity.Property(e => e.SoAddress).HasMaxLength(50);
            entity.Property(e => e.SoFax)
                .HasMaxLength(13)
                .HasColumnName("SoFAX");
            entity.Property(e => e.SoName).HasMaxLength(50);
            entity.Property(e => e.SoPhone).HasMaxLength(13);
            entity.Property(e => e.SoPostal).HasMaxLength(7);
        });

        modelBuilder.Entity<MSmallClassification>(entity =>
        {
            entity.HasKey(e => e.ScId).HasName("PK__M_SmallC__ACB791BA805D9A7F");

            entity.ToTable("M_SmallClassification");

            entity.Property(e => e.ScId).HasColumnName("ScID");
            entity.Property(e => e.McId).HasColumnName("McID");
            entity.Property(e => e.ScName).HasMaxLength(50);

            entity.HasOne(d => d.Mc).WithMany(p => p.MSmallClassifications)
                .HasForeignKey(d => d.McId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_M_SmallClassification_ToM_MajorClassification");
        });

        modelBuilder.Entity<TArrival>(entity =>
        {
            entity.HasKey(e => e.ArId);

            entity.ToTable("T_Arrival");

            entity.Property(e => e.ArId).HasColumnName("ArID");
            entity.Property(e => e.ArDate).HasColumnType("date");
            entity.Property(e => e.ClId).HasColumnName("ClID");
            entity.Property(e => e.EmId).HasColumnName("EmID");
            entity.Property(e => e.OrId).HasColumnName("OrID");
            entity.Property(e => e.SoId).HasColumnName("SoID");

            entity.HasOne(d => d.Cl).WithMany(p => p.TArrivals)
                .HasForeignKey(d => d.ClId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Arrival_ToM_Client");

            entity.HasOne(d => d.Em).WithMany(p => p.TArrivals)
                .HasForeignKey(d => d.EmId)
                .HasConstraintName("FK_T_Arrival_ToM_Employee");

            entity.HasOne(d => d.Or).WithMany(p => p.TArrivals)
                .HasForeignKey(d => d.OrId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Arrival_ToT_Order");

            entity.HasOne(d => d.So).WithMany(p => p.TArrivals)
                .HasForeignKey(d => d.SoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Arrival_ToM_SalesOffice");
        });

        modelBuilder.Entity<TArrivalDetail>(entity =>
        {
            entity.HasKey(e => e.ArDetailId);

            entity.ToTable("T_ArrivalDetail");

            entity.Property(e => e.ArDetailId).HasColumnName("ArDetailID");
            entity.Property(e => e.ArId).HasColumnName("ArID");
            entity.Property(e => e.PrId).HasColumnName("PrID");

            entity.HasOne(d => d.Ar).WithMany(p => p.TArrivalDetails)
                .HasForeignKey(d => d.ArId)
                .HasConstraintName("FK_T_ArrivalDetail_ToT_Arrival");

            entity.HasOne(d => d.Pr).WithMany(p => p.TArrivalDetails)
                .HasForeignKey(d => d.PrId)
                .HasConstraintName("FK_T_ArrivalDetail_ToM_Product");
        });

        modelBuilder.Entity<TChumon>(entity =>
        {
            entity.HasKey(e => e.ChId).HasName("PK__T_Chumon__AF02F0B8EDF28122");

            entity.ToTable("T_Chumon");

            entity.Property(e => e.ChId).HasColumnName("ChID");
            entity.Property(e => e.ChDate).HasColumnType("date");
            entity.Property(e => e.ClId).HasColumnName("ClID");
            entity.Property(e => e.EmId).HasColumnName("EmID");
            entity.Property(e => e.OrId).HasColumnName("OrID");
            entity.Property(e => e.SoId).HasColumnName("SoID");

            entity.HasOne(d => d.Cl).WithMany(p => p.TChumons)
                .HasForeignKey(d => d.ClId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Chumon_ToM_Client");

            entity.HasOne(d => d.Em).WithMany(p => p.TChumons)
                .HasForeignKey(d => d.EmId)
                .HasConstraintName("FK_T_Chumon_ToM_Employee");

            entity.HasOne(d => d.Or).WithMany(p => p.TChumons)
                .HasForeignKey(d => d.OrId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Chumon_ToT_Order");

            entity.HasOne(d => d.So).WithMany(p => p.TChumons)
                .HasForeignKey(d => d.SoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Chumon_ToM_SalesOffice");
        });

        modelBuilder.Entity<TChumonDetail>(entity =>
        {
            entity.HasKey(e => e.ChDetailId);

            entity.ToTable("T_ChumonDetail");

            entity.Property(e => e.ChDetailId).HasColumnName("ChDetailID");
            entity.Property(e => e.ChId).HasColumnName("ChID");
            entity.Property(e => e.PrId).HasColumnName("PrID");

            entity.HasOne(d => d.Ch).WithMany(p => p.TChumonDetails)
                .HasForeignKey(d => d.ChId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_ChumonDetail_ToT_Chumon");

            entity.HasOne(d => d.Pr).WithMany(p => p.TChumonDetails)
                .HasForeignKey(d => d.PrId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_ChumonDetail_ToM_Product");
        });

        modelBuilder.Entity<THattyu>(entity =>
        {
            entity.HasKey(e => e.HaId);

            entity.ToTable("T_Hattyu");

            entity.Property(e => e.HaId).HasColumnName("HaID");
            entity.Property(e => e.EmId).HasColumnName("EmID");
            entity.Property(e => e.HaDate).HasColumnType("date");
            entity.Property(e => e.MaId).HasColumnName("MaID");

            entity.HasOne(d => d.Em).WithMany(p => p.THattyus)
                .HasForeignKey(d => d.EmId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Hattyu_ToM_Employee");

            entity.HasOne(d => d.Ma).WithMany(p => p.THattyus)
                .HasForeignKey(d => d.MaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Hattyu_ToM_Maker");
        });

        modelBuilder.Entity<THattyuDetail>(entity =>
        {
            entity.HasKey(e => e.HaDetailId);

            entity.ToTable("T_HattyuDetail");

            entity.Property(e => e.HaDetailId).HasColumnName("HaDetailID");
            entity.Property(e => e.HaId).HasColumnName("HaID");
            entity.Property(e => e.PrId).HasColumnName("PrID");

            entity.HasOne(d => d.Ha).WithMany(p => p.THattyuDetails)
                .HasForeignKey(d => d.HaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_HattyuDetail_ToT_Hattyu");

            entity.HasOne(d => d.Pr).WithMany(p => p.THattyuDetails)
                .HasForeignKey(d => d.PrId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_HattyuDetail_ToM_Product");
        });

        modelBuilder.Entity<TOrder>(entity =>
        {
            entity.HasKey(e => e.OrId).HasName("PK__T_Order__E1649648FAE4D6E9");

            entity.ToTable("T_Order");

            entity.Property(e => e.OrId).HasColumnName("OrID");
            entity.Property(e => e.ClCharge).HasMaxLength(50);
            entity.Property(e => e.ClId).HasColumnName("ClID");
            entity.Property(e => e.EmId).HasColumnName("EmID");
            entity.Property(e => e.OrDate).HasColumnType("date");
            entity.Property(e => e.SoId).HasColumnName("SoID");

            entity.HasOne(d => d.Cl).WithMany(p => p.TOrders)
                .HasForeignKey(d => d.ClId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Order_ToM_Client");

            entity.HasOne(d => d.Em).WithMany(p => p.TOrders)
                .HasForeignKey(d => d.EmId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Order_ToM_Employee");

            entity.HasOne(d => d.So).WithMany(p => p.TOrders)
                .HasForeignKey(d => d.SoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Order_ToM_SalesOffice");
        });

        modelBuilder.Entity<TOrderDetail>(entity =>
        {
            entity.HasKey(e => e.OrDetailId).HasName("PK__T_OrderD__45EDE90EEC5B2390");

            entity.ToTable("T_OrderDetail");

            entity.Property(e => e.OrDetailId).HasColumnName("OrDetailID");
            entity.Property(e => e.OrId).HasColumnName("OrID");
            entity.Property(e => e.OrTotalPrice).HasColumnType("decimal(10, 0)");
            entity.Property(e => e.PrId).HasColumnName("PrID");

            entity.HasOne(d => d.Or).WithMany(p => p.TOrderDetails)
                .HasForeignKey(d => d.OrId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_OrderDetail_ToT_Order");

            entity.HasOne(d => d.Pr).WithMany(p => p.TOrderDetails)
                .HasForeignKey(d => d.PrId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_OrderDetail_ToM_Product");
        });

        modelBuilder.Entity<TSale>(entity =>
        {
            entity.HasKey(e => e.SaId);

            entity.ToTable("T_Sale");

            entity.Property(e => e.SaId).HasColumnName("SaID");
            entity.Property(e => e.OrId).HasColumnName("OrID");
            entity.Property(e => e.ClId).HasColumnName("ClID");
            entity.Property(e => e.EmId).HasColumnName("EmID");
            entity.Property(e => e.SaDate).HasColumnType("date");
            entity.Property(e => e.SoId).HasColumnName("SoID");

            entity.HasOne(d => d.Or).WithMany(p => p.TSales)
                .HasForeignKey(d => d.OrId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Sale_ToT_Order");

            entity.HasOne(d => d.Cl).WithMany(p => p.TSales)
                .HasForeignKey(d => d.ClId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Sale_ToM_Client");

            entity.HasOne(d => d.Em).WithMany(p => p.TSales)
                .HasForeignKey(d => d.EmId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Sale_ToM_Employee");

            entity.HasOne(d => d.So).WithMany(p => p.TSales)
                .HasForeignKey(d => d.SoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Sale_ToM_SalesOffice");
        });

        modelBuilder.Entity<TSaleDetail>(entity =>
        {
            entity.HasKey(e => e.SaDetailId);

            entity.ToTable("T_SaleDetail");

            entity.Property(e => e.SaDetailId).HasColumnName("SaDetailID");
            entity.Property(e => e.PrId).HasColumnName("PrID");
            entity.Property(e => e.SaId).HasColumnName("SaID");
            entity.Property(e => e.SaPrTotalPrice).HasColumnType("decimal(10, 0)");

            entity.HasOne(d => d.Pr).WithMany(p => p.TSaleDetails)
                .HasForeignKey(d => d.PrId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_SaleDetail_ToM_Product");

            entity.HasOne(d => d.Sa).WithMany(p => p.TSaleDetails)
                .HasForeignKey(d => d.SaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_SaleDetail_ToT_Sale");
        });

        modelBuilder.Entity<TShipment>(entity =>
        {
            entity.HasKey(e => e.ShId);

            entity.ToTable("T_Shipment");

            entity.Property(e => e.ShId).HasColumnName("ShID");
            entity.Property(e => e.ClId).HasColumnName("ClID");
            entity.Property(e => e.EmId).HasColumnName("EmID");
            entity.Property(e => e.OrId).HasColumnName("OrID");
            entity.Property(e => e.ShFinishDate).HasColumnType("date");
            entity.Property(e => e.SoId).HasColumnName("SoID");

            entity.HasOne(d => d.Cl).WithMany(p => p.TShipments)
                .HasForeignKey(d => d.ClId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Shipment_ToM_Client");

            entity.HasOne(d => d.Em).WithMany(p => p.TShipments)
                .HasForeignKey(d => d.EmId)
                .HasConstraintName("FK_T_Shipment_ToM_Employee");

            entity.HasOne(d => d.Or).WithMany(p => p.TShipments)
                .HasForeignKey(d => d.OrId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Shipment_ToT_Order");

            entity.HasOne(d => d.So).WithMany(p => p.TShipments)
                .HasForeignKey(d => d.SoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Shipment_ToM_SalesOffice");
        });

        modelBuilder.Entity<TShipmentDetail>(entity =>
        {
            entity.HasKey(e => e.ShDetailId);

            entity.ToTable("T_ShipmentDetail");

            entity.Property(e => e.ShDetailId).HasColumnName("ShDetailID");
            entity.Property(e => e.PrId).HasColumnName("PrID");
            entity.Property(e => e.ShId).HasColumnName("ShID");

            entity.HasOne(d => d.Pr).WithMany(p => p.TShipmentDetails)
                .HasForeignKey(d => d.PrId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_ShipmentDetail_ToM_Product");

            entity.HasOne(d => d.Sh).WithMany(p => p.TShipmentDetails)
                .HasForeignKey(d => d.ShId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_ShipmentDetail_ToT_Shipment");
        });

        modelBuilder.Entity<TStock>(entity =>
        {
            entity.HasKey(e => e.StId).HasName("PK__T_Stock__C33CEFE204B2DFA6");

            entity.ToTable("T_Stock");

            entity.Property(e => e.StId).HasColumnName("StID");
            entity.Property(e => e.PrId).HasColumnName("PrID");

            entity.HasOne(d => d.Pr).WithMany(p => p.TStocks)
                .HasForeignKey(d => d.PrId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Stock_ToM_Product");
        });

        modelBuilder.Entity<TSyukko>(entity =>
        {
            entity.HasKey(e => e.SyId);

            entity.ToTable("T_Syukko");

            entity.Property(e => e.SyId).HasColumnName("SyID");
            entity.Property(e => e.ClId).HasColumnName("ClID");
            entity.Property(e => e.EmId).HasColumnName("EmID");
            entity.Property(e => e.OrId).HasColumnName("OrID");
            entity.Property(e => e.SoId).HasColumnName("SoID");
            entity.Property(e => e.SyDate).HasColumnType("date");

            entity.HasOne(d => d.Cl).WithMany(p => p.TSyukkos)
                .HasForeignKey(d => d.ClId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Syukko_ToM_Client");

            entity.HasOne(d => d.Em).WithMany(p => p.TSyukkos)
                .HasForeignKey(d => d.EmId)
                .HasConstraintName("FK_T_Syukko_ToM_Employee");

            entity.HasOne(d => d.Or).WithMany(p => p.TSyukkos)
                .HasForeignKey(d => d.OrId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Syukko_ToT_Order");

            entity.HasOne(d => d.So).WithMany(p => p.TSyukkos)
                .HasForeignKey(d => d.SoId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Syukko_ToM_SalesOffice");
        });

        modelBuilder.Entity<TSyukkoDetail>(entity =>
        {
            entity.HasKey(e => e.SyDetailId);

            entity.ToTable("T_SyukkoDetail");

            entity.Property(e => e.SyDetailId).HasColumnName("SyDetailID");
            entity.Property(e => e.PrId).HasColumnName("PrID");
            entity.Property(e => e.SyId).HasColumnName("SyID");

            entity.HasOne(d => d.Pr).WithMany(p => p.TSyukkoDetails)
                .HasForeignKey(d => d.PrId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_SyukkoDetail_ToM_Product");

            entity.HasOne(d => d.Sy).WithMany(p => p.TSyukkoDetails)
                .HasForeignKey(d => d.SyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_SyukkoDetail_ToT_Syukko");
        });

        modelBuilder.Entity<TWarehousing>(entity =>
        {
            entity.HasKey(e => e.WaId);

            entity.ToTable("T_Warehousing");

            entity.Property(e => e.WaId).HasColumnName("WaID");
            entity.Property(e => e.EmId).HasColumnName("EmID");
            entity.Property(e => e.HaId).HasColumnName("HaID");
            entity.Property(e => e.WaDate).HasColumnType("date");

            entity.HasOne(d => d.Em).WithMany(p => p.TWarehousings)
                .HasForeignKey(d => d.EmId)
                .HasConstraintName("FK_T_Warehousing_ToM_Employee");

            entity.HasOne(d => d.Ha).WithMany(p => p.TWarehousings)
                .HasForeignKey(d => d.HaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_Warehousing_ToT_Hattyu");
        });

        modelBuilder.Entity<TWarehousingDetail>(entity =>
        {
            entity.HasKey(e => e.WaDetailId);

            entity.ToTable("T_WarehousingDetail");

            entity.Property(e => e.WaDetailId).HasColumnName("WaDetailID");
            entity.Property(e => e.PrId).HasColumnName("PrID");
            entity.Property(e => e.WaId).HasColumnName("WaID");

            entity.HasOne(d => d.Pr).WithMany(p => p.TWarehousingDetails)
                .HasForeignKey(d => d.PrId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_WarehousingDetail_ToM_Product");

            entity.HasOne(d => d.Wa).WithMany(p => p.TWarehousingDetails)
                .HasForeignKey(d => d.WaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_T_WarehousingDetail_ToT_Warehousing");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
