using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SalesManagement_SysDev
{
    // Design preview only. These DTOs and rows are not EF entities.
    public partial class ChumonView : UserControl
    {
        private readonly List<PreviewRow> _rows = CreatePreviewRows();
        public event EventHandler? SearchRequested;
        public bool IsBusy => false;
        public bool CanLeave() => true;
        public IReadOnlyList<PreviewRow> PreviewRows => _rows.AsReadOnly();

        public ChumonView() : this(0) { }
        public ChumonView(int loginEmployeeId)
        {
            InitializeComponent();
            ShowRows();
        }

        private void ShowRows()
        {
            var selected = gridChumons.SelectedItem as PreviewRow;
            gridChumons.ItemsSource = _rows;
            gridChumons.SelectedItem = selected ?? _rows.FirstOrDefault();
            UpdateDetails();
            txtMessage.Text = "画面サンプル・DB未接続。表示件数：" + _rows.Count + "件。検索画面から対象の注文を選択できます。";
        }

        public bool OpenChumon(int id)
        {
            var row = _rows.SingleOrDefault(x => x.Id == id);
            if (row == null) return false;
            gridChumons.SelectedItem = row;
            // Content is hosted again by the menu after this method returns.
            UpdateDetails();
            return true;
        }

        private void UpdateDetails()
        {
            if (gridChumons.SelectedItem is PreviewRow row)
            {
                gridDetails.ItemsSource = row.Details;
                txtSelected.Text = $"注文ID：{row.Id}　受注ID：{row.OrderId}　顧客：{row.Client}　営業所：{row.Office}　状態：{row.State}";
                txtDetailTotal.Text = $"合計：{row.Total:N0} 円";
            }
            else
            {
                gridDetails.ItemsSource = null;
                txtSelected.Text = "注文を選択すると、ここに選択内容を表示します。";
                txtDetailTotal.Text = "合計：—";
            }
        }

        private void Chumons_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateDetails();
        private void List_Click(object sender, RoutedEventArgs e) => ShowRows();
        private void ShowSearch_Click(object sender, RoutedEventArgs e) => SearchRequested?.Invoke(this, EventArgs.Empty);
        private void Hide_Click(object sender, RoutedEventArgs e) => ShowPreviewNotice();
        private void Confirm_Click(object sender, RoutedEventArgs e) => ShowPreviewNotice();

        private void ShowPreviewNotice()
        {
            if (gridChumons.SelectedItem is not PreviewRow)
            {
                MessageBox.Show("一覧から対象の注文を選択してください。", "注文管理");
                return;
            }
            MessageBox.Show("デザイン確認用の画面です。\n非表示・注文確定のDB処理はまだ接続していません。\n在庫数の変更や出庫伝票の作成は行いません。",
                "注文管理 — 画面サンプル", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public sealed class PreviewRow
        {
            public int Id { get; set; }
            public int OrderId { get; set; }
            public DateTime Date { get; set; }
            public string Client { get; set; } = "";
            public string Office { get; set; } = "";
            public string Employee { get; set; } = "";
            public string State { get; set; } = "";
            public List<PreviewLine> Details { get; set; } = new();
            public decimal Total => Details.Sum(x => x.Total);
        }

        public sealed class PreviewLine
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; } = "";
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal Total => UnitPrice * Quantity;
        }

        internal static List<PreviewRow> CreatePreviewRows() => new List<PreviewRow>
        {
                new PreviewRow
                {
                    Id = 101, OrderId = 61, Date = new DateTime(2026, 10, 20),
                    Client = "株式会社さくら商事", Office = "東京営業所", Employee = "未担当",
                    State = "未確定", Details = new List<PreviewLine>
                    {
                    new PreviewLine { ProductId = 1001, ProductName = "ワイヤレスマウス", Quantity = 5, UnitPrice = 2400m },
                    new PreviewLine { ProductId = 1002, ProductName = "USBキーボード", Quantity = 3, UnitPrice = 3800m },
                    new PreviewLine { ProductId = 1003, ProductName = "液晶モニター 24型", Quantity = 2, UnitPrice = 24800m }
                    }
                },
                new PreviewRow
                {
                    Id = 102, OrderId = 62, Date = new DateTime(2026, 10, 20),
                    Client = "株式会社みらい産業", Office = "大阪営業所", Employee = "未担当",
                    State = "未確定", Details = new List<PreviewLine>
                    {
                    new PreviewLine { ProductId = 1001, ProductName = "ワイヤレスマウス", Quantity = 6, UnitPrice = 2400m },
                    new PreviewLine { ProductId = 1002, ProductName = "USBキーボード", Quantity = 4, UnitPrice = 3800m }
                    }
                },
                new PreviewRow
                {
                    Id = 103, OrderId = 63, Date = new DateTime(2026, 10, 20),
                    Client = "青葉テクノ株式会社", Office = "東京営業所", Employee = "山田 太郎",
                    State = "確定済", Details = new List<PreviewLine>
                    {
                    new PreviewLine { ProductId = 1001, ProductName = "ワイヤレスマウス", Quantity = 7, UnitPrice = 2400m }
                    }
                }
        };
    }
}
