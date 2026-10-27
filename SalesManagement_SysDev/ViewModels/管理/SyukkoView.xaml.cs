using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;

namespace SalesManagement_SysDev
{
    public partial class SyukkoView : UserControl
    {
        private readonly int _loginEmployeeId;
        private bool _initialized, _changingSelection;
        public event EventHandler? SearchRequested;
        public bool IsBusy { get; private set; }
        public string DisplayLoadError { get; private set; } = "";

        public SyukkoView() : this(0) { }
        public SyukkoView(int loginEmployeeId)
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
            txtMessage.Text = "出庫情報を読み込んでいます…";
            try { await action(); return true; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                DisplayLoadError = ReadError(ex);
                txtMessage.Text = DisplayLoadError;
                return false;
            }
            finally { IsBusy = false; mainPanel.IsEnabled = true; }
        }

        private async void View_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized || DesignerProperties.GetIsInDesignMode(this)) return;
            await ReloadAsync();
        }

        private async Task ReloadAsync()
        {
            int? selectedId = (gridSyukkos.SelectedItem as SyukkoRow)?.Id;
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                var rows = await ReadRowsAsync(db, _loginEmployeeId, new SyukkoFilter());
                BindRows(rows, selectedId);
                _initialized = true;
                txtMessage.Text = rows.Count == 0
                    ? "表示対象の出庫は0件です。注文管理で注文確定が完了しているか、同じDBに接続しているか確認してください。" + ConnectionLabel()
                    : $"{rows.Count}件の出庫をDBから取得しました。注文確定で作成された出庫と商品明細を確認できます。";
            });
        }

        public async Task<bool> OpenSyukkoAsync(int id)
        {
            return await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                var rows = await ReadRowsAsync(db, _loginEmployeeId, new SyukkoFilter { ExactId = id });
                if (rows.Count == 0)
                    throw new InvalidOperationException("対象の出庫は存在しないか、非表示になっています。検索結果を更新してください。");
                BindRows(rows, id);
                _initialized = true;
                txtMessage.Text = $"出庫ID：{id} をDBから再取得しました。「一覧表示」で全件に戻れます。";
            });
        }

        private void BindRows(List<SyukkoRow> rows, int? selectedId)
        {
            _changingSelection = true;
            try
            {
                gridSyukkos.ItemsSource = rows;
                gridSyukkos.SelectedItem = rows.FirstOrDefault(x => x.Id == selectedId) ?? rows.FirstOrDefault();
            }
            finally { _changingSelection = false; }
            UpdateDetails();
        }

        private void UpdateDetails()
        {
            if (gridSyukkos.SelectedItem is SyukkoRow row)
            {
                gridDetails.ItemsSource = row.Details;
                txtSelected.Text = $"出庫ID：{row.Id}　受注ID：{row.OrderId}　顧客：{row.Client}　営業所：{row.Office}　担当者：{row.Employee}　状態：{row.State}";
                txtDetailTotal.Text = row.Total.HasValue ? $"合計：{row.Total.Value:N0} 円" : "合計：—（受注明細との照合が必要）";
            }
            else
            {
                gridDetails.ItemsSource = null;
                txtSelected.Text = "出庫を選択すると、ここに選択内容を表示します。";
                txtDetailTotal.Text = "合計：—";
            }
        }

        private void Syukkos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        { if (!_changingSelection) UpdateDetails(); }
        private async void List_Click(object sender, RoutedEventArgs e) => await ReloadAsync();
        private void ShowSearch_Click(object sender, RoutedEventArgs e)
        { if (!IsBusy) SearchRequested?.Invoke(this, EventArgs.Empty); }

        internal static async Task CheckPermissionAsync(SalesManagementContext db, int loginId)
        {
            if (loginId <= 0)
                throw new InvalidOperationException("メニューからログイン社員IDを渡して出庫管理を開いてください。");
            bool allowed = await db.MEmployees.AsNoTracking().AnyAsync(x => x.EmId == loginId && x.EmFlag == 0 &&
                (x.Po.PoName.Trim() == "管理者" || x.Po.PoName.Trim() == "物流" || x.Po.PoName.Trim() == "物流担当"));
            if (!allowed)
                throw new InvalidOperationException("出庫管理を利用する権限がありません。再ログインしてください。");
        }

        internal static async Task<List<SyukkoRow>> ReadRowsAsync(SalesManagementContext db, int loginId, SyukkoFilter filter)
        {
            filter.Validate();
            await CheckPermissionAsync(db, loginId);
            var query = db.TSyukkos.AsNoTracking().Where(x => x.SyFlag == 0);
            if (filter.ExactId.HasValue) query = query.Where(x => x.SyId == filter.ExactId.Value);
            if (filter.IdPrefix.Length > 0) query = query.Where(x => x.SyId.ToString().StartsWith(filter.IdPrefix));
            if (filter.OrderIdPrefix.Length > 0) query = query.Where(x => x.OrId.ToString().StartsWith(filter.OrderIdPrefix));
            if (filter.ClientId.HasValue) query = query.Where(x => x.ClId == filter.ClientId.Value);
            if (filter.OfficeId.HasValue) query = query.Where(x => x.SoId == filter.OfficeId.Value);
            if (filter.State == -1) query = query.Where(x => x.SyStateFlag == null);
            else if (filter.State.HasValue) query = query.Where(x => x.SyStateFlag == filter.State.Value);
            if (filter.From.HasValue) query = query.Where(x => x.SyDate >= filter.From.Value.Date);
            if (filter.To.HasValue) query = query.Where(x => x.SyDate <= filter.To.Value.Date);

            var headers = await query.Include(x => x.TSyukkoDetails).AsSingleQuery()
                .OrderByDescending(x => x.SyId).ToListAsync();
            if (headers.Count == 0) return new List<SyukkoRow>();
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
                .Where(p => query.Any(h => h.TSyukkoDetails.Any(d => d.PrId == p.PrId)))
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
            var result = new List<SyukkoRow>();
            foreach (var header in headers)
            {
                var row = new SyukkoRow
                {
                    Id = header.SyId, OrderId = header.OrId, Date = header.SyDate, RawState = header.SyStateFlag,
                    Client = clients.TryGetValue(header.ClId, out var client) ? client : $"（顧客未登録：{header.ClId}）",
                    Office = offices.TryGetValue(header.SoId, out var office) ? office : $"（営業所未登録：{header.SoId}）",
                    Employee = !header.EmId.HasValue ? "未担当" :
                        employees.TryGetValue(header.EmId.Value, out var employee) ? employee : $"（社員未登録：{header.EmId}）"
                };
                foreach (var group in header.TSyukkoDetails.GroupBy(d => d.PrId).OrderBy(g => g.Key))
                {
                    long quantity = group.Sum(d => (long)d.SyQuantity);
                    decimal? amount = prices.TryGetValue((header.OrId, group.Key), out var price) &&
                        price.Valid && quantity > 0 && price.Quantity == quantity ? price.Amount : (decimal?)null;
                    row.Details.Add(new SyukkoLine
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
            return "出庫情報を取得できませんでした。" + ConnectionLabel() + "\n" + message;
        }

        public sealed class SyukkoRow
        {
            public int Id { get; set; }
            public int OrderId { get; set; }
            public DateTime? Date { get; set; }
            public string Client { get; set; } = "";
            public string Office { get; set; } = "";
            public string Employee { get; set; } = "";
            public int? RawState { get; set; }
            public string State => RawState switch { 0 => "未出庫", 1 => "出庫済", null => "未設定", _ => "不明" };
            public List<SyukkoLine> Details { get; set; } = new();
            public decimal? Total => Details.Count > 0 && Details.All(d => d.Total.HasValue)
                ? Details.Sum(d => d.Total.GetValueOrDefault()) : (decimal?)null;
        }
        public sealed class SyukkoLine
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; } = "";
            public long Quantity { get; set; }
            public decimal? Total { get; set; }
        }
        public sealed class SyukkoFilter
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
