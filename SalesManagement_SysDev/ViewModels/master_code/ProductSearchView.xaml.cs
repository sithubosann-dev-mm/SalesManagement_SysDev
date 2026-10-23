using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;

namespace SalesManagement_SysDev
{
    public partial class ProductSearchView : UserControl
    {
        private readonly int _loginEmployeeId;
        private bool _initialized;
        private List<MSmallClassification> _small = new();
        public bool IsBusy { get; private set; }
        public event EventHandler? BackRequested;
        public event Func<int, Task<bool>>? EditRequested;

        public ProductSearchView(int loginEmployeeId)
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

        private async Task RunAsync(Func<Task> action)
        {
            if (IsBusy) return;
            IsBusy = true; mainPanel.IsEnabled = false; txtMessage.Text = "処理中です…";
            try { await action(); }
            catch (InvalidOperationException ex) { txtMessage.Text = ex.Message; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                txtMessage.Text = "検索に失敗しました。データベース接続と入力内容を確認してください。";
            }
            finally { IsBusy = false; mainPanel.IsEnabled = true; }
        }

        private async Task InitializeAsync(SalesManagementContext db)
        {
            if (_initialized) return;
            // Include hidden master names for finding active products that still refer to them.
            cmbMaker.ItemsSource = await db.MMakers.AsNoTracking().OrderBy(x => x.MaId).ToListAsync();
            cmbMajor.ItemsSource = await db.MMajorClassifications.AsNoTracking().OrderBy(x => x.McId).ToListAsync();
            _small = await db.MSmallClassifications.AsNoTracking().OrderBy(x => x.ScId).ToListAsync();
            cmbSmall.ItemsSource = _small;
            _initialized = true;
        }

        private async void View_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized) return;
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await ProductView.CheckPermissionAsync(db, _loginEmployeeId);
                await InitializeAsync(db);
                await LoadRowsAsync(db);
            });
        }

        private void Major_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbSmall == null) return;
            cmbSmall.SelectedIndex = -1;
            cmbSmall.ItemsSource = cmbMajor.SelectedValue is int major
                ? _small.Where(x => x.McId == major).ToList() : _small;
        }

        private async Task LoadRowsAsync(SalesManagementContext db, string prefix = "", string name = "",
            int? maker = null, int? major = null, int? small = null)
        {
            gridProducts.ItemsSource = await ProductView.Rows(db, prefix, name, maker, major, small).ToListAsync();
            txtMessage.Text = $"{gridProducts.Items.Count}件が見つかりました。行を選択して編集できます。";
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync(async () =>
            {
                string prefix = txtSearchId.Text.Trim();
                if (prefix.Any(c => c < '0' || c > '9'))
                    throw new InvalidOperationException("商品IDは半角数字で入力してください。");
                string name = txtSearchName.Text.Trim();
                int? maker = cmbMaker.SelectedValue is int ma ? ma : null;
                int? major = cmbMajor.SelectedValue is int mc ? mc : null;
                int? small = cmbSmall.SelectedValue is int sc ? sc : null;
                using var db = new SalesManagementContext();
                await ProductView.CheckPermissionAsync(db, _loginEmployeeId);
                await InitializeAsync(db);
                await LoadRowsAsync(db, prefix, name, maker, major, small);
            });
        }

        private async void List_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await ProductView.CheckPermissionAsync(db, _loginEmployeeId);
                await InitializeAsync(db);
                txtSearchId.Clear(); txtSearchName.Clear();
                cmbMaker.SelectedIndex = cmbMajor.SelectedIndex = cmbSmall.SelectedIndex = -1;
                await LoadRowsAsync(db);
            });
        }

        private async Task EditSelectedAsync()
        {
            if (IsBusy) return;
            if (gridProducts.SelectedItem is not ProductView.ProductRow row)
            { txtMessage.Text = "編集する商品を選択してください。"; return; }
            var handler = EditRequested;
            if (handler == null) { txtMessage.Text = "商品管理への接続が設定されていません。"; return; }
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await ProductView.CheckPermissionAsync(db, _loginEmployeeId);
                if (!await handler(row.Id)) txtMessage.Text = "商品を開けませんでした。対象の商品と接続を確認してください。";
            });
        }

        private async void Edit_Click(object sender, RoutedEventArgs e) => await EditSelectedAsync();

        private async void Products_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var origin = e.OriginalSource as DependencyObject;
            var row = origin == null ? null : ItemsControl.ContainerFromElement(gridProducts, origin) as DataGridRow;
            if (row == null) return;
            gridProducts.SelectedItem = row.Item;
            await EditSelectedAsync();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (CanLeave()) BackRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
