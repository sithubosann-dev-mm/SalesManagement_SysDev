using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;

namespace SalesManagement_SysDev
{
    public partial class EmployeeView : UserControl
    {
        private readonly int _loginEmployeeId;

        private int? _selectedId;
        private bool _initialized;
        private bool _dirty;
        private PendingAction _pending;

        private enum PendingAction { None, Register, Update, Hide, Delete, Clear }

        public bool IsBusy { get; private set; }

        public event EventHandler? SearchRequested;
        public string EditLoadError { get; private set; } = "";

        // パスワード読むことがない
        public sealed class EmployeeRow
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public int OfficeId { get; set; }
            public string Office { get; set; } = "";
            public int PositionId { get; set; }
            public string Position { get; set; } = "";
            public DateTime HireDate { get; set; }
            public string Phone { get; set; } = "";
        }

        public EmployeeView(int loginEmployeeId)
        {
            InitializeComponent();

            _loginEmployeeId = loginEmployeeId;

            txtId.TextChanged += (_, _) => MarkDirty();
            txtName.TextChanged += (_, _) => MarkDirty();
            txtPhone.TextChanged += (_, _) => MarkDirty();

            pwdInitial.PasswordChanged += (_, _) => MarkDirty();

            cmbOffice.SelectionChanged += (_, _) => MarkDirty();
            cmbPosition.SelectionChanged += (_, _) => MarkDirty();

            dpHireDate.SelectedDateChanged += (_, _) => MarkDirty();
        }

        private void MarkDirty()
        {
            if (_initialized && !IsBusy)
            {
                _dirty = true;
            }
        }

        // 画面を離れる前に保存確認
        public bool CanLeave()
        {
            if (IsBusy)
            {
                MessageBox.Show("処理が終わるまでお待ちください。");

                return false;
            }

            if (!_dirty)
            {
                return true;
            }

            return MessageBox.Show(
                "未保存の入力を破棄しますか？",
                "確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        // 確認するdatabase
        private async Task CheckAdminAsync(SalesManagementContext db)
        {
            bool allowed = await db.MEmployees
                .AsNoTracking()
                .AnyAsync(x =>
                    x.EmId == _loginEmployeeId &&
                    x.EmFlag == 0 &&
                    x.Po.PoName == "管理者");

            if (!allowed)
            {
                throw new InvalidOperationException(
                    "社員管理を利用する権限がありません。再ログインしてください。");
            }
        }

        private async Task RunAsync(Func<Task> action)
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;

            formPanel.IsEnabled = false;
            gridEmployees.IsEnabled = false;

            txtMessage.Text = "処理中です…";

            try
            {
                await action();
            }
            catch (InvalidOperationException ex)
            {
                txtMessage.Text = ex.Message;
            }
            catch (DbUpdateException ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                txtMessage.Text = "保存できませんでした。関連データ・DB制約・接続を確認してください。削除できない社員は非表示を利用してください。";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);

                txtMessage.Text =
                    "処理に失敗しました。データベース接続と入力内容を確認してください。";
            }
            finally
            {
                IsBusy = false;

                formPanel.IsEnabled = _initialized;
                gridEmployees.IsEnabled = _initialized;
            }
        }

        private async Task InitializeFormAsync(SalesManagementContext db)
        {
            if (_initialized) return;
            cmbOffice.ItemsSource = await db.MSalesOffices.AsNoTracking()
                .OrderBy(x => x.SoId).ToListAsync();
            cmbPosition.ItemsSource = await db.MPositions.AsNoTracking()
                .OrderBy(x => x.PoId).ToListAsync();
            _initialized = true;
        }

        // 初回表示
        private async void View_Loaded(object sender, RoutedEventArgs e)
        {
            if (_initialized) return;
            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await CheckAdminAsync(db);
                await InitializeFormAsync(db);
                ResetForm();
                await LoadRowsAsync(db);
            });
        }

        // 検索結果のIDを受け取り、最新の社員情報を既存フォームに表示する
        public async Task<bool> OpenForEditAsync(int employeeId)
        {
            EditLoadError = "";
            if (!CanLeave()) return false;
            bool opened = false;

            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await CheckAdminAsync(db);
                await InitializeFormAsync(db);
                ResetForm();
                await LoadRowsAsync(db);

                var row = gridEmployees.Items.Cast<EmployeeRow>()
                    .FirstOrDefault(x => x.Id == employeeId);
                if (row == null)
                    throw new InvalidOperationException("対象社員が見つかりません。検索し直してください。");

                // 通常の一覧選択と同じ処理で、ID固定・パスワード空欄にする
                gridEmployees.SelectedItem = row;
                gridEmployees.ScrollIntoView(row);
                txtMessage.Text = $"社員ID：{row.Id} を編集できます。変更後に更新 → 確定を押してください。";
                opened = true;
            });

            if (!opened) EditLoadError = txtMessage.Text;
            return opened;
        }

        // 非表示ではない社員だけ表示　テストコード
        private async Task LoadRowsAsync(SalesManagementContext db)
        {
            var query = db.MEmployees
                .AsNoTracking()
                .Where(x => x.EmFlag == 0);

            gridEmployees.ItemsSource = await query
                .OrderBy(x => x.EmId)
                .Select(x => new EmployeeRow
                {
                    Id = x.EmId,
                    Name = x.EmName,

                    OfficeId = x.SoId,
                    Office = x.So.SoName,

                    PositionId = x.PoId,
                    Position = x.Po.PoName,

                    HireDate = x.EmHiredate,
                    Phone = x.EmPhone
                })
                .ToListAsync();

            txtMessage.Text =
                $"{gridEmployees.Items.Count}件を表示しています。";
        }

        private void ResetForm()
        {
            CancelPending();
            _selectedId = null;

            gridEmployees.SelectedItem = null;

            txtId.IsReadOnly = false;

            txtId.Clear();
            txtName.Clear();
            txtPhone.Clear();

            cmbOffice.SelectedIndex = -1;
            cmbPosition.SelectedIndex = -1;

            dpHireDate.SelectedDate = DateTime.Today;

            pwdInitial.IsEnabled = true;
            pwdInitial.Clear();

            _dirty = false;
        }

        // 選択た社員を入力表示する
        private void Employees_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (gridEmployees.SelectedItem is not EmployeeRow row)
            {
                return;
            }

            if (_dirty && !IsBusy)
            {
                var result = MessageBox.Show(
                    "未保存の入力を破棄しますか？",
                    "確認",
                    MessageBoxButton.YesNo);

                if (result != MessageBoxResult.Yes)
                {
                    gridEmployees.SelectionChanged -=
                        Employees_SelectionChanged;

                    gridEmployees.SelectedItem = gridEmployees.Items
                        .Cast<EmployeeRow>()
                        .FirstOrDefault(x => x.Id == _selectedId);

                    gridEmployees.SelectionChanged +=
                        Employees_SelectionChanged;

                    return;
                }
            }

            CancelPending();
            _selectedId = row.Id;

            txtId.Text = row.Id.ToString();
            txtId.IsReadOnly = true;

            txtName.Text = row.Name;
            txtPhone.Text = row.Phone;

            cmbOffice.SelectedValue = row.OfficeId;
            cmbPosition.SelectedValue = row.PositionId;

            dpHireDate.SelectedDate = row.HireDate;

            pwdInitial.Clear();
            pwdInitial.IsEnabled = false;

            _dirty = false;
        }

        // 入力チェック
        private int ValidateForm(bool registering)
        {
            if (!int.TryParse(txtId.Text.Trim(), out int id) || id <= 0)
            {
                throw new InvalidOperationException(
                    "社員IDは1以上の整数で入力してください。");
            }

            if (string.IsNullOrWhiteSpace(txtName.Text) ||
                txtName.Text.Trim().Length > 50)
            {
                throw new InvalidOperationException(
                    "社員名は1～50文字で入力してください。");
            }

            if (cmbOffice.SelectedValue is not int ||
                cmbPosition.SelectedValue is not int)
            {
                throw new InvalidOperationException(
                    "営業所と役職を選択してください。");
            }

            if (!dpHireDate.SelectedDate.HasValue)
            {
                throw new InvalidOperationException(
                    "入社日を選択してください。");
            }

            if (string.IsNullOrWhiteSpace(txtPhone.Text) ||
                txtPhone.Text.Trim().Length > 13)
            {
                throw new InvalidOperationException(
                    "電話番号は1～13文字で入力してください。");
            }

            if (registering &&
                (string.IsNullOrWhiteSpace(pwdInitial.Password) ||
                 pwdInitial.Password.Length > 10))
            {
                throw new InvalidOperationException(
                    "初期パスワードは1～10文字で入力してください。");
            }

            return id;
        }


        private void ApplyForm(MEmployee employee)
        {
            employee.EmName = txtName.Text.Trim();

            employee.SoId = (int)cmbOffice.SelectedValue;
            employee.PoId = (int)cmbPosition.SelectedValue;

            employee.EmHiredate = dpHireDate.SelectedDate!.Value.Date;
            employee.EmPhone = txtPhone.Text.Trim();
        }

        private void CancelPending()
        {
            _pending = PendingAction.None;
            btnConfirm.IsEnabled = false;
            txtPending.Text = "操作を選択してから確定を押してください。";
        }

        private string ActionName(PendingAction action)
        {
            return action switch
            {
                PendingAction.Register => "登録",
                PendingAction.Update => "更新",
                PendingAction.Hide => "非表示",
                PendingAction.Delete => "削除",
                PendingAction.Clear => "クリア",
                _ => ""
            };
        }

        // ここでは操作を選ぶだけ。DBへの保存は確定ボタンから行う
        private void ChooseAction(PendingAction action)
        {
            if (IsBusy || !_initialized) return;

            if (action == PendingAction.Register && _selectedId.HasValue)
            {
                txtMessage.Text = "新規登録する場合はクリアを確定してください。";
                return;
            }

            if ((action == PendingAction.Update || action == PendingAction.Hide ||
                 action == PendingAction.Delete) && !_selectedId.HasValue)
            {
                txtMessage.Text = "対象社員を一覧から選択してください。";
                return;
            }

            if ((action == PendingAction.Hide || action == PendingAction.Delete) &&
                _selectedId == _loginEmployeeId)
            {
                txtMessage.Text = "ログイン中の社員は非表示・削除できません。";
                return;
            }

            _pending = action;
            _dirty = true;
            btnConfirm.IsEnabled = true;
            txtPending.Text = $"選択中：{ActionName(action)} — 確定を押すと確認画面が表示されます。";
            txtMessage.Text = action == PendingAction.Delete
                ? "削除はDBからレコードを完全に削除します。"
                : $"{ActionName(action)}を選択しました。まだ確定していません。";
        }

        private void Register_Click(object sender, RoutedEventArgs e) => ChooseAction(PendingAction.Register);
        private void Update_Click(object sender, RoutedEventArgs e) => ChooseAction(PendingAction.Update);
        private void Hide_Click(object sender, RoutedEventArgs e) => ChooseAction(PendingAction.Hide);
        private void Delete_Click(object sender, RoutedEventArgs e) => ChooseAction(PendingAction.Delete);
        private void Clear_Click(object sender, RoutedEventArgs e) => ChooseAction(PendingAction.Clear);

        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (IsBusy || _pending == PendingAction.None) return;

            await RunAsync(async () =>
            {
                PendingAction action = _pending;
                string name = ActionName(action);

                if (action == PendingAction.Clear)
                {
                    if (MessageBox.Show("入力内容と選択中の操作をクリアしますか？", "確認",
                        MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    {
                        txtMessage.Text = "クリアをキャンセルしました。";
                        return;
                    }
                    ResetForm();
                    txtMessage.Text = "新規入力できます。";
                    return;
                }

                using var db = new SalesManagementContext();
                await CheckAdminAsync(db);

                MEmployee employee;
                if (action == PendingAction.Register)
                {
                    int id = ValidateForm(true);
                    if (await db.MEmployees.AnyAsync(x => x.EmId == id))
                        throw new InvalidOperationException("この社員IDは使用されています。");

                    employee = new MEmployee
                    {
                        EmId = id,
                        EmPassword = pwdInitial.Password,
                        EmFlag = 0
                    };
                    ApplyForm(employee);
                }
                else
                {
                    if (!_selectedId.HasValue)
                        throw new InvalidOperationException("対象社員を一覧から選択してください。");

                    employee = await db.MEmployees.SingleOrDefaultAsync(x =>
                        x.EmId == _selectedId.Value && x.EmFlag == 0)
                        ?? throw new InvalidOperationException("対象社員が見つかりません。一覧を再表示してください。");

                    if (action == PendingAction.Update)
                    {
                        ValidateForm(false);
                        if (employee.EmId == _loginEmployeeId &&
                            employee.PoId != (int)cmbPosition.SelectedValue)
                            throw new InvalidOperationException("ログイン中の社員の役職は変更できません。");
                    }
                    else if (employee.EmId == _loginEmployeeId)
                    {
                        throw new InvalidOperationException("ログイン中の社員は非表示・削除できません。");
                    }
                }

                string question = action switch
                {
                    PendingAction.Register => $"社員ID：{employee.EmId}　{employee.EmName} を登録しますか？",
                    PendingAction.Update => $"社員ID：{employee.EmId} の情報を更新しますか？",
                    PendingAction.Hide => $"{employee.EmName} を非表示にしますか？DBのレコードは残ります。",
                    PendingAction.Delete => $"{employee.EmName} をDBから完全に削除しますか？この操作は元に戻せません。",
                    _ => ""
                };

                if (MessageBox.Show(question, name + "の確認", MessageBoxButton.YesNo,
                    action == PendingAction.Delete ? MessageBoxImage.Warning : MessageBoxImage.Question)
                    != MessageBoxResult.Yes)
                {
                    txtMessage.Text = $"{name}をキャンセルしました。入力と操作の選択は残っています。";
                    return;
                }

                switch (action)
                {
                    case PendingAction.Register:
                        db.MEmployees.Add(employee);
                        break;
                    case PendingAction.Update:
                        ApplyForm(employee);
                        break;
                    case PendingAction.Hide:
                        employee.EmFlag = 2;
                        break;
                    case PendingAction.Delete:
                        db.MEmployees.Remove(employee);
                        break;
                }

                await db.SaveChangesAsync();
                ResetForm();
                txtMessage.Text = $"{name}しました。";

                // 保存成功後の再表示エラーは、保存失敗と区別する
                try
                {
                    await LoadRowsAsync(db);
                    txtMessage.Text = $"{name}しました。";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                    txtMessage.Text = $"{name}は完了しました。一覧の更新に失敗しました。一覧表示を押してください。";
                }
            });
        }

        private void OpenSearch_Click(object sender, RoutedEventArgs e)
        {
            if (!CanLeave()) return;
            ResetForm();
            SearchRequested?.Invoke(this, EventArgs.Empty);
        }

        private async void List_Click(object sender, RoutedEventArgs e)
        {
            if (!CanLeave()) return;

            await RunAsync(async () =>
            {
                using var db = new SalesManagementContext();
                await CheckAdminAsync(db);
                ResetForm();
                await LoadRowsAsync(db);
            });
        }
    }
}

