using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SalesManagement_SysDev
{
    public partial class ChumonSearchView : UserControl
    {
        private readonly IReadOnlyList<ChumonView.PreviewRow> _rows;
        public event EventHandler? BackRequested;
        public event Func<int, bool>? DisplayRequested;
        public bool IsBusy => false;
        public bool CanLeave() => true;

        public ChumonSearchView() : this(0) { }
        public ChumonSearchView(int loginEmployeeId)
            : this(loginEmployeeId, ChumonView.CreatePreviewRows()) { }
        public ChumonSearchView(int loginEmployeeId, IReadOnlyList<ChumonView.PreviewRow> rows)
        {
            _rows = rows ?? throw new ArgumentNullException(nameof(rows));
            InitializeComponent();
            cmbSearchClient.ItemsSource = new[] { "すべて" }.Concat(_rows.Select(x => x.Client).Distinct()).ToList();
            cmbSearchOffice.ItemsSource = new[] { "すべて" }.Concat(_rows.Select(x => x.Office).Distinct()).ToList();
            ResetConditions();
            ShowRows(_rows);
        }

        private void ResetConditions()
        {
            txtSearchId.Clear(); txtSearchOrderId.Clear();
            cmbSearchClient.SelectedIndex = 0; cmbSearchOffice.SelectedIndex = 0;
            cmbSearchState.SelectedIndex = 0;
            dpFrom.SelectedDate = null; dpFrom.Text = "";
            dpTo.SelectedDate = null; dpTo.Text = "";
        }

        private void ShowRows(IEnumerable<ChumonView.PreviewRow> rows)
        {
            var list = rows.ToList();
            gridChumons.ItemsSource = list;
            gridChumons.SelectedItem = list.FirstOrDefault();
            btnDisplay.IsEnabled = list.Count != 0;
            txtMessage.Text = $"画面サンプル・DB未接続。表示件数：{list.Count}件。対象を選択して「選択した注文を表示」を押してください。";
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

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string id = txtSearchId.Text.Trim(), orderId = txtSearchOrderId.Text.Trim();
                if (id.Any(c => c < '0' || c > '9') || orderId.Any(c => c < '0' || c > '9'))
                    throw new InvalidOperationException("IDは半角数字で入力してください。");
                DateTime? from = ReadDate(dpFrom, "開始日"), to = ReadDate(dpTo, "終了日");
                if (from.HasValue && to.HasValue && from.Value > to.Value)
                    throw new InvalidOperationException("開始日は終了日以前にしてください。");
                IEnumerable<ChumonView.PreviewRow> query = _rows;
                if (id.Length > 0) query = query.Where(x => x.Id.ToString(CultureInfo.InvariantCulture).StartsWith(id, StringComparison.Ordinal));
                if (orderId.Length > 0) query = query.Where(x => x.OrderId.ToString(CultureInfo.InvariantCulture).StartsWith(orderId, StringComparison.Ordinal));
                if (cmbSearchClient.SelectedIndex > 0) query = query.Where(x => x.Client == (string)cmbSearchClient.SelectedItem);
                if (cmbSearchOffice.SelectedIndex > 0) query = query.Where(x => x.Office == (string)cmbSearchOffice.SelectedItem);
                if (cmbSearchState.SelectedIndex == 1) query = query.Where(x => x.State == "未確定");
                if (cmbSearchState.SelectedIndex == 2) query = query.Where(x => x.State == "確定済");
                if (from.HasValue) query = query.Where(x => x.Date.Date >= from.Value);
                if (to.HasValue) query = query.Where(x => x.Date.Date <= to.Value);
                ShowRows(query);
            }
            catch (InvalidOperationException ex) { txtMessage.Text = ex.Message; }
        }

        private void List_Click(object sender, RoutedEventArgs e)
        {
            ResetConditions(); ShowRows(_rows);
        }
        private void Back_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);
        private void Chumons_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (btnDisplay != null) btnDisplay.IsEnabled = gridChumons.SelectedItem is ChumonView.PreviewRow;
        }
        private void Display_Click(object sender, RoutedEventArgs e) => DisplaySelected();
        private void Chumons_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Ignore column headers, scrollbars and empty space.
            if (e.OriginalSource is DependencyObject target &&
                ItemsControl.ContainerFromElement(gridChumons, target) is DataGridRow)
                DisplaySelected();
        }
        private void DisplaySelected()
        {
            if (gridChumons.SelectedItem is not ChumonView.PreviewRow row)
            {
                txtMessage.Text = "一覧から対象の注文を選択してください。"; return;
            }
            if (DisplayRequested == null)
            {
                txtMessage.Text = "F_menu の注文検索画面との接続を確認してください。"; return;
            }
            if (!DisplayRequested.Invoke(row.Id))
                txtMessage.Text = "注文を表示できませんでした。対象を選択し直してください。";
        }
    }
}
