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
    public partial class HattyuView : UserControl
    {
        private readonly int _loginId;
        private bool _initialized, _loading, _restoring;
        private string _baseline = "";
        private HattyuDisplayRow? _selected;
        public bool IsBusy { get; private set; }
        public HattyuView() : this(0) { }
        public HattyuView(int loginEmployeeId)
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
                MessageBox.Show(ErrorMessage(ex), "発注管理", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    new ReceivingState { Value = 0, Name = "未入庫" },
                    new ReceivingState { Value = 1, Name = "入庫済" },
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
                From = ReadDate(dpFrom, "開始日"), To = ReadDate(dpTo, "終了日")
            };
            f.Validate(); return f;
        }
        private string FormStamp() => JsonSerializer.Serialize(new
        {
            Id = txtId.Text, Product = txtProductId.Text, Quantity = txtQuantity.Text, Reason = txtReason.Text,
            Maker = cmbMaker.SelectedValue, Employee = cmbEmployee.SelectedValue, State = (cmbState.SelectedItem as ReceivingState)?.Value,
            From = dpFrom.Text, To = dpTo.Text
        });
        private void ResetInputs(bool forList)
        {
            _loading = true;
            try
            {
                _selected = null; _restoring = true;
                try { gridData.SelectedItem = null; } finally { _restoring = false; }
                txtId.Clear(); txtProductId.Clear(); txtQuantity.Clear(); txtReason.Clear();
                cmbMaker.SelectedIndex = -1;
                cmbEmployee.SelectedValue = forList ? (int?)null : _loginId;
                cmbState.SelectedIndex = 0;
                dpFrom.SelectedDate = forList ? (DateTime?)null : DateTime.Today;
                dpTo.SelectedDate = null; _baseline = FormStamp();
            }
            finally { _loading = false; }
        }
        private async Task LoadRowsAsync(ReceivingFilter filter, bool reportEmpty = true)
        {
            var rows = await GetHattyusAsync(_loginId, filter);
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
            if (_loading || _restoring || IsBusy || gridData.SelectedItem is not HattyuDisplayRow row) return;
            if (ReferenceEquals(row, _selected)) return;
            if (!CanLeave())
            {
                _restoring = true; try { gridData.SelectedItem = _selected; } finally { _restoring = false; }
                return;
            }
            _loading = true;
            try
            {
                _selected = row; txtId.Text = row.HaID.ToString();
                txtProductId.Text = row.PrID; txtQuantity.Text = row.HaQuantity.ToString();
                cmbMaker.SelectedValue = row.MakerId; cmbEmployee.SelectedValue = row.EmployeeId;
                cmbState.SelectedValue = row.RawState ?? -1;
                dpFrom.SelectedDate = row.HaDate; dpTo.SelectedDate = null; txtReason.Clear();
                _baseline = FormStamp();
            }
            finally { _loading = false; }
        }
        private HattyuDisplayRow RequireSelected()
        {
            var row = _selected ?? throw new InvalidOperationException("一覧から対象を選択してください。");
            if (txtId.Text.Trim() != row.HaID.ToString())
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
                if (!Ask($"発注ID：{row.HaID} を非表示にしますか？\nこの伝票の全明細が一覧から非表示になります。\n入力中の変更・在庫数は保存しません。\n理由：{reason}", "非表示の確認")) return;
                await HideHattyuAsync(_loginId, row.HaID, row.Stamp, reason);
                await AfterWriteAsync("非表示にしました。関連伝票と在庫数は変更していません。");
            });
        }
        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync(async () =>
            {
                var row = RequireSelected();
                if (row.RawState != 0) throw new InvalidOperationException("状態0の未処理伝票を選択してください。");
                if (!Ask($"発注ID：{row.HaID} の商品は物流倉庫に入庫しましたか？\nこの発注の全明細から入庫伝票を作成します。\n在庫数は入庫画面で棚入確定するまで増えません。\n入力中の発注情報の変更は保存しません。", "発注確定")) return;
                int waId = await ConfirmHattyuAsync(_loginId, row.HaID, row.Stamp);
                await AfterWriteAsync($"発注を確定しました。入庫ID：{waId} を作成しました。入庫管理で棚入確定してください。");
            });
        }
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("この仕様書では削除機能は定義されていません。記録を残すため「非表示」を使用してください。", "削除", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private async void Register_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync(async () =>
            {
                if (!string.IsNullOrWhiteSpace(txtId.Text)) throw new InvalidOperationException("新規登録はクリアしてから行ってください。発注IDは自動採番です。");
                int maker = cmbMaker.SelectedValue as int? ?? throw new InvalidOperationException("メーカを選択してください。");
                int employee = cmbEmployee.SelectedValue as int? ?? throw new InvalidOperationException("担当者を選択してください。");
                int product = ReceivingFilter.Number(txtProductId.Text, "商品ID");
                int quantity = ReceivingFilter.Number(txtQuantity.Text, "数量");
                DateTime date = ReadDate(dpFrom, "発注日") ?? throw new InvalidOperationException("期間の左側に発注日を指定してください。");
                if (!string.IsNullOrWhiteSpace(dpTo.Text)) throw new InvalidOperationException("登録時は期間の右側を空欄にしてください。");
                if (!string.IsNullOrWhiteSpace(txtReason.Text)) throw new InvalidOperationException("登録時は非表示理由を空欄にしてください。");
                if ((cmbState.SelectedItem as ReceivingState)?.Value != 0) throw new InvalidOperationException("新規登録は状態「未入庫」で行ってください。");
                if (!Ask($"メーカID：{maker}、商品ID：{product}、数量：{quantity}\nこの内容で新規発注を登録しますか？\n在庫数は変更しません。", "発注登録")) return;
                int id = await RegisterHattyuAsync(_loginId, maker, employee, date, product, quantity);
                await AfterWriteAsync($"発注ID：{id} を登録しました。商品が到着したら発注確定してください。");
            });
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
            int total = await db.THattyus.CountAsync();
            int visible = await db.THattyus.CountAsync(x => x.HaFlag == 0);
            int pending = await db.THattyus.CountAsync(x => x.HaFlag == 0 && x.WaWarehouseFlag == 0);
            int completed = await db.THattyus.CountAsync(x => x.HaFlag == 0 && x.WaWarehouseFlag == 1);
            int unset = await db.THattyus.CountAsync(x => x.HaFlag == 0 && x.WaWarehouseFlag == null);
            string hint = total == 0
                ? "この接続先には発注データがありません。データを確認したSQL Serverとデータベース名が一致するか確認してください。"
                : visible == 0
                    ? "全件が表示対象外です。処理フラグをSQLで確認してください。"
                    : "入力条件に一致する明細がありません。ID・メーカ・担当者・数量・期間を空欄にし、状態を「すべて」にして検索してください。";
            return "表示対象：0明細\n\n" + ConnectionLabel() +
                $"\n\n発注伝票 全件：{total}件\n表示対象（HaFlag=0）：{visible}件" +
                $"\n未処理（WaWarehouseFlag=0）：{pending}件\n処理済（WaWarehouseFlag=1）：{completed}件" +
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

        private static string HattyuStamp(THattyu h) => JsonSerializer.Serialize(new
        {
            h.HaId, h.MaId, h.EmId, h.HaDate, h.WaWarehouseFlag, h.HaFlag, h.HaHidden,
            Lines = h.THattyuDetails.OrderBy(x => x.HaDetailId).Select(x => new { x.HaDetailId, x.PrId, x.HaQuantity }).ToArray()
        });

        public static async Task<List<HattyuDisplayRow>> GetHattyusAsync(int loginId, ReceivingFilter filter)
        {
            filter.Validate(); using var db = new SalesManagementContext(); await CheckPermissionAsync(db, loginId);
            var q = db.THattyus.AsNoTracking().Where(x => x.HaFlag == 0);
            if (filter.IdPrefix != "") q = q.Where(x => x.HaId.ToString().StartsWith(filter.IdPrefix));
            if (filter.ProductIdPrefix != "") q = q.Where(x => x.THattyuDetails.Any(d => d.PrId.ToString().StartsWith(filter.ProductIdPrefix)));
            if (filter.Quantity.HasValue) q = q.Where(x => x.THattyuDetails.Any(d => d.HaQuantity == filter.Quantity.Value));
            if (filter.MakerId.HasValue) q = q.Where(x => x.MaId == filter.MakerId.Value);
            if (filter.EmployeeId.HasValue) q = q.Where(x => x.EmId == filter.EmployeeId.Value);
            if (filter.State == -1) q = q.Where(x => x.WaWarehouseFlag == null);
            else if (filter.State.HasValue) q = q.Where(x => x.WaWarehouseFlag == filter.State.Value);
            if (filter.From.HasValue) q = q.Where(x => x.HaDate >= filter.From.Value.Date);
            if (filter.To.HasValue) q = q.Where(x => x.HaDate <= filter.To.Value.Date);
            // 明細は全件保持する。名称取得の必須ナビゲーションJOINで伝票を落とさない。
            var headers = await q.Include(x => x.THattyuDetails)
                .AsSingleQuery().OrderByDescending(x => x.HaId).ToListAsync();
            var makers = await db.MMakers.AsNoTracking().Where(m => q.Any(h => h.MaId == m.MaId))
                .ToDictionaryAsync(m => m.MaId, m => m.MaName);
            var employees = await db.MEmployees.AsNoTracking().Where(em => q.Any(h => h.EmId == em.EmId))
                .ToDictionaryAsync(em => em.EmId, em => em.EmName);
            var products = await db.MProducts.AsNoTracking()
                .Where(p => q.Any(h => h.THattyuDetails.Any(d => d.PrId == p.PrId)))
                .ToDictionaryAsync(p => p.PrId, p => p.PrName);
            var result = new List<HattyuDisplayRow>();
            foreach (var h in headers)
            {
                string stamp = HattyuStamp(h);
                foreach (var line in h.THattyuDetails.OrderBy(x => x.HaDetailId).DefaultIfEmpty())
                {
                    if (line != null && filter.ProductIdPrefix != "" && !line.PrId.ToString().StartsWith(filter.ProductIdPrefix, StringComparison.Ordinal)) continue;
                    if (line != null && filter.Quantity.HasValue && line.HaQuantity != filter.Quantity.Value) continue;
                    result.Add(new HattyuDisplayRow
                    {
                        HaID = h.HaId, MakerId = h.MaId, EmployeeId = h.EmId, Maker = makers.TryGetValue(h.MaId, out var makerName) ? makerName : $"（メーカ未登録：{h.MaId}）",
                        PrID = line?.PrId.ToString() ?? "", ProductName = line == null ? "（明細なし）" : products.TryGetValue(line.PrId, out var productName) ? productName : $"（商品未登録：{line.PrId}）", HaQuantity = line?.HaQuantity ?? 0,
                        HaDate = h.HaDate, EmID = h.EmId + "：" + (employees.TryGetValue(h.EmId, out var employeeName) ? employeeName : "（社員未登録）"), RawState = h.WaWarehouseFlag,
                        HaFlag = h.HaFlag, Status = h.HaHidden ?? "", Stamp = stamp
                    });
                }
            }
            return result;
        }

        public static async Task<int> RegisterHattyuAsync(int loginId, int makerId, int employeeId, DateTime date, int productId, int quantity)
        {
            if (quantity <= 0) throw new InvalidOperationException("数量は1以上にしてください。");
            using var db = new SalesManagementContext();
            using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await CheckPermissionAsync(db, loginId);
            if (!await db.MMakers.AnyAsync(x => x.MaId == makerId && x.MaFlag == 0))
                throw new InvalidOperationException("選択したメーカは利用できません。");
            if (!await db.MEmployees.AnyAsync(x => x.EmId == employeeId && x.EmFlag == 0 &&
                (x.Po.PoName == "管理者" || x.Po.PoName == "物流" || x.Po.PoName == "物流担当")))
                throw new InvalidOperationException("担当者は利用可能な管理者・物流担当を選択してください。");
            if (!await db.MProducts.AnyAsync(x => x.PrId == productId && x.PrFlag == 0 && x.MaId == makerId))
                throw new InvalidOperationException("商品IDが見つからないか、選択したメーカの商品ではありません。");
            var h = new THattyu { MaId = makerId, EmId = employeeId, HaDate = date.Date, HaFlag = 0, WaWarehouseFlag = 0 };
            h.THattyuDetails.Add(new THattyuDetail { PrId = productId, HaQuantity = quantity });
            db.THattyus.Add(h); await db.SaveChangesAsync(); await tx.CommitAsync(); return h.HaId;
        }

        private static async Task<THattyu> HattyuForWriteAsync(SalesManagementContext db, int id, string expected)
        {
            var h = await db.THattyus.Include(x => x.THattyuDetails).AsSingleQuery().SingleOrDefaultAsync(x => x.HaId == id && x.HaFlag == 0)
                ?? throw new InvalidOperationException("対象の発注が見つかりません。");
            if (HattyuStamp(h) != expected) throw new InvalidOperationException("発注情報が変更されました。一覧から選択し直してください。");
            return h;
        }

        public static async Task HideHattyuAsync(int loginId, int id, string expected, string reason)
        {
            reason = Reason(reason); using var db = new SalesManagementContext();
            using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await CheckPermissionAsync(db, loginId); var h = await HattyuForWriteAsync(db, id, expected);
            h.HaFlag = 2; h.HaHidden = reason;
            await db.SaveChangesAsync(); await tx.CommitAsync();
        }

        public static async Task<int> ConfirmHattyuAsync(int loginId, int id, string expected)
        {
            using var db = new SalesManagementContext();
            using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await CheckPermissionAsync(db, loginId); var h = await HattyuForWriteAsync(db, id, expected);
            if (h.WaWarehouseFlag != 0 || await db.TWarehousings.AnyAsync(x => x.HaId == id))
                throw new InvalidOperationException("未入庫（状態0）の発注だけ確定できます。既存の入庫がある発注は再確定できません。");
            if (h.THattyuDetails.Count == 0 || h.THattyuDetails.Any(x => x.HaQuantity <= 0))
                throw new InvalidOperationException("発注明細の数量を確認してください。");
            var w = new TWarehousing { HaId = h.HaId, EmId = null, WaDate = DateTime.Today, WaFlag = 0, WaShelfFlag = 0 };
            foreach (var line in h.THattyuDetails)
                w.TWarehousingDetails.Add(new TWarehousingDetail { PrId = line.PrId, WaQuantity = line.HaQuantity });
            db.TWarehousings.Add(w); h.WaWarehouseFlag = 1;
            // Do not touch stock here: shelf confirmation owns the stock increase.
            await db.SaveChangesAsync(); await tx.CommitAsync(); return w.WaId;
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

        public sealed class HattyuDisplayRow
        {
            public int HaID { get; set; }
            public int MakerId { get; set; }
            public int EmployeeId { get; set; }
            public string Maker { get; set; } = "";
            public string PrID { get; set; } = "";
            public string ProductName { get; set; } = "";
            public int HaQuantity { get; set; }
            public DateTime HaDate { get; set; }
            public string EmID { get; set; } = "";
            public int? RawState { get; set; }
            public string WaWarehouseFlag => RawState == 0 ? "未入庫" : RawState == 1 ? "入庫済" : "未設定";
            public int HaFlag { get; set; }
            public string Status { get; set; } = "";
            public string Stamp { get; set; } = "";
        }

        #endregion

    }
}
