using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SalesManagement_SysDev.ViewModels.master_code;

namespace SalesManagement_SysDev
{
    public partial class StockSearchView : UserControl
    {
        private readonly int _employeeId;
        private readonly SimpleStockData _data = new();
        private bool _initialized;
        public bool IsBusy { get; private set; }
        public event EventHandler? BackRequested;
        public event Func<int, Task<bool>>? EditRequested;
        public StockSearchView(int loginEmployeeId) { InitializeComponent(); _employeeId = loginEmployeeId; }
        public bool CanLeave()
        {
            if (!IsBusy) return true;
            MessageBox.Show("処理が終わるまでお待ちください。"); return false;
        }
        private async Task RunAsync(Func<Task> action)
        {
            if (IsBusy) return;
            IsBusy = true; mainPanel.IsEnabled = false; txtMessage.Text = "処理中です…";
            try { await action(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                txtMessage.Text = ex is InvalidOperationException ? ex.Message : "検索に失敗しました。DB接続と入力内容を確認してください。";
            }
            finally { IsBusy = false; mainPanel.IsEnabled = true; }
        }
        private async Task SearchAsync(bool all)
        {
            await RunAsync(async () =>
            {
                if (all) { txtSearchStockId.Clear(); txtSearchProductId.Clear(); txtSearchName.Clear(); txtMin.Clear(); txtMax.Clear(); }
                var filter = new SimpleStockFilter
                {
                    StockIdPrefix = txtSearchStockId.Text.Trim(), ProductIdPrefix = txtSearchProductId.Text.Trim(), ProductName = txtSearchName.Text.Trim(),
                    MinQuantity = string.IsNullOrWhiteSpace(txtMin.Text) ? null : SimpleStockFilter.ReadQuantity(txtMin.Text, "在庫数の下限"),
                    MaxQuantity = string.IsNullOrWhiteSpace(txtMax.Text) ? null : SimpleStockFilter.ReadQuantity(txtMax.Text, "在庫数の上限")
                };
                var rows = await _data.GetRowsAsync(_employeeId, filter);
                gridStocks.ItemsSource = rows; _initialized = true;
                txtMessage.Text = $"{rows.Count}件が見つかりました。選択して在庫管理で開けます。";
            });
        }
        private async void View_Loaded(object sender, RoutedEventArgs e) { if (!_initialized) await SearchAsync(true); }
        private async void Search_Click(object sender, RoutedEventArgs e) => await SearchAsync(false);
        private async void List_Click(object sender, RoutedEventArgs e) => await SearchAsync(true);
        private void Back_Click(object sender, RoutedEventArgs e) { if (CanLeave()) BackRequested?.Invoke(this, EventArgs.Empty); }
        private async Task EditAsync()
        {
            if (gridStocks.SelectedItem is not SimpleStockRow row) { txtMessage.Text = "在庫を選択してください。"; return; }
            var handler = EditRequested;
            if (handler == null) { txtMessage.Text = "在庫管理への接続が設定されていません。"; return; }
            await RunAsync(async () =>
            {
                await _data.GetStockAsync(_employeeId, row.Id);
                if (!await handler(row.Id)) txtMessage.Text = "在庫を開けませんでした。対象の在庫と接続を確認してください。";
            });
        }
        private async void Edit_Click(object sender, RoutedEventArgs e) => await EditAsync();
        private async void Stocks_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject origin || ItemsControl.ContainerFromElement(gridStocks, origin) is not DataGridRow row) return;
            gridStocks.SelectedItem = row.Item; await EditAsync();
        }
    }
}
