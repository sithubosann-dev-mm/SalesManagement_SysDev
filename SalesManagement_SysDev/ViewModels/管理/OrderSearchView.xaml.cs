using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;

namespace SalesManagement_SysDev
{
    public partial class OrderSearchView : UserControl
    {
        private readonly int _loginEmployeeId;
        private bool _initialized;
        public bool IsBusy { get; private set; }
        public event EventHandler? BackRequested;
        public event Func<int, Task<bool>>? DisplayRequested;

        public OrderSearchView(int loginEmployeeId)
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

        private async Task CheckPermissionAsync(SalesManagementContext db)
        {
            bool allowed = await db.MEmployees.AsNoTracking().AnyAsync(x =>
                x.EmId == _loginEmployeeId && x.EmFlag == 0 &&
                (x.Po.PoName == "管理者" || x.Po.PoName == "営業" || x.Po.PoName == "営業担当"));
            if (!allowed)
                throw new InvalidOperationException("受注検索を利用する権限がありません。再ログインしてください。");
        }

        private async Task RunAsync(Func<Task> action)
        {
            if (IsBusy) return;
            IsBusy = true;
            searchPanel.IsEnabled = false;
            gridOrders.IsEnabled = false;
            btnDisplay.IsEnabled = false;
            txtMessage.Text = "処理中です…";
            try { await action(); }
            catch (InvalidOperationException ex) { txtMessage.Text = ex.Message; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                txtMessage.Text = "処理に失敗しました。データベース接続と入力内容を確認してください。";
            }
            finally
            {
                IsBusy = false;
                searchPanel.IsEnabled = true;
                gridOrders.IsEnabled = _initialized;
                btnDisplay.IsEnabled = _initialized;
            }
        }

        private async Task InitializeAsync(SalesManagementContext db)
        {
            if (_initialized) return;
            cmbSearchClient.ItemsSource = await db.MClients.AsNoTracking()
                .OrderBy(x => x.ClId).ToListAsync();
            cmbSearchOffice.ItemsSource = await db.MSalesOffices.AsNoTracking()
                .OrderBy(x => x.SoId).ToListAsync();
            _initialized = true;
        }

        private async void View_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized) return;
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await CheckPermissionAsync(db);
                await InitializeAsync(db);
                await LoadRowsAsync(db);
            });
        }

        private async Task LoadRowsAsync(SalesManagementContext db, string prefix = "",
            int? clientId = null, int? officeId = null, int? state = null,
            DateTime? from = null, DateTime? to = null)
        {
            var query = db.TOrders.AsNoTracking().Where(x => x.OrFlag == 0);
            if (prefix != "") query = query.Where(x => x.OrId.ToString().StartsWith(prefix));
            if (clientId.HasValue) query = query.Where(x => x.ClId == clientId.Value);
            if (officeId.HasValue) query = query.Where(x => x.SoId == officeId.Value);
            if (state.HasValue) query = query.Where(x => x.OrStateFlag == state.Value);
            if (from.HasValue) query = query.Where(x => x.OrDate >= from.Value.Date);
            if (to.HasValue && to.Value.Date < DateTime.MaxValue.Date)
            {
                DateTime endExclusive = to.Value.Date.AddDays(1);
                query = query.Where(x => x.OrDate < endExclusive);
            }
            gridOrders.ItemsSource = await query.OrderByDescending(x => x.OrId)
                .Select(x => new OrderView.OrderRow
                {
                    Id = x.OrId,
                    Date = x.OrDate,
                    Client = x.Cl.ClName,
                    Office = x.So.SoName,
                    Employee = x.Em.EmName,
                    EmployeeId = x.EmId,
                    Charge = x.ClCharge ?? "",
                    StateFlag = x.OrFlag,
                    Total = x.TOrderDetails.Sum(d => (decimal?)d.OrTotalPrice) ?? 0m
                }).ToListAsync();
            txtMessage.Text = $"{gridOrders.Items.Count}件が見つかりました。受注を選択して表示できます。";
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync(async () =>
            {
                string prefix = txtSearchId.Text.Trim();
                if (prefix.Any(ch => ch < '0' || ch > '9'))
                    throw new InvalidOperationException("受注IDは半角数字で入力してください。");
                DateTime? from = dpFrom.SelectedDate?.Date;
                DateTime? to = dpTo.SelectedDate?.Date;
                if ((!from.HasValue && !string.IsNullOrWhiteSpace(dpFrom.Text)) ||
                    (!to.HasValue && !string.IsNullOrWhiteSpace(dpTo.Text)))
                    throw new InvalidOperationException("検索期間を正しい日付で入力してください。");
                if (from.HasValue && to.HasValue && from.Value > to.Value)
                    throw new InvalidOperationException("開始日は終了日以前にしてください。");
                int? client = cmbSearchClient.SelectedValue is int clId ? clId : null;
                int? office = cmbSearchOffice.SelectedValue is int soId ? soId : null;
                int? state = cmbSearchState.SelectedIndex > 0 ? cmbSearchState.SelectedIndex - 1 : null;
                using var db = new SalesManagementContext();
                await CheckPermissionAsync(db);
                await InitializeAsync(db);
                await LoadRowsAsync(db, prefix, client, office, state, from, to);
            });
        }

        private async void List_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await CheckPermissionAsync(db);
                await InitializeAsync(db);
                txtSearchId.Clear();
                cmbSearchClient.SelectedIndex = -1;
                cmbSearchOffice.SelectedIndex = -1;
                cmbSearchState.SelectedIndex = 0;
                dpFrom.SelectedDate = dpTo.SelectedDate = null;
                dpFrom.Text = dpTo.Text = "";
                await LoadRowsAsync(db);
            });
        }

        private async Task DisplaySelectedAsync()
        {
            if (IsBusy) return;
            if (gridOrders.SelectedItem is not OrderView.OrderRow row)
            {
                txtMessage.Text = "表示する受注を選択してください。";
                return;
            }
            var handler = DisplayRequested;
            if (handler == null) { txtMessage.Text = "受注管理への接続が設定されていません。"; return; }
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await CheckPermissionAsync(db);
                if (!await handler(row.Id))
                    txtMessage.Text = "受注を表示できませんでした。対象の受注と接続を確認してください。";
            });
        }

        private async void Display_Click(object sender, RoutedEventArgs e) => await DisplaySelectedAsync();

        private async void Orders_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // A header or empty area double-click must not open the previously selected row.
            var origin = e.OriginalSource as DependencyObject;
            var row = origin == null ? null : ItemsControl.ContainerFromElement(gridOrders, origin) as DataGridRow;
            if (row == null) return;
            gridOrders.SelectedItem = row.Item;
            await DisplaySelectedAsync();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (CanLeave()) BackRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
