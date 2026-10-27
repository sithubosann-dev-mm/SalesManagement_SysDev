using System;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SalesManagement_SysDev
{
    public partial class F_menu : Window
    {
        private readonly DispatcherTimer _clockTimer = new DispatcherTimer();
        private readonly CultureInfo _clockCulture = CultureInfo.GetCultureInfo("ja-JP");
        private readonly MEmployee _employee;
        private readonly string _role;
        private readonly string[] _masters = { "顧客管理", "商品管理", "在庫管理", "社員管理" };
        private readonly string[] _business =
        {
            "受注管理", "注文管理", "出庫管理", "入荷管理",
            "出荷管理", "売上管理", "発注管理", "入庫管理"
        };

        public F_menu(MEmployee employee)
        {
            InitializeComponent();
            _employee = employee;
            _role = employee.Po?.PoName?.Trim() switch
            {
                "管理者" => "管理者",
                "営業" or "営業担当" => "営業担当",
                "物流" or "物流担当" => "物流担当",
                _ => ""
            };
            if (_role == "" || employee.EmFlag != 0)
                throw new InvalidOperationException("利用可能な社員・役職ではありません。");
            txtEmployeeInfo.Text = $"社員ID：{employee.EmId}　社員名：{employee.EmName}　役職：{_role}";
            txtOffice.Text = $"営業所：{employee.So?.SoName ?? "未設定"}";
            txtProfileName.Text = employee.EmName;
            txtProfileId.Text = $"社員ID：{employee.EmId}";
            txtProfileRole.Text = $"役職：{_role}";
            txtProfileOffice.Text = $"営業所：{employee.So?.SoName ?? "未設定"}";
            AddNavigationHeading("マスタ管理");
            foreach (string name in _masters) AddMenu(name, panelMaster);
            AddNavigationHeading("業務管理");
            foreach (string name in _business) AddMenu(name, panelBusiness);
            Closing += Menu_Closing;
            Closed += Menu_Closed;
            UpdateClock();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += Clock_Tick;
            _clockTimer.Start();
        }

        private void UpdateClock()
        {
            
            string current = DateTime.Now.ToString("yyyy/MM/dd (ddd) HH:mm", _clockCulture);
            if (txtCurrentDateTime.Text != current)
                txtCurrentDateTime.Text = current;
        }

        private void Clock_Tick(object? sender, EventArgs e) => UpdateClock();

        private void Menu_Closed(object? sender, EventArgs e)
        {
            _clockTimer.Stop();
            _clockTimer.Tick -= Clock_Tick;
            Closed -= Menu_Closed;
        }

        private bool CanAccess(string name)
        {
            bool knownScreen = Array.IndexOf(_masters, name) >= 0 || Array.IndexOf(_business, name) >= 0;
            if (!knownScreen) return false;
            return _role switch
            {
                "管理者" => true,
                "営業担当" => name is "顧客管理" or "受注管理" or "注文管理" or "入荷管理" or "出荷管理" or "売上管理",
                "物流担当" => name is "商品管理" or "在庫管理" or "出庫管理" or "発注管理" or "入庫管理",
                _ => false
            };
        }

        private void AddNavigationHeading(string title)
        {
            panelNavigation.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 133, 143)),
                Margin = new Thickness(0, 14, 0, 12)
            });
        }

        private void AddMenu(string name, WrapPanel panel)
        {
            if (!CanAccess(name)) return;
            var card = new Button { Content = name, Style = (Style)FindResource("MenuButtonStyle") };
            card.Click += (_, _) => OpenScreen(name);
            panel.Children.Add(card);
            var link = new Button { Content = name, Style = (Style)FindResource("SidebarMenuButtonStyle") };
            link.Click += (_, _) => OpenScreen(name);
            panelNavigation.Children.Add(link);
        }

        private bool CanLeaveScreen()
        {
            if (screenHost.Content is SyukkoView syukko) return syukko.CanLeave();
            if (screenHost.Content is SyukkoSearchView syukkoSearch) return syukkoSearch.CanLeave();
            if (screenHost.Content is ChumonView chumon) return chumon.CanLeave();
            if (screenHost.Content is ChumonSearchView chumonSearch) return chumonSearch.CanLeave();
            if (screenHost.Content is HattyuView hattyu) return hattyu.CanLeave();
            if (screenHost.Content is WarehousingView warehousing) return warehousing.CanLeave();
            if (screenHost.Content is StockView stock) return stock.CanLeave();
            if (screenHost.Content is StockSearchView stockSearch) return stockSearch.CanLeave();
            if (screenHost.Content is ClientView client) return client.CanLeave();
            if (screenHost.Content is ClientSearchView clientSearch) return clientSearch.CanLeave();
            if (screenHost.Content is EmployeeView view) return view.CanLeave();
            if (screenHost.Content is EmployeeSearchView search) return search.CanLeave();
            if (screenHost.Content is ProductView product) return product.CanLeave();
            if (screenHost.Content is ProductSearchView productSearch) return productSearch.CanLeave();
            if (screenHost.Content is OrderView order) return order.CanLeave();
            if (screenHost.Content is OrderSearchView orderSearch) return orderSearch.CanLeave();
            return true;
        }

        private void OpenScreen(string name)
        {
            if (!CanAccess(name))
            {
                MessageBox.Show("この画面を利用する権限がありません。", "アクセス制限", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if ((name == "注文管理" && screenHost.Content is ChumonView) ||
                (name == "出庫管理" && screenHost.Content is SyukkoView) ||
                (name == "入荷管理" && screenHost.Content is ArrivalView) ||
                (name == "出荷管理" && screenHost.Content is ShipmentView) ||
                (name == "売上管理" && screenHost.Content is SaleView) ||
                (name == "発注管理" && screenHost.Content is HattyuView) ||
                (name == "入庫管理" && screenHost.Content is WarehousingView) ||
                (name == "在庫管理" && screenHost.Content is StockView) ||
                (name == "受注管理" && screenHost.Content is OrderView) ||
                (name == "商品管理" && screenHost.Content is ProductView) ||
                (name == "社員管理" && screenHost.Content is EmployeeView) ||
                (name == "顧客管理" && screenHost.Content is ClientView)) return;
            if (!CanLeaveScreen()) return;
            if (name == "発注管理")
            {
                screenHost.Content = new HattyuView(_employee.EmId);
            }
            else if (name == "入庫管理")
            {
                screenHost.Content = new WarehousingView(_employee.EmId);
            }
            else if (name == "在庫管理")
            {
                var view = new StockView(_employee.EmId);
                view.SearchRequested += (_, _) => OpenStockSearch(view);
                screenHost.Content = view;
            }
            else if (name == "顧客管理")
            {
                var view = new ClientView(_employee.EmId);
                view.SearchRequested += (_, _) => OpenClientSearch(view);
                screenHost.Content = view;
            }
            else if (name == "社員管理")
            {
                var view = new EmployeeView(_employee.EmId);
                view.SearchRequested += (_, _) => OpenEmployeeSearch(view);
                screenHost.Content = view;
            }
            else if (name == "商品管理")
            {
                var view = new ProductView(_employee.EmId);
                view.SearchRequested += (_, _) => OpenProductSearch(view);
                screenHost.Content = view;
            }
            else if (name == "受注管理")
            {
                var view = new OrderView(_employee.EmId);
                view.SearchRequested += (_, _) => OpenOrderSearch(view);
                screenHost.Content = view;
            }
            else if (name == "注文管理")
            {
                var view = new ChumonView(_employee.EmId);
                view.SearchRequested += (_, _) => OpenChumonSearch(view);
                view.SyukkoRequested += syukkoId => OpenCreatedSyukkoAsync(view, syukkoId);
                screenHost.Content = view;
            }
            else if (name == "出庫管理")
            {
                var view = new SyukkoView(_employee.EmId);
                view.SearchRequested += (_, _) => OpenSyukkoSearch(view);
                screenHost.Content = view;
            }
            else if (name == "入荷管理")
            {
                screenHost.Content = new ArrivalView(_employee.EmId);
            }
            else if (name == "出荷管理")
            {
                screenHost.Content = new ShipmentView(_employee.EmId);
            }
            else if (name == "売上管理")
            {
                screenHost.Content = new SaleView(_employee.EmId);
            }
            else
            {
                screenHost.Content = new TextBlock
                {
                    Text = $"{name}は準備中です。",
                    FontSize = 24,
                    Margin = new Thickness(36),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Brushes.SlateGray
                };
            }
            homeView.Visibility = Visibility.Collapsed;
            screenHost.Visibility = Visibility.Visible;
            btnHamburger.Visibility = Visibility.Visible;
            sidebar.Visibility = Visibility.Visible;
            btnTopLogout.Visibility = Visibility.Collapsed;
            txtTitle.Text = $"販売在庫管理システム　／　{name}";
        }

        private void OpenEmployeeSearch(EmployeeView source)
        {
            if (!CanAccess("社員管理") || screenHost.Content != source) return;
            var search = new EmployeeSearchView(_employee.EmId);
            search.BackRequested += (_, _) =>
            {
                if (screenHost.Content != search || !search.CanLeave()) return;
                screenHost.Content = source;
                txtTitle.Text = "販売在庫管理システム　／　社員管理";
            };
            search.EditRequested += async employeeId =>
            {
                if (!CanAccess("社員管理") || screenHost.Content != search) return false;
                bool opened = await source.OpenForEditAsync(employeeId);
                if (!opened)
                {
                    if (!string.IsNullOrWhiteSpace(source.EditLoadError))
                        MessageBox.Show(source.EditLoadError, "社員情報の読み込み", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                screenHost.Content = source;
                txtTitle.Text = "販売在庫管理システム　／　社員管理";
                return true;
            };
            screenHost.Content = search;
            txtTitle.Text = "販売在庫管理システム　／　社員検索";
        }

        private void OpenChumonSearch(ChumonView source)
        {
            if (!CanAccess("注文管理") || screenHost.Content != source || !source.CanLeave()) return;
            var search = new ChumonSearchView(_employee.EmId);
            search.BackRequested += (_, _) =>
            {
                if (screenHost.Content != search || !search.CanLeave()) return;
                screenHost.Content = source;
                txtTitle.Text = "販売在庫管理システム　／　注文管理";
            };
            search.DisplayRequested += async chumonId =>
            {
                
                if (!CanAccess("注文管理") || screenHost.Content != search) return false;
                bool opened = await source.OpenChumonAsync(chumonId);
                if (screenHost.Content != search) return false;
                if (!opened)
                {
                    if (!string.IsNullOrWhiteSpace(source.DisplayLoadError))
                        MessageBox.Show(source.DisplayLoadError, "注文情報の読み込み",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                screenHost.Content = source;
                txtTitle.Text = "販売在庫管理システム　／　注文管理";
                return true;
            };
            screenHost.Content = search;
            txtTitle.Text = "販売在庫管理システム　／　注文検索";
        }

        private void OpenSyukkoSearch(SyukkoView source)
        {
            if (!CanAccess("出庫管理") || screenHost.Content != source || !source.CanLeave()) return;
            var search = new SyukkoSearchView(_employee.EmId);
            search.BackRequested += (_, _) =>
            {
                if (screenHost.Content != search || !search.CanLeave()) return;
                screenHost.Content = source;
                txtTitle.Text = "販売在庫管理システム　／　出庫管理";
            };
            search.DisplayRequested += async syukkoId =>
            {

                if (!CanAccess("出庫管理") || screenHost.Content != search) return false;
                bool opened = await source.OpenSyukkoAsync(syukkoId);
                if (screenHost.Content != search) return false;
                if (!opened)
                {
                    if (!string.IsNullOrWhiteSpace(source.DisplayLoadError))
                        MessageBox.Show(source.DisplayLoadError, "出庫情報の読み込み",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                screenHost.Content = source;
                txtTitle.Text = "販売在庫管理システム　／　出庫管理";
                return true;
            };
            screenHost.Content = search;
            txtTitle.Text = "販売在庫管理システム　／　出庫検索";
        }

        private async Task OpenCreatedSyukkoAsync(ChumonView source, int syukkoId)
        {

            if (screenHost.Content != source || !CanAccess("出庫管理")) return;
            var view = new SyukkoView(_employee.EmId);
            view.SearchRequested += (_, _) => OpenSyukkoSearch(view);
            bool opened = await view.OpenSyukkoAsync(syukkoId);
            if (!IsVisible || screenHost.Content != source) return;
            if (!opened)
            {
                MessageBox.Show("注文確定は完了しています。出庫画面の表示に失敗しました。\n" + view.DisplayLoadError,
                    "出庫情報の読み込み", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!source.CanLeave()) return;
            screenHost.Content = view;
            txtTitle.Text = "販売在庫管理システム　／　出庫管理";
        }

        private void OpenOrderSearch(OrderView source)
        {
            if (!CanAccess("受注管理") || screenHost.Content != source) return;
            var search = new OrderSearchView(_employee.EmId);
            search.BackRequested += (_, _) =>
            {
                if (screenHost.Content != search || !search.CanLeave()) return;
                screenHost.Content = source;
                txtTitle.Text = "販売在庫管理システム　／　受注管理";
            };
            search.DisplayRequested += async orderId =>
            {
                if (!CanAccess("受注管理") || screenHost.Content != search) return false;
                bool opened = await source.OpenOrderAsync(orderId);
                if (!opened)
                {
                    if (!string.IsNullOrWhiteSpace(source.DisplayLoadError))
                        MessageBox.Show(source.DisplayLoadError, "受注情報の読み込み", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                screenHost.Content = source;
                txtTitle.Text = "販売在庫管理システム　／　受注管理";
                return true;
            };
            screenHost.Content = search;
            txtTitle.Text = "販売在庫管理システム　／　受注検索";
        }

        private void OpenProductSearch(ProductView source)
        {
            if (!CanAccess("商品管理") || screenHost.Content != source) return;
            var search = new ProductSearchView(_employee.EmId);
            search.BackRequested += (_, _) =>
            {
                if (screenHost.Content != search || !search.CanLeave()) return;
                screenHost.Content = source;
                txtTitle.Text = "販売在庫管理システム　／　商品管理";
            };
            search.EditRequested += async productId =>
            {
                if (!CanAccess("商品管理") || screenHost.Content != search) return false;
                bool opened = await source.OpenForEditAsync(productId);
                if (!opened)
                {
                    if (!string.IsNullOrWhiteSpace(source.EditLoadError))
                        MessageBox.Show(source.EditLoadError, "商品情報の読み込み", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                screenHost.Content = source;
                txtTitle.Text = "販売在庫管理システム　／　商品管理";
                return true;
            };
            screenHost.Content = search;
            txtTitle.Text = "販売在庫管理システム　／　商品検索";
        }

        private void OpenClientSearch(ClientView source)
        {
            if (!CanAccess("顧客管理") || screenHost.Content != source) return;
            var search = new ClientSearchView(_employee.EmId);
            search.BackRequested += (_, _) =>
            {
                if (screenHost.Content != search || !search.CanLeave()) return;
                screenHost.Content = source;
                txtTitle.Text = "販売在庫管理システム　／　顧客管理";
            };
            search.EditRequested += async clientId =>
            {
                if (!CanAccess("顧客管理") || screenHost.Content != search) return false;
                bool opened = await source.OpenForEditAsync(clientId);
                if (!opened)
                {
                    if (!string.IsNullOrWhiteSpace(source.EditLoadError))
                        MessageBox.Show(source.EditLoadError, "顧客情報の読み込み", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                screenHost.Content = source;
                txtTitle.Text = "販売在庫管理システム　／　顧客管理";
                return true;
            };
            screenHost.Content = search;
            txtTitle.Text = "販売在庫管理システム　／　顧客検索";
        }

        private void OpenStockSearch(StockView source)
        {
            if (!CanAccess("在庫管理") || screenHost.Content != source) return;
            var search = new StockSearchView(_employee.EmId);
            search.BackRequested += (_, _) =>
            {
                if (screenHost.Content != search || !search.CanLeave()) return;
                screenHost.Content = source;
                txtTitle.Text = "販売在庫管理システム　／　在庫管理";
            };
            search.EditRequested += async stockId =>
            {
                if (!CanAccess("在庫管理") || screenHost.Content != search) return false;
                bool opened = await source.OpenForEditAsync(stockId);
                if (!opened)
                {
                    if (!string.IsNullOrWhiteSpace(source.EditLoadError))
                        MessageBox.Show(source.EditLoadError, "在庫情報の読み込み", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                screenHost.Content = source;
                txtTitle.Text = "販売在庫管理システム　／　在庫管理";
                return true;
            };
            screenHost.Content = search;
            txtTitle.Text = "販売在庫管理システム　／　在庫検索";
        }

        private void btnHamburger_Click(object sender, RoutedEventArgs e)
        {
            sidebar.Visibility = sidebar.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void btnHome_Click(object sender, RoutedEventArgs e)
        {
            if (!CanLeaveScreen()) return;
            screenHost.Content = null;
            screenHost.Visibility = Visibility.Collapsed;
            homeView.Visibility = Visibility.Visible;
            sidebar.Visibility = Visibility.Collapsed;
            btnHamburger.Visibility = Visibility.Collapsed;
            btnTopLogout.Visibility = Visibility.Visible;
            txtTitle.Text = "販売在庫管理システム";
        }

        private void btnLogout_Click(object sender, RoutedEventArgs e)
        {
            if ((screenHost.Content is SyukkoView syukko && syukko.IsBusy) ||
                (screenHost.Content is SyukkoSearchView syukkoSearch && syukkoSearch.IsBusy) ||
                (screenHost.Content is ChumonView chumon && chumon.IsBusy) ||
                (screenHost.Content is ChumonSearchView chumonSearch && chumonSearch.IsBusy) ||
                (screenHost.Content is HattyuView hattyu && hattyu.IsBusy) ||
                (screenHost.Content is WarehousingView warehousing && warehousing.IsBusy) ||
                (screenHost.Content is StockView stock && stock.IsBusy) ||
                (screenHost.Content is StockSearchView stockSearch && stockSearch.IsBusy) ||
                (screenHost.Content is ClientView client && client.IsBusy) ||
                (screenHost.Content is ClientSearchView clientSearch && clientSearch.IsBusy) ||
                (screenHost.Content is EmployeeView view && view.IsBusy) ||
                (screenHost.Content is EmployeeSearchView search && search.IsBusy) ||
                (screenHost.Content is ProductView product && product.IsBusy) ||
                (screenHost.Content is ProductSearchView productSearch && productSearch.IsBusy) ||
                (screenHost.Content is OrderView order && order.IsBusy) ||
                (screenHost.Content is OrderSearchView orderSearch && orderSearch.IsBusy))
            {
                MessageBox.Show("処理が終わるまでお待ちください。");
                return;
            }
            var result = MessageBox.Show("ログアウトしますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes) Close();
        }

        private void Menu_Closing(object? sender, CancelEventArgs e) => e.Cancel = !CanLeaveScreen();
    }
}
