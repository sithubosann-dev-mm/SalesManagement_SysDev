using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Text.Json;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;

namespace SalesManagement_SysDev
{
    public partial class OrderView : UserControl
    {
        private readonly int _loginEmployeeId;
        private readonly ObservableCollection<LineRow> _draft = new();
        private bool _initialized;
        private bool _dirty;
        private int? _selectedId;
        private int? _editingId;
        private string _editStamp = "";
        private string _loginEmployeeLabel = "";

        public bool IsBusy { get; private set; }
        public event EventHandler? SearchRequested;
        public string DisplayLoadError { get; private set; } = "";

        public sealed class LineRow : INotifyPropertyChanged
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; } = "";
            public decimal UnitPrice { get; set; }
            private int _quantity;
            private decimal _total;
            public int Quantity
            {
                get => _quantity;
                set { _quantity = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Quantity))); }
            }
            public decimal Total
            {
                get => _total;
                set { _total = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Total))); }
            }
            public event PropertyChangedEventHandler? PropertyChanged;
        }

        public sealed class OrderRow
        {
            public int Id { get; set; }
            public DateTime? Date { get; set; }
            public string Client { get; set; } = "";
            public string Office { get; set; } = "";
            public string Employee { get; set; } = "";
            public int EmployeeId { get; set; }
            public string Charge { get; set; } = "";
            public int? StateFlag { get; set; }
            public decimal Total { get; set; }
            public string State => StateFlag switch
            {
                0 => "未確定",
                1 => "確定済",
                null => "未設定",
                _ => "不明"
            };
        }

        public OrderView(int loginEmployeeId)
        {
            InitializeComponent();
            _loginEmployeeId = loginEmployeeId;
            gridDraft.ItemsSource = _draft;
            cmbClient.SelectionChanged += (_, _) => MarkDirty();
            cmbOffice.SelectionChanged += (_, _) => MarkDirty();
            dpOrderDate.SelectedDateChanged += (_, _) => MarkDirty();
            txtCharge.TextChanged += (_, _) => MarkDirty();
            cmbProduct.SelectionChanged += (_, _) => MarkDirty();
            txtQuantity.TextChanged += (_, _) => MarkDirty();
            ShowTotal();
        }

        private void MarkDirty()
        {
            if (_initialized && !IsBusy) _dirty = true;
        }

        public bool CanLeave()
        {
            if (IsBusy)
            {
                MessageBox.Show("処理が終わるまでお待ちください。");
                return false;
            }
            if (!CommitDraftEdits()) return false;
            return !_dirty || MessageBox.Show("未登録の入力を破棄しますか？", "確認",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        // メニューshow
        private async Task CheckPermissionAsync(SalesManagementContext db)
        {
            bool allowed = await db.MEmployees.AsNoTracking().AnyAsync(x =>
                x.EmId == _loginEmployeeId && x.EmFlag == 0 &&
                (x.Po.PoName == "管理者" || x.Po.PoName == "営業" || x.Po.PoName == "営業担当"));
            if (!allowed)
                throw new InvalidOperationException("受注管理を利用する権限がありません。再ログインしてください。");
        }

        private async Task RunAsync(Func<Task> action)
        {
            if (IsBusy) return;
            IsBusy = true;
            tabs.IsEnabled = false;
            headerPanel.IsEnabled = false;
            txtMessage.Text = "処理中です…";
            try { await action(); }
            catch (InvalidOperationException ex) { txtMessage.Text = ex.Message; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                txtMessage.Text = "処理に失敗しました。DB接続・制約・入力内容を確認してください。";
            }
            finally
            {
                IsBusy = false;
                tabs.IsEnabled = true;
                headerPanel.IsEnabled = true;
            }
        }

        private async Task InitializeAsync(SalesManagementContext db)
        {
            if (_initialized) return;
            await CheckPermissionAsync(db);
            var clients = await db.MClients.AsNoTracking().Where(x => x.ClFlag == 0)
                .OrderBy(x => x.ClId).ToListAsync();
            cmbClient.ItemsSource = clients;
            cmbOffice.ItemsSource = await db.MSalesOffices.AsNoTracking()
                .Where(x => x.SoFlag == 0).OrderBy(x => x.SoId).ToListAsync();
            cmbProduct.ItemsSource = await db.MProducts.AsNoTracking()
                .Where(x => x.PrFlag == 0).OrderBy(x => x.PrId).ToListAsync();
            var employee = await db.MEmployees.AsNoTracking()
                .SingleAsync(x => x.EmId == _loginEmployeeId);
            _loginEmployeeLabel = $"{employee.EmId}：{employee.EmName}";
            txtEmployee.Text = _loginEmployeeLabel;
            cmbOffice.SelectedValue = employee.SoId;
            dpOrderDate.SelectedDate = DateTime.Today;
            _initialized = true;
            _dirty = false;
        }

        private async void View_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized) return;
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await InitializeAsync(db);
                await LoadOrdersAsync(db);
            });
        }

        private void ShowTotal()
        {
            txtTotal.Text = $"合計：{_draft.Sum(x => x.Total):N0} 円";
        }

        private void AddLine_Click(object sender, RoutedEventArgs e)
        {
            if (!CommitDraftEdits()) return;
            if (!_initialized || IsBusy) return;
            if (cmbProduct.SelectedItem is not MProduct product ||
                !int.TryParse(txtQuantity.Text.Trim(), out int quantity) || quantity <= 0)
            {
                txtMessage.Text = "商品を選択し、数量を1以上の整数で入力してください。";
                return;
            }

            if (_draft.Any(x => x.ProductId == product.PrId))
            {
                txtMessage.Text = "同じ商品は追加済みです。明細を削除してから数量を指定して追加してください。";
                return;
            }

            decimal price = Convert.ToDecimal(product.Price);
            decimal total = price * quantity;
            if (price < 0 || total > 9999999999m)
            {
                txtMessage.Text = "商品価格または明細金額がDBの保存範囲外です。";
                return;
            }
            _draft.Add(new LineRow
            {
                ProductId = product.PrId,
                ProductName = product.PrName,
                UnitPrice = price,
                Quantity = quantity,
                Total = total
            });
            _dirty = true;
            ShowTotal();
            txtMessage.Text = "明細を追加しました。まだDBには登録されていません。";
        }

        private void ChangeQuantity_Click(object sender, RoutedEventArgs e)
        {
            if (IsBusy) return;
            if (gridDraft.SelectedItem is not LineRow line ||
                !int.TryParse(txtQuantity.Text.Trim(), out int quantity) || quantity <= 0)
            {
                txtMessage.Text = "明細を選択し、新しい数量を1以上の整数で入力してください。";
                return;
            }
            decimal total = line.UnitPrice * quantity;
            if (total > 9999999999m)
            {
                txtMessage.Text = "明細金額が保存範囲を超えています。";
                return;
            }
            line.Quantity = quantity;
            line.Total = total;
            gridDraft.Items.Refresh();
            _dirty = true;
            ShowTotal();
            txtMessage.Text = "数量を変更しました。保存するまではDBには反映されません。";
        }

        private void RemoveLine_Click(object sender, RoutedEventArgs e)
        {
            if (!CommitDraftEdits()) return;
            if (IsBusy) return;
            if (gridDraft.SelectedItem is not LineRow row)
            {
                txtMessage.Text = "削除する明細を選択してください。";
                return;
            }
            if (MessageBox.Show("入力中の明細を削除しますか？", "確認",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            _draft.Remove(row);
            _dirty = true;
            ShowTotal();
        }

        private void ResetDraft()
        {
            txtEmployee.Text = _loginEmployeeLabel;
            _editingId = null;
            _editStamp = "";
            btnSave.Content = "登録する";
            txtSavedId.Text = "自動採番";
            _draft.Clear();
            cmbClient.SelectedIndex = -1;
            cmbProduct.SelectedIndex = -1;
            txtCharge.Clear();
            txtQuantity.Text = "1";
            dpOrderDate.SelectedDate = DateTime.Today;
            _dirty = false;
            ShowTotal();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (!CanLeave()) return;
            ResetDraft();
            txtMessage.Text = "登録フォームをクリアしました。";
        }

        // 登録は受注と受注を save
        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!CommitDraftEdits()) return;
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await InitializeAsync(db);
                await CheckPermissionAsync(db);
                if (cmbClient.SelectedValue is not int clientId ||
                    cmbOffice.SelectedValue is not int officeId ||
                    !dpOrderDate.SelectedDate.HasValue || _draft.Count == 0)
                    throw new InvalidOperationException("顧客・営業所・受注日と1件以上の明細が必要です。");

                if (!await db.MClients.AnyAsync(x => x.ClId == clientId && x.ClFlag == 0) ||
                    !await db.MSalesOffices.AnyAsync(x => x.SoId == officeId && x.SoFlag == 0))
                    throw new InvalidOperationException("顧客または営業所が利用できません。一度画面を開き直してください。");
                if (_draft.Any(x => x.Quantity <= 0 || x.Total < 0 || x.Total > 9999999999m))
                    throw new InvalidOperationException("数量・明細金額を確認してください。");
                if (txtCharge.Text.Trim().Length > 50)
                    throw new InvalidOperationException("顧客担当者名は50文字以内で入力してください。");

                foreach (var line in _draft)
                {
                    var product = await db.MProducts.AsNoTracking()
                        .SingleOrDefaultAsync(x => x.PrId == line.ProductId && x.PrFlag == 0);
                    if (product == null || (!_editingId.HasValue && Convert.ToDecimal(product.Price) != line.UnitPrice))
                        throw new InvalidOperationException("商品の状態または価格が変わっています。画面を開き直して入力してください。");
                }

                string operation = _editingId.HasValue ? "更新" : "登録";
                if (MessageBox.Show($"明細{_draft.Count}件、合計{_draft.Sum(x => x.Total):N0}円の受注を{operation}しますか？",
                    "受注" + operation, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    txtMessage.Text = $"{operation}をキャンセルしました。";
                    return;
                }

                using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                TOrder order;
                if (_editingId.HasValue)
                {
                    order = await db.TOrders.SingleOrDefaultAsync(x => x.OrId == _editingId.Value && x.OrFlag == 0)
                        ?? throw new InvalidOperationException("対象の受注が見つかりません。");
                    if (order.OrStateFlag != 0 || await db.TChumons.AnyAsync(x => x.OrId == order.OrId))
                        throw new InvalidOperationException("確定済み・送信済みの受注は編集できません。");
                    var oldLines = await db.TOrderDetails.Where(x => x.OrId == order.OrId).ToListAsync();
                    if (Stamp(order, oldLines) != _editStamp)
                        throw new InvalidOperationException("別の操作で受注が変更されました。編集し直してください。");
                    db.TOrderDetails.RemoveRange(oldLines);
                    order.ClId = clientId;
                    order.SoId = officeId;
                    order.OrDate = dpOrderDate.SelectedDate.Value.Date;
                    order.ClCharge = txtCharge.Text.Trim();
                    
                }
                else
                {
                    order = new TOrder
                    {
                        ClId = clientId,
                        SoId = officeId,
                        EmId = _loginEmployeeId,
                        OrDate = dpOrderDate.SelectedDate.Value.Date,
                        ClCharge = txtCharge.Text.Trim(),
                        OrFlag = 0,
                        OrStateFlag = 0
                    };
                    db.TOrders.Add(order);
                    await db.SaveChangesAsync();
                }
                foreach (var line in _draft)
                    db.TOrderDetails.Add(new TOrderDetail
                    {
                        OrId = order.OrId,
                        PrId = line.ProductId,
                        OrQuantity = line.Quantity,
                        OrTotalPrice = line.Total
                    });
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
                ResetDraft();
                txtSavedId.Text = order.OrId.ToString();
                ShowPage(1);
                await ReloadAfterSaveAsync(db, $"受注ID：{order.OrId} を{operation}しました。");
            });
        }

        private async Task LoadOrdersAsync(SalesManagementContext db,
            string idPrefix = "", int? clientId = null, DateTime? from = null, DateTime? to = null,
            int? officeId = null, int? state = null, int? exactId = null)
        {
            var query = db.TOrders.AsNoTracking().Where(x => x.OrFlag == 0);
            if (exactId.HasValue) query = query.Where(x => x.OrId == exactId.Value);
            if (idPrefix != "") query = query.Where(x => x.OrId.ToString().StartsWith(idPrefix));
            if (clientId.HasValue) query = query.Where(x => x.ClId == clientId.Value);
            if (officeId.HasValue) query = query.Where(x => x.SoId == officeId.Value);
            if (state.HasValue) query = query.Where(x => x.OrStateFlag == state.Value);
            if (from.HasValue) query = query.Where(x => x.OrDate >= from.Value);
            if (to.HasValue && to.Value.Date < DateTime.MaxValue.Date)
            {
                var endExclusive = to.Value.Date.AddDays(1);
                query = query.Where(x => x.OrDate < endExclusive);
            }
            _selectedId = null;
            txtSelected.Text = "受注を選択すると、ここに選択内容を表示します。";
            gridDetails.ItemsSource = null;
            txtDetailTotal.Text = "合計：—";
            gridOrders.ItemsSource = await query.OrderByDescending(x => x.OrId)
                .Select(x => new OrderRow
                {
                    Id = x.OrId,
                    Date = x.OrDate,
                    Client = x.Cl.ClName,
                    Office = x.So.SoName,
                    Employee = x.Em.EmName,
                    EmployeeId = x.EmId,
                    Charge = x.ClCharge ?? "",
                    StateFlag = x.OrStateFlag,
                    Total = x.TOrderDetails.Sum(d => (decimal?)d.OrTotalPrice) ?? 0m
                }).ToListAsync();
            txtMessage.Text = $"{gridOrders.Items.Count}件を表示しています。";
        }

        private async Task ReloadAfterSaveAsync(SalesManagementContext db, string message)
        {
            try { await LoadOrdersAsync(db); txtMessage.Text = message; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                _selectedId = null;
                gridOrders.ItemsSource = null;
                gridDetails.ItemsSource = null;
                txtMessage.Text = message + " 一覧の更新に失敗しました。一覧表示を押してください。";
            }
        }

        private async void List_Click(object sender, RoutedEventArgs e)
        {
            if (!CanLeave()) return;
            ResetDraft();
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await InitializeAsync(db);
                await CheckPermissionAsync(db);
                await LoadOrdersAsync(db);
                ShowPage(1);
            });
        }

        private async void Orders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsBusy) return;
            if (gridOrders.SelectedItem is not OrderRow row)
            {
                _selectedId = null;
                txtSelected.Text = "受注を選択してください。";
                gridDetails.ItemsSource = null;
                txtDetailTotal.Text = "合計：—";
                return;
            }
            _selectedId = null;
            gridDetails.ItemsSource = null;
            txtDetailTotal.Text = "合計：—";
            await RunAsync(() => LoadSelectedDetailsAsync(row));
        }

        private async Task LoadSelectedDetailsAsync(OrderRow row)
        {
            using var db = new SalesManagementContext();
            await CheckPermissionAsync(db);
            gridDetails.ItemsSource = await db.TOrderDetails.AsNoTracking()
                .Where(x => x.OrId == row.Id)
                .OrderBy(x => x.OrDetailId)
                .Select(x => new LineRow
                {
                    ProductId = x.PrId,
                    ProductName = x.Pr.PrName,
                    Quantity = x.OrQuantity,
                    Total = x.OrTotalPrice
                }).ToListAsync();
            txtDetailTotal.Text = $"合計：{row.Total:N0} 円";
            txtMessage.Text = $"受注ID：{row.Id} — {row.State}";
            _selectedId = row.Id;
            txtSelected.Text = $"受注ID：{row.Id}　顧客：{row.Client}　営業所：{row.Office}\n社員：{row.EmployeeId} {row.Employee}　顧客担当者：{row.Charge}　受注日：{row.Date:yyyy/MM/dd}　状態：{row.State}";
        }

        public async Task<bool> OpenOrderAsync(int orderId)
        {
            if (IsBusy) { DisplayLoadError = "処理が終わるまでお待ちください。"; return false; }
            bool opened = false;
            DisplayLoadError = "";
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await CheckPermissionAsync(db);
                await InitializeAsync(db);
                await LoadOrdersAsync(db, exactId: orderId);
                var row = gridOrders.Items.OfType<OrderRow>().SingleOrDefault()
                    ?? throw new InvalidOperationException("対象の受注が見つかりません。非表示にされた可能性があります。");
                gridOrders.SelectedItem = row;
                await LoadSelectedDetailsAsync(row);
                ShowPage(1);
                opened = true;
            });
            if (!opened) DisplayLoadError = txtMessage.Text;
            return opened;
        }

        private bool CommitDraftEdits()
        {
            return gridDraft.CommitEdit(DataGridEditingUnit.Cell, true)
                && gridDraft.CommitEdit(DataGridEditingUnit.Row, true);
        }

        private void Draft_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit || e.Row.Item is not LineRow line
                || e.EditingElement is not TextBox input) return;
            if (!int.TryParse(input.Text.Trim(), out int quantity) || quantity <= 0)
            {
                e.Cancel = true;
                txtMessage.Text = "数量は1以上の整数で入力してください。";
                return;
            }
            decimal total = line.UnitPrice * quantity;
            if (total > 9999999999m)
            {
                e.Cancel = true;
                txtMessage.Text = "明細金額が上限を超えています。数量を減らしてください。";
                return;
            }
            if (line.Quantity != quantity)
            {
                line.Quantity = quantity;
                line.Total = total;
                _dirty = true;
                ShowTotal();
            }
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            if (IsBusy) return;
            gridDraft.CancelEdit(DataGridEditingUnit.Cell);
            gridDraft.CancelEdit(DataGridEditingUnit.Row);
            if (!CanLeave()) return;
            ResetDraft();
            ShowPage(1);
            txtMessage.Text = "入力を取り消しました。データベースは変更していません。";
        }

        private void BackToList_Click(object sender, RoutedEventArgs e)
        {
            if (!IsBusy) ShowPage(1);
        }

        private void ShowPage(int page)
        {
            txtHeading.Text = page == 0 ? (_editingId.HasValue ? "受注編集" : "受注登録") : page == 2 ? "受注検索" : "受注管理";
            pageRegister.Visibility = page == 0 ? Visibility.Visible : Visibility.Collapsed;
            pageList.Visibility = page == 1 ? Visibility.Visible : Visibility.Collapsed;
            txtPageTitle.Text = page == 0
                ? (_editingId.HasValue ? "受注編集 — 未確定の受注を変更します。" : "新規登録 — 顧客と商品を入力してください。")
                : page == 1 ? "受注一覧 — 受注を選択して編集・確定できます。" : "受注検索 — 検索結果は一覧に表示します。";
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            if (!CanLeave()) return;
            ResetDraft();
            ShowPage(0);
            txtMessage.Text = "新しい受注を入力してください。";
        }

        private void ShowSearch_Click(object sender, RoutedEventArgs e)
        {
            if (!CanLeave()) return;
            ResetDraft();
            ShowPage(1);
            SearchRequested?.Invoke(this, EventArgs.Empty);
        }

        private string Stamp(TOrder order, IEnumerable<TOrderDetail> lines)
        {
            return JsonSerializer.Serialize(new
            {
                order.ClId,
                order.SoId,
                order.OrDate,
                order.ClCharge,
                Lines = lines.OrderBy(x => x.OrDetailId)
                    .Select(x => new { x.OrDetailId, x.PrId, x.OrQuantity, x.OrTotalPrice }).ToArray()
            });
        }

        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            if (gridOrders.SelectedItem is not OrderRow row)
            {
                txtMessage.Text = "編集する受注を選択してください。";
                return;
            }
            if (row.StateFlag != 0)
            {
                txtMessage.Text = "確定済みの受注は編集できません。";
                return;
            }
            if (!CanLeave()) return;
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await CheckPermissionAsync(db);
                var order = await db.TOrders.AsNoTracking().SingleOrDefaultAsync(x => x.OrId == row.Id && x.OrFlag == 0)
                    ?? throw new InvalidOperationException("対象の受注が見つかりません。");
                if (order.OrStateFlag != 0 || await db.TChumons.AnyAsync(x => x.OrId == order.OrId))
                    throw new InvalidOperationException("確定済み・送信済みの受注は編集できません。");
                var lines = await db.TOrderDetails.AsNoTracking().Where(x => x.OrId == row.Id)
                    .Include(x => x.Pr).ToListAsync();
                ResetDraft();
                _editingId = order.OrId;
                _editStamp = Stamp(order, lines);
                txtSavedId.Text = order.OrId.ToString();
                cmbClient.SelectedValue = order.ClId;
                cmbOffice.SelectedValue = order.SoId;
                dpOrderDate.SelectedDate = order.OrDate;
                txtCharge.Text = order.ClCharge ?? "";
                var employee = await db.MEmployees.AsNoTracking().SingleAsync(x => x.EmId == order.EmId);
                txtEmployee.Text = $"{employee.EmId}：{employee.EmName}";
                foreach (var line in lines)
                    _draft.Add(new LineRow
                    {
                        ProductId = line.PrId,
                        ProductName = line.Pr.PrName,
                        Quantity = line.OrQuantity,
                        Total = line.OrTotalPrice,
                        UnitPrice = line.OrQuantity > 0 ? line.OrTotalPrice / line.OrQuantity : 0
                    });
                btnSave.Content = "更新する";
                _dirty = false;
                ShowTotal();
                ShowPage(0);
                txtMessage.Text = "数量は明細表で直接編集できます。変更後に更新するを押してください。";
            });
        }

        private async void Hide_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedId.HasValue)
            {
                txtMessage.Text = "非表示にする受注を選択してください。";
                return;
            }
            int id = _selectedId.Value;
            if (MessageBox.Show($"受注ID：{id} を非表示にしますか？", "確認",
                MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                await CheckPermissionAsync(db);
                var order = await db.TOrders.SingleOrDefaultAsync(x => x.OrId == id && x.OrFlag == 0)
                    ?? throw new InvalidOperationException("対象の受注が見つかりません。");
                order.OrFlag = 2;
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                await ReloadAfterSaveAsync(db, "非表示にしました。注文側のレコードは変更しません。");
            });
        }

        // 確定保存、注文送信
        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (gridOrders.SelectedItem is not OrderRow row || !_selectedId.HasValue)
            {
                txtMessage.Text = "確定する受注を選択してください。";
                return;
            }
            if (row.StateFlag != 0)
            {
                txtMessage.Text = "この受注は確定済みです。";
                return;
            }
            int id = _selectedId.Value;
            if (MessageBox.Show($"受注ID：{id} を確定し、注文管理へ送信しますか？",
                "受注確定", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                
                using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                await CheckPermissionAsync(db);
                var order = await db.TOrders.SingleOrDefaultAsync(x => x.OrId == id && x.OrFlag == 0)
                    ?? throw new InvalidOperationException("対象の受注が見つかりません。");
                if (order.OrStateFlag != 0 || await db.TChumons.AnyAsync(x => x.OrId == id))
                    throw new InvalidOperationException("この受注は確定済み、または注文に送信済みです。");
                var details = await db.TOrderDetails.AsNoTracking().Where(x => x.OrId == id).ToListAsync();
                if (details.Count == 0)
                    throw new InvalidOperationException("明細がない受注は確定できません。");

                var chumon = new TChumon
                {
                    OrId = order.OrId,
                    SoId = order.SoId,
                    ClId = order.ClId,
                    ChDate = DateTime.Today,
                    ChFlag = 0,
                    ChStateFlag = 0
                };
                db.TChumons.Add(chumon);

                // nullを確認
                if (db.Entry(chumon).Property("EmId").Metadata.IsNullable == false)
                    throw new InvalidOperationException("TChumon.EmIdがNULLを許可していません。設計書とDBモデルを確認してください。");
                db.Entry(chumon).Property("EmId").CurrentValue = null;
                await db.SaveChangesAsync();
                foreach (var detail in details)
                    db.TChumonDetails.Add(new TChumonDetail
                    {
                        ChId = chumon.ChId,
                        PrId = detail.PrId,
                        ChQuantity = detail.OrQuantity
                    });
                order.OrStateFlag = 1;
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                await ReloadAfterSaveAsync(db, $"受注ID：{id} を確定しました。注文ID：{chumon.ChId}");
            });
        }
    }
}

