using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;

namespace SalesManagement_SysDev
{
    public partial class SaleSearchView : UserControl
    {
        private readonly int _loginEmployeeId;
        private bool _lookupsLoaded, _initialized;
        public event EventHandler? BackRequested;
        public event Func<int, Task<bool>>? DisplayRequested;
        public bool IsBusy { get; private set; }

        public SaleSearchView() : this(0) { }
        public SaleSearchView(int loginEmployeeId)
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
            IsBusy = true;
            searchPanel.IsEnabled = false;
            gridSales.IsEnabled = false;
            btnDisplay.IsEnabled = false;
            txtMessage.Text = "売上情報を読み込んでいます…";
            try { await action(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                txtMessage.Text = SaleView.ReadError(ex);
            }
            finally
            {
                IsBusy = false;
                searchPanel.IsEnabled = true;
                gridSales.IsEnabled = true;
                btnDisplay.IsEnabled = gridSales.SelectedItem is SaleView.SaleRow;
            }
        }

        private async void View_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized || DesignerProperties.GetIsInDesignMode(this)) return;
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await EnsureLookupsAsync(db);
                await LoadRowsAsync(db, new SaleView.SaleFilter());
                _initialized = true;
            });
        }

        private async Task EnsureLookupsAsync(SalesManagementContext db)
        {
            await SaleView.CheckPermissionAsync(db, _loginEmployeeId);
            if (_lookupsLoaded) return;
            var clients = await db.MClients.AsNoTracking().OrderBy(x => x.ClId)
                .Select(x => new SearchChoice { Id = x.ClId, Name = x.ClName }).ToListAsync();
            var offices = await db.MSalesOffices.AsNoTracking().OrderBy(x => x.SoId)
                .Select(x => new SearchChoice { Id = x.SoId, Name = x.SoName }).ToListAsync();
            clients.Insert(0, new SearchChoice { Name = "すべて" });
            offices.Insert(0, new SearchChoice { Name = "すべて" });
            cmbSearchClient.ItemsSource = clients;
            cmbSearchOffice.ItemsSource = offices;
            ResetConditions();
            _lookupsLoaded = true;
        }

        private void ResetConditions()
        {
            txtSearchId.Clear(); txtSearchOrderId.Clear();
            cmbSearchClient.SelectedIndex = 0;
            cmbSearchOffice.SelectedIndex = 0;
            dpFrom.SelectedDate = null; dpFrom.Text = "";
            dpTo.SelectedDate = null; dpTo.Text = "";
        }

        private async Task LoadRowsAsync(SalesManagementContext db, SaleView.SaleFilter filter)
        {
            var rows = await SaleView.ReadRowsAsync(db, _loginEmployeeId, filter);
            gridSales.ItemsSource = rows;
            gridSales.SelectedItem = rows.FirstOrDefault();
            txtMessage.Text = rows.Count == 0
                ? "条件に一致する売上は0件です。「一覧表示」で全件を確認できます。" + SaleView.ConnectionLabel()
                : $"{rows.Count}件の売上が見つかりました。対象を選択して「選択した売上を表示」を押してください。";
        }

        private static DateTime? ReadDate(DatePicker picker, string label)
        {
            if (string.IsNullOrWhiteSpace(picker.Text)) return null;
            if (!picker.SelectedDate.HasValue || !DateTime.TryParse(picker.Text,
                picker.Language.GetSpecificCulture(), DateTimeStyles.None, out var parsed) ||
                parsed.Date != picker.SelectedDate.Value.Date)
                throw new InvalidOperationException(label + "を正しい日付で入力してください。");
            return picker.SelectedDate.Value.Date;
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await EnsureLookupsAsync(db);
                var filter = new SaleView.SaleFilter
                {
                    IdPrefix = txtSearchId.Text.Trim(), OrderIdPrefix = txtSearchOrderId.Text.Trim(),
                    ClientId = cmbSearchClient.SelectedValue as int?,
                    OfficeId = cmbSearchOffice.SelectedValue as int?,
                    From = ReadDate(dpFrom, "開始日"), To = ReadDate(dpTo, "終了日")
                };
                filter.Validate();
                await LoadRowsAsync(db, filter);
                _initialized = true;
            });
        }

        private async void List_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await EnsureLookupsAsync(db);
                ResetConditions();
                await LoadRowsAsync(db, new SaleView.SaleFilter());
                _initialized = true;
            });
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        { if (CanLeave()) BackRequested?.Invoke(this, EventArgs.Empty); }

        private void Sales_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (btnDisplay != null)
                btnDisplay.IsEnabled = !IsBusy && gridSales.SelectedItem is SaleView.SaleRow;
        }
        private async void Display_Click(object sender, RoutedEventArgs e) => await DisplaySelectedAsync();
        private async void Sales_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject target &&
                ItemsControl.ContainerFromElement(gridSales, target) is DataGridRow)
                await DisplaySelectedAsync();
        }

        private async Task DisplaySelectedAsync()
        {
            if (IsBusy) return;
            if (gridSales.SelectedItem is not SaleView.SaleRow row)
            { txtMessage.Text = "一覧から対象の売上を選択してください。"; return; }
            var handler = DisplayRequested;
            if (handler == null)
            { txtMessage.Text = "売上管理への接続が設定されていません。F_menu を確認してください。"; return; }
            await RunAsync(async () =>
            {
                if (!await handler(row.Id))
                    txtMessage.Text = "売上を表示できませんでした。「一覧表示」で検索結果を更新してください。";
            });
        }

        public sealed class SearchChoice
        {
            public int? Id { get; set; }
            public string Name { get; set; } = "";
        }
    }
}
