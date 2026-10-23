using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;

namespace SalesManagement_SysDev
{
    public partial class F_login : Window
    {
        private const int MaxFailedAttempts = 3;

        private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(3);
        private static readonly Dictionary<int, LoginAttempt> LoginAttempts = new();

        private readonly DispatcherTimer _lockTimer = new DispatcherTimer();

        private bool _isLoggingIn;
        private int? _countdownEmployeeId;

        public MEmployee? LoggedInEmployee { get; private set; }
        public string? LoggedInRole { get; private set; }

        private sealed class LoginAttempt
        {
            public int FailedCount { get; set; }
            public DateTimeOffset? LockedUntil { get; set; }
        }

        public F_login()
        {
            InitializeComponent();

            _lockTimer.Interval = TimeSpan.FromSeconds(1);
            _lockTimer.Tick += LockTimer_Tick;

            txtEmployeeId.TextChanged += (_, _) => UpdateLockCountdown();

            Loaded += (_, _) =>
            {
                txtEmployeeId.Focus();
                UpdateLockCountdown();
            };

            Closed += (_, _) => _lockTimer.Stop();
        }

        private async void btnLogin_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoggingIn)
            {
                return;
            }

            LoggedInEmployee = null;
            LoggedInRole = null;
            txtMessage.Visibility = Visibility.Collapsed;

            string employeeIdText = txtEmployeeId.Text.Trim();
            string password = pwdPassword.Password;

            if (string.IsNullOrWhiteSpace(employeeIdText))
            {
                ShowMessage("社員IDを入力してください。");
                txtEmployeeId.Focus();
                return;
            }

            if (!int.TryParse(employeeIdText, NumberStyles.None,
                CultureInfo.InvariantCulture, out int employeeId))
            {
                ShowMessage("社員IDは数字で入力してください。");
                txtEmployeeId.Focus();
                return;
            }

            if (ShowLockMessage(employeeId))
            {
                _countdownEmployeeId = employeeId;
                _lockTimer.Start();
                pwdPassword.Clear();
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowMessage("パスワードを入力してください。");
                pwdPassword.Focus();
                return;
            }

            SetBusy(true);

            try
            {
                using var context = new SalesManagementContext();

                var employee = await context.MEmployees
                    .AsNoTracking()
                    .Include(x => x.Po)
                    .Include(x => x.So)
                    .SingleOrDefaultAsync(x => x.EmId == employeeId);

                if (employee == null ||
                    !string.Equals(employee.EmPassword, password, StringComparison.Ordinal))
                {
                    RegisterFailedAttempt(employeeId);
                    pwdPassword.Clear();
                    return;
                }

                LoginAttempts.Remove(employeeId);
                _lockTimer.Stop();
                _countdownEmployeeId = null;

                string? role = GetLoginRole(employee);

                if (role == null)
                {
                    pwdPassword.Clear();
                    ShowMessage("役職が未設定、または対応していない役職です。管理者に確認してください。");
                    return;
                }

                LoggedInEmployee = employee;
                LoggedInRole = role;
                pwdPassword.Clear();

                // ログイン画面メニュー表示
                var menu = new F_menu(employee)
                {
                    Owner = this
                };

                Hide();

                try
                {
                    menu.ShowDialog();
                }
                finally
                {
                    // ログアウト終了_
                    // ログイン画面へ戻る
                    LoggedInEmployee = null;
                    LoggedInRole = null;

                    txtEmployeeId.Clear();
                    pwdPassword.Clear();

                    Show();
                    Activate();

                    ShowMessage("ログアウトしました。", true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                ShowMessage("ログインまたはメニューの表示に失敗しました。接続や設定を確認してください。");
            }
            finally
            {
                SetBusy(false);
                txtEmployeeId.Focus();
            }
        }

        private string? GetLoginRole(MEmployee employee)
        {
            return employee.Po?.PoName?.Trim() switch
            {
                "管理者" => "管理者",
                "物流" or "物流担当" => "物流担当",
                "営業" or "営業担当" => "営業担当",
                _ => null
            };
        }

        private void RegisterFailedAttempt(int employeeId)
        {
            if (!LoginAttempts.TryGetValue(employeeId, out var attempt))
            {
                attempt = new LoginAttempt();
                LoginAttempts.Add(employeeId, attempt);
            }

            attempt.FailedCount++;

            if (attempt.FailedCount >= MaxFailedAttempts)
            {
                attempt.LockedUntil = DateTimeOffset.UtcNow.Add(LockDuration);
                UpdateLockCountdown();
                return;
            }

            int remainingAttempts = MaxFailedAttempts - attempt.FailedCount;

            ShowMessage(
                $"社員IDまたはパスワードを確認してください。あと{remainingAttempts}回で3分間ロックされます。");
        }

        private bool ShowLockMessage(int employeeId)
        {
            if (!LoginAttempts.TryGetValue(employeeId, out var attempt) ||
                !attempt.LockedUntil.HasValue)
            {
                return false;
            }

            TimeSpan remaining = attempt.LockedUntil.Value - DateTimeOffset.UtcNow;

            if (remaining <= TimeSpan.Zero)
            {
                LoginAttempts.Remove(employeeId);
                return false;
            }

            int totalSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            ShowMessage($"ログイン制限中です。残り {minutes:D2}:{seconds:D2}");

            return true;
        }

        private void LockTimer_Tick(object? sender, EventArgs e)
        {
            UpdateLockCountdown();
        }

        private void UpdateLockCountdown()
        {
            if (!int.TryParse(txtEmployeeId.Text.Trim(), NumberStyles.None,
                CultureInfo.InvariantCulture, out int employeeId))
            {
                _lockTimer.Stop();

                if (_countdownEmployeeId.HasValue)
                {
                    txtMessage.Visibility = Visibility.Collapsed;
                }

                _countdownEmployeeId = null;
                return;
            }

            if (ShowLockMessage(employeeId))
            {
                _countdownEmployeeId = employeeId;
                _lockTimer.Start();
                return;
            }

            _lockTimer.Stop();

            if (_countdownEmployeeId == employeeId)
            {
                ShowMessage("ログイン制限が解除されました。再度ログインしてください。", true);
            }
            else if (_countdownEmployeeId.HasValue)
            {
                txtMessage.Visibility = Visibility.Collapsed;
            }

            _countdownEmployeeId = null;
        }

        private void btnPasswordChange_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoggingIn)
            {
                return;
            }

            var window = new F_passwordChange(txtEmployeeId.Text.Trim())
            {
                Owner = this
            };

            if (window.ShowDialog() == true)
            {
                LoggedInEmployee = null;
                LoggedInRole = null;
                pwdPassword.Clear();

                if (int.TryParse(txtEmployeeId.Text.Trim(), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int employeeId) &&
                    ShowLockMessage(employeeId))
                {
                    _countdownEmployeeId = employeeId;
                    _lockTimer.Start();
                }
                else
                {
                    ShowMessage(
                        "パスワードを変更しました。新しいパスワードでログインしてください。",
                        true);
                }

                pwdPassword.Focus();
            }
        }

        private void ShowMessage(string message, bool isSuccess = false)
        {
            txtMessage.Text = message;

            txtMessage.Foreground = isSuccess
                ? new SolidColorBrush(Color.FromRgb(16, 133, 141))
                : new SolidColorBrush(Color.FromRgb(214, 69, 69));

            txtMessage.Visibility = Visibility.Visible;
        }

        private void SetBusy(bool busy)
        {
            _isLoggingIn = busy;
            txtEmployeeId.IsEnabled = !busy;
            pwdPassword.IsEnabled = !busy;
            btnLogin.IsEnabled = !busy;
            btnPasswordChange.IsEnabled = !busy;
            btnLogin.Content = busy ? "確認中..." : "ログイン";
        }
    }
}