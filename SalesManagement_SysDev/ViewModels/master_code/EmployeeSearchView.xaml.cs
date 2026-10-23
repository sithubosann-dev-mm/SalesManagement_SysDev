using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;

namespace SalesManagement_SysDev
{
    public partial class EmployeeSearchView : UserControl
    {
        private readonly int _loginEmployeeId;
        private bool _initialized;

        public bool IsBusy { get; private set; }
        public event EventHandler? BackRequested;
        public event Func<int, Task<bool>>? EditRequested;

        public EmployeeSearchView(int loginEmployeeId)
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

        private async Task CheckAdminAsync(SalesManagementContext db)
        {
            bool allowed = await db.MEmployees.AsNoTracking().AnyAsync(x =>
                x.EmId == _loginEmployeeId &&
                x.EmFlag == 0 &&
                x.Po.PoName == "管理者");

            if (!allowed)
            {
                throw new InvalidOperationException(
                    "社員検索を利用する権限がありません。再ログインしてください。");
            }
        }

        private async Task RunAsync(Func<Task> action)
        {
            if (IsBusy) return;

            IsBusy = true;
            searchPanel.IsEnabled = false;
            gridEmployees.IsEnabled = false;
            btnEdit.IsEnabled = false;
            txtMessage.Text = "処理中です…";

            try
            {
                await action();
            }
            catch (InvalidOperationException ex)
            {
                txtMessage.Text = ex.Message;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                txtMessage.Text =
                    "検索に失敗しました。データベース接続と入力内容を確認してください。";
            }
            finally
            {
                IsBusy = false;
                // 初回に接続できない、一覧表示から再試行できる
                searchPanel.IsEnabled = true;
                gridEmployees.IsEnabled = _initialized;
                btnEdit.IsEnabled = _initialized;
            }
        }

        private async Task InitializeAsync(SalesManagementContext db)
        {
            if (_initialized) return;

            cmbSearchOffice.ItemsSource = await db.MSalesOffices.AsNoTracking()
                .OrderBy(x => x.SoId).ToListAsync();
            cmbSearchPosition.ItemsSource = await db.MPositions.AsNoTracking()
                .OrderBy(x => x.PoId).ToListAsync();
            _initialized = true;
        }

        private async void View_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized) return;

            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await CheckAdminAsync(db);
                await InitializeAsync(db);
                await LoadRowsAsync(db, "", "", null, null);
            });
        }

        // 入力した条件をすべて満たす社員を表示する
        private async Task LoadRowsAsync(
            SalesManagementContext db, string idPrefix, string name,
            int? officeId, int? positionId)
        {
            var query = db.MEmployees.AsNoTracking().Where(x => x.EmFlag == 0);

            if (idPrefix != "")
                query = query.Where(x => x.EmId.ToString().StartsWith(idPrefix));
            if (name != "")
                query = query.Where(x => x.EmName.Contains(name));
            if (officeId.HasValue)
                query = query.Where(x => x.SoId == officeId.Value);
            if (positionId.HasValue)
                query = query.Where(x => x.PoId == positionId.Value);

            // パスワードは結果に読み込まない
            gridEmployees.ItemsSource = await query.OrderBy(x => x.EmId)
                .Select(x => new EmployeeView.EmployeeRow
                {
                    Id = x.EmId,
                    Name = x.EmName,
                    OfficeId = x.SoId,
                    Office = x.So.SoName,
                    PositionId = x.PoId,
                    Position = x.Po.PoName,
                    HireDate = x.EmHiredate,
                    Phone = x.EmPhone
                }).ToListAsync();

            txtMessage.Text = $"{gridEmployees.Items.Count}件が見つかりました。";
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync(async () =>
            {
                string idPrefix = txtSearchId.Text.Trim();
                if (idPrefix.Any(ch => ch < '0' || ch > '9'))
                {
                    throw new InvalidOperationException(
                        "検索する社員IDは半角数字で入力してください。");
                }

                string name = txtSearchName.Text.Trim();
                int? officeId = cmbSearchOffice.SelectedValue is int soId ? soId : null;
                int? positionId = cmbSearchPosition.SelectedValue is int poId ? poId : null;

                using var db = new SalesManagementContext();
                await CheckAdminAsync(db);
                await InitializeAsync(db);
                await LoadRowsAsync(db, idPrefix, name, officeId, positionId);
            });
        }

        private async void List_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await CheckAdminAsync(db);
                await InitializeAsync(db);

                txtSearchId.Clear();
                txtSearchName.Clear();
                cmbSearchOffice.SelectedIndex = -1;
                cmbSearchPosition.SelectedIndex = -1;

                await LoadRowsAsync(db, "", "", null, null);
            });
        }

        private async Task RequestEditAsync()
        {
            if (IsBusy) return;
            if (gridEmployees.SelectedItem is not EmployeeView.EmployeeRow row)
            {
                txtMessage.Text = "編集する社員を検索結果から選択してください。";
                return;
            }
            if (EditRequested == null)
            {
                txtMessage.Text = "社員管理画面との接続がありません。メニューから画面を開き直してください。";
                return;
            }

            int id = row.Id;
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await CheckAdminAsync(db);
                bool exists = await db.MEmployees.AsNoTracking()
                    .AnyAsync(x => x.EmId == id && x.EmFlag == 0);
                if (!exists)
                    throw new InvalidOperationException("対象社員が見つかりません。検索し直してください。");

                bool opened = await EditRequested.Invoke(id);
                if (!opened)
                    txtMessage.Text = "社員情報を開けませんでした。表示された内容を確認してください。";
            });
        }

        private async void Edit_Click(object sender, RoutedEventArgs e)
        {
            await RequestEditAsync();
        }

        private async void Employees_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // ヘッダーや空白のダブルクリックでは編集を開かない
            if (IsBusy || e.OriginalSource is not DependencyObject target) return;
            if (ItemsControl.ContainerFromElement(gridEmployees, target) is not DataGridRow row ||
                row.Item is not EmployeeView.EmployeeRow) return;

            gridEmployees.SelectedItem = row.Item;
            e.Handled = true;
            await RequestEditAsync();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (CanLeave())
                BackRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
