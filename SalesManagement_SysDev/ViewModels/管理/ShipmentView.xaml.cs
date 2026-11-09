using System;
using System.Data;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;

namespace SalesManagement_SysDev
{
    public partial class ShipmentView : UserControl
    {
        private readonly int _loginEmployeeId;
        private bool _initialized, _changingSelection;
        public event EventHandler? SearchRequested;
        public bool IsBusy { get; private set; }
        public string DisplayLoadError { get; private set; } = "";

        public ShipmentView() : this(0) { }
        public ShipmentView(int loginEmployeeId)
        {
            InitializeComponent();
            _loginEmployeeId = loginEmployeeId;
        }

        public bool CanLeave()
        {
            if (!IsBusy) return true;
            MessageBox.Show("処理が終わるまでお待ちください。");
            return false;
        }

        private async Task<bool> RunAsync(Func<Task> action)
        {
            if (IsBusy) return false;
            IsBusy = true;
            mainPanel.IsEnabled = false;
            DisplayLoadError = "";
            txtMessage.Text = "出荷情報を読み込んでいます…";
            try { await action(); return true; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                DisplayLoadError = ReadError(ex);
                txtMessage.Text = DisplayLoadError;
                return false;
            }
            finally { IsBusy = false; mainPanel.IsEnabled = true; UpdateActions(); }
        }

        private async void View_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized || DesignerProperties.GetIsInDesignMode(this)) return;
            await ReloadAsync();
        }

        private async Task ReloadAsync()
        {
            int? selectedId = (gridShipments.SelectedItem as ShipmentRow)?.Id;
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                var rows = await ReadRowsAsync(db, _loginEmployeeId, new ShipmentFilter());
                BindRows(rows, selectedId);
                _initialized = true;
                txtMessage.Text = rows.Count == 0
                    ? "表示対象の出荷は0件です。入荷管理で入荷確定が完了しているか確認してください。" + ConnectionLabel()
                    : $"{rows.Count}件の出荷をDBから取得しました。入荷確定で作成された出荷と商品明細を確認できます。";
            });
        }

        public async Task<bool> OpenShipmentAsync(int id)
        {
            return await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                var rows = await ReadRowsAsync(db, _loginEmployeeId, new ShipmentFilter { ExactId = id });
                if (rows.Count == 0)
                    throw new InvalidOperationException("対象の出荷は存在しないか、非表示になっています。検索結果を更新してください。");
                BindRows(rows, id);
                _initialized = true;
                txtMessage.Text = $"出荷ID：{id} をDBから再取得しました。「一覧表示」で全件に戻れます。";
            });
        }

        private void BindRows(List<ShipmentRow> rows, int? selectedId)
        {
            _changingSelection = true;
            try
            {
                gridShipments.ItemsSource = rows;
                gridShipments.SelectedItem = rows.FirstOrDefault(x => x.Id == selectedId) ?? rows.FirstOrDefault();
            }
            finally { _changingSelection = false; }
            UpdateDetails();
        }

        private void UpdateDetails()
        {
            if (txtHideReason != null) txtHideReason.Clear();
            UpdateActions();
            if (gridShipments.SelectedItem is ShipmentRow row)
            {
                gridDetails.ItemsSource = row.Details;
                txtSelected.Text = $"出荷ID：{row.Id}　受注ID：{row.OrderId}　顧客：{row.Client}　営業所：{row.Office}　担当者：{row.Employee}　状態：{row.State}";
                txtDetailTotal.Text = row.Total.HasValue ? $"合計：{row.Total.Value:N0} 円" : "合計：—（受注明細との照合が必要）";
            }
            else
            {
                gridDetails.ItemsSource = null;
                txtSelected.Text = "出荷を選択すると、ここに選択内容を表示します。";
                txtDetailTotal.Text = "合計：—";
            }
        }

        private void Shipments_SelectionChanged(object sender, SelectionChangedEventArgs e)
        { if (!_changingSelection) UpdateDetails(); }
        private async void List_Click(object sender, RoutedEventArgs e) => await ReloadAsync();
        private void ShowSearch_Click(object sender, RoutedEventArgs e)
        { if (!IsBusy) SearchRequested?.Invoke(this, EventArgs.Empty); }

        private void UpdateActions()
        {
            bool pending = !IsBusy && gridShipments.SelectedItem is ShipmentRow row && row.RawState == 0;
            if (btnConfirm != null) btnConfirm.IsEnabled = pending;
            if (btnHide != null) btnHide.IsEnabled = pending;
        }

        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (IsBusy || gridShipments.SelectedItem is not ShipmentRow row || row.RawState != 0) return;
            int id = row.Id;
            if (MessageBox.Show($"出荷ID：{id} を確定しますか？\n顧客への引き渡し完了を確認してください。売上伝票を作成します。在庫数量は変更しません。",
                "出荷確定", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await RunAsync(async () =>
            {
                int saleId;
                using (var db = new SalesManagementContext())
                {
                    await CheckPermissionAsync(db, _loginEmployeeId);
                    using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                    var shipment = await db.TShipments.Include(x => x.TShipmentDetails)
                        .SingleOrDefaultAsync(x => x.ShId == id);
                    if (shipment == null || shipment.ShFlag != 0 || shipment.ShStateFlag != 0)
                        throw new InvalidOperationException("対象は更新済み、非表示、または未確定ではありません。一覧を更新してください。");
                    if (shipment.TShipmentDetails.Count == 0 || shipment.TShipmentDetails.Any(d => d.PrId <= 0 || d.ShQuantity <= 0))
                        throw new InvalidOperationException("商品または数量が不正な出荷明細があります。元データを確認してください。");
                    // Sale has no Shipment FK: one sale per received order in this workflow.
                    // Include hidden records so hiding cannot cause duplicate sales.
                    if (await db.TSales.AnyAsync(x => x.OrId == shipment.OrId))
                        throw new InvalidOperationException("この受注の売上伝票は既に存在します。重複作成せずに中止しました。");

                    // Use the order's agreed amounts, never the current product price.
                    var orderLines = await db.TOrderDetails.AsNoTracking()
                        .Where(x => x.OrId == shipment.OrId)
                        .Select(x => new { x.PrId, x.OrQuantity, x.OrTotalPrice }).ToListAsync();
                    if (orderLines.Count == 0 || orderLines.Any(x => x.OrQuantity <= 0 || x.OrTotalPrice < 0))
                        throw new InvalidOperationException("受注明細の数量または金額が不正です。元データを確認してください。");
                    var ordered = orderLines.GroupBy(x => x.PrId).ToDictionary(g => g.Key, g => new
                    {
                        Quantity = g.Sum(x => (long)x.OrQuantity),
                        Amount = g.Sum(x => x.OrTotalPrice)
                    });
                    var delivered = shipment.TShipmentDetails.GroupBy(x => x.PrId)
                        .ToDictionary(g => g.Key, g => g.Sum(x => (long)x.ShQuantity));
                    if (ordered.Count != delivered.Count || delivered.Any(x =>
                        !ordered.TryGetValue(x.Key, out var order) || order.Quantity != x.Value))
                        throw new InvalidOperationException("受注と出荷の商品・数量が一致しません。売上を作成せずに中止しました。");

                    var sale = new TSale
                    {
                        OrId = shipment.OrId, ClId = shipment.ClId, SoId = shipment.SoId,
                        EmId = _loginEmployeeId, SaDate = DateTime.Today, SaFlag = 0, SaHidden = null
                    };
                    foreach (var item in delivered)
                    {
                        decimal amount = ordered[item.Key].Amount;
                        if (item.Value > int.MaxValue || amount > 9999999999m || decimal.Truncate(amount) != amount)
                            throw new InvalidOperationException("売上数量または金額がDBの保存範囲を超えています。元データを確認してください。");
                        sale.TSaleDetails.Add(new TSaleDetail
                        {
                            PrId = item.Key, SaQuantity = checked((int)item.Value), SaPrTotalPrice = amount
                        });
                    }
                    db.TSales.Add(sale);
                    shipment.ShFinishDate = DateTime.Today;
                    shipment.EmId = _loginEmployeeId;
                    shipment.ShStateFlag = 1;
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                    saleId = sale.SaId;
                }
                await RefreshAfterSaveAsync(id, $"出荷ID：{id} を確定しました。売上ID：{saleId} を作成しました。売上管理で確認してください。");
            });
        }

        private async void Hide_Click(object sender, RoutedEventArgs e)
        {
            if (IsBusy || gridShipments.SelectedItem is not ShipmentRow row || row.RawState != 0) return;
            string reason = txtHideReason.Text.Trim();
            if (reason.Length == 0)
            { txtMessage.Text = "非表示理由を入力してください。"; txtHideReason.Focus(); return; }
            int id = row.Id;
            if (MessageBox.Show($"出荷ID：{id} を非表示にしますか？\n非表示は受注の取消ではありません。在庫は変更しません。",
                "非表示", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await RunAsync(async () =>
            {
                using (var db = new SalesManagementContext())
                {
                    await CheckPermissionAsync(db, _loginEmployeeId);
                    using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                    var shipment = await db.TShipments.SingleOrDefaultAsync(x => x.ShId == id);
                    if (shipment == null || shipment.ShFlag != 0 || shipment.ShStateFlag != 0)
                        throw new InvalidOperationException("対象は更新済みです。一覧を更新してください。");
                    if (await db.TSales.AnyAsync(x => x.OrId == shipment.OrId))
                        throw new InvalidOperationException("既に売上伝票が存在するため、非表示にできません。");
                    shipment.ShFlag = 1;
                    shipment.ShHidden = reason;
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                await RefreshAfterSaveAsync(null, $"出荷ID：{id} を非表示にしました。");
            });
        }

        private async Task RefreshAfterSaveAsync(int? selectedId, string success)
        {
            // A failed refresh must not report a committed write as failed.
            gridShipments.ItemsSource = null;
            UpdateDetails();
            try
            {
                using var db = new SalesManagementContext();
                BindRows(await ReadRowsAsync(db, _loginEmployeeId, new ShipmentFilter()), selectedId);
                txtMessage.Text = success;
            }
            catch (Exception ex)
            {
                txtMessage.Text = success + "\n保存は完了しています。一覧の再表示に失敗しました。「一覧表示」で更新してください。\n" + ReadError(ex);
            }
        }

        internal static async Task CheckPermissionAsync(SalesManagementContext db, int loginId)
        {
            if (loginId <= 0)
                throw new InvalidOperationException("メニューからログイン社員IDを渡して出荷管理を開いてください。");
            bool allowed = await db.MEmployees.AsNoTracking().AnyAsync(x => x.EmId == loginId && x.EmFlag == 0 &&
                (x.Po.PoName.Trim() == "管理者" || x.Po.PoName.Trim() == "営業" || x.Po.PoName.Trim() == "営業担当"));
            if (!allowed)
                throw new InvalidOperationException("出荷管理を利用する権限がありません。再ログインしてください。");
        }

        internal static async Task<List<ShipmentRow>> ReadRowsAsync(SalesManagementContext db, int loginId, ShipmentFilter filter)
        {
            filter.Validate();
            await CheckPermissionAsync(db, loginId);
            var query = db.TShipments.AsNoTracking().Where(x => x.ShFlag == 0);
            if (filter.ExactId.HasValue) query = query.Where(x => x.ShId == filter.ExactId.Value);
            if (filter.IdPrefix.Length > 0) query = query.Where(x => x.ShId.ToString().StartsWith(filter.IdPrefix));
            if (filter.OrderIdPrefix.Length > 0) query = query.Where(x => x.OrId.ToString().StartsWith(filter.OrderIdPrefix));
            if (filter.ClientId.HasValue) query = query.Where(x => x.ClId == filter.ClientId.Value);
            if (filter.OfficeId.HasValue) query = query.Where(x => x.SoId == filter.OfficeId.Value);
            if (filter.State == -1) query = query.Where(x => x.ShStateFlag == null);
            else if (filter.State.HasValue) query = query.Where(x => x.ShStateFlag == filter.State.Value);
            if (filter.From.HasValue) query = query.Where(x => x.ShFinishDate >= filter.From.Value.Date);
            if (filter.To.HasValue) query = query.Where(x => x.ShFinishDate <= filter.To.Value.Date);

            var headers = await query.Include(x => x.TShipmentDetails).AsSingleQuery()
                .OrderByDescending(x => x.ShId).ToListAsync();
            if (headers.Count == 0) return new List<ShipmentRow>();
            var clients = await db.MClients.AsNoTracking().Where(c => query.Any(h => h.ClId == c.ClId))
                .Select(c => new { c.ClId, c.ClName })
                .ToDictionaryAsync(c => c.ClId, c => c.ClName);
            var offices = await db.MSalesOffices.AsNoTracking().Where(s => query.Any(h => h.SoId == s.SoId))
                .Select(s => new { s.SoId, s.SoName })
                .ToDictionaryAsync(s => s.SoId, s => s.SoName);
            var employees = await db.MEmployees.AsNoTracking().Where(em => query.Any(h => h.EmId == em.EmId))
                .Select(em => new { em.EmId, em.EmName })
                .ToDictionaryAsync(em => em.EmId, em => em.EmName);
            var products = await db.MProducts.AsNoTracking()
                .Where(p => query.Any(h => h.TShipmentDetails.Any(d => d.PrId == p.PrId)))
                .Select(p => new { p.PrId, p.PrName })
                .ToDictionaryAsync(p => p.PrId, p => p.PrName);
            
            var orderLines = await db.TOrderDetails.AsNoTracking().Where(d => query.Any(h => h.OrId == d.OrId))
                .Select(d => new { d.OrId, d.PrId, d.OrQuantity, d.OrTotalPrice }).ToListAsync();
            var prices = orderLines.GroupBy(d => new { d.OrId, d.PrId })
                .ToDictionary(g => (g.Key.OrId, g.Key.PrId), g => new
                {
                    Quantity = g.Sum(d => (long)d.OrQuantity),
                    Amount = g.Sum(d => d.OrTotalPrice),
                    Valid = g.All(d => d.OrQuantity > 0)
                });
            var result = new List<ShipmentRow>();
            foreach (var header in headers)
            {
                var row = new ShipmentRow
                {
                    Id = header.ShId, OrderId = header.OrId, Date = header.ShFinishDate, RawState = header.ShStateFlag,
                    Client = clients.TryGetValue(header.ClId, out var client) ? client : $"（顧客未登録：{header.ClId}）",
                    Office = offices.TryGetValue(header.SoId, out var office) ? office : $"（営業所未登録：{header.SoId}）",
                    Employee = !header.EmId.HasValue ? "未担当" :
                        employees.TryGetValue(header.EmId.Value, out var employee) ? employee : $"（社員未登録：{header.EmId}）"
                };
                foreach (var group in header.TShipmentDetails.GroupBy(d => d.PrId).OrderBy(g => g.Key))
                {
                    long quantity = group.Sum(d => (long)d.ShQuantity);
                    decimal? amount = prices.TryGetValue((header.OrId, group.Key), out var price) &&
                        price.Valid && quantity > 0 && price.Quantity == quantity ? price.Amount : (decimal?)null;
                    row.Details.Add(new ShipmentLine
                    {
                        ProductId = group.Key, Quantity = quantity, Total = amount,
                        ProductName = products.TryGetValue(group.Key, out var product) ? product : $"（商品未登録：{group.Key}）"
                    });
                }
                result.Add(row);
            }
            return result;
        }

        internal static string ConnectionLabel()
        {
            try
            {
                using var db = new SalesManagementContext();
                var c = db.Database.GetDbConnection();
                return $"\n接続先：{c.DataSource} ／ DB：{c.Database}";
            }
            catch { return ""; }
        }
        internal static string ReadError(Exception error)
        {
            var cause = error.GetBaseException();
            string message = System.Text.RegularExpressions.Regex.Replace(cause.Message,
                @"(?i)(password|pwd)\s*=\s*(?:""[^""]*""|'[^']*'|[^;\r\n]*)", "$1=***");
            if (message.Length > 1200) message = message.Substring(0, 1200);
            return "出荷処理に失敗しました。" + ConnectionLabel() + "\n" + message;
        }

        public sealed class ShipmentRow
        {
            public int Id { get; set; }
            public int OrderId { get; set; }
            public DateTime? Date { get; set; }
            public string Client { get; set; } = "";
            public string Office { get; set; } = "";
            public string Employee { get; set; } = "";
            public int? RawState { get; set; }
            public string State => RawState switch { 0 => "未出荷", 1 => "出荷済", null => "未設定", _ => "不明" };
            public List<ShipmentLine> Details { get; set; } = new();
            public decimal? Total => Details.Count > 0 && Details.All(d => d.Total.HasValue)
                ? Details.Sum(d => d.Total.GetValueOrDefault()) : (decimal?)null;
        }
        public sealed class ShipmentLine
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; } = "";
            public long Quantity { get; set; }
            public decimal? Total { get; set; }
        }
        public sealed class ShipmentFilter
        {
            public int? ExactId { get; set; }
            public string IdPrefix { get; set; } = "";
            public string OrderIdPrefix { get; set; } = "";
            public int? ClientId { get; set; }
            public int? OfficeId { get; set; }
            public int? State { get; set; }
            public DateTime? From { get; set; }
            public DateTime? To { get; set; }
            public void Validate()
            {
                if (IdPrefix.Any(c => c < '0' || c > '9') || OrderIdPrefix.Any(c => c < '0' || c > '9'))
                    throw new InvalidOperationException("IDは半角数字で入力してください。");
                if (State.HasValue && State != -1 && State != 0 && State != 1)
                    throw new InvalidOperationException("状態を選択し直してください。");
                if (From.HasValue && To.HasValue && From.Value.Date > To.Value.Date)
                    throw new InvalidOperationException("開始日は終了日以前にしてください。");
            }
        }
    }
}
