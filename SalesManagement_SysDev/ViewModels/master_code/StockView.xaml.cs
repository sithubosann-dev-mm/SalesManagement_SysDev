using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using SalesManagement_SysDev.ViewModels.master_code;

namespace SalesManagement_SysDev
{
    public partial class StockView : UserControl
    {
        private readonly int _employeeId;
        private readonly SimpleStockData _service = new();
        private readonly SimpleStockDialogs _dialogs = new();
        private bool _initialized, _loading, _restoring;
        private SimpleStockSnapshot? _snapshot;
        private string _baseline = "", _pendingStamp = "";
        private SimpleStockAction _pending;
        public bool IsBusy { get; private set; }
        private bool IsIdle => !IsBusy;
        public string EditLoadError { get; private set; } = "";
        public event EventHandler? SearchRequested;
        private string QuantityText { get => txtQuantity.Text; set => txtQuantity.Text = value; }
        private string PendingText { get => txtPending.Text; set => txtPending.Text = value; }
        private string Message { get => txtMessage.Text; set => txtMessage.Text = value; }
        private List<SimpleStockRow> Stocks { get => (List<SimpleStockRow>?)gridStocks.ItemsSource ?? new(); set => gridStocks.ItemsSource = value; }
        private SimpleStockRow? _selectedStock;
        private SimpleStockRow? SelectedStock
        {
            get => _selectedStock;
            set { _selectedStock = value; _restoring = true; try { gridStocks.SelectedItem = value; } finally { _restoring = false; } }
        }
        public StockView(int loginEmployeeId)
        {
            InitializeComponent(); _employeeId = loginEmployeeId;
            txtQuantity.TextChanged += (_, _) =>
            {
                if (!_loading && _pending != SimpleStockAction.None)
                { ClearPending(); PendingText = "在庫数が変更されました。操作を選び直してください。"; }
            };
            ShowSelection();
        }
        private void RefreshButtons()
        {
            btnUpdate.IsEnabled = btnHide.IsEnabled = IsIdle && _snapshot != null;
            btnClear.IsEnabled = btnList.IsEnabled = btnOpenSearch.IsEnabled = IsIdle;
            btnConfirm.IsEnabled = IsIdle && _pending != SimpleStockAction.None;
        }
        private void ShowSelection()
        {
            txtStockId.Text = _snapshot?.Row.Id.ToString() ?? "未選択";
            txtProductId.Text = _snapshot?.Row.ProductId.ToString() ?? "—";
            txtProductName.Text = _snapshot?.Row.ProductName ?? "";
            txtSafety.Text = _snapshot?.Row.SafetyStock.ToString() ?? "—";
            txtCurrentQuantity.Text = _snapshot?.Row.Quantity.ToString() ?? "—";
            txtMode.Text = _snapshot == null ? "一覧から更新する在庫を選択してください。" : $"在庫ID：{_snapshot.Row.Id} を選択しています。";
            RefreshButtons();
        }
        private async Task<bool> RunAsync(Func<Task> action)
        {
            if (IsBusy) return false;
            IsBusy = true; mainPanel.IsEnabled = false; RefreshButtons(); Message = "処理中です…";
            try { await action(); return true; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                Message = ex is InvalidOperationException ? ex.Message : "処理に失敗しました。DB接続と入力内容を確認してください。";
                return false;
            }
            finally { IsBusy = false; mainPanel.IsEnabled = true; RefreshButtons(); }
        }
        private async void View_Loaded(object sender, RoutedEventArgs e) => await InitializeAsync();
        private void ClearPending()
        { _pending = SimpleStockAction.None; _pendingStamp = ""; PendingText = "操作を選択してから確定を押してください。"; RefreshButtons(); }
        private void ResetForm()
        {
            _loading = true;
            try { ClearPending(); _snapshot = null; SelectedStock = null; QuantityText = ""; _baseline = QuantityText; ShowSelection(); }
            finally { _loading = false; }
        }
        public bool CanLeave()
        {
            if (IsBusy) { _dialogs.Inform("処理が終わるまでお待ちください。"); return false; }
            return !_initialized || QuantityText == _baseline && _pending == SimpleStockAction.None || _dialogs.Confirm("未保存の入力を破棄しますか？");
        }
        public async Task InitializeAsync()
        {
            if (_initialized) return;
            await RunAsync(async () => { await LoadRowsAsync(); _initialized = true; ResetForm(); });
        }
        private async Task LoadRowsAsync()
        { Stocks = await _service.GetRowsAsync(_employeeId); Message = $"{Stocks.Count}件を表示しています。"; }
        private async Task LoadForEditAsync(int id)
        {
            var snapshot = await _service.GetStockAsync(_employeeId, id);
            _loading = true;
            try
            {
                ClearPending(); _snapshot = snapshot; QuantityText = snapshot.Row.Quantity.ToString();
                _baseline = QuantityText; ShowSelection();
                Message = "更新後の在庫数を入力して「更新 → 確定」を押してください。";
            }
            finally { _loading = false; }
        }
        public async Task<bool> TrySelectAsync(SimpleStockRow row)
        {
            if (!CanLeave()) return false;
            bool loaded = await RunAsync(async () => { await LoadForEditAsync(row.Id); SelectedStock = row; _initialized = true; });
            if (!loaded) { string error = Message; ResetForm(); Message = error; }
            return loaded;
        }
        public async Task<bool> OpenForEditAsync(int stockId)
        {
            EditLoadError = ""; if (!CanLeave()) return false;
            bool opened = await RunAsync(async () =>
            {
                await LoadRowsAsync();
                var row = Stocks.SingleOrDefault(x => x.Id == stockId) ?? throw new InvalidOperationException("在庫が見つかりません。");
                await LoadForEditAsync(stockId); SelectedStock = row; _initialized = true;
            });
            if (!opened) { EditLoadError = Message; ResetForm(); Message = EditLoadError; }
            return opened;
        }
        private void Stage(SimpleStockAction action)
        {
            if (IsBusy || _snapshot == null) return;
            ClearPending();
            try
            {
                if (action == SimpleStockAction.Update) SimpleStockFilter.ReadQuantity(QuantityText);
                _pending = action; _pendingStamp = QuantityText;
                PendingText = (action == SimpleStockAction.Update ? "更新" : "非表示") + "を選択しました。まだ保存していません。確定を押してください。";
                Message = action == SimpleStockAction.Update ? "入力値を更新後の在庫数として保存します。" : "非表示は在庫数を変更しません。";
                RefreshButtons();
            }
            catch (InvalidOperationException ex) { Message = ex.Message; }
        }
        private async Task ConfirmAsync()
        {
            if (_snapshot == null || _pending == SimpleStockAction.None) return;
            if (_pendingStamp != QuantityText) { ClearPending(); Message = "入力が変更されました。操作を選び直してください。"; return; }
            var snapshot = _snapshot; var action = _pending;
            await RunAsync(async () =>
            {
                string label = action == SimpleStockAction.Update ? "更新" : "非表示";
                int quantity = action == SimpleStockAction.Update ? SimpleStockFilter.ReadQuantity(QuantityText) : snapshot.Row.Quantity;
                string question = action == SimpleStockAction.Update
                    ? $"在庫ID：{snapshot.Row.Id}（{snapshot.Row.ProductName}）\n在庫数を {snapshot.Row.Quantity} → {quantity} に更新しますか？\n入出庫処理ではなく、在庫数を直接訂正します。"
                    : $"在庫ID：{snapshot.Row.Id} を非表示にしますか？\n在庫管理フラグを0から2に変更します。在庫数は変更しません。\n入力中の在庫数は保存しません。";
                if (!_dialogs.Confirm(question, label + "の確認")) { Message = label + "をキャンセルしました。"; return; }
                if (action == SimpleStockAction.Update) await _service.UpdateAsync(_employeeId, snapshot.Row.Id, snapshot.Stamp, quantity);
                else await _service.HideAsync(_employeeId, snapshot.Row.Id, snapshot.Stamp);
                string success = $"在庫ID：{snapshot.Row.Id} を{label}しました。";
                ResetForm();
                try { await LoadRowsAsync(); Message = success; }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); Stocks = new(); Message = success + " 一覧の再取得に失敗しました。一覧表示を押してください。"; }
            });
        }
        private void Clear() { if (!CanLeave()) return; ResetForm(); Message = "入力と選択をクリアしました。"; }
        private async Task ListAsync()
        {
            if (!CanLeave()) return;
            await RunAsync(async () => { ResetForm(); await LoadRowsAsync(); _initialized = true; });
        }
        private void OpenSearch()
        { if (!CanLeave()) return; ResetForm(); SearchRequested?.Invoke(this, EventArgs.Empty); }
        private void Update_Click(object sender, RoutedEventArgs e) => Stage(SimpleStockAction.Update);
        private void Hide_Click(object sender, RoutedEventArgs e) => Stage(SimpleStockAction.Hide);
        private void Clear_Click(object sender, RoutedEventArgs e) => Clear();
        private void OpenSearch_Click(object sender, RoutedEventArgs e) => OpenSearch();
        private async void List_Click(object sender, RoutedEventArgs e) => await ListAsync();
        private async void Confirm_Click(object sender, RoutedEventArgs e) => await ConfirmAsync();
        private async void Stocks_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_restoring || IsBusy || gridStocks.SelectedItem is not SimpleStockRow row || ReferenceEquals(row, SelectedStock)) return;
            await TrySelectAsync(row);
            _restoring = true; try { gridStocks.SelectedItem = SelectedStock; } finally { _restoring = false; }
        }
    }
}

namespace SalesManagement_SysDev.ViewModels.master_code
{
    public enum SimpleStockAction { None, Update, Hide }
    public sealed class SimpleStockRow
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public int Quantity { get; set; }
        public int SafetyStock { get; set; }
        public int ProductFlag { get; set; }
        public string ProductState => ProductFlag == 0 ? "表示中" : "非表示";
        public string StockState => Quantity < SafetyStock ? "安全在庫未満" : "通常";
    }
    public sealed class SimpleStockSnapshot
    {
        public SimpleStockRow Row { get; set; } = new();
        public string Stamp { get; set; } = "";
    }
    public sealed class SimpleStockFilter
    {
        public string StockIdPrefix { get; set; } = "";
        public string ProductIdPrefix { get; set; } = "";
        public string ProductName { get; set; } = "";
        public int? MinQuantity { get; set; }
        public int? MaxQuantity { get; set; }
        public void Validate()
        {
            if (StockIdPrefix.Any(c => c < '0' || c > '9') || ProductIdPrefix.Any(c => c < '0' || c > '9'))
                throw new InvalidOperationException("在庫ID・商品IDは半角数字で入力してください。");
            if (ProductName.Length > 50) throw new InvalidOperationException("商品名は50文字以内で検索してください。");
            if (MinQuantity < 0 || MaxQuantity < 0 || MinQuantity.HasValue && MaxQuantity.HasValue && MinQuantity > MaxQuantity)
                throw new InvalidOperationException("在庫数の範囲を確認してください。0以上で、下限は上限以下にしてください。");
        }
        public static int ReadQuantity(string text, string label = "在庫数")
        {
            text = text.Trim();
            if (text.Length == 0 || text.Any(c => c < '0' || c > '9') ||
                !int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int n))
                throw new InvalidOperationException(label + "は0～2147483647の半角整数で入力してください。");
            return n;
        }
    }
}

namespace SalesManagement_SysDev.ViewModels.master_code
{
    public sealed class SimpleStockData
    {
        private static async Task CheckPermissionAsync(SalesManagementContext db, int employeeId)
        {
            if (!await db.MEmployees.AsNoTracking().AnyAsync(x => x.EmId == employeeId && x.EmFlag == 0 &&
                (x.Po.PoName == "管理者" || x.Po.PoName == "物流" || x.Po.PoName == "物流担当")))
                throw new InvalidOperationException("在庫管理を利用する権限がありません。再ログインしてください。");
        }
        private static string Stamp(TStock stock) => JsonSerializer.Serialize(new
        { stock.StId, stock.PrId, stock.StQuantity, stock.StFlag });

        public async Task<List<SimpleStockRow>> GetRowsAsync(int employeeId, SimpleStockFilter? filter = null)
        {
            filter ??= new SimpleStockFilter(); filter.Validate();
            using var db = new SalesManagementContext();
            await CheckPermissionAsync(db, employeeId);
            // A hidden product may still have real inventory. Filter the stock flag only.
            var query = db.TStocks.AsNoTracking().Where(x => x.StFlag == 0);
            if (filter.StockIdPrefix != "") query = query.Where(x => x.StId.ToString().StartsWith(filter.StockIdPrefix));
            if (filter.ProductIdPrefix != "") query = query.Where(x => x.PrId.ToString().StartsWith(filter.ProductIdPrefix));
            if (filter.ProductName != "") query = query.Where(x => x.Pr.PrName.Contains(filter.ProductName));
            if (filter.MinQuantity.HasValue) query = query.Where(x => x.StQuantity >= filter.MinQuantity.Value);
            if (filter.MaxQuantity.HasValue) query = query.Where(x => x.StQuantity <= filter.MaxQuantity.Value);
            return await query.OrderBy(x => x.StId).Select(x => new SimpleStockRow
            {
                Id = x.StId, ProductId = x.PrId, ProductName = x.Pr.PrName, Quantity = x.StQuantity,
                SafetyStock = x.Pr.PrSafetyStock, ProductFlag = x.Pr.PrFlag
            }).ToListAsync();
        }
        public async Task<SimpleStockSnapshot> GetStockAsync(int employeeId, int stockId)
        {
            using var db = new SalesManagementContext();
            await CheckPermissionAsync(db, employeeId);
            var stock = await db.TStocks.AsNoTracking().Include(x => x.Pr)
                .SingleOrDefaultAsync(x => x.StId == stockId && x.StFlag == 0)
                ?? throw new InvalidOperationException("在庫が見つかりません。非表示にされた可能性があります。");
            return new SimpleStockSnapshot
            {
                Row = new SimpleStockRow
                {
                    Id = stock.StId, ProductId = stock.PrId, ProductName = stock.Pr.PrName,
                    Quantity = stock.StQuantity, SafetyStock = stock.Pr.PrSafetyStock, ProductFlag = stock.Pr.PrFlag
                },
                Stamp = Stamp(stock)
            };
        }
        private static async Task<TStock> GetForWriteAsync(SalesManagementContext db, int id, string expectedStamp)
        {
            var stock = await db.TStocks.SingleOrDefaultAsync(x => x.StId == id && x.StFlag == 0)
                ?? throw new InvalidOperationException("対象の在庫が見つかりません。");
            if (Stamp(stock) != expectedStamp)
                throw new InvalidOperationException("在庫情報が変更されました。一覧から選択し直してください。");
            return stock;
        }
        public async Task UpdateAsync(int employeeId, int stockId, string expectedStamp, int quantity)
        {
            if (quantity < 0) throw new InvalidOperationException("在庫数は0以上にしてください。");
            using var db = new SalesManagementContext();
            using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await CheckPermissionAsync(db, employeeId);
            var stock = await GetForWriteAsync(db, stockId, expectedStamp);
            // Absolute correction: does not create receiving/shipping/sales records.
            stock.StQuantity = quantity;
            await db.SaveChangesAsync(); await tx.CommitAsync();
        }
        public async Task HideAsync(int employeeId, int stockId, string expectedStamp)
        {
            using var db = new SalesManagementContext();
            using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await CheckPermissionAsync(db, employeeId);
            var stock = await GetForWriteAsync(db, stockId, expectedStamp);
            stock.StFlag = 2;
            await db.SaveChangesAsync(); await tx.CommitAsync();
        }
    }
}

namespace SalesManagement_SysDev.ViewModels.master_code
{
    internal sealed class SimpleStockDialogs
    {
        public bool Confirm(string message, string title = "確認") => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        public void Inform(string message) => MessageBox.Show(message);
    }
}
