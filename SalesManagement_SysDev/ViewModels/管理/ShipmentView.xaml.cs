using System.Windows;
using System.Windows.Controls;

namespace SalesManagement_SysDev
{
    // UI preview only. No entity queries, database writes or stock changes.
    public partial class ShipmentView : UserControl
    {
        public ShipmentView() => InitializeComponent();
        // Same constructor shape as existing menu screens; ID is unused in this preview.
        public ShipmentView(int loginEmployeeId) : this() { }
        public bool IsBusy => false;
        public bool CanLeave() => true;

        private void PreviewOnly_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("デザイン確認用の画面です。\n表示データはサンプルです。\n検索・非表示・確定のDB処理はまだ接続していません。",
                "出荷管理 — 画面サンプル", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
