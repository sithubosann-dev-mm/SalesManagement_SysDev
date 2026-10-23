using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using SalesManagement_SysDev.ViewModels.master_code;

namespace SalesManagement_SysDev
{
    public partial class ClientView : UserControl
    {
        private readonly int _employeeId;
        private readonly SimpleClientData _service = new();
        private readonly SimplePostalLookup _addresses = new();
        private readonly SimpleClientDialogs _dialogs = new();
        private bool _initialized, _loading, _restoring;
        private int? _editingId;
        private string _entityStamp = "", _baseline = "", _pendingStamp = "";
        private SimpleClientAction _pending;
        public bool IsBusy { get; private set; }
        private bool IsIdle => !IsBusy;
        private bool Ready => IsIdle && _initialized;
        public string EditLoadError { get; private set; } = "";
        public event EventHandler? SearchRequested;
        private new string Name { get => txtName.Text; set => txtName.Text = value; }
        private string Phone { get => txtPhone.Text; set => txtPhone.Text = value; }
        private string Fax { get => txtFax.Text; set => txtFax.Text = value; }
        private string Postal { get => txtPostal.Text; set => txtPostal.Text = value; }
        private string Address { get => txtAddress.Text; set => txtAddress.Text = value; }
        private string Reason { get => txtReason.Text; set => txtReason.Text = value; }
        private string PendingText { get => txtPending.Text; set => txtPending.Text = value; }
        private string Message { get => txtMessage.Text; set => txtMessage.Text = value; }
        private int? OfficeId { get => cmbOffice.SelectedValue as int?; set => cmbOffice.SelectedValue = value; }
        private List<MSalesOffice> Offices { get => (List<MSalesOffice>?)cmbOffice.ItemsSource ?? new(); set => cmbOffice.ItemsSource = value; }
        private List<SimpleClientListRow> Clients { get => (List<SimpleClientListRow>?)gridClients.ItemsSource ?? new(); set => gridClients.ItemsSource = value; }
        private SimpleClientListRow? _selectedClient;
        private SimpleClientListRow? SelectedClient
        {
            get => _selectedClient;
            set { _selectedClient = value; _restoring = true; try { gridClients.SelectedItem = value; } finally { _restoring = false; } }
        }
        private List<SimplePostalCandidate> Candidates
        {
            get => (List<SimplePostalCandidate>?)cmbAddressCandidates.ItemsSource ?? new();
            set { cmbAddressCandidates.ItemsSource = value; panelAddressCandidates.Visibility = value.Count > 1 ? Visibility.Visible : Visibility.Collapsed; }
        }
        private SimplePostalCandidate? SelectedAddress { get => cmbAddressCandidates.SelectedItem as SimplePostalCandidate; set => cmbAddressCandidates.SelectedItem = value; }
        public ClientView(int loginEmployeeId)
        {
            InitializeComponent(); _employeeId = loginEmployeeId;
            mainPanel.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler((_, _) => InputChanged()));
            cmbOffice.SelectionChanged += (_, _) => InputChanged();
            txtPostal.TextChanged += (_, _) => { Candidates = new(); SelectedAddress = null; RefreshButtons(); };
            cmbAddressCandidates.SelectionChanged += (_, _) => RefreshButtons();
            RefreshButtons();
        }
        private async void View_Loaded(object sender, RoutedEventArgs e) => await InitializeAsync();
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
        private void RefreshButtons()
        {
            btnRegister.IsEnabled = Ready && !_editingId.HasValue;
            btnUpdate.IsEnabled = btnHide.IsEnabled = btnDelete.IsEnabled = Ready && _editingId.HasValue;
            btnClear.IsEnabled = btnLookupAddress.IsEnabled = Ready;
            btnConfirm.IsEnabled = Ready && _pending != SimpleClientAction.None;
            btnList.IsEnabled = btnOpenSearch.IsEnabled = IsIdle;
            btnApplyAddress.IsEnabled = Ready && SelectedAddress != null;
        }
        private void InputChanged()
        {
            if (_loading || !_initialized || _pending == SimpleClientAction.None) return;
            ClearPending(); PendingText = "入力が変更されました。操作を選び直してください。";
        }
        private string FormStamp() => JsonSerializer.Serialize(new { Name, OfficeId, Phone, Fax, Postal, Address, Reason });
        private void UpdateModeText()
        {
            txtId.Text = _editingId?.ToString() ?? "自動採番";
            txtMode.Text = _editingId.HasValue ? $"顧客ID：{_editingId} を編集しています。" : "新規顧客を登録できます。";
        }
        public bool CanLeave()
        {
            if (IsBusy) { _dialogs.Inform("処理が終わるまでお待ちください。"); return false; }
            return !_initialized || _baseline == FormStamp() && _pending == SimpleClientAction.None
                || _dialogs.Confirm("未保存の入力を破棄しますか？");
        }
        private async Task EnsureInitializedAsync()
        {
            if (_initialized) return;
            Offices = await _service.GetOfficesAsync(_employeeId);
            _initialized = true; ResetForm();
        }
        public async Task InitializeAsync()
        {
            if (_initialized) return;
            await RunAsync(async () => { await EnsureInitializedAsync(); await LoadRowsAsync(); });
        }
        private async Task LoadRowsAsync()
        {
            Clients = await _service.GetRowsAsync(_employeeId);
            Message = $"{Clients.Count}件を表示しています。";
        }
        private void ClearPending()
        {
            _pending = SimpleClientAction.None; _pendingStamp = "";
            PendingText = "操作を選択してから確定を押してください。"; RefreshButtons();
        }
        private void ResetForm()
        {
            _loading = true;
            try
            {
                ClearPending(); _editingId = null; _entityStamp = ""; SelectedClient = null;
                Name = Phone = Fax = Postal = Address = Reason = ""; OfficeId = null;
                Candidates = new(); SelectedAddress = null; UpdateModeText(); _baseline = FormStamp();
            }
            finally { _loading = false; RefreshButtons(); }
        }
        private async Task LoadForEditAsync(int id)
        {
            var snapshot = await _service.GetClientAsync(_employeeId, id);
            var row = snapshot.Row;
            _loading = true;
            try
            {
                ClearPending(); _editingId = row.Id; _entityStamp = snapshot.Stamp;
                Name = row.Name; Phone = row.Phone; Fax = row.Fax; Postal = row.Postal; Address = row.Address; Reason = "";
                OfficeId = Offices.Any(x => x.SoId == row.OfficeId) ? row.OfficeId : null;
                Candidates = new(); SelectedAddress = null; UpdateModeText(); _baseline = FormStamp();
                Message = OfficeId.HasValue ? "操作を選択してから確定を押してください。" : "営業所が非表示です。更新する場合は利用可能な営業所を選択してください。";
            }
            finally { _loading = false; RefreshButtons(); }
        }
        public async Task<bool> TrySelectAsync(SimpleClientListRow row)
        {
            if (!CanLeave()) return false;
            bool loaded = await RunAsync(async () => { await EnsureInitializedAsync(); await LoadForEditAsync(row.Id); SelectedClient = row; });
            if (!loaded) { string error = Message; ResetForm(); Message = error; }
            return loaded;
        }
        public async Task<bool> OpenForEditAsync(int id)
        {
            EditLoadError = "";
            if (!CanLeave()) return false;
            bool opened = await RunAsync(async () =>
            {
                await EnsureInitializedAsync(); await LoadRowsAsync();
                var row = Clients.SingleOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("顧客が見つかりません。");
                await LoadForEditAsync(id); SelectedClient = row;
            });
            if (!opened) { EditLoadError = Message; ResetForm(); Message = EditLoadError; }
            return opened;
        }
        private SimpleClientInput ReadInput()
        {
            var input = new SimpleClientInput { OfficeId = OfficeId ?? 0, Name = Name.Trim(), Phone = Phone.Trim(),
                Fax = Fax.Trim(), Postal = Postal.Trim(), Address = Address.Trim() };
            input.Validate(); return input;
        }
        private void Stage(SimpleClientAction action)
        {
            if (!Ready) return;
            ClearPending();
            try
            {
                if (action == SimpleClientAction.Register && _editingId.HasValue)
                    throw new InvalidOperationException("新規登録する場合はクリアを押してください。");
                if (action != SimpleClientAction.Register && !_editingId.HasValue)
                    throw new InvalidOperationException("対象の顧客を一覧から選択してください。");
                if (action == SimpleClientAction.Register || action == SimpleClientAction.Update)
                {
                    ReadInput();
                    if (!string.IsNullOrWhiteSpace(Reason)) throw new InvalidOperationException("登録・更新する場合は非表示理由を空欄にしてください。");
                }
                if (action == SimpleClientAction.Hide && (Reason.Trim().Length == 0 || Reason.Trim().Length > 1000))
                    throw new InvalidOperationException("非表示理由を1～1000文字で入力してください。");
                _pending = action; _pendingStamp = FormStamp();
                PendingText = Label(action) + "を選択しました。まだ保存していません。確定を押してください。";
                Message = action == SimpleClientAction.Delete ? "削除は元に戻せません。業務データで使用されている顧客は削除できません。"
                    : "確定後の確認画面でYesを選ぶと処理します。";
                RefreshButtons();
            }
            catch (InvalidOperationException ex) { Message = ex.Message; }
        }
        private async Task ConfirmAsync()
        {
            if (_pending == SimpleClientAction.None) return;
            if (_pendingStamp != FormStamp()) { ClearPending(); Message = "入力が変更されました。操作を選び直してください。"; return; }
            var action = _pending;
            await RunAsync(async () =>
            {
                SimpleClientInput? input = action == SimpleClientAction.Register || action == SimpleClientAction.Update ? ReadInput() : null;
                string target = _editingId.HasValue ? $"顧客ID：{_editingId}" : $"顧客「{Name.Trim()}」";
                string question = action == SimpleClientAction.Hide
                    ? target + "を非表示にしますか？\n入力中の顧客情報の変更は保存しません。\n理由：" + Reason.Trim()
                    : action == SimpleClientAction.Delete ? target + "を完全に削除しますか？\n入力中の変更は保存しません。この操作は元に戻せません。"
                    : target + "を" + Label(action) + "しますか？";
                if (!_dialogs.Confirm(question, Label(action) + "の確認", action == SimpleClientAction.Delete))
                { Message = Label(action) + "をキャンセルしました。入力と操作の選択は残っています。"; return; }
                int id;
                if (input != null) id = await _service.SaveAsync(_employeeId, input, _editingId, _entityStamp);
                else
                {
                    id = _editingId ?? throw new InvalidOperationException("顧客を選択してください。");
                    if (action == SimpleClientAction.Hide) await _service.HideAsync(_employeeId, id, _entityStamp, Reason.Trim());
                    else await _service.DeleteAsync(_employeeId, id, _entityStamp);
                }
                string success = $"顧客ID：{id} を{Label(action)}しました。";
                ResetForm();
                try { await LoadRowsAsync(); Message = success; }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); Clients = new(); Message = success + " 一覧の再取得に失敗しました。一覧表示を押してください。"; }
            });
        }
        private async Task LookupAddressAsync()
        {
            await RunAsync(async () =>
            {
                Candidates = new(); SelectedAddress = null;
                var results = await _addresses.SearchAsync(Postal);
                Candidates = results;
                if (results.Count == 0) { Message = "該当する住所が見つかりません。郵便番号を確認するか、住所を手入力してください。"; return; }
                if (results.Count == 1) ApplyAddress(results[0]);
                else Message = $"住所が{results.Count}件見つかりました。候補を選択して「住所を反映」を押してください。";
            });
        }
        private void ApplyAddress(SimplePostalCandidate candidate)
        {
            if (SimpleClientInput.NormalizePostal(Postal) != candidate.Postal)
            { Message = "郵便番号が変わりました。住所を検索し直してください。"; return; }
            if (candidate.Address.Length > 50) { Message = "検索した住所が50文字を超えています。住所を手入力してください。"; return; }
            if (Address.Trim() != "" && Address != candidate.Address && !_dialogs.Confirm("入力済みの住所を検索した住所に置き換えますか？"))
            { Message = "住所の置き換えをキャンセルしました。"; return; }
            Address = candidate.Address;
            Message = "住所を反映しました。番地・建物名を追加入力してください（住所全体50文字以内）。まだ保存していません。";
        }
        private void Clear() { if (!CanLeave()) return; ResetForm(); Message = "新規登録フォームに戻しました。"; }
        private async Task ListAsync()
        {
            if (!CanLeave()) return;
            await RunAsync(async () => { await EnsureInitializedAsync(); ResetForm(); await LoadRowsAsync(); });
        }
        private void OpenSearch()
        {
            if (!CanLeave()) return;
            ResetForm(); SearchRequested?.Invoke(this, EventArgs.Empty);
        }
        private static string Label(SimpleClientAction action) => action switch
        {
            SimpleClientAction.Register => "登録", SimpleClientAction.Update => "更新", SimpleClientAction.Hide => "非表示",
            SimpleClientAction.Delete => "削除", _ => ""
        };
        private void Register_Click(object sender, RoutedEventArgs e) => Stage(SimpleClientAction.Register);
        private void Update_Click(object sender, RoutedEventArgs e) => Stage(SimpleClientAction.Update);
        private void Hide_Click(object sender, RoutedEventArgs e) => Stage(SimpleClientAction.Hide);
        private void Delete_Click(object sender, RoutedEventArgs e) => Stage(SimpleClientAction.Delete);
        private void Clear_Click(object sender, RoutedEventArgs e) => Clear();
        private void OpenSearch_Click(object sender, RoutedEventArgs e) => OpenSearch();
        private async void Confirm_Click(object sender, RoutedEventArgs e) => await ConfirmAsync();
        private async void List_Click(object sender, RoutedEventArgs e) => await ListAsync();
        private async void LookupAddress_Click(object sender, RoutedEventArgs e) => await LookupAddressAsync();
        private void ApplyAddress_Click(object sender, RoutedEventArgs e) { if (SelectedAddress != null) ApplyAddress(SelectedAddress); }
        private async void Clients_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_restoring || IsBusy || gridClients.SelectedItem is not SimpleClientListRow row || ReferenceEquals(row, SelectedClient)) return;
            await TrySelectAsync(row);
            _restoring = true; try { gridClients.SelectedItem = SelectedClient; } finally { _restoring = false; }
        }
    }
}

namespace SalesManagement_SysDev.ViewModels.master_code
{
    public enum SimpleClientAction { None, Register, Update, Hide, Delete }

    public sealed class SimpleClientListRow
    {
        public int Id { get; set; }
        public int OfficeId { get; set; }
        public string Office { get; set; } = "";
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Fax { get; set; } = "";
        public string Postal { get; set; } = "";
        public string Address { get; set; } = "";
    }

    public sealed class SimpleClientSnapshot
    {
        public SimpleClientListRow Row { get; set; } = new();
        public string Stamp { get; set; } = "";
    }

    public sealed class SimpleClientFilter
    {
        public string IdPrefix { get; set; } = "";
        public string Name { get; set; } = "";
        public int? OfficeId { get; set; }
        public string Phone { get; set; } = "";
        public void Validate()
        {
            if (IdPrefix.Any(c => c < '0' || c > '9'))
                throw new InvalidOperationException("顧客IDは半角数字で入力してください。");
            if (Name.Length > 50 || Phone.Length > 13)
                throw new InvalidOperationException("顧客名は50文字、電話番号は13文字以内で検索してください。");
        }
    }

    public sealed class SimpleClientInput
    {
        public int OfficeId { get; set; }
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Fax { get; set; } = "";
        public string Postal { get; set; } = "";
        public string Address { get; set; } = "";

        public static string NormalizePostal(string value)
        {
            value = value.Trim();
            if (!Regex.IsMatch(value, @"\A(?:[0-9]{7}|[0-9]{3}-[0-9]{4})\z"))
                throw new InvalidOperationException("郵便番号は半角数字7桁、または123-4567形式で入力してください。");
            return value.Replace("-", "");
        }
        public void Validate()
        {
            if (OfficeId <= 0) throw new InvalidOperationException("営業所を選択してください。");
            if (Name.Trim().Length == 0 || Name.Trim().Length > 50)
                throw new InvalidOperationException("顧客名を1～50文字で入力してください。");
            if (Address.Trim().Length == 0 || Address.Trim().Length > 50)
                throw new InvalidOperationException("住所を1～50文字で入力してください。");
            if (!Regex.IsMatch(NormalizePostal(Postal), @"\A[0-9]{7}\z"))
                throw new InvalidOperationException("郵便番号は半角数字7桁で入力してください。（例：123-4567）");
            ValidatePhone(Phone.Trim(), "電話番号", true);
            ValidatePhone(Fax.Trim(), "FAX", false);
        }
        private static void ValidatePhone(string value, string label, bool required)
        {
            if (!required && value.Length == 0) return;
            if (value.Length == 0 || value.Length > 13 || !Regex.IsMatch(value, @"\A[0-9]+(?:-[0-9]+)*\z"))
                throw new InvalidOperationException(label + "は半角数字と区切りのハイフンで13文字以内にしてください。");
        }
    }

    public sealed class SimplePostalCandidate
    {
        public string Postal { get; set; } = "";
        public string Address { get; set; } = "";
        public string Label => Postal + "　" + Address;
    }
}

namespace SalesManagement_SysDev.ViewModels.master_code
{
    public sealed class SimpleClientData
    {
        private static async Task CheckPermissionAsync(SalesManagementContext db, int employeeId)
        {
            if (!await db.MEmployees.AsNoTracking().AnyAsync(x => x.EmId == employeeId && x.EmFlag == 0 &&
                (x.Po.PoName == "管理者" || x.Po.PoName == "営業" || x.Po.PoName == "営業担当")))
                throw new InvalidOperationException("顧客管理を利用する権限がありません。再ログインしてください。");
        }
        private static IQueryable<SimpleClientListRow> Project(IQueryable<MClient> query) =>
            query.OrderBy(x => x.ClId).Select(x => new SimpleClientListRow
            {
                Id = x.ClId, OfficeId = x.SoId, Office = x.So.SoName, Name = x.ClName,
                Phone = x.ClPhone, Fax = x.ClFax, Postal = x.ClPostal, Address = x.ClAddress
            });

        private static string Stamp(MClient client) => JsonSerializer.Serialize(new
        {
            client.ClId, client.SoId, client.ClName, client.ClAddress, client.ClPhone,
            client.ClPostal, client.ClFax, client.ClFlag, client.ClHidden
        });

        public async Task<List<MSalesOffice>> GetOfficesAsync(int employeeId, bool search = false)
        {
            using var db = new SalesManagementContext();
            await CheckPermissionAsync(db, employeeId);
            // Search includes hidden offices, so active clients referring to them remain discoverable.
            return await db.MSalesOffices.AsNoTracking().Where(x => search || x.SoFlag == 0)
                .OrderBy(x => x.SoId).ToListAsync();
        }
        public async Task<List<SimpleClientListRow>> GetRowsAsync(int employeeId, SimpleClientFilter? filter = null)
        {
            filter ??= new SimpleClientFilter(); filter.Validate();
            using var db = new SalesManagementContext();
            await CheckPermissionAsync(db, employeeId);
            var query = db.MClients.AsNoTracking().Where(x => x.ClFlag == 0);
            if (filter.IdPrefix != "") query = query.Where(x => x.ClId.ToString().StartsWith(filter.IdPrefix));
            if (filter.Name != "") query = query.Where(x => x.ClName.Contains(filter.Name));
            if (filter.OfficeId.HasValue) query = query.Where(x => x.SoId == filter.OfficeId.Value);
            if (filter.Phone != "") query = query.Where(x => x.ClPhone.Contains(filter.Phone));
            return await Project(query).ToListAsync();
        }
        public async Task<SimpleClientSnapshot> GetClientAsync(int employeeId, int clientId)
        {
            using var db = new SalesManagementContext();
            await CheckPermissionAsync(db, employeeId);
            var entity = await db.MClients.AsNoTracking().Include(x => x.So)
                .SingleOrDefaultAsync(x => x.ClId == clientId && x.ClFlag == 0)
                ?? throw new InvalidOperationException("顧客が見つかりません。非表示にされた可能性があります。");
            return new SimpleClientSnapshot
            {
                Row = new SimpleClientListRow
                {
                    Id = entity.ClId, OfficeId = entity.SoId, Office = entity.So.SoName,
                    Name = entity.ClName, Phone = entity.ClPhone, Fax = entity.ClFax,
                    Postal = entity.ClPostal, Address = entity.ClAddress
                },
                Stamp = Stamp(entity)
            };
        }

        private static async Task<MClient> GetForWriteAsync(SalesManagementContext db, int id, string stamp)
        {
            var client = await db.MClients.SingleOrDefaultAsync(x => x.ClId == id && x.ClFlag == 0)
                ?? throw new InvalidOperationException("対象の顧客が見つかりません。");
            if (Stamp(client) != stamp)
                throw new InvalidOperationException("別の操作で顧客情報が変更されました。一覧から選択し直してください。");
            return client;
        }
        public async Task<int> SaveAsync(int employeeId, SimpleClientInput input, int? editingId, string expectedStamp)
        {
            input.Validate();
            using var db = new SalesManagementContext();
            using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await CheckPermissionAsync(db, employeeId);
            if (!await db.MSalesOffices.AnyAsync(x => x.SoId == input.OfficeId && x.SoFlag == 0))
                throw new InvalidOperationException("選択した営業所は利用できません。画面を開き直してください。");
            var client = editingId.HasValue ? await GetForWriteAsync(db, editingId.Value, expectedStamp)
                : new MClient { ClFlag = 0 };
            client.SoId = input.OfficeId; client.ClName = input.Name.Trim(); client.ClAddress = input.Address.Trim();
            client.ClPhone = input.Phone.Trim(); client.ClPostal = SimpleClientInput.NormalizePostal(input.Postal);
            client.ClFax = input.Fax.Trim();
            if (!editingId.HasValue) db.MClients.Add(client);
            await db.SaveChangesAsync(); await tx.CommitAsync();
            return client.ClId;
        }
        public async Task HideAsync(int employeeId, int clientId, string expectedStamp, string reason)
        {
            reason = reason.Trim();
            if (reason.Length == 0 || reason.Length > 1000)
                throw new InvalidOperationException("非表示理由を1～1000文字で入力してください。");
            using var db = new SalesManagementContext();
            using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await CheckPermissionAsync(db, employeeId);
            var client = await GetForWriteAsync(db, clientId, expectedStamp);
            client.ClFlag = 2; client.ClHidden = reason;
            await db.SaveChangesAsync(); await tx.CommitAsync();
        }
        public async Task DeleteAsync(int employeeId, int clientId, string expectedStamp)
        {
            using var db = new SalesManagementContext();
            using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await CheckPermissionAsync(db, employeeId);
            var client = await GetForWriteAsync(db, clientId, expectedStamp);
            bool used = await db.TOrders.AnyAsync(x => x.ClId == clientId)
                || await db.TChumons.AnyAsync(x => x.ClId == clientId)
                || await db.TArrivals.AnyAsync(x => x.ClId == clientId)
                || await db.TSales.AnyAsync(x => x.ClId == clientId)
                || await db.TShipments.AnyAsync(x => x.ClId == clientId)
                || await db.TSyukkos.AnyAsync(x => x.ClId == clientId);
            if (used) throw new InvalidOperationException("業務データで使用されている顧客は削除できません。非表示を使用してください。");
            db.MClients.Remove(client);
            await db.SaveChangesAsync(); await tx.CommitAsync();
        }
    }
}

namespace SalesManagement_SysDev.ViewModels.master_code
{
    public sealed class SimplePostalLookup
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        public async Task<List<SimplePostalCandidate>> SearchAsync(string postal)
        {
            postal = SimpleClientInput.NormalizePostal(postal);
            if (!Regex.IsMatch(postal, @"\A[0-9]{7}\z"))
                throw new InvalidOperationException("住所検索には半角数字7桁の郵便番号が必要です。");
            try
            {
                // Only the postal code is sent; no client name, phone or address is included.
                using var response = await Client.GetAsync("https://zipcloud.ibsnet.co.jp/api/search?zipcode=" + postal + "&limit=1000");
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<ZipcloudResponse>(json)
                    ?? throw new InvalidOperationException("住所検索の応答を読み取れませんでした。");
                if (data.Status != 200) throw new InvalidOperationException("住所検索：" + (data.Message ?? "検索に失敗しました。"));
                return (data.Results ?? new List<ZipcloudResult>()).Where(x => x.Postal == postal)
                    .Select(x => new SimplePostalCandidate { Postal = postal, Address = x.Address1 + x.Address2 + x.Address3 })
                    .GroupBy(x => x.Address).Select(x => x.First()).ToList();
            }
            catch (TaskCanceledException ex) { throw new InvalidOperationException("住所検索がタイムアウトしました。住所は手入力できます。", ex); }
            catch (HttpRequestException ex) { throw new InvalidOperationException("住所検索に接続できません。ネット接続を確認するか、住所を手入力してください。", ex); }
            catch (JsonException ex) { throw new InvalidOperationException("住所検索の応答形式が正しくありません。住所を手入力してください。", ex); }
        }
        public sealed class ZipcloudResponse
        {
            [JsonPropertyName("status")] public int Status { get; set; }
            [JsonPropertyName("message")] public string? Message { get; set; }
            [JsonPropertyName("results")] public List<ZipcloudResult>? Results { get; set; }
        }
        public sealed class ZipcloudResult
        {
            [JsonPropertyName("zipcode")] public string Postal { get; set; } = "";
            [JsonPropertyName("address1")] public string Address1 { get; set; } = "";
            [JsonPropertyName("address2")] public string Address2 { get; set; } = "";
            [JsonPropertyName("address3")] public string Address3 { get; set; } = "";
        }
    }
}

namespace SalesManagement_SysDev.ViewModels.master_code
{
    internal sealed class SimpleClientDialogs
    {
        public bool Confirm(string message, string title = "確認", bool destructive = false) =>
            MessageBox.Show(message, title, MessageBoxButton.YesNo, destructive ? MessageBoxImage.Warning : MessageBoxImage.Question) == MessageBoxResult.Yes;
        public void Inform(string message) => MessageBox.Show(message);
    }
}
