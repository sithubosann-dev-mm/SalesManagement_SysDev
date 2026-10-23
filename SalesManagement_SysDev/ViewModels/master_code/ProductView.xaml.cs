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

namespace SalesManagement_SysDev
{
    public partial class ProductView : UserControl
    {
        private enum PendingOperation { None, Register, Update, Hide, Delete }
        private PendingOperation _pending;
        private string _pendingStamp = "";
        private readonly int _loginEmployeeId;
        private bool _initialized;
        private bool _suppressSelection;
        private int? _editingId;
        private string _entityStamp = "";
        private string _baseline = "";
        private List<MSmallClassification> _small = new();
        public bool IsBusy { get; private set; }
        public string EditLoadError { get; private set; } = "";
        public event EventHandler? SearchRequested;

        public sealed class ProductRow
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Maker { get; set; } = "";
            public decimal Price { get; set; }
            public string Major { get; set; } = "";
            public string Small { get; set; } = "";
            public string Model { get; set; } = "";
            public string Color { get; set; } = "";
            public DateTime ReleaseDate { get; set; }
            public int SafetyStock { get; set; }
        }

        public ProductView(int loginEmployeeId)
        {
            InitializeComponent();
            _loginEmployeeId = loginEmployeeId;
            btnUpdate.IsEnabled = btnHide.IsEnabled = btnDelete.IsEnabled = false;
            mainPanel.AddHandler(TextBox.TextChangedEvent, new TextChangedEventHandler((_, _) => CheckPendingInput()));
            cmbMaker.SelectionChanged += (_, _) => CheckPendingInput();
            cmbMajor.SelectionChanged += (_, _) => CheckPendingInput();
            cmbSmall.SelectionChanged += (_, _) => CheckPendingInput();
            dpRelease.SelectedDateChanged += (_, _) => CheckPendingInput();
        }

        internal static async Task CheckPermissionAsync(SalesManagementContext db, int employeeId)
        {
            bool allowed = await db.MEmployees.AsNoTracking().AnyAsync(x =>
                x.EmId == employeeId && x.EmFlag == 0 &&
                (x.Po.PoName == "管理者" || x.Po.PoName == "物流" || x.Po.PoName == "物流担当"));
            if (!allowed) throw new InvalidOperationException("商品管理を利用する権限がありません。再ログインしてください。");
        }

        internal static IQueryable<ProductRow> Rows(SalesManagementContext db,
            string prefix = "", string name = "", int? maker = null, int? major = null, int? small = null)
        {
            var query = db.MProducts.AsNoTracking().Where(x => x.PrFlag == 0);
            if (prefix != "") query = query.Where(x => x.PrId.ToString().StartsWith(prefix));
            if (name != "") query = query.Where(x => x.PrName.Contains(name));
            if (maker.HasValue) query = query.Where(x => x.MaId == maker.Value);
            if (major.HasValue) query = query.Where(x => x.Sc.McId == major.Value);
            if (small.HasValue) query = query.Where(x => x.ScId == small.Value);
            return query.OrderBy(x => x.PrId).Select(x => new ProductRow
            {
                Id = x.PrId,
                Name = x.PrName,
                Maker = x.Ma.MaName,
                Price = x.Price,
                Major = x.Sc.Mc.McName,
                Small = x.Sc.ScName,
                Model = x.PrModelNumber,
                Color = x.PrColor,
                ReleaseDate = x.PrReleaseDate,
                SafetyStock = x.PrSafetyStock
            });
        }

        private string FormStamp() => JsonSerializer.Serialize(new
        {
            Name = txtName.Text,
            Price = txtPrice.Text,
            Maker = cmbMaker.SelectedValue,
            Major = cmbMajor.SelectedValue,
            Small = cmbSmall.SelectedValue,
            Model = txtModel.Text,
            Color = txtColor.Text,
            Release = dpRelease.Text,
            Jan = txtJan.Text,
            Safety = txtSafety.Text,
            Reason = txtReason.Text
        });

        private static string EntityStamp(MProduct p) => JsonSerializer.Serialize(new
        {
            p.PrId,
            p.MaId,
            p.PrName,
            p.Price,
            p.PrJcode,
            p.PrSafetyStock,
            p.ScId,
            p.PrModelNumber,
            p.PrColor,
            p.PrReleaseDate,
            p.PrFlag,
            p.PrHidden
        });

        public bool CanLeave()
        {
            if (IsBusy) { MessageBox.Show("処理が終わるまでお待ちください。"); return false; }
            return !_initialized || (_baseline == FormStamp() && _pending == PendingOperation.None) ||
                MessageBox.Show("未保存の入力を破棄しますか？", "確認", MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        private async Task RunAsync(Func<Task> action)
        {
            if (IsBusy) return;
            IsBusy = true;
            mainPanel.IsEnabled = false;
            txtMessage.Text = "処理中です…";
            try { await action(); }
            catch (InvalidOperationException ex) { txtMessage.Text = ex.Message; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                txtMessage.Text = "処理に失敗しました。データベース接続と入力内容を確認してください。";
            }
            finally { IsBusy = false; mainPanel.IsEnabled = true; }
        }

        private async Task InitializeAsync(SalesManagementContext db)
        {
            if (_initialized) return;
            cmbMaker.ItemsSource = await db.MMakers.AsNoTracking().Where(x => x.MaFlag == 0)
                .OrderBy(x => x.MaId).ToListAsync();
            cmbMajor.ItemsSource = await db.MMajorClassifications.AsNoTracking().Where(x => x.McFlag == 0)
                .OrderBy(x => x.McId).ToListAsync();
            _small = await db.MSmallClassifications.AsNoTracking().Where(x => x.ScFlag == 0 && x.Mc.McFlag == 0)
                .OrderBy(x => x.ScId).ToListAsync();
            _initialized = true;
            ResetForm();
        }

        private async void View_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized) return;
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await CheckPermissionAsync(db, _loginEmployeeId);
                await InitializeAsync(db);
                await LoadRowsAsync(db);
            });
        }

        private void Major_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSmall == null) return;
            cmbSmall.ItemsSource = cmbMajor.SelectedValue is int major
                ? _small.Where(x => x.McId == major).ToList() : new List<MSmallClassification>();
            cmbSmall.SelectedIndex = -1;
        }

        private void ClearSelection()
        {
            _suppressSelection = true;
            try { gridProducts.SelectedItem = null; }
            finally { _suppressSelection = false; }
        }

        private void ResetForm()
        {
            ClearPending();
            _editingId = null;
            _entityStamp = "";
            txtId.Text = "自動採番";
            txtName.Clear(); txtPrice.Clear(); txtJan.Clear(); txtModel.Clear(); txtColor.Clear(); txtReason.Clear();
            txtSafety.Text = "0";
            cmbMaker.SelectedIndex = cmbMajor.SelectedIndex = cmbSmall.SelectedIndex = -1;
            dpRelease.SelectedDate = DateTime.Today;
            txtMode.Text = "新規商品を登録できます。";
            btnRegister.IsEnabled = true;
            btnUpdate.IsEnabled = btnHide.IsEnabled = btnDelete.IsEnabled = false;
            _baseline = FormStamp();
        }

        private async Task LoadRowsAsync(SalesManagementContext db)
        {
            gridProducts.ItemsSource = await Rows(db).ToListAsync();
            txtMessage.Text = $"{gridProducts.Items.Count}件を表示しています。";
        }

        private async Task LoadForEditAsync(SalesManagementContext db, int id)
        {
            var p = await db.MProducts.AsNoTracking().Include(x => x.Sc)
                .SingleOrDefaultAsync(x => x.PrId == id && x.PrFlag == 0)
                ?? throw new InvalidOperationException("商品が見つかりません。非表示にされた可能性があります。");
            // All database reads finish before replacing the current form.
            ClearPending();
            _editingId = p.PrId;
            _entityStamp = EntityStamp(p);
            txtId.Text = p.PrId.ToString();
            txtName.Text = p.PrName;
            txtPrice.Text = p.Price.ToString("0", CultureInfo.InvariantCulture);
            cmbMaker.SelectedValue = p.MaId;
            cmbMajor.SelectedValue = p.Sc.McId;
            cmbSmall.SelectedValue = p.ScId;
            txtModel.Text = p.PrModelNumber;
            txtColor.Text = p.PrColor;
            txtJan.Text = p.PrJcode ?? "";
            txtSafety.Text = p.PrSafetyStock.ToString(CultureInfo.InvariantCulture);
            dpRelease.SelectedDate = p.PrReleaseDate;
            txtReason.Clear();
            txtMode.Text = $"商品ID：{p.PrId} を編集しています。";
            btnRegister.IsEnabled = false;
            btnUpdate.IsEnabled = btnHide.IsEnabled = btnDelete.IsEnabled = true;
            _baseline = FormStamp();
            txtMessage.Text = "操作を選択してから確定を押してください。非表示は理由の入力が必要です。";
            if (cmbMaker.SelectedIndex < 0 || cmbMajor.SelectedIndex < 0 || cmbSmall.SelectedIndex < 0)
                txtMessage.Text = "メーカーまたは分類が非表示です。更新する場合は利用可能な項目を選び直してください。";
        }

        private async void Products_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsBusy || _suppressSelection) return;
            if (!CanLeave())
            {
                _suppressSelection = true;
                try { gridProducts.SelectedItem = e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null; }
                finally { _suppressSelection = false; }
                return;
            }
            if (gridProducts.SelectedItem is not ProductRow row) return;
            bool loaded = false;
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await CheckPermissionAsync(db, _loginEmployeeId);
                await InitializeAsync(db);
                await LoadForEditAsync(db, row.Id);
                loaded = true;
            });
            if (!loaded)
            {
                // A failed selection must never leave an old editable product under a new highlighted row.
                string error = txtMessage.Text;
                ClearSelection(); ResetForm(); txtMessage.Text = error;
            }
        }

        public async Task<bool> OpenForEditAsync(int productId)
        {
            if (IsBusy || !CanLeave()) return false;
            bool opened = false;
            EditLoadError = "";
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await CheckPermissionAsync(db, _loginEmployeeId);
                await InitializeAsync(db);
                await LoadRowsAsync(db);
                var row = gridProducts.Items.OfType<ProductRow>().SingleOrDefault(x => x.Id == productId)
                    ?? throw new InvalidOperationException("商品が見つかりません。非表示にされた可能性があります。");
                await LoadForEditAsync(db, productId);
                gridProducts.SelectedItem = row;
                opened = true;
            });
            if (!opened)
            {
                EditLoadError = txtMessage.Text;
                ClearSelection(); ResetForm(); txtMessage.Text = EditLoadError;
            }
            return opened;
        }

        private MProduct ReadInput()
        {
            string name = txtName.Text.Trim(), model = txtModel.Text.Trim(), color = txtColor.Text.Trim();
            if (name.Length == 0 || name.Length > 50 || model.Length == 0 || model.Length > 20 || color.Length == 0 || color.Length > 20)
                throw new InvalidOperationException("商品名は1～50文字、型番と色は1～20文字で入力してください。");
            if (cmbMaker.SelectedValue is not int maker || cmbMajor.SelectedValue is not int || cmbSmall.SelectedValue is not int small)
                throw new InvalidOperationException("メーカー・大分類・小分類を選択してください。");
            string priceText = txtPrice.Text.Trim();
            if (priceText.Length == 0 || priceText.Any(c => c < '0' || c > '9') ||
                !decimal.TryParse(priceText, NumberStyles.None, CultureInfo.InvariantCulture, out decimal price) || price > 999999999m)
                throw new InvalidOperationException("価格は0～999999999の半角整数で入力してください。");
            string safetyText = txtSafety.Text.Trim();
            if (safetyText.Length == 0 || safetyText.Any(c => c < '0' || c > '9') || !int.TryParse(safetyText, out int safety))
                throw new InvalidOperationException("安全在庫数は0～2147483647の半角整数で入力してください。");
            string jan = txtJan.Text.Trim();
            if (jan.Length > 13 || jan.Any(c => c < '0' || c > '9'))
                throw new InvalidOperationException("JANコードは半角数字13桁以内で入力してください。");
            if (!dpRelease.SelectedDate.HasValue ||
                !DateTime.TryParse(dpRelease.Text, CultureInfo.GetCultureInfo("ja-JP"), DateTimeStyles.None, out DateTime parsedRelease) ||
                parsedRelease.Date != dpRelease.SelectedDate.Value.Date)
                throw new InvalidOperationException("発売日を正しい日付で入力してください。");
            return new MProduct
            {
                MaId = maker,
                ScId = small,
                PrName = name,
                Price = price,
                PrSafetyStock = safety,
                PrModelNumber = model,
                PrColor = color,
                PrReleaseDate = dpRelease.SelectedDate.Value.Date,
                PrJcode = jan == "" ? null : jan,
                PrFlag = 0
            };
        }

        private async Task ValidateReferencesAsync(SalesManagementContext db, MProduct input)
        {
            if (!await db.MMakers.AnyAsync(x => x.MaId == input.MaId && x.MaFlag == 0) ||
                !await db.MSmallClassifications.AnyAsync(x => x.ScId == input.ScId && x.ScFlag == 0 && x.Mc.McFlag == 0))
                throw new InvalidOperationException("選択したメーカーまたは分類が利用できません。画面を開き直してください。");
        }

        private async Task SaveAsync(bool update)
        {
            if (IsBusy) return;
            if (update != _editingId.HasValue) { txtMessage.Text = "登録または更新の対象を確認してください。"; return; }
            await RunAsync(async () =>
            {
                MProduct input = ReadInput();
                string operation = update ? "更新" : "登録";
                if (!string.IsNullOrWhiteSpace(txtReason.Text))
                    throw new InvalidOperationException("非表示理由は非表示時のみ使用します。登録・更新する場合は理由を空欄にしてください。");
                if (MessageBox.Show($"商品「{input.PrName}」を{operation}しますか？", "確認",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                { txtMessage.Text = operation + "をキャンセルしました。"; return; }
                using var db = new SalesManagementContext();
                using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                await CheckPermissionAsync(db, _loginEmployeeId);
                await ValidateReferencesAsync(db, input);
                MProduct product;
                if (update)
                {
                    product = await db.MProducts.SingleOrDefaultAsync(x => x.PrId == _editingId!.Value && x.PrFlag == 0)
                        ?? throw new InvalidOperationException("更新する商品が見つかりません。");
                    if (EntityStamp(product) != _entityStamp)
                        throw new InvalidOperationException("別の操作で商品が変更されました。一覧表示から選択し直してください。");
                    product.PrName = input.PrName; product.MaId = input.MaId; product.ScId = input.ScId;
                    product.Price = input.Price; product.PrJcode = input.PrJcode; product.PrSafetyStock = input.PrSafetyStock;
                    product.PrModelNumber = input.PrModelNumber; product.PrColor = input.PrColor;
                    product.PrReleaseDate = input.PrReleaseDate;
                }
                else
                {
                    product = input;
                    db.MProducts.Add(product);
                    // EF inserts the product first and uses its generated ID for the stock row.
                    db.TStocks.Add(new TStock { Pr = product, StQuantity = 0, StFlag = 0 });
                }
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                await AfterSaveAsync(db, $"商品ID：{product.PrId} を{operation}しました。" + (update ? "" : "在庫数0で在庫を作成しました。"));
            });
        }

        private async Task AfterSaveAsync(SalesManagementContext db, string message)
        {
            ClearSelection(); ResetForm();
            try { await LoadRowsAsync(db); txtMessage.Text = message; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                gridProducts.ItemsSource = null;
                txtMessage.Text = message + " 一覧の再取得に失敗しました。一覧表示を押してください。";
            }
        }

        private void ClearPending()
        {
            _pending = PendingOperation.None;
            _pendingStamp = "";
            btnConfirm.IsEnabled = false;
            txtPending.Text = "操作を選択してから確定を押してください。";
        }

        private void CheckPendingInput()
        {
            if (IsBusy || _pending == PendingOperation.None) return;
            if (_pendingStamp != FormStamp())
            {
                ClearPending();
                txtPending.Text = "入力が変更されました。操作を選び直してください。";
            }
        }

        private void Stage(PendingOperation operation)
        {
            if (IsBusy) return;
            ClearPending();
            try
            {
                if (operation == PendingOperation.Register)
                {
                    if (_editingId.HasValue) throw new InvalidOperationException("新規登録する場合はクリアを押してください。");
                    ReadInput();
                }
                else if (!_editingId.HasValue)
                    throw new InvalidOperationException("対象の商品を一覧から選択してください。");
                if (operation == PendingOperation.Update) ReadInput();
                if ((operation == PendingOperation.Register || operation == PendingOperation.Update)
                    && !string.IsNullOrWhiteSpace(txtReason.Text))
                    throw new InvalidOperationException("登録・更新する場合は非表示理由を空欄にしてください。");
                if (operation == PendingOperation.Hide && (txtReason.Text.Trim().Length == 0 || txtReason.Text.Trim().Length > 1000))
                    throw new InvalidOperationException("非表示理由を1～1000文字で入力してください。");
                _pending = operation;
                _pendingStamp = FormStamp();
                btnConfirm.IsEnabled = true;
                string label = operation == PendingOperation.Register ? "登録" : operation == PendingOperation.Update ? "更新"
                    : operation == PendingOperation.Hide ? "非表示" : "削除";
                txtPending.Text = $"{label}を選択しました。確定を押してください。まだ保存していません。";
                txtMessage.Text = operation == PendingOperation.Delete
                    ? "削除は元に戻せません。関連する業務データや在庫がある商品は削除できません。"
                    : "確定後の確認画面でYesを選ぶと処理します。";
            }
            catch (InvalidOperationException ex) { txtMessage.Text = ex.Message; }
        }

        private void Register_Click(object sender, RoutedEventArgs e) => Stage(PendingOperation.Register);
        private void Update_Click(object sender, RoutedEventArgs e) => Stage(PendingOperation.Update);
        private void Hide_Click(object sender, RoutedEventArgs e) => Stage(PendingOperation.Hide);
        private void Delete_Click(object sender, RoutedEventArgs e) => Stage(PendingOperation.Delete);

        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (IsBusy || _pending == PendingOperation.None) return;
            if (_pendingStamp != FormStamp())
            { ClearPending(); txtMessage.Text = "入力が変更されました。操作を選び直してください。"; return; }
            var operation = _pending;
            if (operation == PendingOperation.Register) await SaveAsync(false);
            else if (operation == PendingOperation.Update) await SaveAsync(true);
            else if (operation == PendingOperation.Hide) await PerformHideAsync();
            else if (operation == PendingOperation.Delete) await PerformDeleteAsync();
        }

        private async Task PerformHideAsync()
        {
            if (IsBusy || !_editingId.HasValue) return;
            int id = _editingId.Value;
            string reason = txtReason.Text.Trim();
            if (reason.Length == 0 || reason.Length > 1000)
            { txtMessage.Text = "非表示理由を1～1000文字で入力してください。"; return; }
            if (MessageBox.Show($"商品ID：{id} を非表示にしますか？\n商品情報の入力中の変更は保存しません。\n理由：{reason}",
                "非表示の確認", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                await CheckPermissionAsync(db, _loginEmployeeId);
                var product = await db.MProducts.SingleOrDefaultAsync(x => x.PrId == id && x.PrFlag == 0)
                    ?? throw new InvalidOperationException("非表示にする商品が見つかりません。");
                if (EntityStamp(product) != _entityStamp)
                    throw new InvalidOperationException("商品が変更されました。一覧表示から選択し直してください。");
                product.PrFlag = 2;
                product.PrHidden = reason;
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                await AfterSaveAsync(db, $"商品ID：{id} を非表示にしました。在庫数は変更していません。");
            });
        }

        private async Task PerformDeleteAsync()
        {
            if (IsBusy || !_editingId.HasValue) return;
            int id = _editingId.Value;
            if (MessageBox.Show($"商品ID：{id} を完全に削除しますか？\n在庫数0の在庫レコードも削除します。\n商品情報の入力中の変更は保存しません。この操作は元に戻せません。",
                "削除の確認", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                await CheckPermissionAsync(db, _loginEmployeeId);
                var product = await db.MProducts.SingleOrDefaultAsync(x => x.PrId == id && x.PrFlag == 0)
                    ?? throw new InvalidOperationException("削除する商品が見つかりません。");
                if (EntityStamp(product) != _entityStamp)
                    throw new InvalidOperationException("商品が変更されました。一覧表示から選択し直してください。");
                bool used = await db.TOrderDetails.AnyAsync(x => x.PrId == id)
                    || await db.TChumonDetails.AnyAsync(x => x.PrId == id)
                    || await db.TArrivalDetails.AnyAsync(x => x.PrId == id)
                    || await db.THattyuDetails.AnyAsync(x => x.PrId == id)
                    || await db.TSaleDetails.AnyAsync(x => x.PrId == id)
                    || await db.TShipmentDetails.AnyAsync(x => x.PrId == id)
                    || await db.TSyukkoDetails.AnyAsync(x => x.PrId == id)
                    || await db.TWarehousingDetails.AnyAsync(x => x.PrId == id);
                if (used) throw new InvalidOperationException("業務データで使用されている商品は削除できません。非表示を使用してください。");
                var stocks = await db.TStocks.Where(x => x.PrId == id).ToListAsync();
                if (stocks.Any(x => x.StQuantity != 0))
                    throw new InvalidOperationException("在庫数が0ではない商品は削除できません。");
                db.TStocks.RemoveRange(stocks);
                db.MProducts.Remove(product);
                await db.SaveChangesAsync();
                await tx.CommitAsync();
                await AfterSaveAsync(db, $"商品ID：{id} を削除しました。");
            });
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (!CanLeave()) return;
            ClearSelection(); ResetForm(); txtMessage.Text = "新規登録フォームに戻しました。";
        }

        private async void List_Click(object sender, RoutedEventArgs e)
        {
            if (!CanLeave()) return;
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await CheckPermissionAsync(db, _loginEmployeeId);
                await InitializeAsync(db);
                ClearSelection(); ResetForm(); await LoadRowsAsync(db);
            });
        }

        private void OpenSearch_Click(object sender, RoutedEventArgs e)
        {
            if (!CanLeave()) return;
            ClearSelection(); ResetForm(); SearchRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
