using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SalesManagement_SysDev
{
    public partial class WarehousingView : UserControl
    {
        private readonly int _loginId;
        private bool _initialized, _loading, _restoring;
        private string _baseline = "";
        private WarehousingDisplayRow? _selected;
        public bool IsBusy { get; private set; }
        public WarehousingView() : this(0) { }
        public WarehousingView(int loginEmployeeId)
        {
            InitializeComponent(); _loginId = loginEmployeeId;
        }
        private static bool Ask(string question, string title = "確認") =>
            MessageBox.Show(question, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        public bool CanLeave()
        {
            if (IsBusy) { MessageBox.Show("処理が終わるまでお待ちください。"); return false; }
            return !_initialized || _baseline == FormStamp() || Ask("未保存の入力を破棄しますか？");
        }
        private async Task RunAsync(Func<Task> action)
        {
            if (IsBusy) return;
            IsBusy = true; mainPanel.IsEnabled = false;
            try { await action(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                MessageBox.Show(ErrorMessage(ex), "入庫管理", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally { IsBusy = false; mainPanel.IsEnabled = true; }
        }
        private async void View_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized || DesignerProperties.GetIsInDesignMode(this)) return;
            await RunAsync(EnsureInitializedAsync);
        }
        private async Task EnsureInitializedAsync()
        {
            if (_initialized) return;
            if (_loginId <= 0) throw new InvalidOperationException("メニューからログイン社員IDを渡して画面を開いてください。");
            var lookups = await GetLookupsAsync(_loginId);
            _loading = true;
            try
            {
                cmbMaker.ItemsSource = lookups.Makers; cmbEmployee.ItemsSource = lookups.Employees;
                cmbState.ItemsSource = new List<ReceivingState>
                {
                    new ReceivingState { Value = 0, Name = "未棚入" },
                    new ReceivingState { Value = 1, Name = "棚入済" },
                    new ReceivingState { Value = null, Name = "すべて" },
                    new ReceivingState { Value = -1, Name = "未設定（旧データ）" }
                };
                // 初回は担当者・期間・処理状態で絞らず、非表示以外を表示する。
                ResetInputs(true);
                cmbState.SelectedIndex = 2; // すべて
            }
            finally { _loading = false; }
            await LoadRowsAsync(ReadFilter());
            _initialized = true; _baseline = FormStamp();
        }
        private static DateTime? ReadDate(DatePicker picker, string label)
        {
            if (string.IsNullOrWhiteSpace(picker.Text)) return null;
            if (!picker.SelectedDate.HasValue || !DateTime.TryParse(picker.Text, picker.Language.GetSpecificCulture(), DateTimeStyles.None, out DateTime parsed)
                || parsed.Date != picker.SelectedDate.Value.Date)
                throw new InvalidOperationException(label + "を正しい日付で入力してください。");
            return picker.SelectedDate.Value.Date;
        }
        private ReceivingFilter ReadFilter()
        {
            var f = new ReceivingFilter
            {
                IdPrefix = txtId.Text.Trim(), ProductIdPrefix = txtProductId.Text.Trim(),
                MakerId = cmbMaker.SelectedValue as int?, EmployeeId = cmbEmployee.SelectedValue as int?,
                Quantity = string.IsNullOrWhiteSpace(txtQuantity.Text) ? null : ReceivingFilter.Number(txtQuantity.Text, "検索数量", true),
                State = (cmbState.SelectedItem as ReceivingState)?.Value,
                From = ReadDate(dpFrom, "開始日"), To = ReadDate(dpTo, "終了日"),
                HattyuIdPrefix = txtHattyuId.Text.Trim()
            };
            f.Validate(); return f;
        }
        private string FormStamp() => JsonSerializer.Serialize(new
        {
            Id = txtId.Text, Product = txtProductId.Text, Quantity = txtQuantity.Text, Reason = txtReason.Text,
            Maker = cmbMaker.SelectedValue, Employee = cmbEmployee.SelectedValue, State = (cmbState.SelectedItem as ReceivingState)?.Value,
            From = dpFrom.Text, To = dpTo.Text, Hattyu = txtHattyuId.Text
        });
        private void ResetInputs(bool forList)
        {
            _loading = true;
            try
            {
                _selected = null; _restoring = true;
                try { gridData.SelectedItem = null; } finally { _restoring = false; }
                txtId.Clear(); txtProductId.Clear(); txtQuantity.Clear(); txtReason.Clear();
                txtHattyuId.Clear();
                cmbMaker.SelectedIndex = -1;
                cmbEmployee.SelectedValue = null;
                cmbState.SelectedIndex = 0;
                dpFrom.SelectedDate = null;
                dpTo.SelectedDate = null; _baseline = FormStamp();
            }
            finally { _loading = false; }
        }
        private async Task LoadRowsAsync(ReceivingFilter filter, bool reportEmpty = true)
        {
            var rows = await GetWarehousingsAsync(_loginId, filter);
            _loading = true;
            try { _selected = null; gridData.ItemsSource = rows; gridData.ToolTip = $"表示件数：{rows.Count}明細"; }
            finally { _loading = false; }
            _baseline = FormStamp();
            if (rows.Count == 0 && reportEmpty)
                MessageBox.Show(await EmptyResultMessageAsync(_loginId), "表示データの確認",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private async void List_Click(object sender, RoutedEventArgs e)
        {
            if (!CanLeave()) return;
            await RunAsync(async () => { await EnsureInitializedAsync(); ResetInputs(true); await LoadRowsAsync(new ReceivingFilter { State = 0 }); });
        }
        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            if (!CanLeave()) return;
            await RunAsync(async () => { await EnsureInitializedAsync(); await LoadRowsAsync(ReadFilter()); });
        }
        private void Clear_Click(object sender, RoutedEventArgs e)
        { if (CanLeave()) ResetInputs(false); }
        private void Data_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _restoring || IsBusy || gridData.SelectedItem is not WarehousingDisplayRow row) return;
            if (ReferenceEquals(row, _selected)) return;
            if (!CanLeave())
            {
                _restoring = true; try { gridData.SelectedItem = _selected; } finally { _restoring = false; }
                return;
            }
            _loading = true;
            try
            {
                _selected = row; txtId.Text = row.WaID.ToString();
                txtProductId.Text = row.PrID; txtQuantity.Text = row.WaQuantity.ToString();
                cmbMaker.SelectedValue = row.MakerId; cmbEmployee.SelectedValue = row.EmployeeId;
                cmbState.SelectedValue = row.RawState ?? -1;
                dpFrom.SelectedDate = row.WaDate; dpTo.SelectedDate = null; txtReason.Clear();
                txtHattyuId.Text = row.HaID.ToString();
                _baseline = FormStamp();
            }
            finally { _loading = false; }
        }
        private WarehousingDisplayRow RequireSelected()
        {
            var row = _selected ?? throw new InvalidOperationException("一覧から対象を選択してください。");
            if (txtId.Text.Trim() != row.WaID.ToString())
                throw new InvalidOperationException("入力IDが変更されています。一覧から対象を選択し直してください。");
            return row;
        }
        private async Task AfterWriteAsync(string message)
        {
            ResetInputs(true);
            try { await LoadRowsAsync(new ReceivingFilter { State = 0 }, reportEmpty: false); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex); gridData.ItemsSource = null;
                message += "\n保存は完了しましたが一覧の再取得に失敗しました。一覧表示を押してください。";
            }
            MessageBox.Show(message, "完了", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private async void Hide_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync(async () =>
            {
                var row = RequireSelected(); string reason = txtReason.Text.Trim();
                if (reason.Length == 0 || reason.Length > 1000) throw new InvalidOperationException("非表示理由を1～1000文字で入力してください。");
                if (!Ask($"入庫ID：{row.WaID} を非表示にしますか？\nこの伝票の全明細が一覧から非表示になります。\n入力中の変更・在庫数は保存しません。\n理由：{reason}", "非表示の確認")) return;
                await HideWarehousingAsync(_loginId, row.WaID, row.Stamp, reason);
                await AfterWriteAsync("非表示にしました。関連伝票と在庫数は変更していません。");
            });
        }
        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync(async () =>
            {
                var row = RequireSelected();
                if (row.RawState != 0) throw new InvalidOperationException("状態0の未処理伝票を選択してください。");
                if (!Ask($"入庫ID：{row.WaID} の商品は倉庫の棚に入りましたか？\nこの入庫の全明細数量を在庫に加算します。\n画面で入力し直した数量・日付・担当者は保存しません。\n確定担当者としてログイン社員を記録します。", "入庫確定")) return;
                await ConfirmWarehousingAsync(_loginId, row.WaID, row.Stamp);
                await AfterWriteAsync("棚入を確定して在庫数を加算しました。この入庫を再確定することはできません。");
            });
        }
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("この仕様書では削除機能は定義されていません。記録を残すため「非表示」を使用してください。", "削除", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        private static string ConnectionLabel()
        {
            try
            {
                using var db = new SalesManagementContext();
                var connection = db.Database.GetDbConnection();
                // 接続文字列全体やパスワードは表示しない。
                return $"接続先：{connection.DataSource}\nデータベース：{connection.Database}";
            }
            catch { return "接続先情報を取得できませんでした。"; }
        }

        private static string ErrorMessage(Exception error)
        {
            if (error is InvalidOperationException && error.InnerException == null)
                return error.Message + "\n\n" + ConnectionLabel();
            var cause = error.GetBaseException();
            string detail = System.Text.RegularExpressions.Regex.Replace(cause.Message,
                @"(?i)(password|pwd)\s*=\s*(?:""[^""]*""|'[^']*'|[^;\r\n]*)", "$1=***");
            if (detail.Length > 1600) detail = detail.Substring(0, 1600);
            return "処理に失敗しました。\n\n" + ConnectionLabel() +
                "\n\n" + cause.GetType().Name + "：" + detail +
                "\n\nこのメッセージを選択した状態で Ctrl+C を押すと内容をコピーできます。";
        }

        private static async Task<string> EmptyResultMessageAsync(int loginId)
        {
            using var db = new SalesManagementContext();
            await CheckPermissionAsync(db, loginId);
            int total = await db.TWarehousings.CountAsync();
            int visible = await db.TWarehousings.CountAsync(x => x.WaFlag == 0);
            int pending = await db.TWarehousings.CountAsync(x => x.WaFlag == 0 && x.WaShelfFlag == 0);
            int completed = await db.TWarehousings.CountAsync(x => x.WaFlag == 0 && x.WaShelfFlag == 1);
            int unset = await db.TWarehousings.CountAsync(x => x.WaFlag == 0 && x.WaShelfFlag == null);
            string hint = total == 0
                ? "この接続先には入庫データがありません。データを確認したSQL Serverとデータベース名が一致するか確認してください。"
                : visible == 0
                    ? "全件が表示対象外です。処理フラグをSQLで確認してください。"
                    : "入力条件に一致する明細がありません。ID・メーカ・担当者・数量・期間を空欄にし、状態を「すべて」にして検索してください。";
            return "表示対象：0明細\n\n" + ConnectionLabel() +
                $"\n\n入庫伝票 全件：{total}件\n表示対象（WaFlag=0）：{visible}件" +
                $"\n未処理（WaShelfFlag=0）：{pending}件\n処理済（WaShelfFlag=1）：{completed}件" +
                $"\n状態未設定（NULL）：{unset}件\n\n" + hint +
                "\n「一覧表示」は未処理（状態0）の伝票だけを表示します。";
        }

        #region Database queries and transactions (kept in this code-behind file)

        private static async Task CheckPermissionAsync(SalesManagementContext db, int loginId)
        {
            if (!await db.MEmployees.AsNoTracking().AnyAsync(x => x.EmId == loginId && x.EmFlag == 0 &&
                (x.Po.PoName == "管理者" || x.Po.PoName == "物流" || x.Po.PoName == "物流担当")))
                throw new InvalidOperationException("発注・入庫管理を利用する権限がありません。再ログインしてください。");
        }

        public static async Task<ReceivingLookups> GetLookupsAsync(int loginId)
        {
            using var db = new SalesManagementContext(); await CheckPermissionAsync(db, loginId);
            // Keep historical hidden master names searchable; validate active records again on registration.
            return new ReceivingLookups
            {
                Makers = await db.MMakers.AsNoTracking().OrderBy(x => x.MaId)
                    .Select(x => new ReceivingChoice { Id = x.MaId, Name = x.MaId + "：" + x.MaName + (x.MaFlag == 0 ? "" : "（非表示）") }).ToListAsync(),
                Employees = await db.MEmployees.AsNoTracking().OrderBy(x => x.EmId)
                    .Select(x => new ReceivingChoice { Id = x.EmId, Name = x.EmId + "：" + x.EmName + (x.EmFlag == 0 ? "" : "（非表示）") }).ToListAsync()
            };
        }

        private static string Reason(string reason)
        {
            reason = reason.Trim();
            if (reason.Length == 0 || reason.Length > 1000) throw new InvalidOperationException("非表示理由を1～1000文字で入力してください。");
            return reason;
        }

        private static string WarehousingStamp(TWarehousing w) => JsonSerializer.Serialize(new
        {
            w.WaId, w.HaId, w.EmId, w.WaDate, w.WaShelfFlag, w.WaFlag, w.WaHidden,
            Lines = w.TWarehousingDetails.OrderBy(x => x.WaDetailId).Select(x => new { x.WaDetailId, x.PrId, x.WaQuantity }).ToArray()
        });

        public static async Task<List<WarehousingDisplayRow>> GetWarehousingsAsync(int loginId, ReceivingFilter filter)
        {
            filter.Validate(); using var db = new SalesManagementContext(); await CheckPermissionAsync(db, loginId);
            var q = db.TWarehousings.AsNoTracking().Where(x => x.WaFlag == 0);
            if (filter.IdPrefix != "") q = q.Where(x => x.WaId.ToString().StartsWith(filter.IdPrefix));
            if (filter.HattyuIdPrefix != "") q = q.Where(x => x.HaId.ToString().StartsWith(filter.HattyuIdPrefix));
            if (filter.ProductIdPrefix != "") q = q.Where(x => x.TWarehousingDetails.Any(d => d.PrId.ToString().StartsWith(filter.ProductIdPrefix)));
            if (filter.Quantity.HasValue) q = q.Where(x => x.TWarehousingDetails.Any(d => d.WaQuantity == filter.Quantity.Value));
            if (filter.MakerId.HasValue) q = q.Where(x => x.Ha.MaId == filter.MakerId.Value);
            if (filter.EmployeeId.HasValue) q = q.Where(x => x.EmId == filter.EmployeeId.Value);
            if (filter.State == -1) q = q.Where(x => x.WaShelfFlag == null);
            else if (filter.State.HasValue) q = q.Where(x => x.WaShelfFlag == filter.State.Value);
            if (filter.From.HasValue) q = q.Where(x => x.WaDate >= filter.From.Value.Date);
            if (filter.To.HasValue) q = q.Where(x => x.WaDate <= filter.To.Value.Date);
            // 名称用の関連レコードがなくても入庫伝票と全明細を表示する。
            var headers = await q.Include(x => x.TWarehousingDetails)
                .AsSingleQuery().OrderByDescending(x => x.WaId).ToListAsync();
            var parentMakers = await db.THattyus.AsNoTracking().Where(h => q.Any(w => w.HaId == h.HaId))
                .ToDictionaryAsync(h => h.HaId, h => h.MaId);
            var makers = await db.MMakers.AsNoTracking()
                .Where(m => db.THattyus.Any(h => h.MaId == m.MaId && q.Any(w => w.HaId == h.HaId)))
                .ToDictionaryAsync(m => m.MaId, m => m.MaName);
            var employees = await db.MEmployees.AsNoTracking().Where(em => q.Any(w => w.EmId == em.EmId))
                .ToDictionaryAsync(em => em.EmId, em => em.EmName);
            var products = await db.MProducts.AsNoTracking()
                .Where(p => q.Any(w => w.TWarehousingDetails.Any(d => d.PrId == p.PrId)))
                .ToDictionaryAsync(p => p.PrId, p => p.PrName);
            var result = new List<WarehousingDisplayRow>();
            foreach (var w in headers)
            {
                string stamp = WarehousingStamp(w);
                int makerId = parentMakers.TryGetValue(w.HaId, out int parentMakerId) ? parentMakerId : 0;
                foreach (var line in w.TWarehousingDetails.OrderBy(x => x.WaDetailId).DefaultIfEmpty())
                {
                    if (line != null && filter.ProductIdPrefix != "" && !line.PrId.ToString().StartsWith(filter.ProductIdPrefix, StringComparison.Ordinal)) continue;
                    if (line != null && filter.Quantity.HasValue && line.WaQuantity != filter.Quantity.Value) continue;
                    result.Add(new WarehousingDisplayRow
                    {
                        WaID = w.WaId, HaID = w.HaId, MakerId = makerId, EmployeeId = w.EmId, Maker = makers.TryGetValue(makerId, out var makerName) ? makerName : "（発注・メーカ未登録）",
                        PrID = line?.PrId.ToString() ?? "", ProductName = line == null ? "（明細なし）" : products.TryGetValue(line.PrId, out var productName) ? productName : $"（商品未登録：{line.PrId}）", WaQuantity = line?.WaQuantity ?? 0,
                        WaDate = w.WaDate, EmID = w.EmId.HasValue ? w.EmId + "：" + (employees.TryGetValue(w.EmId.Value, out var employeeName) ? employeeName : "（社員未登録）") : "未担当",
                        RawState = w.WaShelfFlag, WaFlag = w.WaFlag, HiddenReason = w.WaHidden ?? "", Stamp = stamp
                    });
                }
            }
            return result;
        }

        private static async Task<TWarehousing> WarehousingForWriteAsync(SalesManagementContext db, int id, string expected)
        {
            var w = await db.TWarehousings.Include(x => x.TWarehousingDetails).AsSingleQuery().SingleOrDefaultAsync(x => x.WaId == id && x.WaFlag == 0)
                ?? throw new InvalidOperationException("対象の入庫が見つかりません。");
            if (WarehousingStamp(w) != expected) throw new InvalidOperationException("入庫情報が変更されました。一覧から選択し直してください。");
            return w;
        }

        public static async Task HideWarehousingAsync(int loginId, int id, string expected, string reason)
        {
            reason = Reason(reason); using var db = new SalesManagementContext();
            using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await CheckPermissionAsync(db, loginId); var w = await WarehousingForWriteAsync(db, id, expected);
            w.WaFlag = 2; w.WaHidden = reason;
            await db.SaveChangesAsync(); await tx.CommitAsync();
        }

        public static async Task ConfirmWarehousingAsync(int loginId, int id, string expected)
        {
            using var db = new SalesManagementContext();
            using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await CheckPermissionAsync(db, loginId); var w = await WarehousingForWriteAsync(db, id, expected);
            if (w.WaShelfFlag != 0) throw new InvalidOperationException("未棚入（状態0）の入庫だけ確定できます。棚入済みの入庫は再確定できません。");
            if (w.TWarehousingDetails.Count == 0 || w.TWarehousingDetails.Any(x => x.WaQuantity <= 0))
                throw new InvalidOperationException("入庫明細の数量を確認してください。");
            if (await db.TWarehousings.CountAsync(x => x.HaId == w.HaId) != 1)
                throw new InvalidOperationException("同じ発注に複数の入庫があります。重複・分納の扱いを確認してから処理してください。");
            var h = await db.THattyus.SingleAsync(x => x.HaId == w.HaId);
            if (h.WaWarehouseFlag != 1) throw new InvalidOperationException("発注側の入庫状態が一致しません。データを確認してください。");
            foreach (var group in w.TWarehousingDetails.GroupBy(x => x.PrId).OrderBy(x => x.Key))
            {
                long add = group.Sum(x => (long)x.WaQuantity);
                // Include hidden stock rows, avoiding accidental duplicates and retaining their flag.
                var stocks = await db.TStocks.Where(x => x.PrId == group.Key).OrderBy(x => x.StId).ToListAsync();
                if (stocks.Count > 1) throw new InvalidOperationException($"商品ID：{group.Key} の在庫が複数あります。在庫データを確認してください。");
                var stock = stocks.SingleOrDefault();
                long next = (stock?.StQuantity ?? 0) + add;
                if ((stock?.StQuantity ?? 0) < 0 || next > int.MaxValue)
                    throw new InvalidOperationException($"商品ID：{group.Key} の在庫数が範囲外になります。");
                if (stock == null) { stock = new TStock { PrId = group.Key, StFlag = 0 }; db.TStocks.Add(stock); }
                stock.StQuantity = (int)next;
            }
            w.WaShelfFlag = 1; w.EmId = loginId;
            await db.SaveChangesAsync(); await tx.CommitAsync();
        }

        #endregion

        #region Local row and filter types (no separate Models/Data file)

        public sealed class ReceivingChoice
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public override string ToString() => Name;
        }

        public sealed class ReceivingLookups
        {
            public List<ReceivingChoice> Makers { get; set; } = new();
            public List<ReceivingChoice> Employees { get; set; } = new();
        }

        public sealed class ReceivingState
        {
            // null=all, -1=legacy NULL, 0=pending, 1=confirmed.
            public int? Value { get; set; }
            public string Name { get; set; } = "";
        }

        public sealed class ReceivingFilter
        {
            public string IdPrefix { get; set; } = "";
            public string HattyuIdPrefix { get; set; } = "";
            public string ProductIdPrefix { get; set; } = "";
            public int? MakerId { get; set; }
            public int? EmployeeId { get; set; }
            public int? Quantity { get; set; }
            public int? State { get; set; }
            public DateTime? From { get; set; }
            public DateTime? To { get; set; }
            public void Validate()
            {
                foreach (var id in new[] { IdPrefix, HattyuIdPrefix, ProductIdPrefix })
                    if (id.Any(c => c < '0' || c > '9')) throw new InvalidOperationException("IDは半角数字で入力してください。");
                if (Quantity < 0) throw new InvalidOperationException("数量は0以上で検索してください。");
                if (State.HasValue && State != -1 && State != 0 && State != 1)
                    throw new InvalidOperationException("状態を選択し直してください。");
                if (From.HasValue && To.HasValue && From.Value.Date > To.Value.Date)
                    throw new InvalidOperationException("開始日は終了日以前にしてください。");
            }
            public static int Number(string text, string label, bool allowZero = false)
            {
                text = text.Trim();
                if (text.Length == 0 || text.Any(c => c < '0' || c > '9') ||
                    !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || (!allowZero && n == 0))
                    throw new InvalidOperationException(label + (allowZero ? "は0～2147483647" : "は1～2147483647") + "の半角整数で入力してください。");
                return n;
            }
        }

        public sealed class WarehousingDisplayRow
        {
            public int WaID { get; set; }
            public int HaID { get; set; }
            public int MakerId { get; set; }
            public int? EmployeeId { get; set; }
            public string Maker { get; set; } = "";
            public string PrID { get; set; } = "";
            public string ProductName { get; set; } = "";
            public int WaQuantity { get; set; }
            public DateTime WaDate { get; set; }
            public string EmID { get; set; } = "";
            public int? RawState { get; set; }
            public int WaFlag { get; set; }
            public string Status => (RawState == 0 ? "未棚入" : RawState == 1 ? "棚入済" : "状態未設定") + (string.IsNullOrEmpty(HiddenReason) ? "" : " / " + HiddenReason);
            public string HiddenReason { get; set; } = "";
            public string Stamp { get; set; } = "";
        }

        #endregion

    }
}
