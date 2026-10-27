using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;

namespace SalesManagement_SysDev
{
    public partial class ChumonView : UserControl
    {
        private readonly int _loginEmployeeId;
        private bool _initialized, _changingSelection;
        public event EventHandler? SearchRequested;
        public event Func<int, Task>? SyukkoRequested;
        public bool IsBusy { get; private set; }
        public string DisplayLoadError { get; private set; } = "";

        public ChumonView() : this(0) { }
        public ChumonView(int loginEmployeeId)
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

        private async Task<bool> RunAsync(Func<Task> action, string progress = "注文情報を読み込んでいます…")
        {
            if (IsBusy) return false;
            IsBusy = true;
            mainPanel.IsEnabled = false;
            DisplayLoadError = "";
            txtMessage.Text = progress;
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
            int? selectedId = (gridChumons.SelectedItem as ChumonRow)?.Id;
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                var rows = await ReadRowsAsync(db, _loginEmployeeId, new ChumonFilter());
                BindRows(rows, selectedId);
                _initialized = true;
                txtMessage.Text = rows.Count == 0
                    ? "表示対象の注文は0件です。受注管理で受注確定が完了しているか、同じDBに接続しているか確認してください。" + ConnectionLabel()
                    : $"{rows.Count}件の注文をDBから取得しました。受注確定で作成された注文と商品明細を確認できます。";
            });
        }

        public async Task<bool> OpenChumonAsync(int id)
        {
            return await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                var rows = await ReadRowsAsync(db, _loginEmployeeId, new ChumonFilter { ExactId = id });
                if (rows.Count == 0)
                    throw new InvalidOperationException("対象の注文は存在しないか、非表示になっています。検索結果を更新してください。");
                BindRows(rows, id);
                _initialized = true;
                txtMessage.Text = $"注文ID：{id} をDBから再取得しました。「一覧表示」で全件に戻れます。";
            });
        }

        private void BindRows(List<ChumonRow> rows, int? selectedId)
        {
            _changingSelection = true;
            try
            {
                gridChumons.ItemsSource = rows;
                gridChumons.SelectedItem = rows.FirstOrDefault(x => x.Id == selectedId) ?? rows.FirstOrDefault();
            }
            finally { _changingSelection = false; }
            UpdateDetails();
        }

        private void UpdateDetails()
        {
            btnConfirm.IsEnabled = (gridChumons.SelectedItem as ChumonRow)?.RawState == 0;
            if (gridChumons.SelectedItem is ChumonRow row)
            {
                gridDetails.ItemsSource = row.Details;
                txtSelected.Text = $"注文ID：{row.Id}　受注ID：{row.OrderId}　顧客：{row.Client}　営業所：{row.Office}　担当者：{row.Employee}　状態：{row.State}";
                txtDetailTotal.Text = row.Total.HasValue ? $"合計：{row.Total.Value:N0} 円" : "合計：—（受注明細との照合が必要）";
            }
            else
            {
                gridDetails.ItemsSource = null;
                txtSelected.Text = "注文を選択すると、ここに選択内容を表示します。";
                txtDetailTotal.Text = "合計：—";
            }
        }

        private void Chumons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        { if (!_changingSelection) UpdateDetails(); }
        private async void List_Click(object sender, RoutedEventArgs e) => await ReloadAsync();
        private void ShowSearch_Click(object sender, RoutedEventArgs e)
        { if (!IsBusy) SearchRequested?.Invoke(this, EventArgs.Empty); }

        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (IsBusy) return;
            if (gridChumons.SelectedItem is not ChumonRow row || row.RawState != 0)
            { txtMessage.Text = "未確定（状態0）の注文を選択してください。"; return; }
            if (MessageBox.Show($"注文ID：{row.Id}（受注ID：{row.OrderId}）を確定しますか？\n全明細の数量を在庫から減らし、出庫伝票を作成します。",
                "注文確定", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            int? createdId = null;
            bool completed = await RunAsync(async () =>
            {
                createdId = await ConfirmChumonAsync(_loginEmployeeId, row.Id, row.Stamp);
                string saved = $"注文ID：{row.Id} を確定しました。出庫ID：{createdId} を作成し、在庫数を減算しました。";
                try
                {
                    using var db = new SalesManagementContext();
                    var rows = await ReadRowsAsync(db, _loginEmployeeId, new ChumonFilter());
                    BindRows(rows, row.Id);
                    _initialized = true;
                    txtMessage.Text = saved;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                    BindRows(new List<ChumonRow>(), null);
                    txtMessage.Text = saved + "\n一覧の再取得に失敗しました。「一覧表示」で更新してください。";
                }
            }, "注文を確定しています…");
            if (!completed)
            {
                txtMessage.Text += "\n再操作の前に「一覧表示」で注文状態と出庫伝票を確認してください。";
                return;
            }
            var handler = SyukkoRequested;
            if (createdId.HasValue && handler != null)
            {
                try { await handler(createdId.Value); }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                    txtMessage.Text = $"注文確定は完了しています。出庫ID：{createdId} の画面表示に失敗しました。出庫管理から確認してください。";
                }
            }
        }

        private static string ChumonStamp(TChumon header) => JsonSerializer.Serialize(new
        {
            header.ChId, header.OrId, header.ClId, header.SoId, header.EmId,
            header.ChDate, header.ChStateFlag, header.ChFlag, header.ChHidden,
            Lines = header.TChumonDetails.OrderBy(d => d.ChDetailId)
                .Select(d => new { d.ChDetailId, d.PrId, d.ChQuantity }).ToArray()
        });

        internal static async Task<int> ConfirmChumonAsync(int loginId, int chumonId, string expectedStamp)
        {
            using var db = new SalesManagementContext();
            using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await CheckPermissionAsync(db, loginId);
            var header = await db.TChumons.AsTracking().Include(h => h.TChumonDetails).AsSingleQuery()
                .SingleOrDefaultAsync(h => h.ChId == chumonId && h.ChFlag == 0)
                ?? throw new InvalidOperationException("対象の注文は存在しないか、非表示になっています。");
            if (header.ChStateFlag != 0)
                throw new InvalidOperationException("未確定（状態0）の注文だけ確定できます。確定済みの注文は再確定できません。");
            if (string.IsNullOrEmpty(expectedStamp) || ChumonStamp(header) != expectedStamp)
                throw new InvalidOperationException("注文情報が変更されています。一覧を更新して明細を確認してください。");
            
            if (await db.TSyukkos.AnyAsync(s => s.OrId == header.OrId))
                throw new InvalidOperationException("この受注の出庫伝票は既に存在します。重複作成はできません。");
            if (await db.TChumons.CountAsync(h => h.OrId == header.OrId) != 1)
                throw new InvalidOperationException("同じ受注に複数の注文があります。重複・分納の扱いを確認してください。");
            var order = await db.TOrders.AsNoTracking().SingleOrDefaultAsync(o => o.OrId == header.OrId)
                ?? throw new InvalidOperationException("元の受注が見つかりません。");
            if (order.OrFlag != 0 || order.OrStateFlag != 1 || order.ClId != header.ClId || order.SoId != header.SoId)
                throw new InvalidOperationException("元の受注が非表示・未確定、または顧客・営業所が一致しません。関連情報を確認してください。");
            if (header.TChumonDetails.Count == 0 || header.TChumonDetails.Any(d => d.ChQuantity <= 0))
                throw new InvalidOperationException("注文には1件以上の明細と、1以上の数量が必要です。");
            var required = new Dictionary<int, int>();
            foreach (var group in header.TChumonDetails.GroupBy(d => d.PrId))
            {
                long quantity = group.Sum(d => (long)d.ChQuantity);
                if (quantity > int.MaxValue)
                    throw new InvalidOperationException($"商品ID：{group.Key} の合計数量が上限を超えています。");
                required.Add(group.Key, (int)quantity);
            }
            var original = await db.TOrderDetails.AsNoTracking().Where(d => d.OrId == header.OrId)
                .Select(d => new { d.PrId, d.OrQuantity }).ToListAsync();
            var originalQuantities = original.GroupBy(d => d.PrId)
                .ToDictionary(g => g.Key, g => g.Sum(d => (long)d.OrQuantity));
            if (original.Any(d => d.OrQuantity <= 0) || originalQuantities.Count != required.Count ||
                required.Any(pair => !originalQuantities.TryGetValue(pair.Key, out long q) || q != pair.Value))
                throw new InvalidOperationException("注文と元の受注で商品・数量が一致しません。関連明細を確認してください。");

            var deductions = new List<(TStock Stock, int Quantity)>();
         
            foreach (var pair in required.OrderBy(p => p.Key))
            {
                if (!await db.MProducts.AnyAsync(p => p.PrId == pair.Key && p.PrFlag == 0))
                    throw new InvalidOperationException($"商品ID：{pair.Key} は存在しないか、非表示です。");
                var stocks = await db.TStocks.AsTracking().Where(s => s.PrId == pair.Key).ToListAsync();
                if (stocks.Count != 1)
                    throw new InvalidOperationException($"商品ID：{pair.Key} の在庫レコードが未登録、または複数あります。在庫管理を確認してください。");
                var stock = stocks[0];
                if (stock.StFlag != 0 || stock.StQuantity < 0)
                    throw new InvalidOperationException($"商品ID：{pair.Key} の在庫が非表示、または数量が不正です。");
                if (stock.StQuantity < pair.Value)
                    throw new InvalidOperationException($"在庫が不足しています。商品ID：{pair.Key}、必要数：{pair.Value}、現在庫数：{stock.StQuantity}。注文は確定していません。");
                deductions.Add((stock, pair.Value));
            }
          
            foreach (var deduction in deductions)
                deduction.Stock.StQuantity -= deduction.Quantity;
            var syukko = new TSyukko
            {
                OrId = header.OrId, ClId = header.ClId, SoId = header.SoId,
                EmId = null, SyDate = null, SyStateFlag = 0, SyFlag = 0
            };
            foreach (var detail in header.TChumonDetails.OrderBy(d => d.ChDetailId))
                syukko.TSyukkoDetails.Add(new TSyukkoDetail { PrId = detail.PrId, SyQuantity = detail.ChQuantity });
            db.TSyukkos.Add(syukko);
            header.ChStateFlag = 1;
            header.EmId = loginId;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return syukko.SyId;
        }

        internal static async Task CheckPermissionAsync(SalesManagementContext db, int loginId)
        {
            if (loginId <= 0)
                throw new InvalidOperationException("メニューからログイン社員IDを渡して注文管理を開いてください。");
            bool allowed = await db.MEmployees.AsNoTracking().AnyAsync(x => x.EmId == loginId && x.EmFlag == 0 &&
                (x.Po.PoName.Trim() == "管理者" || x.Po.PoName.Trim() == "営業" || x.Po.PoName.Trim() == "営業担当"));
            if (!allowed)
                throw new InvalidOperationException("注文管理を利用する権限がありません。再ログインしてください。");
        }

        internal static async Task<List<ChumonRow>> ReadRowsAsync(SalesManagementContext db, int loginId, ChumonFilter filter)
        {
            filter.Validate();
            await CheckPermissionAsync(db, loginId);
            var query = db.TChumons.AsNoTracking().Where(x => x.ChFlag == 0);
            if (filter.ExactId.HasValue) query = query.Where(x => x.ChId == filter.ExactId.Value);
            if (filter.IdPrefix.Length > 0) query = query.Where(x => x.ChId.ToString().StartsWith(filter.IdPrefix));
            if (filter.OrderIdPrefix.Length > 0) query = query.Where(x => x.OrId.ToString().StartsWith(filter.OrderIdPrefix));
            if (filter.ClientId.HasValue) query = query.Where(x => x.ClId == filter.ClientId.Value);
            if (filter.OfficeId.HasValue) query = query.Where(x => x.SoId == filter.OfficeId.Value);
            if (filter.State == -1) query = query.Where(x => x.ChStateFlag == null);
            else if (filter.State.HasValue) query = query.Where(x => x.ChStateFlag == filter.State.Value);
            if (filter.From.HasValue) query = query.Where(x => x.ChDate >= filter.From.Value.Date);
            if (filter.To.HasValue) query = query.Where(x => x.ChDate <= filter.To.Value.Date);

            
            var headers = await query.Include(x => x.TChumonDetails).AsSingleQuery()
                .OrderByDescending(x => x.ChId).ToListAsync();
            if (headers.Count == 0) return new List<ChumonRow>();
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
                .Where(p => query.Any(h => h.TChumonDetails.Any(d => d.PrId == p.PrId)))
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
            var result = new List<ChumonRow>();
            foreach (var header in headers)
            {
                var row = new ChumonRow
                {
                    Id = header.ChId, OrderId = header.OrId, Date = header.ChDate, RawState = header.ChStateFlag,
                    Stamp = ChumonStamp(header),
                    Client = clients.TryGetValue(header.ClId, out var client) ? client : $"（顧客未登録：{header.ClId}）",
                    Office = offices.TryGetValue(header.SoId, out var office) ? office : $"（営業所未登録：{header.SoId}）",
                    Employee = !header.EmId.HasValue ? "未担当" :
                        employees.TryGetValue(header.EmId.Value, out var employee) ? employee : $"（社員未登録：{header.EmId}）"
                };
                foreach (var group in header.TChumonDetails.GroupBy(d => d.PrId).OrderBy(g => g.Key))
                {
                    long quantity = group.Sum(d => (long)d.ChQuantity);
                    decimal? amount = prices.TryGetValue((header.OrId, group.Key), out var price) &&
                        price.Valid && quantity > 0 && price.Quantity == quantity ? price.Amount : (decimal?)null;
                    row.Details.Add(new ChumonLine
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
            return "処理を完了できませんでした。" + ConnectionLabel() + "\n" + message;
        }

        public sealed class ChumonRow
        {
            public string Stamp { get; set; } = "";
            public int Id { get; set; }
            public int OrderId { get; set; }
            public DateTime? Date { get; set; }
            public string Client { get; set; } = "";
            public string Office { get; set; } = "";
            public string Employee { get; set; } = "";
            public int? RawState { get; set; }
            public string State => RawState switch { 0 => "未確定", 1 => "確定済", null => "未設定", _ => "不明" };
            public List<ChumonLine> Details { get; set; } = new();
            public decimal? Total => Details.Count > 0 && Details.All(d => d.Total.HasValue)
                ? Details.Sum(d => d.Total.GetValueOrDefault()) : (decimal?)null;
        }
        public sealed class ChumonLine
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; } = "";
            public long Quantity { get; set; }
            public decimal? Total { get; set; }
        }
        public sealed class ChumonFilter
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
