using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WorkstationStudio
{
    // ===== 工位块可视化 =====
    public class BlockVisual : Border
    {
        private const double CELL = 78;

        // 网线口号的颜色是“焊死”的，不随任何房间配色变化
        public static readonly Color PortSingle = Color.FromRgb(0xFF, 0x45, 0x45);   // 单网线：亮红
        public static readonly Color PortDual = Color.FromRgb(0x1E, 0xD7, 0x6A);     // 双网线：亮绿

        public BlockInstance Block { get; private set; }
        private MainWindow _owner;
        private Grid _grid;

        public BlockVisual(BlockInstance b, MainWindow owner)
        {
            Block = b;
            _owner = owner;
            if (b.Cols <= 0) b.Cols = 2;
            if (b.Rows <= 0) b.Rows = 2;
            if (b.Cells == null) b.Cells = new List<WorkCellData>();
            this.Width = b.Cols * CELL;
            this.Height = b.Rows * CELL;
            this.CornerRadius = new CornerRadius(7);
            this.BorderThickness = new Thickness(1.5);
            this.Padding = new Thickness(2.5);
            this.Cursor = (b.Locked || (owner != null && owner.RoomLocked)) ? Cursors.Arrow : Cursors.SizeAll;
            this.SnapsToDevicePixels = true;

            var th = ThemePreset.Current;
            Color c;
            try { c = (Color)ColorConverter.ConvertFromString(b.Color); }
            catch { c = ThemePreset.C(ThemePreset.BlockPalette[0]); }
            this.Background = new SolidColorBrush(Color.FromArgb((byte)th.BlockFillAlpha, c.R, c.G, c.B));
            this.BorderBrush = new SolidColorBrush(Color.FromArgb((byte)th.BlockEdgeAlpha, c.R, c.G, c.B));

            BuildContextMenu();

            _grid = new Grid();
            for (int i = 0; i < b.Cols; i++) _grid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int j = 0; j < b.Rows; j++) _grid.RowDefinitions.Add(new RowDefinition());
            this.Child = _grid;

            this.MouseLeftButtonDown += BlockVisual_MouseLeftButtonDown;
            this.MouseMove += BlockVisual_MouseMove;
            this.MouseLeftButtonUp += BlockVisual_MouseLeftButtonUp;
        }

        private void BuildContextMenu()
        {
            var cm = new ContextMenu();
            cm.Background = new SolidColorBrush(Color.FromRgb(35, 42, 52));
            cm.BorderBrush = new SolidColorBrush(Color.FromRgb(55, 65, 79));
            var miLock = new MenuItem { Header = Block.Locked ? Loc.T("解除固定") : Loc.T("固定位置（拖不动）") };
            miLock.Foreground = Brushes.White;
            miLock.Click += (s, e) => _owner.ToggleLock(Block);
            var miDel = new MenuItem { Header = Block.Locked ? Loc.T("已固定 · 不能删除") : Loc.T("删除这个工位块") };
            if (Block.Locked)
            {
                miDel.IsEnabled = false;
                miDel.Foreground = new SolidColorBrush(Color.FromRgb(112, 122, 136));
            }
            else miDel.Foreground = new SolidColorBrush(Color.FromRgb(224, 108, 108));
            miDel.Click += (s, e) => _owner.DeleteBlockDirect(Block);
            cm.Items.Add(miLock);
            cm.Items.Add(new Separator());
            cm.Items.Add(miDel);
            this.ContextMenu = cm;
        }

        public void RefreshLockMenu()
        {
            BuildContextMenu();
            this.Cursor = Block.Locked ? Cursors.Arrow : Cursors.SizeAll;
        }

        public void Render()
        {
            if (Block.Cols <= 0) Block.Cols = 2;
            if (Block.Rows <= 0) Block.Rows = 2;
            _grid.Children.Clear();
            _grid.ColumnDefinitions.Clear();
            _grid.RowDefinitions.Clear();
            for (int i = 0; i < Block.Cols; i++) _grid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int j = 0; j < Block.Rows; j++) _grid.RowDefinitions.Add(new RowDefinition());

            foreach (var slot in Block.Layout())
            {
                var v = MakeCell(slot);
                Grid.SetColumn(v, Math.Max(0, slot.Col));
                Grid.SetRow(v, Math.Max(0, slot.Row));
                if (slot.ColSpan > 1) Grid.SetColumnSpan(v, slot.ColSpan);
                if (slot.RowSpan > 1) Grid.SetRowSpan(v, slot.RowSpan);
                _grid.Children.Add(v);
            }
        }

        private UIElement MakeCell(CellSlot slot)
        {
            var th = ThemePreset.Current;
            var cell = slot.Cell;
            bool isLeader = slot.IsLeader;
            bool selected = _owner != null && ReferenceEquals(_owner.SelCell, cell);

            Color normalBorder = isLeader ? th.CellBorderLeader : th.CellBorder;
            var border = new Border
            {
                Background = new SolidColorBrush(isLeader ? th.CellBgLeader : th.CellBg),
                BorderBrush = new SolidColorBrush(selected ? th.SelBorder : normalBorder),
                BorderThickness = new Thickness(selected ? 2 : 1),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(1.5),
                Padding = new Thickness(3, 2, 3, 2),
                Tag = cell
            };

            // 网线口号永远贴顶，显示器型号永远贴底，中间给人名 + 主机型号
            var g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            border.Child = g;

            // ---- 网口（顶部）：单口亮红、双口亮绿，双口用 ◉ 标记 ----
            if (!string.IsNullOrEmpty(cell.Port) || cell.Dual)
            {
                Color portColor = cell.Dual ? PortDual : PortSingle;
                var sp = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top
                };
                if (!string.IsNullOrEmpty(cell.Port))
                {
                    sp.Children.Add(new TextBlock
                    {
                        Text = cell.Port, FontSize = 8.5, LineHeight = 10,
                        Foreground = new SolidColorBrush(portColor),
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });
                }
                if (cell.Dual)
                {
                    var dot = new TextBlock
                    {
                        Text = "◉", FontSize = 9, Margin = new Thickness(3, -1, 0, 0),
                        Foreground = new SolidColorBrush(PortDual),
                        VerticalAlignment = VerticalAlignment.Center,
                        Cursor = Cursors.Hand, ToolTip = Loc.T("双网线 · 点开看主机信息")
                    };
                    dot.MouseLeftButtonDown += (s, e) =>
                    {
                        e.Handled = true;
                        _owner.ShowMachinePopup(dot, cell);
                    };
                    sp.Children.Add(dot);
                }
                Grid.SetRow(sp, 0);
                g.Children.Add(sp);
            }

            // ---- 中间：人名 + 主机型号 ----
            var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            if (!string.IsNullOrEmpty(cell.Name))
            {
                mid.Children.Add(new TextBlock
                {
                    Text = cell.Name, FontSize = isLeader ? 14 : 13, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(th.NameFg),
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 0, 0, 1)
                });
            }
            if (ComputerUtil.Display(cell.Computer).Length > 0)
                mid.Children.Add(BuildComputerText(cell, isLeader, th));
            Grid.SetRow(mid, 1);
            g.Children.Add(mid);

            // ---- 显示器（底部） ----
            if (!string.IsNullOrEmpty(cell.Monitor))
            {
                var mt = new TextBlock
                {
                    Text = cell.Monitor, FontSize = 8.5, LineHeight = 10,
                    Foreground = new SolidColorBrush(th.MonFg),
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 1, 0, 0)
                };
                Grid.SetRow(mt, 2);
                g.Children.Add(mt);
            }

            // ---- 外层：内容 + 搜索/空闲闪耀层 ----
            var outer = new Grid { Tag = cell };
            outer.Children.Add(border);

            Color? flash = _owner != null ? _owner.FlashColorFor(cell) : null;
            if (flash.HasValue)
            {
                var fc = flash.Value;
                var ov = new Border
                {
                    CornerRadius = new CornerRadius(5),
                    BorderThickness = new Thickness(3),
                    BorderBrush = new SolidColorBrush(fc),
                    Background = new SolidColorBrush(Color.FromArgb(58, fc.R, fc.G, fc.B)),
                    Margin = new Thickness(0.5),
                    IsHitTestVisible = false,
                    Opacity = 0.5
                };
                var anim = new DoubleAnimation(0.45, 1.0, TimeSpan.FromSeconds(0.9))
                {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                ov.BeginAnimation(UIElement.OpacityProperty, anim);
                outer.Children.Add(ov);
            }

            // ---- Ctrl 拖动：内容互换（领导位和小格子都能拖，跟锁不锁无关） ----
            outer.AllowDrop = true;
            outer.DragOver += (s, e) =>
            {
                if (!e.Data.GetDataPresent(typeof(CellDragPayload))) return;
                e.Effects = DragDropEffects.Move;
                border.BorderBrush = new SolidColorBrush(th.SelBorder);
                border.BorderThickness = new Thickness(2);
                e.Handled = true;
            };
            outer.DragLeave += (s, e) =>
            {
                border.BorderBrush = new SolidColorBrush(selected ? th.SelBorder : normalBorder);
                border.BorderThickness = new Thickness(selected ? 2 : 1);
            };
            outer.Drop += (s, e) =>
            {
                var payload = e.Data.GetData(typeof(CellDragPayload)) as CellDragPayload;
                e.Handled = true;
                if (payload == null || payload.Cell == null) return;
                if (ReferenceEquals(payload.Cell, cell) && ReferenceEquals(payload.Block, Block)) return;
                _owner.SwapCellData(payload.Block, payload.Cell, Block, cell);
            };

            outer.MouseLeftButtonDown += (s, e) =>
            {
                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                if (ctrl)
                {
                    e.Handled = true;
                    _owner.SelectCellExternal(Block, cell);
                    _owner.BeginCellDrag(outer, Block, cell);
                    return;
                }
                if (e.ClickCount >= 2)
                {
                    e.Handled = true;
                    _owner.OpenCellEditor(Block, cell);
                    return;
                }
                _owner.SelectCellExternal(Block, cell);
            };

            // 右下角隐藏红点：机器备注里带“固态”才亮，鼠标悬停才显示是哪台机器（只给数字）
            string solid = SolidModelText(cell);
            if (solid.Length > 0)
            {
                var dot = new Ellipse
                {
                    Width = 6, Height = 6,
                    Fill = new SolidColorBrush(Color.FromArgb(205, 0xFF, 0x3B, 0x30)),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 3.5, 3.5),
                    ToolTip = MakeSolidTip(solid)
                };
                ToolTipService.SetInitialShowDelay(dot, 150);
                ToolTipService.SetBetweenShowDelay(dot, 120);
                ToolTipService.SetShowDuration(dot, 6000);
                outer.Children.Add(dot);
            }
            return outer;
        }

        // 备注里带“固态”的机器：只保留型号里的数字，多台用空格隔开
        private static string SolidModelText(WorkCellData cell)
        {
            if (cell == null || cell.Machines == null || cell.Machines.Count == 0) return "";
            var list = new List<string>();
            foreach (var m in cell.Machines)
            {
                if (m == null) continue;
                if (!m.IsSolid) continue;   // 手动标了红点，或备注里有“固态”
                string label = OnlyDigits(m.Model);
                if (label.Length == 0) label = (m.Model ?? "").Trim();
                if (label.Length == 0) label = Loc.T("固态");   // 还没写机型也照亮，提示就写“固态”
                bool dup = false;
                foreach (var s in list) if (string.Equals(s, label, StringComparison.OrdinalIgnoreCase)) dup = true;
                if (!dup) list.Add(label);
            }
            return string.Join(" ", list.ToArray());
        }

        private static string OnlyDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder();
            foreach (char ch in s) if (ch >= '0' && ch <= '9') sb.Append(ch);
            return sb.ToString();
        }

        // 悬停小提示：小框小字，只写机型数字；去掉系统自带的白色底板
        private static ToolTip MakeSolidTip(string text)
        {
            var b = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 31, 39)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x8F, 0x3A, 0x36)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 1, 5, 2)
            };
            b.Child = new TextBlock
            {
                Text = text, FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0xAE))
            };

            var tip = new ToolTip
            {
                Content = b,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HasDropShadow = false
            };
            var tpl = new ControlTemplate(typeof(ToolTip));
            tpl.VisualTree = new FrameworkElementFactory(typeof(ContentPresenter));
            tip.Template = tpl;
            return tip;
        }

        // 主机型号：逐个机型上色（颜色只来自底部机器统计面板，房间配色不覆盖）
        // 没配过色的机型用网页版约定的默认蓝 #2980B9
        private TextBlock BuildComputerText(WorkCellData cell, bool isLeader, ThemePreset th)
        {
            var tb = new TextBlock
            {
                FontSize = isLeader ? 13.5 : 12.5, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x29, 0x80, 0xB9)),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 14,
                LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                Margin = new Thickness(0, 1, 0, 0)
            };
            bool first = true;
            foreach (var seg in ComputerUtil.Parse(cell.Computer))
            {
                string t = (seg.Text ?? "").Trim();
                if (t.Length == 0) continue;
                if (!first) tb.Inlines.Add(new Run(" "));
                first = false;
                var run = new Run(t);
                if (seg.IsModel && _owner != null)
                {
                    string hex = _owner.ModelColorHex(t);
                    if (!string.IsNullOrEmpty(hex))
                    {
                        try { run.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
                        catch { }
                    }
                }
                tb.Inlines.Add(run);
            }
            return tb;
        }

        private void BlockVisual_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Button) return;
            _owner.BeginBlockDrag(this, e);
        }
        private void BlockVisual_MouseMove(object sender, MouseEventArgs e) { _owner.MoveBlockDrag(this, e); }
        private void BlockVisual_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { _owner.EndBlockDrag(this); }
    }

    // ===== 隐藏信息弹窗 =====
    public class PopupWindow : Window
    {
        public PopupWindow(UIElement anchor, WorkCellData c, Point screenPos)
        {
            WindowStyle = WindowStyle.None; AllowsTransparency = true;
            Background = Brushes.Transparent; ShowInTaskbar = false; Topmost = true;
            ResizeMode = ResizeMode.NoResize; SizeToContent = SizeToContent.WidthAndHeight;
            Left = Math.Max(10, screenPos.X - 20); Top = Math.Max(10, screenPos.Y - 20);

            var wrap = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(24, 30, 38)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(63, 167, 214)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14), Margin = new Thickness(10), MinWidth = 250
            };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = Loc.F("{0} · 主机信息", string.IsNullOrEmpty(c.Name) ? Loc.T("工位") : c.Name),
                FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(95, 208, 245)), Margin = new Thickness(0, 0, 0, 8)
            });
            if (c.Machines == null || c.Machines.Count == 0)
            {
                sp.Children.Add(new TextBlock
                {
                    Text = Loc.T("暂无主机信息。点「同步数据」从表格导入，或在右侧「主机详细信息」里点『编辑』手填。"),
                    FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(147, 160, 176)),
                    TextWrapping = TextWrapping.Wrap, MaxWidth = 280
                });
            }
            else
            {
                foreach (var m in c.Machines)
                {
                    var card = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(32, 39, 49)),
                        CornerRadius = new CornerRadius(5), Padding = new Thickness(9), Margin = new Thickness(0, 0, 0, 8)
                    };
                    var gp = new Grid();
                    for (int i = 0; i < 4; i++) gp.RowDefinitions.Add(new RowDefinition());
                    var head = new TextBlock
                    {
                        Text = "[" + Loc.NetLabel(m.NetType) + "] " +
                               (string.IsNullOrEmpty(m.Model) ? Loc.T("型号未识别") : m.Model),
                        FontSize = 12, FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(230, 235, 242))
                    };
                    Grid.SetRow(head, 0); gp.Children.Add(head);
                    AddRow(gp, 1, "IP", m.Ip, Color.FromRgb(230, 235, 242));
                    AddRow(gp, 2, "MAC", m.Mac, Color.FromRgb(230, 235, 242));
                    AddRow(gp, 3, Loc.T("备注"), m.Note, Color.FromRgb(147, 160, 176));
                    card.Child = gp;
                    sp.Children.Add(card);
                }
            }
            wrap.Child = sp;
            this.Content = wrap;
            this.MouseDown += (s, e) => this.Close();
            this.Deactivated += (s, e) => this.Close();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(14) };
            timer.Tick += (s, e) => { timer.Stop(); if (this.IsActive) this.Close(); };
            timer.Start();
        }
        private static void AddRow(Grid g, int row, string key, string val, Color color)
        {
            if (string.IsNullOrEmpty(val)) return;
            var p = new StackPanel { Orientation = Orientation.Horizontal };
            p.Children.Add(new TextBlock { Text = key + "  ", FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(147, 160, 176)) });
            p.Children.Add(new TextBlock { Text = val, FontSize = 11, Foreground = new SolidColorBrush(color), TextWrapping = TextWrapping.Wrap });
            Grid.SetRow(p, row); g.Children.Add(p);
        }
    }

    // ===== 工号弹窗 =====
    public class EmpPopupWindow : Window
    {
        public EmpPopupWindow(UIElement anchor, WorkCellData c, Point screenPos)
        {
            WindowStyle = WindowStyle.None; AllowsTransparency = true;
            Background = Brushes.Transparent; ShowInTaskbar = false; Topmost = true;
            ResizeMode = ResizeMode.NoResize; SizeToContent = SizeToContent.WidthAndHeight;
            Left = Math.Max(10, screenPos.X - 20); Top = Math.Max(10, screenPos.Y - 20);
            var wrap = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(24, 30, 38)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(79, 178, 134)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16), Margin = new Thickness(10), MinWidth = 180
            };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = Loc.T("工号标签"), FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(147, 160, 176)), Margin = new Thickness(0, 0, 0, 5)
            });
            sp.Children.Add(new TextBlock
            {
                Text = string.IsNullOrEmpty(c.EmployeeId) ? Loc.T("（未填写）") : c.EmployeeId,
                FontSize = 20, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(99, 214, 164)), TextWrapping = TextWrapping.Wrap
            });
            if (!string.IsNullOrEmpty(c.Name))
                sp.Children.Add(new TextBlock
                {
                    Text = c.Name, FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(230, 235, 242)), Margin = new Thickness(0, 6, 0, 0)
                });
            wrap.Child = sp;
            this.Content = wrap;
            this.MouseDown += (s, e) => this.Close();
            this.Deactivated += (s, e) => this.Close();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            timer.Tick += (s, e) => { timer.Stop(); if (this.IsActive) this.Close(); };
            timer.Start();
        }
    }

    // 小工具：输入框双击全选（WPF 默认双击只选一个词）
    internal static class UiHelp
    {
        public static void SelectAllOnDoubleClick(TextBox tb)
        {
            if (tb == null) return;
            // 不拦截默认处理，等这一下点击处理完再把选区扩成全文，避免和文本框自身的
            // 光标定位/拖选打架（拦截 Preview 反而会让选区不生效）
            tb.PreviewMouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount < 2) return;
                TextBox box = tb;
                box.Dispatcher.BeginInvoke(new Action(delegate
                {
                    try { box.Focus(); box.SelectAll(); } catch { }
                }), System.Windows.Threading.DispatcherPriority.Input);
            };
            tb.MouseDoubleClick += (s, e) =>
            {
                try { tb.Focus(); tb.SelectAll(); e.Handled = true; } catch { }
            };
        }
    }

    // ===== 双击工位：就地编辑窗 =====
    public class CellEditorWindow : Window
    {
        private WorkCellData _cell;
        private Action _onChanged;
        private readonly List<TextBox> _clearable = new List<TextBox>();
        private CheckBox _dualBox;
        private Button _btnClear;
        private bool _confirmClear;
        private DispatcherTimer _resetTimer;

        public CellEditorWindow(WorkCellData cell, string title, Point screenPos, Action onChanged)
        {
            _cell = cell;
            _onChanged = onChanged;

            Title = title;
            Width = 300; SizeToContent = SizeToContent.Height;
            WindowStyle = WindowStyle.None; AllowsTransparency = true;
            Background = Brushes.Transparent; ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            Left = Math.Max(10, screenPos.X); Top = Math.Max(10, screenPos.Y);
            ShowActivated = true;

            var wrap = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 32, 41)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(63, 167, 214)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9),
                Padding = new Thickness(12), Margin = new Thickness(12)
            };
            var sp = new StackPanel();
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var t = new TextBlock
            {
                Text = title, FontSize = 12, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(95, 208, 245))
            };
            head.Children.Add(t);
            var close = new Button
            {
                Content = "×", Width = 20, Height = 20, Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Right, Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(147, 160, 176)),
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand
            };
            close.Click += (s, e) => this.Close();
            DockPanel.SetDock(close, Dock.Right);
            head.Children.Add(close);
            sp.Children.Add(head);

            AddField(sp, Loc.T("人名"), cell.Name, delegate (string v) { _cell.Name = v; }, true);
            // 网线口号在墙上，清空时保留，所以不参与清空
            AddField(sp, Loc.T("网线口号"), cell.Port, delegate (string v) { _cell.Port = v; }, false);
            AddField(sp, Loc.T("主机型号"), cell.Computer, delegate (string v) { _cell.Computer = v; }, true);
            AddField(sp, Loc.T("显示器型号"), cell.Monitor, delegate (string v) { _cell.Monitor = v; }, true);
            AddField(sp, Loc.T("工号"), cell.EmployeeId, delegate (string v) { _cell.EmployeeId = v; }, true);

            _dualBox = new CheckBox
            {
                Content = Loc.T("双网线"), IsChecked = cell.Dual, Foreground = new SolidColorBrush(Color.FromRgb(230, 235, 242)),
                Margin = new Thickness(0, 4, 0, 0)
            };
            _dualBox.Click += (s, e) => { _cell.Dual = _dualBox.IsChecked == true; Fire(); };
            sp.Children.Add(_dualBox);

            // 底部：清空（小） + 完成（大）
            var btnRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var done = new Button
            {
                Content = Loc.T("完成"), Width = 104, Height = 32, Padding = new Thickness(0),
                Style = Res("ToolBtn"), Margin = new Thickness(0),
                Background = new SolidColorBrush(Color.FromRgb(0x2E, 0x6E, 0x8E)),
                Foreground = new SolidColorBrush(Color.FromRgb(240, 246, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0xA7, 0xD6)),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand,
                FontSize = 13.5, FontWeight = FontWeights.Bold,
                ToolTip = Loc.T("保存并关闭（输入其实已经即时生效）")
            };
            done.Click += (s, e) => this.Close();
            Grid.SetColumn(done, 2);
            btnRow.Children.Add(done);

            _btnClear = new Button
            {
                Content = Loc.T("清空"), Width = 64, Height = 26, Padding = new Thickness(0),
                Style = Res("ToolBtn"), Margin = new Thickness(0),
                Background = new SolidColorBrush(Color.FromRgb(46, 34, 34)),
                Foreground = new SolidColorBrush(Color.FromRgb(224, 140, 140)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(96, 56, 56)),
                BorderThickness = new Thickness(1), Cursor = Cursors.Hand, FontSize = 11.5,
                HorizontalAlignment = HorizontalAlignment.Left,
                ToolTip = Loc.T("清空这个人/这台机的内容；网线口号留在墙上不动")
            };
            _btnClear.Click += Clear_Click;
            Grid.SetColumn(_btnClear, 0);
            btnRow.Children.Add(_btnClear);

            sp.Children.Add(btnRow);
            sp.Children.Add(new TextBlock
            {
                Text = Loc.T("输入即生效 · 双击任一输入框可全选 · 完成后关闭窗口"),
                FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(110, 125, 142)),
                Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap
            });

            wrap.Child = sp;
            this.Content = wrap;
            this.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) this.Close(); };
        }

        // 清空：第一次点变成“再点一次清空”，3 秒内再点才真清（误点不丢数据）
        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (!_confirmClear)
            {
                _confirmClear = true;
                _btnClear.Content = Loc.T("确认清空");
                _btnClear.Background = new SolidColorBrush(Color.FromRgb(0x5C, 0x24, 0x24));
                _btnClear.Foreground = new SolidColorBrush(Color.FromRgb(255, 176, 176));
                if (_resetTimer == null)
                {
                    _resetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                    _resetTimer.Tick += (s2, e2) => { _resetTimer.Stop(); ResetClearButton(); };
                }
                _resetTimer.Stop();
                _resetTimer.Start();
                return;
            }

            _resetTimer.Stop();
            ResetClearButton();

            foreach (var tb in _clearable) tb.Text = "";
            _cell.Dual = false;
            if (_dualBox != null) _dualBox.IsChecked = false;
            _cell.Machines = new List<MachineInfo>();
            Fire();
        }

        private void ResetClearButton()
        {
            _confirmClear = false;
            if (_btnClear == null) return;
            _btnClear.Content = Loc.T("清空");
            _btnClear.Background = new SolidColorBrush(Color.FromRgb(46, 34, 34));
            _btnClear.Foreground = new SolidColorBrush(Color.FromRgb(224, 140, 140));
        }

        private void AddField(StackPanel sp, string label, string val, Action<string> setter, bool clearable)
        {
            sp.Children.Add(new TextBlock
            {
                Text = label, FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(147, 160, 176)), Margin = new Thickness(0, 2, 0, 2)
            });
            var tb = new TextBox
            {
                Text = val ?? "", Height = 26, FontSize = 13,
                Background = new SolidColorBrush(Color.FromRgb(20, 25, 32)),
                Foreground = new SolidColorBrush(Color.FromRgb(230, 235, 242)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 65, 79)),
                CaretBrush = new SolidColorBrush(Color.FromRgb(63, 167, 214)),
                Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(0, 0, 0, 4)
            };
            UiHelp.SelectAllOnDoubleClick(tb);
            tb.TextChanged += (s, e) => { setter(((TextBox)s).Text); Fire(); };
            sp.Children.Add(tb);
            if (clearable) _clearable.Add(tb);
        }

        private void Fire() { if (_onChanged != null) _onChanged(); }

        private static Style Res(string key)
        {
            try { return Application.Current != null ? Application.Current.TryFindResource(key) as Style : null; }
            catch { return null; }
        }
    }

    // ===== 输入对话框 =====
    public class InputDialog : Window
    {
        public string Result { get; private set; }
        private TextBox _tb;
        public InputDialog(string title, string label, string def)
        {
            Title = title; Width = 340; Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Owner = Application.Current.MainWindow;
            Background = new SolidColorBrush(Color.FromRgb(27, 32, 40));
            Foreground = Brushes.White; ResizeMode = ResizeMode.NoResize;
            var sp = new StackPanel { Margin = new Thickness(16) };
            sp.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 8) });
            _tb = new TextBox
            {
                Text = def, Height = 28,
                Background = new SolidColorBrush(Color.FromRgb(22, 27, 34)),
                Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(55, 65, 79)),
                Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 0, 0, 14)
            };
            sp.Children.Add(_tb);
            var ok = new Button { Content = Loc.T("确定"), Width = 80, Height = 28, HorizontalAlignment = HorizontalAlignment.Right };
            ok.Click += (s, e) => { Result = _tb.Text.Trim(); DialogResult = true; Close(); };
            sp.Children.Add(ok);
            this.Content = sp;
            _tb.Focus(); _tb.SelectAll();
        }
    }
}
