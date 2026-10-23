using System.Diagnostics;
using System.Globalization;
using System.Windows;
using Microsoft.EntityFrameworkCore;

namespace SalesManagement_SysDev
{
    public partial class F_passwordChange : Window
    {
        private bool _isSaving;

        public F_passwordChange() : this(string.Empty)
        {
        }

        public F_passwordChange(string employeeId)
        {
            InitializeComponent();
            txtEmployeeId.Text = employeeId;

            Loaded += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(txtEmployeeId.Text))
                {
                    txtEmployeeId.Focus();
                }
                else
                {
                    pwdCurrent.Focus();
                }
            };

            Closing += (_, e) =>
            {
                if (_isSaving)
                {
                    e.Cancel = true;
                }
            };
        }

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_isSaving)
            {
                return;
            }

            txtError.Visibility = Visibility.Collapsed;

            if (!int.TryParse(txtEmployeeId.Text.Trim(), NumberStyles.None,
                CultureInfo.InvariantCulture, out int employeeId))
            {
                ShowError("社員IDを数字で入力してください。");
                txtEmployeeId.Focus();
                return;
            }

            string currentPassword = pwdCurrent.Password;
            string newPassword = pwdNew.Password;
            string confirmPassword = pwdConfirm.Password;

            if (string.IsNullOrEmpty(currentPassword))
            {
                ShowError("現在のパスワードを入力してください。");
                pwdCurrent.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length > 10)
            {
                ShowError("新しいパスワードは1～10文字で入力してください。");
                pwdNew.Focus();
                return;
            }

            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                ShowError("確認用のパスワードが一致しません。");
                pwdConfirm.Focus();
                return;
            }

            if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
            {
                ShowError("現在と異なるパスワードを入力してください。");
                pwdNew.Focus();
                return;
            }

            SetBusy(true);
            bool saved = false;

            try
            {
                using var context = new SalesManagementContext();

                var employee = await context.MEmployees
                    .SingleOrDefaultAsync(e => e.EmId == employeeId);

                if (employee == null ||
                    !string.Equals(employee.EmPassword, currentPassword, StringComparison.Ordinal))
                {
                    ShowError("社員IDまたは現在のパスワードが正しくありません。");
                    pwdCurrent.Clear();
                    pwdCurrent.Focus();
                    return;
                }

                // パスワード更新
                employee.EmPassword = newPassword;
                await context.SaveChangesAsync();
                saved = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                ShowError("パスワードを変更できませんでした。データベースの接続を確認してください。");
            }
            finally
            {
                SetBusy(false);
            }

            if (saved)
            {
                pwdCurrent.Clear();
                pwdNew.Clear();
                pwdConfirm.Clear();
                DialogResult = true;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (!_isSaving)
            {
                DialogResult = false;
            }
        }

        private void ShowError(string message)
        {
            txtError.Text = message;
            txtError.Visibility = Visibility.Visible;
        }

        private void SetBusy(bool busy)
        {
            _isSaving = busy;
            btnSave.IsEnabled = !busy;
            btnCancel.IsEnabled = !busy;
            txtEmployeeId.IsEnabled = !busy;
            pwdCurrent.IsEnabled = !busy;
            pwdNew.IsEnabled = !busy;
            pwdConfirm.IsEnabled = !busy;
            btnSave.Content = busy ? "変更中..." : "変更する";
        }
    }
}