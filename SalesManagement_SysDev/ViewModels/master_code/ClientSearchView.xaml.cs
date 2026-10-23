using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SalesManagement_SysDev.ViewModels.master_code;

namespace SalesManagement_SysDev
{
    public partial class ClientSearchView : UserControl
    {
        private readonly int _employeeId;
        private readonly SimpleClientData _data = new();
        private bool _initialized;
        public bool IsBusy { get; private set; }
        public event EventHandler? BackRequested;
        public event Func<int, Task<bool>>? EditRequested;
        public ClientSearchView(int loginEmployeeId) { InitializeComponent(); _employeeId = loginEmployeeId; }
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
                if (!_initialized)
                {
                    cmbOffice.ItemsSource = await _data.GetOfficesAsync(_employeeId, true);
                    _initialized = true;
                }
                if (all) { txtSearchId.Clear(); txtSearchName.Clear(); txtSearchPhone.Clear(); cmbOffice.SelectedIndex = -1; }
                var filter = new SimpleClientFilter
                {
                    IdPrefix = txtSearchId.Text.Trim(), Name = txtSearchName.Text.Trim(), Phone = txtSearchPhone.Text.Trim(),
                    OfficeId = cmbOffice.SelectedValue as int?
                };
                var rows = await _data.GetRowsAsync(_employeeId, filter);
                gridClients.ItemsSource = rows;
                txtMessage.Text = $"{rows.Count}件が見つかりました。行を選択して編集できます。";
            });
        }
        private async void View_Loaded(object sender, RoutedEventArgs e) { if (!_initialized) await SearchAsync(true); }
        private async void Search_Click(object sender, RoutedEventArgs e) => await SearchAsync(false);
        private async void List_Click(object sender, RoutedEventArgs e) => await SearchAsync(true);
        private void Back_Click(object sender, RoutedEventArgs e) { if (CanLeave()) BackRequested?.Invoke(this, EventArgs.Empty); }
        private async Task EditSelectedAsync()
        {
            if (gridClients.SelectedItem is not SimpleClientListRow row) { txtMessage.Text = "編集する顧客を選択してください。"; return; }
            var handler = EditRequested;
            if (handler == null) { txtMessage.Text = "顧客管理への接続が設定されていません。"; return; }
            await RunAsync(async () =>
            {
                await _data.GetClientAsync(_employeeId, row.Id);
                if (!await handler(row.Id)) txtMessage.Text = "顧客を開けませんでした。接続と対象の顧客を確認してください。";
            });
        }
        private async void Edit_Click(object sender, RoutedEventArgs e) => await EditSelectedAsync();
        private async void Clients_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject origin || ItemsControl.ContainerFromElement(gridClients, origin) is not DataGridRow row) return;
            gridClients.SelectedItem = row.Item; await EditSelectedAsync();
        }
    }
}
