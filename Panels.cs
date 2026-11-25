using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace WorkstationStudio
{
    // ===== 两个浮层面板共用的小样式 =====
    internal static class PanelUi
    {
        public static readonly Color Bg = Color.FromRgb(26, 31, 39);
        public static readonly Color Card = Color.FromRgb(32, 38, 47);
        public static readonly Color Line = Color.FromRgb(55, 65, 79);
        public static readonly Color Text = Color.FromRgb(230, 235, 242);
        public static readonly Color Sub = Color.FromRgb(147, 160, 176);
        public static readonly Color Accent = Color.FromRgb(0x3F, 0xA7, 0xD6);

        public static SolidColorBrush B(Color c) { return new SolidColorBrush(c); }

        public static TextBlock Label(string text, double top = 0)
        {
            return new TextBlock
            {
                Text = text, FontSize = 11, Foreground = B(Sub),
                Margin = new Thickness(0, top, 0, 2)
            };
        }

        public static TextBox Box(string text)
        {
            var tb = new TextBox
            {
                Text = text ?? "", Height = 26, FontSize = 12.5, Padding = new Thickness(6, 3, 6, 3),
                Background = B(Color.FromRgb(20, 25, 32)), Foreground = B(Text),
                BorderBrush = B(Line), BorderThickness = new Thickness(1),
                CaretBrush = B(Accent), Margin = new Thickness(0, 0, 0, 6)
            };
            UiHelp.SelectAllOnDoubleClick(tb);
            return tb;
        }

        // 自己的小按钮样式：任何状态都用我们设的底色（WPF 默认模板会盖掉）
        public static Style FlatButton()
        {
            var st = new Style(typeof(Button));
            var tpl = new ControlTemplate(typeof(Button));
            var bd = new FrameworkElementFactory(typeof(Border));
            bd.Name = "bd";
            bd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            bd.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            bd.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            bd.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(cp);
            tpl.VisualTree = bd;
            var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(58, 71, 86)), "bd"));
            tpl.Triggers.Add(hover);
            st.Setters.Add(new Setter(Button.TemplateProperty, tpl));
            return st;
        }

        public static Style FlatToggle()
        {
            var st = new Style(typeof(ToggleButton));
            var tpl = new ControlTemplate(typeof(ToggleButton));
            var bd = new FrameworkElementFactory(typeof(Border));
            bd.Name = "bd";
            bd.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            bd.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
            bd.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
            bd.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            bd.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bd.AppendChild(cp);
            tpl.VisualTree = bd;
            st.Setters.Add(new Setter(ToggleButton.TemplateProperty, tpl));
            return st;
        }

        private static bool HasButtonParent(DependencyObject d)
        {
            while (d != null)
            {
                if (d is Button || d is ToggleButton) return true;
                d = VisualTreeHelper.GetParent(d);
            }
            return false;
        }

        // 浮层里按 F1~F5 也转发给主窗口，保证两个详情面板在任意焦点下都能开关
        public static void ForwardFunctionKeys(Window w, MainWindow main)
        {
            w.PreviewKeyDown += (s, e) =>
            {
                if (e.Key >= Key.F1 && e.Key <= Key.F5)
                {
                    main.HandleFunctionKey(e.Key);
                    e.Handled = true;
                }
            };
        }

        // 深色圆角小窗，带标题 + ×，可拖动
        public static Border CardWindow(Window w, string title, out StackPanel body)
        {
            w.WindowStyle = WindowStyle.None;
            w.AllowsTransparency = true;
            w.Background = Brushes.Transparent;
            w.ShowInTaskbar = false;
            w.ResizeMode = ResizeMode.NoResize;
            w.SizeToContent = SizeToContent.Height;
            w.Title = title;
            w.FontFamily = new FontFamily("Microsoft YaHei, Segoe UI");

            var card = new Border
            {
                Background = B(Bg), BorderBrush = B(Color.FromRgb(63, 70, 84)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9),
                Padding = new Thickness(13), Margin = new Thickness(10)
            };

            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            head.Children.Add(new TextBlock
            {
                Text = title, FontSize = 13.5, FontWeight = FontWeights.Bold, Foreground = B(Text)
            });
            var close = new Button
            {
                Content = "×", Width = 20, Height = 20, Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right, Background = Brushes.Transparent,
                Foreground = B(Sub), BorderThickness = new Thickness(0), Cursor = Cursors.Hand
            };
            close.Click += (s, e) => { w.Close(); FocusHelp.Restore(); };
            // 任何方式关闭浮层（× / F2/F4 toggle / 互斥关掉），焦点都归位到主窗口，
            // 防止透明浮层关闭后整个程序退到别的程序后面。
            w.Closed += (s, e) => FocusHelp.Restore();
            DockPanel.SetDock(close, Dock.Right);
            head.Children.Add(close);

            body = new StackPanel();
            var root = new StackPanel();
            root.Children.Add(head);
            root.Children.Add(body);
            card.Child = root;


            // 拖动：按在标题条上就能拖
            head.MouseLeftButtonDown += (s, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed) return;
                if (HasButtonParent(e.OriginalSource as DependencyObject)) return;   // 别抢 × 的点击
                try { w.DragMove(); } catch { }
            };
            return card;
        }
    }

    // ===== 房间属性（只放房间本身的东西） =====
    public class RoomPanelWindow : Window
    {
        private MainWindow _main;
        private ComboBox _rooms;
        private ToggleButton _lock;
        private TextBox _cols, _rows;
        private TextBox _name;          // 当前房间名（解锁后可改）
        private TextBox _newName;       // 新建房间时用的名字（留空自动编号）
        private Button _addRoomBtn;     // 新建房间，空配置首次启动时呼吸闪光
        private DispatcherTimer _pulse;
        private Border _floorSw, _gridSw;
        private TextBlock _floorTx, _gridTx;
        private TextBlock _hint;
        private Button _floorBtn, _gridBtn;
        private Button _delRoomBtn;
        private bool _init;

        public RoomPanelWindow(MainWindow main)
        {
            _main = main;
            StackPanel body;
            var card = PanelUi.CardWindow(this, Loc.T("房间属性"), out body);
            this.Content = card;
            this.Width = 306;
            PanelUi.ForwardFunctionKeys(this, main);

            body.Children.Add(PanelUi.Label(Loc.T("当前房间")));
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8), LastChildFill = true };
            _lock = new ToggleButton
            {
                Width = 52, Height = 26, Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(0),
                Content = Loc.T("锁"), FontSize = 11.5, Cursor = Cursors.Hand,
                Background = PanelUi.B(PanelUi.Card), Foreground = PanelUi.B(PanelUi.Text),
                BorderBrush = PanelUi.B(PanelUi.Line), BorderThickness = new Thickness(1),
                ToolTip = Loc.T("锁上以后这个房间的所有内容都不能改（再点解锁）"),
                Style = PanelUi.FlatToggle()
            };
            _lock.Click += (s, e) => { _main.SetRoomLock(_lock.IsChecked == true); Refresh(); };
            DockPanel.SetDock(_lock, Dock.Right);
            row.Children.Add(_lock);

            _rooms = new ComboBox { Height = 26, FontSize = 12.5 };
            _rooms.SelectionChanged += RoomPicked;
            row.Children.Add(_rooms);
            body.Children.Add(row);

            // 房间改名：解锁后可直接改，回车或点别处生效；锁定状态只能看
            body.Children.Add(PanelUi.Label(Loc.T("房间名（解锁后可改，回车生效）"), 8));
            _name = PanelUi.Box("");
            _name.Margin = new Thickness(0, 0, 0, 10);
            _name.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { ApplyRename(); e.Handled = true; }
            };
            _name.LostFocus += (s, e) => ApplyRename();
            body.Children.Add(_name);

            // 新建房间：名字在这里先填，留空则自动按「房间N」编号
            body.Children.Add(PanelUi.Label(Loc.T("新建房间名（留空自动编号）")));
            _newName = PanelUi.Box("");
            _newName.Margin = new Thickness(0, 0, 0, 8);
            _newName.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { DoCreateRoom(); e.Handled = true; }
            };
            body.Children.Add(_newName);

            var btns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var add = SmallBtn(Loc.T("新建房间"), false);
            _addRoomBtn = add;
            add.ToolTip = Loc.T("按上面的名字新建房间（留空自动编号）");
            add.Click += (s, e) => DoCreateRoom();
            var del = SmallBtn(Loc.T("删除房间"), true);
            del.Click += (s, e) => { _main.DeleteCurrentRoom(); Refresh(); };
            _delRoomBtn = del;
            btns.Children.Add(add);
            btns.Children.Add(del);
            body.Children.Add(btns);

            body.Children.Add(PanelUi.Label(Loc.T("列（宽） / 行（高）")));
            var sizeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            _cols = PanelUi.Box("");
            _cols.Width = 96; _cols.Margin = new Thickness(0, 0, 6, 0);
            _cols.TextChanged += SizeChanged;
            _rows = PanelUi.Box("");
            _rows.Width = 96;
            _rows.TextChanged += SizeChanged;
            sizeRow.Children.Add(_cols);
            sizeRow.Children.Add(new TextBlock { Text = "×", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0, 8, 0), Foreground = PanelUi.B(PanelUi.Sub) });
            sizeRow.Children.Add(_rows);
            body.Children.Add(sizeRow);

            var colors = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            _floorSw = Swatch(out _floorTx);
            _gridSw = Swatch(out _gridTx);
            var fb = ColorBtn(Loc.T("地板色"), _floorSw, _floorTx);
            fb.Click += (s, e) => { _main.PickFloorColor(); Refresh(); };
            var gb = ColorBtn(Loc.T("格线色"), _gridSw, _gridTx);
            gb.Click += (s, e) => { _main.PickGridColor(); Refresh(); };
            _floorBtn = fb;
            _gridBtn = gb;
            var c1 = new StackPanel { Width = 136 };
            c1.Children.Add(PanelUi.Label(Loc.T("地板色")));
            c1.Children.Add(fb);
            var c2 = new StackPanel { Width = 136, Margin = new Thickness(8, 0, 0, 0) };
            c2.Children.Add(PanelUi.Label(Loc.T("格线色")));
            c2.Children.Add(gb);
            colors.Children.Add(c1);
            colors.Children.Add(c2);
            body.Children.Add(colors);

            _hint = new TextBlock
            {
                Text = "", FontSize = 10.5, Foreground = PanelUi.B(PanelUi.Sub),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0)
            };
            body.Children.Add(_hint);

            // 打印：把这块地板上的全部工位按黑白线稿出一张布局图，走系统打印机
            var printBtn = SmallBtn(Loc.T("打印房间布局"), false);
            printBtn.Height = 30;
            printBtn.Margin = new Thickness(0, 12, 0, 0);
            printBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
            printBtn.HorizontalContentAlignment = HorizontalAlignment.Center;
            printBtn.ToolTip = Loc.T("打印当前房间地板里的全部工位（黑白线稿，A4，调用系统打印机）");
            printBtn.Click += (s, e) => _main.PrintRoomLayout();
            body.Children.Add(printBtn);

            this.Closed += (s, e) => StopPulse();
            this.Loaded += (s, e) => Refresh();
        }

        private static Button SmallBtn(string text, bool danger)
        {
            var b = new Button
            {
                Content = text, Height = 26, Padding = new Thickness(10, 0, 10, 0), FontSize = 12,
                Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand,
                Background = PanelUi.B(danger ? Color.FromRgb(92, 42, 42) : PanelUi.Card),
                Foreground = PanelUi.B(danger ? Color.FromRgb(240, 190, 190) : PanelUi.Text),
                BorderBrush = PanelUi.B(danger ? Color.FromRgb(120, 60, 60) : PanelUi.Line),
                BorderThickness = new Thickness(1)
            };
            b.Style = TryStyle("ToolBtn");
            return b;
        }

        private static Style TryStyle(string key)
        {
            try { return Application.Current.TryFindResource(key) as Style; }
            catch { return null; }
        }

        private static Border Swatch(out TextBlock tx)        {
            var b = new Border
            {
                Width = 22, Height = 14, CornerRadius = new CornerRadius(3),
                Background = PanelUi.B(PanelUi.Line), BorderBrush = PanelUi.B(Color.FromRgb(74, 86, 101)),
                BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            tx = new TextBlock { FontSize = 9.5, VerticalAlignment = VerticalAlignment.Center, Foreground = PanelUi.B(PanelUi.Sub) };
            return b;
        }

        private static Button ColorBtn(string label, Border sw, TextBlock tx)
        {
            var b = new Button
            {
                Height = 26, Padding = new Thickness(6, 0, 6, 0), Cursor = Cursors.Hand,
                Background = PanelUi.B(PanelUi.Card), BorderBrush = PanelUi.B(PanelUi.Line),
                BorderThickness = new Thickness(1), HorizontalContentAlignment = HorizontalAlignment.Left
            };
            b.Style = TryStyle("ToolBtn");
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(sw);
            sp.Children.Add(tx);
            b.Content = sp;
            b.ToolTip = Loc.T("点开选") + label;
            return b;
        }

        // 回车/失焦时把输入框里的名字落到当前房间；改不成（空名/重名/锁定）就还原
        private void ApplyRename()
        {
            if (_init || _main == null || _name == null) return;
            var room = _main.CurrentRoomData;
            if (room == null) return;
            string want = (_name.Text ?? "").Trim();
            if (want == (room.Name ?? "").Trim()) return;
            if (!_main.RenameCurrentRoom(want)) _name.Text = room.Name ?? "";
        }

        private void DoCreateRoom()
        {
            if (_main == null) return;
            StopPulse();
            string nm = _newName != null ? _newName.Text : null;
            if (_newName != null) _newName.Text = "";
            _main.NewRoomFromPanel(nm);
            Refresh();
        }

        // ===== 空配置首次启动：新建房间按钮呼吸闪光（和搜索命中同一支粉）=====
        internal void PulseNewRoom()
        {
            if (_addRoomBtn == null) return;
            StopPulse();
            var glow = new DropShadowEffect
            {
                Color = Color.FromRgb(0xFF, 0x2D, 0x9B), ShadowDepth = 0,
                BlurRadius = 10, Opacity = 0.6, RenderingBias = RenderingBias.Performance
            };
            _addRoomBtn.Effect = glow;
            var t0 = DateTime.Now;
            _pulse = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
            _pulse.Tick += (s, e) =>
            {
                if (_addRoomBtn == null) { StopPulse(); return; }
                double k = 0.5 + 0.5 * Math.Sin((DateTime.Now - t0).TotalSeconds * Math.PI * 2 / 1.3);
                _addRoomBtn.Background = PanelUi.B(Mix(Color.FromRgb(0x46, 0x10, 0x30), Color.FromRgb(0xFF, 0x2D, 0x9B), 0.18 + 0.55 * k));
                _addRoomBtn.Foreground = PanelUi.B(Mix(Color.FromRgb(0xFF, 0xE4, 0xF2), Colors.White, k));
                _addRoomBtn.BorderBrush = PanelUi.B(Mix(Color.FromRgb(0x7A, 0x1F, 0x52), Color.FromRgb(0xFF, 0x5F, 0xB5), k));
                _addRoomBtn.BorderThickness = new Thickness(1.2 + 0.6 * k);
                glow.BlurRadius = 6 + 16 * k;
                glow.Opacity = 0.30 + 0.55 * k;
            };
            _pulse.Start();
        }

        internal void StopPulse()
        {
            if (_pulse != null) { _pulse.Stop(); _pulse = null; }
            if (_addRoomBtn == null) return;
            _addRoomBtn.Effect = null;
            // 还原成普通小按钮的样子（本地值盖着样式，得手动恢复）
            _addRoomBtn.Background = PanelUi.B(PanelUi.Card);
            _addRoomBtn.Foreground = PanelUi.B(PanelUi.Text);
            _addRoomBtn.BorderBrush = PanelUi.B(PanelUi.Line);
            _addRoomBtn.BorderThickness = new Thickness(1);
        }

        private static Color Mix(Color a, Color b, double t)
        {
            if (t < 0) t = 0; if (t > 1) t = 1;
            return Color.FromRgb(
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }

        public void Refresh()
        {
            if (_main == null) return;
            _init = true;
            try
            {
                var room = _main.CurrentRoomData;
                _rooms.Items.Clear();
                foreach (var r in _main.AllRooms)
                    _rooms.Items.Add(new ComboBoxItem { Content = r.Name, Tag = r.Id });
                for (int i = 0; i < _main.AllRooms.Count; i++)
                {
                    var it = _rooms.Items[i] as ComboBoxItem;
                    if (it != null && room != null && (string)it.Tag == room.Id) { _rooms.SelectedIndex = i; break; }
                }
                if (room == null) return;

                _cols.Text = room.Cols.ToString(CultureInfo.InvariantCulture);
                _rows.Text = room.Rows.ToString(CultureInfo.InvariantCulture);
                _floorSw.Background = PanelUi.B(Safe(room.FloorColor, Color.FromRgb(30, 35, 43)));
                _gridSw.Background = PanelUi.B(Safe(room.GridColor, Color.FromRgb(44, 51, 63)));
                _floorTx.Text = (room.FloorColor ?? "").ToUpperInvariant();
                _gridTx.Text = (room.GridColor ?? "").ToUpperInvariant();

                // 房间名：解锁才能改，锁定时置灰只读
                _name.Text = room.Name ?? "";
                _name.IsEnabled = !room.Locked;
                _name.Opacity = room.Locked ? 0.5 : 1;
                _name.ToolTip = room.Locked ? Loc.T("房间已锁定，解锁后才能改名") : Loc.T("改完按回车（或点别处）生效");

                _lock.IsChecked = room.Locked;
                _lock.Content = room.Locked ? Loc.T("已锁") : Loc.T("锁");
                _lock.Background = PanelUi.B(room.Locked ? Color.FromRgb(0x7A, 0x5A, 0x1E) : PanelUi.Card);
                _lock.Foreground = PanelUi.B(room.Locked ? Color.FromRgb(0xFF, 0xD9, 0x8A) : PanelUi.Text);

                // 房间锁 = 大小不可改 + 工位不能移动 + 地板格线色不可改
                _cols.IsEnabled = !room.Locked;
                _rows.IsEnabled = !room.Locked;
                _cols.Opacity = room.Locked ? 0.5 : 1;
                _rows.Opacity = room.Locked ? 0.5 : 1;
                _floorBtn.IsEnabled = !room.Locked;
                _gridBtn.IsEnabled = !room.Locked;
                _floorBtn.Opacity = room.Locked ? 0.5 : 1;
                _gridBtn.Opacity = room.Locked ? 0.5 : 1;
                if (_delRoomBtn != null)
                {
                    _delRoomBtn.IsEnabled = !room.Locked;
                    _delRoomBtn.Opacity = room.Locked ? 0.5 : 1;
                    _delRoomBtn.ToolTip = room.Locked ? Loc.T("房间已锁定，先解锁再删") : Loc.T("删除当前房间（两次确认）");
                }
                _hint.Text = room.Locked
                    ? Loc.T("房间已锁定：房间大小、工位块位置/增删、地板格线色都不能改；工位内容仍可编辑、Ctrl 交换照常。点「已锁」解锁。")
                    : Loc.T("锁上后房间大小不能改、工位不能移动、地板格线色锁定；工位内容仍可编辑、Ctrl 交换照常。");
            }
            finally { _init = false; }
        }

        private void RoomPicked(object sender, SelectionChangedEventArgs e)
        {
            if (_init) return;
            var it = _rooms.SelectedItem as ComboBoxItem;
            if (it == null) return;
            _main.SwitchRoomById((string)it.Tag);
            Refresh();
        }

        private void SizeChanged(object sender, TextChangedEventArgs e)
        {
            if (_init) return;
            _main.SetRoomSizeFromPanel(_cols.Text, _rows.Text);
        }

        internal static Color Safe(string hex, Color fallback)
        {
            try { return (Color)ColorConverter.ConvertFromString(hex); }
            catch { return fallback; }
        }
    }

    // ===== 工位详情（选中工位 + 主机详细信息） =====
    public class CellPanelWindow : Window
    {
        private MainWindow _main;
        private TextBlock _title;
        private TextBlock _empty;
        private StackPanel _form;
        private TextBox _name, _port, _computer, _monitor, _emp;
        private CheckBox _dual;
        private ToggleButton _editBtn;
        private TextBlock _editHint;
        private StackPanel _machines;
        private ScrollViewer _machineScroll;
        private Button _addBtn, _delBlockBtn;
        private TextBlock _machineEmpty;

        private BlockInstance _block;
        private WorkCellData _cell;
        private bool _roomLocked;
        private bool _edit;
        private bool _bind;
        private bool _rebuild;

        public CellPanelWindow(MainWindow main)
        {
            _main = main;
            StackPanel body;
            var card = PanelUi.CardWindow(this, Loc.T("工位详情"), out body);
            this.Content = card;
            this.Width = 320;
            PanelUi.ForwardFunctionKeys(this, main);

            _title = new TextBlock
            {
                Text = "", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = PanelUi.B(PanelUi.Accent),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6)
            };
            body.Children.Add(_title);

            _empty = new TextBlock
            {
                Text = Loc.T("点房间里的工位格，这里显示它的人名、机型、IP/MAC。"),
                FontSize = 11, Foreground = PanelUi.B(PanelUi.Sub), TextWrapping = TextWrapping.Wrap
            };
            body.Children.Add(_empty);

            _form = new StackPanel { Visibility = Visibility.Collapsed };
            body.Children.Add(_form);

            _form.Children.Add(PanelUi.Label(Loc.T("人名")));
            _name = PanelUi.Box(""); _name.TextChanged += Field; _form.Children.Add(_name);
            _form.Children.Add(PanelUi.Label(Loc.T("网线口号")));
            _port = PanelUi.Box(""); _port.TextChanged += Field; _form.Children.Add(_port);
            _form.Children.Add(PanelUi.Label(Loc.T("主机型号（空格分开，最多 4 个）")));
            _computer = PanelUi.Box(""); _computer.TextChanged += Field; _form.Children.Add(_computer);
            _form.Children.Add(PanelUi.Label(Loc.T("显示器型号")));
            _monitor = PanelUi.Box(""); _monitor.TextChanged += Field; _form.Children.Add(_monitor);
            _form.Children.Add(PanelUi.Label(Loc.T("工号")));
            _emp = PanelUi.Box(""); _emp.TextChanged += Field; _form.Children.Add(_emp);

            _dual = new CheckBox { Content = Loc.T("双网线"), Margin = new Thickness(0, 0, 0, 10), Foreground = PanelUi.B(PanelUi.Text) };
            _dual.Click += (s, e) => { if (_bind || _cell == null) return; _cell.Dual = _dual.IsChecked == true; Changed(); };
            _form.Children.Add(_dual);

            var mh = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
            mh.Children.Add(new TextBlock
            {
                Text = Loc.T("主机详细信息"), FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = PanelUi.B(Color.FromRgb(79, 178, 134)), VerticalAlignment = VerticalAlignment.Center
            });
            _editBtn = new ToggleButton
            {
                Content = Loc.T("编辑"), Height = 22, Padding = new Thickness(9, 0, 9, 0), FontSize = 11,
                Margin = new Thickness(8, 0, 0, 0), Cursor = Cursors.Hand,
                Background = PanelUi.B(PanelUi.Card), Foreground = PanelUi.B(PanelUi.Text),
                BorderBrush = PanelUi.B(PanelUi.Line), BorderThickness = new Thickness(1),
                ToolTip = Loc.T("点开可改主机名 / IP / MAC / 备注，也能加删主机"),
                Style = PanelUi.FlatToggle()
            };
            _editBtn.Click += (s, e) => ToggleEdit(_editBtn.IsChecked == true);
            mh.Children.Add(_editBtn);
            _editHint = new TextBlock
            {
                Text = Loc.T("只读"), FontSize = 10.5, Foreground = PanelUi.B(PanelUi.Sub),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0)
            };
            mh.Children.Add(_editHint);
            _form.Children.Add(mh);

            _machineEmpty = new TextBlock
            {
                Text = Loc.T("暂无主机信息。点「编辑」后再点下面的 + 加一台。"),
                FontSize = 10.5, Foreground = PanelUi.B(PanelUi.Sub), TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4), Visibility = Visibility.Collapsed
            };
            _form.Children.Add(_machineEmpty);

            _machines = new StackPanel();
            _machineScroll = new ScrollViewer
            {
                MaxHeight = 268, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _machines, Margin = new Thickness(0, 0, 0, 2)
            };
            _form.Children.Add(_machineScroll);

            _addBtn = new Button
            {
                Content = "＋", Width = 22, Height = 20, FontSize = 11, Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left, Cursor = Cursors.Hand,
                Background = PanelUi.B(Color.FromRgb(40, 48, 58)),
                Foreground = PanelUi.B(Color.FromRgb(180, 200, 220)),
                BorderBrush = PanelUi.B(PanelUi.Line), BorderThickness = new Thickness(1),
                ToolTip = Loc.T("加一台机器（备注/IP/MAC 自己填）"), Visibility = Visibility.Collapsed
            };
            _addBtn.Click += (s, e) => AddMachine();
            _form.Children.Add(_addBtn);

            _form.Children.Add(new Border
            {
                Height = 1, Background = PanelUi.B(PanelUi.Line), Margin = new Thickness(0, 10, 0, 8)
            });
            _delBlockBtn = new Button
            {
                Content = Loc.T("删除选中工位块"), Height = 28, FontSize = 12, Cursor = Cursors.Hand,
                Background = PanelUi.B(Color.FromRgb(110, 42, 42)),
                Foreground = PanelUi.B(Color.FromRgb(232, 238, 246)),
                BorderBrush = PanelUi.B(Color.FromRgb(130, 60, 60)), BorderThickness = new Thickness(1)
            };
            _delBlockBtn.Style = TryStyle("ToolBtn");
            _delBlockBtn.Click += (s, e) => _main.DeleteSelectedBlockFromPanel();
            _form.Children.Add(_delBlockBtn);

            this.Loaded += (s, e) => { if (_cell != null) Bind(_block, _cell, _roomLocked); };
        }

        private static Style TryStyle(string key)
        {
            try { return Application.Current.TryFindResource(key) as Style; }
            catch { return null; }
        }

        public void Bind(BlockInstance b, WorkCellData c, bool roomLocked)
        {
            _block = b; _cell = c; _roomLocked = roomLocked;
            if (c == null)
            {
                _form.Visibility = Visibility.Collapsed;
                _empty.Visibility = Visibility.Visible;
                _title.Text = "";
                return;
            }
            _bind = true;
            try
            {
                _form.Visibility = Visibility.Visible;
                _empty.Visibility = Visibility.Collapsed;

                int idx = b != null && b.Cells != null ? b.Cells.IndexOf(c) : -1;
                _title.Text = (b != null ? b.Name : Loc.T("工位")) +
                              (idx >= 0 ? Loc.F(" · 第 {0} 格", idx + 1) : "") +
                              (c.IsLeader ? Loc.T("（领导位）") : "") + (b != null && b.Locked ? Loc.T(" · 已固定") : "") +
                              (roomLocked ? Loc.T(" · 房间已锁") : "");

                _name.Text = c.Name;
                _port.Text = c.Port;
                _computer.Text = c.Computer;
                _monitor.Text = c.Monitor;
                _emp.Text = c.EmployeeId;
                _dual.IsChecked = c.Dual;

                // 房间锁定只锁布局（工位位置/增删、地板格线色），工位内容照样能编辑
                foreach (var tb in new[] { _name, _port, _computer, _monitor, _emp })
                    tb.IsReadOnly = false;
                _dual.IsEnabled = true;
                _editBtn.IsEnabled = true;

                // 换格子就回到只读，避免误改
                _edit = false;
                _editBtn.IsChecked = false;
                _editBtn.Content = Loc.T("编辑");
                _editHint.Text = Loc.T("只读");

                RebuildMachines();
                UpdateDeleteButton();
            }
            finally { _bind = false; }
        }

        public void SetRoomLocked(bool locked)
        {
            if (_cell != null) Bind(_block, _cell, locked);
        }

        public void RefreshTitle()
        {
            if (_cell == null || _bind) return;
            int idx = _block != null && _block.Cells != null ? _block.Cells.IndexOf(_cell) : -1;
            _title.Text = (_block != null ? _block.Name : Loc.T("工位")) +
                          (idx >= 0 ? Loc.F(" · 第 {0} 格", idx + 1) : "") +
                          (_cell.IsLeader ? Loc.T("（领导位）") : "") + (_block != null && _block.Locked ? Loc.T(" · 已固定") : "") +
                          (_roomLocked ? Loc.T(" · 房间已锁") : "");
        }

        private void UpdateDeleteButton()
        {
            bool can = _block != null && !_block.Locked && !_roomLocked;
            _delBlockBtn.IsEnabled = can;
            _delBlockBtn.Content = _block != null && _block.Locked ? Loc.T("已固定 · 不能删除")
                : (_roomLocked ? Loc.T("房间已锁 · 不能删除") : Loc.T("删除选中工位块"));
        }

        private void ToggleEdit(bool on)
        {
            _edit = on;
            if (_edit && _cell != null && (_cell.Machines == null || _cell.Machines.Count == 0))
            {
                _cell.Machines = new List<MachineInfo>();
                _cell.Machines.Add(new MachineInfo());
                Changed();
            }
            _editBtn.Content = _edit ? Loc.T("编辑中") : Loc.T("编辑");
            _editHint.Text = _edit ? Loc.T("可改主机名 / IP / MAC / 备注") : Loc.T("只读");
            RebuildMachines();
        }

        private void Field(object sender, TextChangedEventArgs e)
        {
            if (_bind || _rebuild || _cell == null) return;
            _cell.Name = _name.Text;
            _cell.Port = _port.Text;
            _cell.Computer = _computer.Text;
            _cell.Monitor = _monitor.Text;
            _cell.EmployeeId = _emp.Text;
            Changed();
        }

        private void Changed()
        {
            if (_cell == null) return;
            _main.CellEdited(_block, _cell);
        }

        // ---- 主机卡片 ----
        private void RebuildMachines()
        {
            _rebuild = true;
            try { RebuildMachinesCore(); }
            finally { _rebuild = false; }
        }

        private void RebuildMachinesCore()
        {
            _machines.Children.Clear();
            var list = _cell != null ? _cell.Machines : null;
            bool hasAny = list != null && list.Count > 0;
            _machineEmpty.Visibility = hasAny ? Visibility.Collapsed : Visibility.Visible;
            _machineScroll.Visibility = hasAny ? Visibility.Visible : Visibility.Collapsed;
            _addBtn.Visibility = _edit ? Visibility.Visible : Visibility.Collapsed;
            if (!hasAny) return;

            for (int i = 0; i < list.Count; i++)
                _machines.Children.Add(MakeMachineCard(list[i], i));
        }

        private Border MakeMachineCard(MachineInfo m, int idx)
        {
            var card = new Border
            {
                Background = PanelUi.B(PanelUi.Card), CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 5, 7, 5), Margin = new Thickness(0, 0, 0, 6)
            };
            var g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 第一行：[内外] + 主机名（可写）
            var head = new DockPanel { LastChildFill = true };
            head.Children.Add(new TextBlock
            {
                Text = "[" + Loc.NetLabel(m.NetType) + "]",
                FontSize = 11, FontWeight = FontWeights.Bold, Foreground = PanelUi.B(PanelUi.Text),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0)
            });

            // 右上角小红 ×（只在编辑态出现）
            var del = new Button
            {
                Content = "×", Width = 16, Height = 16, Padding = new Thickness(0), FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = PanelUi.B(Color.FromRgb(0xE0, 0x6C, 0x6C)), Cursor = Cursors.Hand,
                Visibility = _edit ? Visibility.Visible : Visibility.Collapsed,
                ToolTip = Loc.T("删除这台机器")
            };
            bool confirm = false;
            var revert = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            revert.Tick += (s, e) =>
            {
                revert.Stop(); confirm = false;
                del.Content = "×"; del.Width = 16;
            };
            del.Click += (s, e) =>
            {
                if (!confirm)
                {
                    confirm = true; del.Content = Loc.T("删?"); del.Width = 28;
                    revert.Stop(); revert.Start();
                    return;
                }
                revert.Stop();
                if (_cell.Machines != null && idx < _cell.Machines.Count)
                {
                    _cell.Machines.RemoveAt(idx);
                    Changed();
                    RebuildMachines();
                }
            };
            DockPanel.SetDock(del, Dock.Right);
            head.Children.Add(del);

            var model = new TextBox
            {
                Text = m.Model ?? "", FontSize = 11.5, FontWeight = FontWeights.Bold,
                Padding = new Thickness(2, 1, 2, 1), BorderThickness = new Thickness(0),
                Background = Brushes.Transparent, Foreground = PanelUi.B(PanelUi.Text),
                CaretBrush = PanelUi.B(PanelUi.Accent),
                IsReadOnly = !_edit,
                ToolTip = Loc.T("主机名/型号，可改")
            };
            UiHelp.SelectAllOnDoubleClick(model);
            model.TextChanged += (s, e) =>
            {
                if (_bind || _rebuild) return;
                m.Model = model.Text;
                // 格子里的型号栏空着就顺手补上，已有内容不动
                if (string.IsNullOrEmpty((_cell.Computer ?? "").Trim())) _computer.Text = model.Text;
                Changed();
            };
            head.Children.Add(model);
            Grid.SetRow(head, 0);
            g.Children.Add(head);

            g.Children.Add(MachineLine(1, "IP", m.Ip, delegate (string v) { m.Ip = v; }));
            g.Children.Add(MachineLine(2, "MAC", m.Mac, delegate (string v) { m.Mac = v; }));
            g.Children.Add(MachineLine(3, Loc.T("备注"), m.Note, delegate (string v) { m.Note = v; }));

            // 左下角固态小点：进入「编辑中」就能点（房间锁定只锁布局，不锁内容）
            bool canMark = _edit;
            var foot = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 10, Height = 10, VerticalAlignment = VerticalAlignment.Center,
                Fill = PanelUi.B(m.IsSolid
                    ? Color.FromRgb(0xFF, 0x3B, 0x30)
                    : Color.FromRgb(70, 78, 92)),
                Opacity = canMark ? 1.0 : 0.4,
                Cursor = canMark ? Cursors.Hand : Cursors.Arrow,
                ToolTip = !canMark ? Loc.T("点上面的「编辑」后才能标固态")
                                   : (m.IsSolid ? Loc.T("已标固态（点一下取消）") : Loc.T("点一下标成固态"))
            };
            dot.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                if (!_edit) return;   // 只读状态下点不动
                m.SetSolid(!m.IsSolid);
                Changed();
                RebuildMachines();
            };
            foot.Children.Add(dot);
            foot.Children.Add(new TextBlock
            {
                Text = Loc.T("固态"), FontSize = 9.5, Margin = new Thickness(4, 0, 0, 0),
                Foreground = PanelUi.B(m.IsSolid ? Color.FromRgb(0xFF, 0x9A, 0x94) : PanelUi.Sub),
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetRow(foot, 4);
            g.Children.Add(foot);

            card.Child = g;
            return card;
        }

        private UIElement MachineLine(int row, string label, string val, Action<string> setter)
        {
            var gg = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            gg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            gg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gg.Children.Add(new TextBlock
            {
                Text = label, FontSize = 10, Foreground = PanelUi.B(PanelUi.Sub),
                VerticalAlignment = VerticalAlignment.Center
            });
            var tb = new TextBox
            {
                Text = val ?? "", Height = 22, FontSize = 11, Padding = new Thickness(4, 1, 4, 1),
                IsReadOnly = !_edit,
                Background = PanelUi.B(Color.FromRgb(20, 25, 32)),
                Foreground = PanelUi.B(PanelUi.Text),
                BorderBrush = PanelUi.B(Color.FromRgb(48, 56, 68)),
                BorderThickness = new Thickness(1),
                CaretBrush = PanelUi.B(PanelUi.Accent)
            };
            UiHelp.SelectAllOnDoubleClick(tb);
            tb.TextChanged += (s, e) =>
            {
                if (_bind || _rebuild) return;
                setter(((TextBox)s).Text);
                Changed();
            };
            Grid.SetColumn(tb, 1);
            gg.Children.Add(tb);
            Grid.SetRow(gg, row);
            return gg;
        }

        private void AddMachine()
        {
            if (_cell == null) return;
            if (_cell.Machines == null) _cell.Machines = new List<MachineInfo>();
            if (_cell.Machines.Count >= 6)
            {
                MessageBox.Show(Loc.T("一个工位最多加 6 台机器，够用了吧。"), Loc.T("提示"));
                return;
            }
            _cell.Machines.Add(new MachineInfo());
            Changed();
            RebuildMachines();
            _machineScroll.ScrollToEnd();
        }
    }
}
