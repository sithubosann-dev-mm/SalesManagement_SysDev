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
    public partial class SaleView : UserControl
    {
        private readonly int _loginEmployeeId;
        private bool _initialized, _changingSelection;
        public event EventHandler? SearchRequested;
        public bool IsBusy { get; private set; }
        public string DisplayLoadError { get; private set; } = "";

        public SaleView() : this(0) { }
        public SaleView(int loginEmployeeId)
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
            txtMessage.Text = "売上情報を読み込んでいます…";
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
            int? selectedId = (gridSales.SelectedItem as SaleRow)?.Id;
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                var rows = await ReadRowsAsync(db, _loginEmployeeId, new SaleFilter());
                BindRows(rows, selectedId);
                _initialized = true;
                txtMessage.Text = rows.Count == 0
                    ? "表示対象の売上は0件です。出荷管理で出荷確定が完了しているか確認してください。" + ConnectionLabel()
                    : $"{rows.Count}件の売上をDBから取得しました。出荷確定で作成された売上と商品明細を確認できます。";
            });
        }

        public async Task<bool> OpenSaleAsync(int id)
        {
            return await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                var rows = await ReadRowsAsync(db, _loginEmployeeId, new SaleFilter { ExactId = id });
                if (rows.Count == 0)
                    throw new InvalidOperationException("対象の売上は存在しないか、非表示になっています。検索結果を更新してください。");
                BindRows(rows, id);
                _initialized = true;
                txtMessage.Text = $"売上ID：{id} をDBから再取得しました。「一覧表示」で全件に戻れます。";
            });
        }

        private void BindRows(List<SaleRow> rows, int? selectedId)
        {
            _changingSelection = true;
            try
            {
                gridSales.ItemsSource = rows;
                gridSales.SelectedItem = rows.FirstOrDefault(x => x.Id == selectedId) ?? rows.FirstOrDefault();
            }
            finally { _changingSelection = false; }
            UpdateDetails();
        }

        private void UpdateDetails()
        {
            if (txtHideReason != null) txtHideReason.Clear();
            UpdateActions();
            if (gridSales.SelectedItem is SaleRow row)
            {
                gridDetails.ItemsSource = row.Details;
                txtSelected.Text = $"売上ID：{row.Id}　受注ID：{row.OrderId}　顧客：{row.Client}　営業所：{row.Office}　担当者：{row.Employee}";
                txtDetailTotal.Text = row.Total.HasValue ? $"合計：{row.Total.Value:N0} 円" : "合計：—（売上明細がありません）";
            }
            else
            {
                gridDetails.ItemsSource = null;
                txtSelected.Text = "売上を選択すると、ここに選択内容を表示します。";
                txtDetailTotal.Text = "合計：—";
            }
        }

        private void Sales_SelectionChanged(object sender, SelectionChangedEventArgs e)
        { if (!_changingSelection) UpdateDetails(); }
        private async void List_Click(object sender, RoutedEventArgs e) => await ReloadAsync();
        private void ShowSearch_Click(object sender, RoutedEventArgs e)
        { if (!IsBusy) SearchRequested?.Invoke(this, EventArgs.Empty); }

        private void UpdateActions()
        {
            bool selected = !IsBusy && gridSales.SelectedItem is SaleRow;
            if (btnHide != null) btnHide.IsEnabled = selected;
        }

        private async void Hide_Click(object sender, RoutedEventArgs e)
        {
            if (IsBusy || gridSales.SelectedItem is not SaleRow row) return;
            string reason = txtHideReason.Text.Trim();
            if (reason.Length == 0)
            { txtMessage.Text = "非表示理由を入力してください。"; txtHideReason.Focus(); return; }
            int id = row.Id;
            if (MessageBox.Show($"売上ID：{id} を非表示にしますか？\n表示一覧から隠します。売上の取消・返金ではありません。売上金額と在庫は変更しません。",
                "非表示", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await RunAsync(async () =>
            {
                using (var db = new SalesManagementContext())
                {
                    await CheckPermissionAsync(db, _loginEmployeeId);
                    using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                    var sale = await db.TSales.SingleOrDefaultAsync(x => x.SaId == id);
                    if (sale == null || sale.SaFlag != 0)
                        throw new InvalidOperationException("対象は更新済みです。一覧を更新してください。");
                    sale.SaFlag = 1;
                    sale.SaHidden = reason;
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                }
                await RefreshAfterSaveAsync(null, $"売上ID：{id} を非表示にしました。");
            });
        }

        private async Task RefreshAfterSaveAsync(int? selectedId, string success)
        {
            // A failed refresh must not report a committed write as failed.
            gridSales.ItemsSource = null;
            UpdateDetails();
            try
            {
                using var db = new SalesManagementContext();
                BindRows(await ReadRowsAsync(db, _loginEmployeeId, new SaleFilter()), selectedId);
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
                throw new InvalidOperationException("メニューからログイン社員IDを渡して売上管理を開いてください。");
            bool allowed = await db.MEmployees.AsNoTracking().AnyAsync(x => x.EmId == loginId && x.EmFlag == 0 &&
                (x.Po.PoName.Trim() == "管理者" || x.Po.PoName.Trim() == "営業" || x.Po.PoName.Trim() == "営業担当"));
            if (!allowed)
                throw new InvalidOperationException("売上管理を利用する権限がありません。再ログインしてください。");
        }

        internal static async Task<List<SaleRow>> ReadRowsAsync(SalesManagementContext db, int loginId, SaleFilter filter)
        {
            filter.Validate();
            await CheckPermissionAsync(db, loginId);
            var query = db.TSales.AsNoTracking().Where(x => x.SaFlag == 0);
            if (filter.ExactId.HasValue) query = query.Where(x => x.SaId == filter.ExactId.Value);
            if (filter.IdPrefix.Length > 0) query = query.Where(x => x.SaId.ToString().StartsWith(filter.IdPrefix));
            if (filter.OrderIdPrefix.Length > 0) query = query.Where(x => x.OrId.ToString().StartsWith(filter.OrderIdPrefix));
            if (filter.ClientId.HasValue) query = query.Where(x => x.ClId == filter.ClientId.Value);
            if (filter.OfficeId.HasValue) query = query.Where(x => x.SoId == filter.OfficeId.Value);
            if (filter.From.HasValue) query = query.Where(x => x.SaDate >= filter.From.Value.Date);
            if (filter.To.HasValue) query = query.Where(x => x.SaDate <= filter.To.Value.Date);

            var headers = await query.Include(x => x.TSaleDetails).AsSingleQuery()
                .OrderByDescending(x => x.SaId).ToListAsync();
            if (headers.Count == 0) return new List<SaleRow>();
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
                .Where(p => query.Any(h => h.TSaleDetails.Any(d => d.PrId == p.PrId)))
                .Select(p => new { p.PrId, p.PrName })
                .ToDictionaryAsync(p => p.PrId, p => p.PrName);
            
            var result = new List<SaleRow>();
            foreach (var header in headers)
            {
                var row = new SaleRow
                {
                    Id = header.SaId, OrderId = header.OrId, Date = header.SaDate,
                    Client = clients.TryGetValue(header.ClId, out var client) ? client : $"（顧客未登録：{header.ClId}）",
                    Office = offices.TryGetValue(header.SoId, out var office) ? office : $"（営業所未登録：{header.SoId}）",
                    Employee = employees.TryGetValue(header.EmId, out var employee) ? employee : $"（社員未登録：{header.EmId}）"
                };
                foreach (var group in header.TSaleDetails.GroupBy(d => d.PrId).OrderBy(g => g.Key))
                {
                    long quantity = group.Sum(d => (long)d.SaQuantity);
                    decimal amount = group.Sum(d => d.SaPrTotalPrice);
                    row.Details.Add(new SaleLine
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
            return "売上処理に失敗しました。" + ConnectionLabel() + "\n" + message;
        }

        public sealed class SaleRow
        {
            public int Id { get; set; }
            public int OrderId { get; set; }
            public DateTime? Date { get; set; }
            public string Client { get; set; } = "";
            public string Office { get; set; } = "";
            public string Employee { get; set; } = "";
            public string State => "計上済";
            public List<SaleLine> Details { get; set; } = new();
            public decimal? Total => Details.Count > 0 && Details.All(d => d.Total.HasValue)
                ? Details.Sum(d => d.Total.GetValueOrDefault()) : (decimal?)null;
        }
        public sealed class SaleLine
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; } = "";
            public long Quantity { get; set; }
            public decimal? Total { get; set; }
        }
        public sealed class SaleFilter
        {
            public int? ExactId { get; set; }
            public string IdPrefix { get; set; } = "";
            public string OrderIdPrefix { get; set; } = "";
            public int? ClientId { get; set; }
            public int? OfficeId { get; set; }
            public DateTime? From { get; set; }
            public DateTime? To { get; set; }
            public void Validate()
            {
                if (IdPrefix.Any(c => c < '0' || c > '9') || OrderIdPrefix.Any(c => c < '0' || c > '9'))
                    throw new InvalidOperationException("IDは半角数字で入力してください。");
                if (From.HasValue && To.HasValue && From.Value.Date > To.Value.Date)
                    throw new InvalidOperationException("開始日は終了日以前にしてください。");
            }
        }
    }
}
