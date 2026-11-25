using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace WorkstationStudio
{
    public class TemplateVm
    {
        public BlockTemplate T { get; set; }
        public int Index { get; set; }
        public string No { get { return Index.ToString(CultureInfo.InvariantCulture); } }
        public string Name { get { return T.Name; } }
        public string ShapeText
        {
            get { return Loc.F("{0}×{1} · {2} 格", T.Rows, T.Cols, T.CellCount); }
        }
        public string LeaderText
        {
            get
            {
                if (!T.HasLeader) return "";
                return MainWindow.LeaderLabel(T.LeaderVertical, T.LeaderPos);
            }
        }
    }

    public partial class MainWindow : Window
    {
        private const double CELL = 78;          // 一个大格像素
        private const int SUB = 2;               // 一个大格 = 2×2 个小格
        private const double SNAP = CELL / SUB;  // 小格吸附单位

        private AppData _app;
        private RoomData _room;
        private BlockInstance _selBlock;
        private WorkCellData _selCell;
        private Dictionary<string, List<MachineInfo>> _machines;
        private Dictionary<string, BlockVisual> _visuals = new Dictionary<string, BlockVisual>();
        private string _curSavePath = "";
        // 启动时是否读到了已有的房间配置：读不到才自动展开工位库 + 房间属性并提示新建
        private bool _hasExistingConfig;
        private bool _suppress;
        private bool _libRefreshing;

        // 两个浮层面板
        private RoomPanelWindow _roomPanel;
        private CellPanelWindow _cellPanel;
        private string _pendingCols, _pendingRows;
        private string _pendingRoomId;   // 待应用的尺寸改动属于哪个房间，防止切房间后串房

        // 工位块拖动
        private bool _dragBlock;
        private BlockVisual _dragVisual;
        private Point _dragOffset;

        // 工位库拖动
        private Point _libDragStart;
        private TemplateVm _libDragCandidate;
        private bool _libReorderOn;

        // 设计器状态
        private bool _libLeaderVertical = true;
        private int _libLeaderPos = 0;   // 0 单独, 1 左/上, 2 右

        // 机型颜色：唯一来源是底部机器统计面板
        private Dictionary<string, string> _modelColors =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // 点了「空闲」数字后要高亮的机型（只亮空工位）
        private string _idleModel;
        // 点了机器名后要高亮的机型（亮所有含该型号的工位，和搜索一样）
        private string _hlModel;

        // 搜索
        private List<KeyValuePair<BlockInstance, WorkCellData>> _hits =
            new List<KeyValuePair<BlockInstance, WorkCellData>>();
        private HashSet<WorkCellData> _flashSet = new HashSet<WorkCellData>();
        private int _hitIdx = -1;
        private string _hitQuery = "";

        // 缩放 / 平移
        private double _zoom = 1.0;
        private DateTime _fitUntil = DateTime.MinValue;
        private bool _panning;
        private Point _panStart;
        private double _panH, _panV;

        // 防抖
        private DispatcherTimer _roomTimer;
        private DispatcherTimer _cellTimer;
        private CellEditorWindow _cellEditor;

        // 存档两段式确认：第一下只提醒，第二下才真覆盖；超时自动解除
        private bool _saveArmed;
        private DispatcherTimer _saveArmTimer;

        public MainWindow()
        {
            InitializeComponent();
            this.Title = AppInfo.FullName;
            try { InitCore(); }
            catch (Exception ex)
            {
                App.Log("CTOR EX: " + ex);
                MessageBox.Show(Loc.F("初始化失败: {0}\n\n{1}", ex.Message, ex.StackTrace), Loc.T("启动错误"));
                throw;
            }
        }

        internal WorkCellData SelCell { get { return _selCell; } }

        // 老版本把存档放在 exe 旁边。这里只负责「判断有没有」，**绝不自动读取**：
        // 否则别人拿到一个「exe + 存档」的文件夹，一打开就静默读出上一台机器的真人名、IP、MAC。
        // 需要导入时由用户在 存档 ▾ → 导入程序目录的旧存档 里主动操作。
        private static bool HasLegacySave()
        {
            try
            {
                if (System.IO.File.Exists(Paths.SaveFile)) return false;
                return System.IO.File.Exists(Paths.LegacySaveFile);
            }
            catch { return false; }
        }

        private void InitCore()
        {
            // 搜索框双击全选（和就地编辑窗一致）
            UiHelp.SelectAllOnDoubleClick(txtSearch);

            _app = new AppData();
            _machines = new Dictionary<string, List<MachineInfo>>(StringComparer.OrdinalIgnoreCase);
            // 存档统一放到 文档\Zogrid\zogrid.json；旧存档只做提示，绝不自动读取
            bool legacyPending = HasLegacySave();
            _curSavePath = Paths.SaveFile;

            _roomTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            _roomTimer.Tick += (s, e) => { _roomTimer.Stop(); ApplyRoomSettings(); };

            _saveArmTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            _saveArmTimer.Tick += (s, e) => { DisarmSave(); Status(Loc.T("存档确认已超时，未写入文件")); };

            SeedLibrary();
            NewRoom(true);
            RefreshRoomCombo();
            RefreshLibrary();
            SelectTemplate(_app.Library.Count > 0 ? _app.Library[0] : null);
            _hasExistingConfig = LoadOrNew();
            RebuildModelColors();
            RedrawCanvas();
            UpdateSavePathText();
            Status(Loc.T("就绪 · F1 工位库 / F2 房间属性 / F3 机器统计 / F4 工位详情 / F5 看全 · 滚轮缩放 · 中键拖动画布 · Ctrl 拖动格子互换"));


            canvasScroll.PreviewMouseDown += CanvasScroll_PreviewMouseDown;
            canvasScroll.PreviewMouseMove += CanvasScroll_PreviewMouseMove;
            canvasScroll.PreviewMouseUp += CanvasScroll_PreviewMouseUp;
            // 双击工位的编辑窗：点主窗口里别处就关掉它（焦点始终在主程序内，不会让程序退到后台）。
            // 编辑窗是独立 Window，点它自己不会触发这里的 PreviewMouseDown。
            this.PreviewMouseDown += (s, e) =>
            {
                if (_cellEditor != null && e.ChangedButton == MouseButton.Left)
                { try { _cellEditor.Close(); } catch { } _cellEditor = null; }
            };
            // 列表拖动排序结束时兜底：无论鼠标捕获在谁手里都能收尾
            this.PreviewMouseUp += (s, e) =>
            {
                if (_libReorderOn && e.ChangedButton == MouseButton.Left)
                {
                    FinishLibReorder(true, e.GetPosition(libList).Y);
                    e.Handled = true;
                }
            };
            // 布局稳定前（工具栏、底部统计面板尺寸陆续确定）持续自适应，稳定后就听用户的
            canvasScroll.SizeChanged += (s, e) => { if (DateTime.Now < _fitUntil) FitView(); };
            this.Loaded += (s, e) =>
            {
                try { this.Activate(); this.Focus(); } catch { }
                RequestFit();
                Dispatcher.BeginInvoke(new Action(RequestFit), DispatcherPriority.ApplicationIdle);
                var fit = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
                fit.Tick += (s2, e2) => { fit.Stop(); RequestFit(); };
                fit.Start();

                // 启动时一个已存在的房间配置都没读到：自动摊开工位库 + 房间属性，
                // 并让「新建房间」按钮呼吸闪光，告诉第一次用的人从哪儿开始
                if (!_hasExistingConfig)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ApplyLibPane(true);
                        ShowRoomPanel();
                        if (_roomPanel != null) _roomPanel.PulseNewRoom();
                        RequestFit();
                        Status(Loc.F("没找到已有配置，已新建「{0}」。填好房间名后按「新建房间」，或直接从左侧工位库拖工位进来", _room != null ? _room.Name : Loc.T("房间")));
                        // 程序目录里有老存档时提醒一句（只是提醒，绝不自动读）
                        if (legacyPending)
                            Status(Loc.T("程序目录下有旧存档，需要的话在「存档 ▾」里手动导入"));
                    }), DispatcherPriority.ApplicationIdle);
                }
            };
            this.Closed += (s, e) => { if (_cellEditor != null) { try { _cellEditor.Close(); } catch { } } };
        }

        private void SeedLibrary()
        {
            _app.Library.Add(new BlockTemplate { Name = Loc.T("双人工位"), Rows = 2, Cols = 2, Color = ThemePreset.BlockPalette[0] });
            _app.Library.Add(new BlockTemplate { Name = Loc.T("三人工位"), Rows = 2, Cols = 3, Color = ThemePreset.BlockPalette[1] });
            _app.Library.Add(new BlockTemplate
            {
                Name = Loc.T("带领导位"), Rows = 2, Cols = 3, Color = ThemePreset.BlockPalette[2],
                HasLeader = true, LeaderVertical = true, LeaderPos = 1
            });
        }

        // 房间名取现有最大编号 +1，删掉中间房间后再新建也不会重名
        private string NextRoomName()
        {
            int max = 0;
            // 中英文房间名（「房间3」/「Room 3」）都要能累计编号
            string pfx = Loc.T("房间");
            var rx = new System.Text.RegularExpressions.Regex(
                "^(?:" + System.Text.RegularExpressions.Regex.Escape("房间") + "|" +
                System.Text.RegularExpressions.Regex.Escape(pfx) + @")(\d+)$");
            foreach (var r in _app.Rooms)
            {
                var m = rx.Match(r.Name ?? "");
                if (m.Success)
                {
                    int n;
                    if (int.TryParse(m.Groups[1].Value, out n) && n > max) max = n;
                }
            }
            return Loc.T("房间") + (max + 1);
        }

        // 新建时用户自己填了名字：和已有房间重名就往后加（2）（3）……
        private string UniqueRoomName(string want)
        {
            string name = want;
            int n = 2;
            while (RoomNameExists(name, null))
            {
                name = want + "（" + n + "）";
                n++;
            }
            return name;
        }

        private bool RoomNameExists(string name, RoomData except)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string t = name.Trim();
            foreach (var r in _app.Rooms)
            {
                if (r == null || ReferenceEquals(r, except)) continue;
                if (string.Equals((r.Name ?? "").Trim(), t, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private void NewRoom(bool silent) { NewRoom(silent, null); }

        // name 为空：按「房间N」自动编号；填了名字就用它（重名自动加后缀）
        private void NewRoom(bool silent, string name)
        {
            ClearPendingRoomSettings();
            string nm = (name ?? "").Trim();
            nm = nm.Length == 0 ? NextRoomName() : UniqueRoomName(nm);
            _room = new RoomData { Name = nm, Cols = 16, Rows = 12 };
            var th = ThemePreset.Current;
            _room.ThemeKey = th.Key;
            _room.FloorColor = th.Floor;
            _room.GridColor = th.Grid;
            _app.Rooms.Add(_room);
            _app.ActiveRoomId = _room.Id;
            ClearSelection();
            ApplyRoomFieldsFromData();
            RedrawCanvas();
            if (!silent)
            {
                RefreshRoomCombo();
                Status(Loc.T("已新建 ") + _room.Name);
                RequestFit();
            }
        }

        // 返回 true 表示确实读到了已有房间配置（用来决定启动时要不要自动摊开两个面板）
        private bool LoadOrNew()
        {
            if (!System.IO.File.Exists(_curSavePath)) return false;
            try
            {
                _app = Storage.Load(_curSavePath);
                if (_app.Rooms == null || _app.Rooms.Count == 0) throw new Exception(Loc.T("空存档"));
                if (_app.Library == null) _app.Library = new List<BlockTemplate>();
                Storage.Migrate(_app);
                ClearPendingRoomSettings();
                _room = _app.Rooms.FirstOrDefault(r => r.Id == _app.ActiveRoomId) ?? _app.Rooms[0];
                ApplyRoomFieldsFromData();
                RefreshRoomCombo();
                RefreshLibrary();
                RedrawCanvas();
                Status(Loc.T("已读取上次存档"));
                return true;
            }
            catch (Exception ex)
            {
                App.Log("LOAD EX: " + ex);
                Status(Loc.T("上次存档读取失败（已新建房间）: ") + ex.Message);
                return false;
            }
        }

        private void UpdateSavePathText()
        {
            savePathText.Text = Loc.T("存档: ") + _curSavePath;
            savePathText.ToolTip = _curSavePath;
        }

        internal RoomData CurrentRoomData { get { return _room; } }
        internal IList<RoomData> AllRooms { get { return _app.Rooms; } }
        internal bool RoomLocked { get { return _room != null && _room.Locked; } }

        private void ApplyRoomFieldsFromData()
        {
            if (_room == null) return;
            ThemePreset.Current = ThemePreset.Get(_room.ThemeKey);
            UpdateSwatches();
            RefreshPanels();
        }

        // 工具栏配色提示 + 画布上的锁定角标
        private void UpdateSwatches()
        {
            if (_room == null) return;
            if (btnTheme != null) btnTheme.ToolTip = Loc.F("当前配色：{0}（点开换 6 套之一）", ThemePreset.Current.Name);
        }

        // 任何房间级变化后，两个浮层都跟着刷新
        internal void RefreshPanels()
        {
            if (_roomPanel != null) _roomPanel.Refresh();
            if (_cellPanel != null) _cellPanel.Bind(_selBlock, _selCell, RoomLocked);
        }

        private static Color SafeColor(string hex, Color fallback)
        {
            try { return (Color)ColorConverter.ConvertFromString(hex); }
            catch { return fallback; }
        }

        private void BtnTheme_Click(object sender, RoutedEventArgs e)
        {
            if (_room == null) return;
            var t = ThemeWindow.Pick(this, ThemePreset.Get(_room.ThemeKey).Key);
            if (t == null) return;
            ApplyTheme(t);
        }

        private void ApplyTheme(ThemePreset t)
        {
            if (_room == null || t == null) return;
            _room.ThemeKey = t.Key;
            _room.FloorColor = t.Floor;
            _room.GridColor = t.Grid;
            ThemePreset.Current = t;
            UpdateSwatches();
            RedrawCanvas();
            Status(Loc.F("配色已换成「{0}」", t.Name));
        }

        internal void PickFloorColor()
        {
            if (_room == null) return;
            if (RoomLocked) { Status(Loc.T("房间已锁定，改不了地板色（F2 里解锁）")); return; }
            string hex = ColorPickWindow.Pick(this, _room.FloorColor);
            if (string.IsNullOrEmpty(hex)) return;
            _room.FloorColor = hex;
            UpdateSwatches();
            RedrawCanvas();
            Status(Loc.T("地板色 → ") + hex);
        }

        internal void PickGridColor()
        {
            if (_room == null) return;
            if (RoomLocked) { Status(Loc.T("房间已锁定，改不了格线色（F2 里解锁）")); return; }
            string hex = ColorPickWindow.Pick(this, _room.GridColor);
            if (string.IsNullOrEmpty(hex)) return;
            _room.GridColor = hex;
            UpdateSwatches();
            RedrawCanvas();
            Status(Loc.T("格线色 → ") + hex);
        }

        // 打印当前房间的地板布局（黑白线稿，系统打印机）
        internal void PrintRoomLayout()
        {
            if (_room == null) return;
            try
            {
                RoomPrinter.Print(this, _room);
                Status(Loc.F("已发送到打印机：房间「{0}」布局图", _room.Name));
            }
            finally
            {
                FocusHelp.Restore();   // 打完别让程序掉到别的窗口后面
            }
        }

        private void Status(string s) { statusText.Text = s; }

        private void RefreshRoomCombo()
        {
            if (_roomPanel != null) _roomPanel.Refresh();
        }

        internal void SwitchRoomById(string id)
        {
            var r = _app.Rooms.FirstOrDefault(x => x.Id == id);
            if (r == null || ReferenceEquals(r, _room)) return;
            ClearPendingRoomSettings();   // 丢掉上一个房间没提交的尺寸输入
            _room = r;
            _app.ActiveRoomId = r.Id;
            ClearSelection();
            ClearSearch();
            ApplyRoomFieldsFromData();
            RedrawCanvas();
            RequestFit();
            Status(r.Locked ? Loc.F("已切换到「{0}」（已锁定）", r.Name) : Loc.F("已切换到「{0}」", r.Name));
        }

        internal void NewRoomFromPanel() { NewRoom(false); }
        internal void NewRoomFromPanel(string name) { NewRoom(false, name); }

        // 房间改名：房间解锁后随时可改；空名/和别的房间重名就拒绝并返回 false
        internal bool RenameCurrentRoom(string newName)
        {
            if (_room == null) return false;
            if (RoomLocked) return false;
            string want = (newName ?? "").Trim();
            if (want.Length == 0) { Status(Loc.T("房间名不能为空")); return false; }
            if (want == (_room.Name ?? "").Trim()) return true;
            if (RoomNameExists(want, _room))
            {
                Status(Loc.F("已经有叫「{0}」的房间了，换个名字", want));
                return false;
            }
            string old = _room.Name;
            _room.Name = want;
            RefreshRoomCombo();
            RefreshPanels();
            RedrawCanvas();
            Status(Loc.F("房间「{0}」已改名为「{1}」", old, want));
            return true;
        }
        internal void DeleteCurrentRoom() { DoDeleteRoom(); }

        internal void SetRoomLock(bool locked)
        {
            if (_room == null) return;
            _room.Locked = locked;
            UpdateSwatches();
            RefreshPanels();
            RedrawCanvas();
            Status(locked ? Loc.T("房间已锁定：大小不能改、工位不能移动、地板格线色锁定；工位内容仍可编辑、Ctrl 交换照常") : Loc.T("房间已解锁，可以编辑了"));
        }

        private void BtnNewRoom_Click(object sender, RoutedEventArgs e) { NewRoom(false); }

        // 删除房间：两次确认
        private void DoDeleteRoom()
        {
            if (_room == null) return;
            if (RoomLocked)
            {
                MessageBox.Show(Loc.F("房间「{0}」已锁定，不能删除。\n请先在房间属性里点「已锁」解锁。", _room.Name),
                    Loc.T("房间已锁定"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_app.Rooms.Count <= 1)
            {
                MessageBox.Show(Loc.T("至少要保留一个房间，删不了最后一个。"), Loc.T("不能删除"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            int cells = 0;
            foreach (var b in _room.Blocks) cells += b.Cells != null ? b.Cells.Count : 0;

            if (MessageBox.Show(Loc.F("要删除整个房间「{0}」？\n房间里有 {1} 个工位块、{2} 个工位格，都会一起删掉。\n\n第 1 次确认，继续吗？",
                                _room.Name, _room.Blocks.Count, cells),
                    Loc.T("删除房间（1/2）"), MessageBoxButton.YesNo, MessageBoxImage.Warning,
                    MessageBoxResult.No) != MessageBoxResult.Yes) return;

            if (MessageBox.Show(Loc.F("再确认一次：删除后房间里的人名、机型、IP/MAC 全部找不回来。\n\n真的删除「{0}」？", _room.Name),
                    Loc.T("删除房间（2/2）"), MessageBoxButton.YesNo,
                    MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            {
                Status(Loc.T("已取消删除房间"));
                return;
            }

            ClearPendingRoomSettings();
            int idx = _app.Rooms.IndexOf(_room);
            _app.Rooms.Remove(_room);
            if (idx < 0) idx = 0;
            if (idx > _app.Rooms.Count - 1) idx = _app.Rooms.Count - 1;
            _room = _app.Rooms[idx];
            _app.ActiveRoomId = _room.Id;
            ClearSelection();
            ClearSearch();
            ApplyRoomFieldsFromData();
            RefreshRoomCombo();
            RedrawCanvas();
            RequestFit();
            Status(Loc.F("已删除房间，当前切换为「{0}」", _room.Name));
        }

        // ---------- 房间尺寸（浮层里改，防抖） ----------
        internal void SetRoomSizeFromPanel(string colsText, string rowsText)
        {
            if (_room == null || RoomLocked) return;
            _pendingCols = colsText;
            _pendingRows = rowsText;
            _pendingRoomId = _room.Id;   // 记住这些数字是哪个房间输入的
            _roomTimer.Stop();
            _roomTimer.Start();
        }

        internal void ClearPendingRoomSettings()
        {
            _roomTimer.Stop();
            _pendingCols = _pendingRows = _pendingRoomId = null;
        }

        private void ApplyRoomSettings()
        {
            // 先取出待应用值并立即清空，避免存档 / 切房间时重复套用到别的房间
            string pc = _pendingCols, pr = _pendingRows, pid = _pendingRoomId;
            _pendingCols = _pendingRows = _pendingRoomId = null;
            if (_room == null || RoomLocked) return;
            // 关键：只有输入这些数字时所在的房间，才允许套用；换了房间就丢弃
            if (pid == null || pid != _room.Id) return;
            int c, r;
            bool changed = false;
            if (int.TryParse((pc ?? "").Trim(), out c) && c >= 4 && c <= 60 && c != _room.Cols)
            { _room.Cols = c; changed = true; }
            if (int.TryParse((pr ?? "").Trim(), out r) && r >= 3 && r <= 45 && r != _room.Rows)
            { _room.Rows = r; changed = true; }
            if (!changed) return;
            ClampAllBlocks();
            RedrawCanvas();
            Status(Loc.F("房间大小 {0}×{1}", _room.Cols, _room.Rows));
        }

        private void ClampAllBlocks()
        {
            foreach (var b in _room.Blocks)
            {
                b.Col = Clamp(b.Col, 0, Math.Max(0, _room.Cols * SUB - b.Cols * SUB));
                b.Row = Clamp(b.Row, 0, Math.Max(0, _room.Rows * SUB - b.Rows * SUB));
            }
        }

        private static bool IsHexColor(string s)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(s ?? "", "^#[0-9A-Fa-f]{6}$");
        }

        private void CanvasScroll_Loaded(object sender, RoutedEventArgs e) { RedrawCanvas(); }

        // ---------- 画布绘制 ----------
        private void RedrawCanvas()
        {
            if (_room == null || roomCanvas == null) return;
            ThemePreset.Current = ThemePreset.Get(_room.ThemeKey);
            var th = ThemePreset.Current;
            double w = _room.Cols * CELL, h = _room.Rows * CELL;
            roomCanvas.Width = w; roomCanvas.Height = h;
            canvasWrap.Width = w; canvasWrap.Height = h;

            try { roomCanvas.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_room.FloorColor)); }
            catch { roomCanvas.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(th.Floor)); }

            try { canvasScroll.Background = new SolidColorBrush(th.CanvasBg); } catch { }
            try { canvasHint.Foreground = new SolidColorBrush(th.HintFg); } catch { }

            roomCanvas.Children.Clear();
            _visuals.Clear();

            Color gridColor;
            try { gridColor = (Color)ColorConverter.ConvertFromString(_room.GridColor); }
            catch { gridColor = ThemePreset.C(th.Grid); }
            var fine = new SolidColorBrush(gridColor) { Opacity = 0.45 };
            var major = new SolidColorBrush(gridColor) { Opacity = 1.0 };
            double step = CELL / SUB;

            int nx = _room.Cols * SUB, ny = _room.Rows * SUB;
            for (int i = 1; i < nx; i++)
            {
                double x = i * step;
                bool isMajor = (i % SUB == 0);
                roomCanvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = x, Y1 = 0, X2 = x, Y2 = h,
                    Stroke = isMajor ? major : fine,
                    StrokeThickness = isMajor ? 1 : 0.6,
                    SnapsToDevicePixels = true,
                    IsHitTestVisible = false
                });
            }
            for (int i = 1; i < ny; i++)
            {
                double y = i * step;
                bool isMajor = (i % SUB == 0);
                roomCanvas.Children.Add(new System.Windows.Shapes.Line
                {
                    X1 = 0, Y1 = y, X2 = w, Y2 = y,
                    Stroke = isMajor ? major : fine,
                    StrokeThickness = isMajor ? 1 : 0.6,
                    SnapsToDevicePixels = true,
                    IsHitTestVisible = false
                });
            }

            foreach (var b in _room.Blocks)
            {
                if (b.Cols <= 0) b.Cols = 2;
                if (b.Rows <= 0) b.Rows = 2;
                var bv = new BlockVisual(b, this);
                bv.Render();
                Canvas.SetLeft(bv, b.Col * SNAP);
                Canvas.SetTop(bv, b.Row * SNAP);
                roomCanvas.Children.Add(bv);
                if (!_visuals.ContainsKey(b.Id)) _visuals[b.Id] = bv;
            }
            canvasHint.Visibility = _room.Blocks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RefreshModelStats();
        }

        private void RenderBlock(BlockInstance b)
        {
            if (b == null) return;
            BlockVisual v;
            if (_visuals.TryGetValue(b.Id, out v)) v.Render();
        }

        // ---------- 缩放 / 平移 / 居中 ----------
        // 以 focus（鼠标所指的点）为锚点缩放：缩放前后“光标下的那块地板”不动
        private void ApplyZoom(double newZoom, Point focus)
        {
            newZoom = Math.Max(0.3, Math.Min(4.0, newZoom));
            if (Math.Abs(newZoom - _zoom) < 0.0001) return;
            try
            {
                canvasScroll.UpdateLayout();
                // 光标下对应的画布内容坐标（未缩放坐标，含 canvasWrap 的边距）
                Point contentPt = canvasScroll.TranslatePoint(focus, canvasWrap);

                _zoom = newZoom;
                zoomTransform.ScaleX = zoomTransform.ScaleY = _zoom;
                btnZoomReset.Content = ((int)Math.Round(_zoom * 100)) + "%";
                canvasScroll.UpdateLayout();

                // 缩放后这个内容点跑到哪了，把差值补回滚动偏移，让它回到光标下
                Point after = canvasWrap.TranslatePoint(contentPt, canvasScroll);
                canvasScroll.ScrollToHorizontalOffset(Math.Max(0, canvasScroll.HorizontalOffset + (after.X - focus.X)));
                canvasScroll.ScrollToVerticalOffset(Math.Max(0, canvasScroll.VerticalOffset + (after.Y - focus.Y)));
            }
            catch { }
        }

        private void CanvasScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            double factor = e.Delta > 0 ? 1.1 : (1.0 / 1.1);
            ApplyZoom(_zoom * factor, e.GetPosition(canvasScroll));
        }

        private void SetZoom(double z)
        {
            _zoom = Math.Max(0.3, Math.Min(4.0, z));
            zoomTransform.ScaleX = zoomTransform.ScaleY = _zoom;
            btnZoomReset.Content = ((int)Math.Round(_zoom * 100)) + "%";
        }

        // 载入房间 / F5：整块地板最多 100% 缩到能看全，然后居中
        private void RequestFit()
        {
            _fitUntil = DateTime.Now.AddSeconds(2.5);
            FitView();
        }

        private void FitView()
        {
            if (_room == null) return;
            try
            {
                canvasScroll.UpdateLayout();
                double vw = canvasScroll.ViewportWidth > 0 ? canvasScroll.ViewportWidth : canvasScroll.ActualWidth;
                double vh = canvasScroll.ViewportHeight > 0 ? canvasScroll.ViewportHeight : canvasScroll.ActualHeight;
                double rw = roomCanvas.ActualWidth, rh = roomCanvas.ActualHeight;
                if (vw < 20 || vh < 20 || rw < 20 || rh < 20) return;
                double scale = Math.Min((vw - 28) / rw, (vh - 28) / rh);
                if (scale > 1.0) scale = 1.0;
                SetZoom(scale);
                canvasScroll.UpdateLayout();
                CenterView();
            }
            catch { }
        }

        private void BtnZoomIn_Click(object sender, RoutedEventArgs e) { ApplyZoom(_zoom * 1.2, new Point(canvasScroll.ActualWidth / 2, canvasScroll.ActualHeight / 2)); }
        private void BtnZoomOut_Click(object sender, RoutedEventArgs e) { ApplyZoom(_zoom / 1.2, new Point(canvasScroll.ActualWidth / 2, canvasScroll.ActualHeight / 2)); }
        private void BtnZoomReset_Click(object sender, RoutedEventArgs e) { ApplyZoom(1.0, new Point(canvasScroll.ActualWidth / 2, canvasScroll.ActualHeight / 2)); }
        private void BtnCenter_Click(object sender, RoutedEventArgs e) { FitView(); }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var w = new AboutWindow(FocusHelp.ActiveOwner(this));
                w.ShowDialog();
            }
            catch (Exception ex) { App.Log("ABOUT EX: " + ex); }
        }

        // ---------- 语言切换 ----------
        private void BtnLang_Click(object sender, RoutedEventArgs e)
        {
            BuildLangMenu();
            popLang.IsOpen = true;
        }

        private void BuildLangMenu()
        {
            if (langHost == null) return;
            langHost.Children.Clear();

            for (int i = 0; i < Loc.Languages.Length; i++)
            {
                string code = Loc.Languages[i][0];
                string label = Loc.Languages[i][1];
                bool cur = code == Loc.Lang;

                var b = new Button
                {
                    Content = (cur ? "✓  " : "     ") + label,
                    Height = 30, FontSize = 12.5, Cursor = Cursors.Hand,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(10, 0, 10, 0),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(cur ? Color.FromRgb(0xFF, 0x6F, 0xB9) : Color.FromRgb(0xE6, 0xEB, 0xF2))
                };
                var st = TryFindResource("MenuBtn") as Style;
                if (st != null) b.Style = st;

                string cap = code;
                b.Click += (s2, e2) => { popLang.IsOpen = false; SwitchLanguage(cap); };
                langHost.Children.Add(b);
            }
        }

        // 换语言要重建整个界面：先把当前改动落盘，再用新语言重新启动自己
        private void SwitchLanguage(string code)
        {
            if (string.IsNullOrEmpty(code) || code == Loc.Lang) return;

            try
            {
                if (_room != null && _app != null) Storage.Save(_curSavePath, _app);
            }
            catch (Exception ex) { App.Log("SAVE BEFORE RELANG: " + ex.Message); }

            Loc.Lang = code;
            Loc.Save();

            try
            {
                string exe = System.Reflection.Assembly.GetEntryAssembly().Location;
                System.Diagnostics.Process.Start(exe);
            }
            catch (Exception ex) { App.Log("RESTART FAIL: " + ex.Message); }

            Application.Current.Shutdown();
        }

        private void CenterView()
        {
            if (_room == null) return;
            try
            {
                canvasScroll.UpdateLayout();
                Point p = roomCanvas.TranslatePoint(new Point(0, 0), canvasScroll);
                double rw = roomCanvas.ActualWidth * _zoom;
                double rh = roomCanvas.ActualHeight * _zoom;
                double vw = canvasScroll.ViewportWidth > 0 ? canvasScroll.ViewportWidth : canvasScroll.ActualWidth;
                double vh = canvasScroll.ViewportHeight > 0 ? canvasScroll.ViewportHeight : canvasScroll.ActualHeight;
                double nx = canvasScroll.HorizontalOffset + p.X + rw / 2 - vw / 2;
                double ny = canvasScroll.VerticalOffset + p.Y + rh / 2 - vh / 2;
                canvasScroll.ScrollToHorizontalOffset(Math.Max(0, nx));
                canvasScroll.ScrollToVerticalOffset(Math.Max(0, ny));
            }
            catch { }
        }

        private void CanvasScroll_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton != MouseButtonState.Pressed) return;
            e.Handled = true;
            _panning = true;
            _panStart = e.GetPosition(canvasScroll);
            _panH = canvasScroll.HorizontalOffset;
            _panV = canvasScroll.VerticalOffset;
            canvasScroll.CaptureMouse();
            Cursor = Cursors.Hand;
        }
        private void CanvasScroll_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_panning) return;
            Point p = e.GetPosition(canvasScroll);
            canvasScroll.ScrollToHorizontalOffset(Math.Max(0, _panH - (p.X - _panStart.X)));
            canvasScroll.ScrollToVerticalOffset(Math.Max(0, _panV - (p.Y - _panStart.Y)));
        }
        private void CanvasScroll_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_panning) return;
            _panning = false;
            canvasScroll.ReleaseMouseCapture();
            Cursor = Cursors.Arrow;
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            if (e.OriginalSource is TextBox || e.OriginalSource is ComboBox) return;
            bool handled = IsFunctionKey(e.Key);
            HandleFunctionKey(e.Key);
            if (handled) e.Handled = true;
        }

        private static bool IsFunctionKey(Key key)
        {
            return key == Key.F1 || key == Key.F2 || key == Key.F3 || key == Key.F4 || key == Key.F5
                   || key == Key.Left || key == Key.Right || key == Key.Up || key == Key.Down || key == Key.Delete;
        }

        // 功能键统一入口；浮层面板拿到焦点时也会把 F 键转发到这里
        internal void HandleFunctionKey(Key key)
        {
            double step = 60;
            switch (key)
            {
                case Key.F1: ToggleLibPane(); break;
                case Key.F2: ShowRoomPanel(); break;
                case Key.F3: ToggleStats(); break;
                case Key.F4: ShowCellPanel(); break;
                case Key.F5: FitView(); break;
                case Key.Left: canvasScroll.ScrollToHorizontalOffset(Math.Max(0, canvasScroll.HorizontalOffset - step)); break;
                case Key.Right: canvasScroll.ScrollToHorizontalOffset(canvasScroll.HorizontalOffset + step); break;
                case Key.Up: canvasScroll.ScrollToVerticalOffset(Math.Max(0, canvasScroll.VerticalOffset - step)); break;
                case Key.Down: canvasScroll.ScrollToVerticalOffset(canvasScroll.VerticalOffset + step); break;
                case Key.Delete: DeleteSelectedBlock(); break;
            }
        }

        // ---------- 面板开关 ----------
        // 注意：点击 ToggleButton 时 WPF 已经先翻转过 IsChecked，这里不能再翻一次
        private void ApplyLibPane(bool on)
        {
            btnLibPane.IsChecked = on;
            colLib.Width = new GridLength(on ? 252 : 0);
        }

        private void ToggleLibPane() { ApplyLibPane(btnLibPane.IsChecked != true); }

        private void BtnLibPane_Click(object sender, RoutedEventArgs e)
        {
            ApplyLibPane(btnLibPane.IsChecked == true);
        }

        private void ApplyStats(bool on)
        {
            btnStatsToggle.IsChecked = on;
            statsPanel.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ToggleStats() { ApplyStats(btnStatsToggle.IsChecked != true); }

        private void BtnStatsToggle_Click(object sender, RoutedEventArgs e)
        {
            ApplyStats(btnStatsToggle.IsChecked == true);
        }

        private void BtnRoomPane_Click(object sender, RoutedEventArgs e) { ShowRoomPanel(); }

        private void BtnCellPane_Click(object sender, RoutedEventArgs e) { ShowCellPanel(); }

        // F1/F3 本来就是 ToggleButton，自带按下态；F2/F4 改成 ToggleButton 后
        // 由面板的真实开合来点亮，别让按钮自己翻状态（× 关掉、互斥关掉都要跟着灭）
        private void SyncPanelButtons()
        {
            if (btnRoomPane != null) btnRoomPane.IsChecked = _roomPanel != null && _roomPanel.IsVisible;
            if (btnCellPane != null) btnCellPane.IsChecked = _cellPanel != null && _cellPanel.IsVisible;
        }

        // 房间属性(F2) 和 工位详情(F4) 互斥：同一时刻只开一个；
        // 再按同一个键 = 关掉它；开一个之前先关掉另一个。
        private void ShowRoomPanel()
        {
            // 房间属性已开着 → 关掉（toggle 关）
            if (_roomPanel != null && _roomPanel.IsVisible)
            {
                _roomPanel.Close();
                _roomPanel = null;
                SyncPanelButtons();
                return;
            }
            // 先关掉另一个面板，避免两个同时开卡住
            if (_cellPanel != null)
            {
                _cellPanel.Close();
                _cellPanel = null;
            }
            if (_roomPanel == null)
            {
                _roomPanel = new RoomPanelWindow(this);
                _roomPanel.Owner = this;
                _roomPanel.Closed += (s, e) => { _roomPanel = null; SyncPanelButtons(); };
                _roomPanel.Left = Math.Max(20, this.Left + this.ActualWidth - 380);
                _roomPanel.Top = Math.Max(20, this.Top + 100);
            }
            _roomPanel.Refresh();
            _roomPanel.Show();
            _roomPanel.Activate();
            SyncPanelButtons();
        }

        private void ShowCellPanel()
        {
            // 工位详情已开着 → 关掉（toggle 关）
            if (_cellPanel != null && _cellPanel.IsVisible)
            {
                _cellPanel.Close();
                _cellPanel = null;
                SyncPanelButtons();
                return;
            }
            // 先关掉另一个面板
            if (_roomPanel != null)
            {
                _roomPanel.Close();
                _roomPanel = null;
            }
            if (_cellPanel == null)
            {
                _cellPanel = new CellPanelWindow(this);
                _cellPanel.Owner = this;
                _cellPanel.Closed += (s, e) => { _cellPanel = null; SyncPanelButtons(); };
                _cellPanel.Left = Math.Max(20, this.Left + this.ActualWidth - 400);
                _cellPanel.Top = Math.Max(20, this.Top + 100);
            }
            _cellPanel.Bind(_selBlock, _selCell, RoomLocked);
            _cellPanel.Show();
            _cellPanel.Activate();
            SyncPanelButtons();
        }

        // ================= 机型颜色（只在机器统计面板里设定） =================
        private void RebuildModelColors()
        {
            _modelColors.Clear();
            if (_app.ModelColors != null)
                foreach (var e in _app.ModelColors)
                    if (e != null && !string.IsNullOrEmpty(e.Model) && !string.IsNullOrEmpty(e.Color))
                        _modelColors[e.Model.Trim()] = e.Color;
        }

        internal string ModelColorHex(string model)
        {
            if (string.IsNullOrEmpty(model)) return null;
            string hex;
            return _modelColors.TryGetValue(model.Trim(), out hex) ? hex : null;
        }

        private void SetModelColor(string model, string hex)
        {
            if (string.IsNullOrEmpty(model) || string.IsNullOrEmpty(hex)) return;
            _modelColors[model] = hex;
            if (_app.ModelColors == null) _app.ModelColors = new List<ModelColorEntry>();
            var e = _app.ModelColors.FirstOrDefault(x => string.Equals(x.Model, model, StringComparison.OrdinalIgnoreCase));
            if (e == null) { e = new ModelColorEntry { Model = model }; _app.ModelColors.Add(e); }
            e.Color = hex;
            RedrawCanvas();
            Status(Loc.F("机型「{0}」的字色 → {1}", model, hex));
        }

        private void PickModelColor(string model)
        {
            string cur = ModelColorHex(model);
            if (string.IsNullOrEmpty(cur)) cur = "#2980B9";
            string hex = ModelColorWindow.Pick(this, model, cur);
            if (string.IsNullOrEmpty(hex)) return;
            SetModelColor(model, hex);
        }

        // ================= 底部机器统计面板 =================
        // 统计规则跟网页版一致：按主机型号栏拆，忽略括号内容，+ / 空格 都算分隔
        // 有机器没人 = 型号有值且人名为空 → 空闲 +1
        private void RefreshModelStats()
        {
            if (statsHost == null || _room == null) return;
            idle.Clear();
            var total = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var b in _room.Blocks)
            {
                if (b.Cells == null) continue;
                foreach (var c in b.Cells)
                {
                    foreach (var m in ComputerUtil.Models(c.Computer))
                    {
                        if (!total.ContainsKey(m)) total[m] = 0;
                        total[m]++;
                        if (string.IsNullOrEmpty((c.Name ?? "").Trim()))
                        {
                            if (!idle.ContainsKey(m)) idle[m] = 0;
                            idle[m]++;
                        }
                    }
                }
            }

            statsHost.Children.Clear();
            int machines = 0, idleSum = 0;
            foreach (var kv in total) machines += kv.Value;
            foreach (var kv in idle) idleSum += kv.Value;
            statsSummary.Text = Loc.F("{0} 种机型 · {1} 台机器 · {2} 台空闲", total.Count, machines, idleSum);

            if (total.Count == 0)
            {
                statsHost.Children.Add(new TextBlock
                {
                    Text = Loc.T("暂无机器数据 —— 在工位「主机型号」里写了型号就会自动统计。"),
                    FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(147, 160, 176)),
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 6, 0, 0)
                });
                return;
            }

            var models = new List<string>(total.Keys);
            models.Sort(delegate (string a, string b)
            {
                int d = total[b].CompareTo(total[a]);
                return d != 0 ? d : string.Compare(a, b, StringComparison.CurrentCulture);
            });

            foreach (var model in models)
            {
                statsHost.Children.Add(MakeModelCard(model, total[model],
                    idle.ContainsKey(model) ? idle[model] : 0));
            }
        }

        private readonly Dictionary<string, int> idle = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private Border MakeModelCard(string model, int total, int idleCount)
        {
            var th = ThemePreset.Current;
            var card = new Border
            {
                Width = 132, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 5, 8, 5),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(35, 42, 52)),
                BorderBrush = new SolidColorBrush(
                    (string.Equals(_idleModel, model, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(_hlModel, model, StringComparison.OrdinalIgnoreCase))
                        ? Color.FromRgb(255, 20, 147) : Color.FromRgb(58, 68, 82)),
                BorderThickness = new Thickness(1)
            };
            var sp = new StackPanel();

            bool nameHl = string.Equals(_hlModel, model, StringComparison.OrdinalIgnoreCase);
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            string hex = ModelColorHex(model);
            var swatch = new Border
            {
                Width = 26, Height = 15, CornerRadius = new CornerRadius(3), Cursor = Cursors.Hand,
                Background = new SolidColorBrush(string.IsNullOrEmpty(hex) ? Color.FromRgb(41, 128, 185) : SafeColor(hex, Color.FromRgb(41, 128, 185))),
                BorderBrush = new SolidColorBrush(Color.FromRgb(90, 100, 116)), BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center,
                ToolTip = Loc.T("点这里改这个机型的字色")
            };
            swatch.MouseLeftButtonDown += (s, e) => { e.Handled = true; PickModelColor(model); };
            head.Children.Add(swatch);
            var nameTb = new TextBlock
            {
                Text = model, FontSize = 11.5, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(nameHl
                    ? Color.FromRgb(255, 45, 155) : Color.FromRgb(230, 235, 242)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                ToolTip = Loc.T("点一下高亮这个机型的所有工位（再点取消）")
            };
            nameTb.MouseLeftButtonDown += (s, e) => { e.Handled = true; ToggleModelHighlight(model); };
            head.Children.Add(nameTb);
            sp.Children.Add(head);

            var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            line.Children.Add(new TextBlock
            {
                Text = Loc.T("总数 "), FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(147, 160, 176))
            });
            line.Children.Add(new TextBlock
            {
                Text = total.ToString(CultureInfo.InvariantCulture), FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 235, 242))
            });
            var idleTb = new TextBlock
            {
                Text = Loc.T("空闲 ") + idleCount, FontSize = 10.5, Margin = new Thickness(9, 0, 0, 0),
                Foreground = new SolidColorBrush(idleCount > 0
                    ? Color.FromRgb(46, 204, 113) : Color.FromRgb(120, 132, 146)),
                Cursor = idleCount > 0 ? Cursors.Hand : Cursors.Arrow,
                ToolTip = idleCount > 0 ? Loc.T("点一下高亮这个机型的空闲工位（再点取消）") : Loc.T("没有空闲机位")
            };
            if (idleCount > 0)
                idleTb.MouseLeftButtonDown += (s, e) => { e.Handled = true; ToggleIdleModel(model); };
            line.Children.Add(idleTb);
            sp.Children.Add(line);

            card.Child = sp;
            return card;
        }

        private void ToggleIdleModel(string model)
        {
            if (string.Equals(_idleModel, model, StringComparison.OrdinalIgnoreCase))
            {
                _idleModel = null;
            }
            else
            {
                // 三种高亮互斥：点亮"空闲"就把搜索和机型高亮一起清掉
                _idleModel = model;
                _hlModel = null;
                ClearSearchHighlight();
            }
            RefreshModelStats();
            RedrawCanvas();
            Status(_idleModel == null ? Loc.T("已取消空闲高亮") : Loc.F("高亮「{0}」的空闲工位（再点一次取消）", _idleModel));
        }

        // 点机器名：高亮所有含该型号的工位（有人/无人都亮），和搜索一个道理
        private void ToggleModelHighlight(string model)
        {
            if (string.Equals(_hlModel, model, StringComparison.OrdinalIgnoreCase))
            {
                _hlModel = null;
            }
            else
            {
                _hlModel = model;
                _idleModel = null;
                ClearSearchHighlight();
            }
            RefreshModelStats();
            RedrawCanvas();
            Status(_hlModel == null ? Loc.T("已取消机型高亮") : Loc.F("高亮「{0}」的所有工位（再点一次取消）", _hlModel));
        }

        // 只清搜索那一路高亮（不打状态栏、不重画，交给调用方统一收尾）
        private void ClearSearchHighlight()
        {
            if (txtSearch != null && !string.IsNullOrEmpty(txtSearch.Text)) txtSearch.Text = "";
            if (searchPopup != null) searchPopup.IsOpen = false;
            if (searchResultsHost != null) searchResultsHost.Children.Clear();
            _hits.Clear();
            _hitIdx = -1;
            _hitQuery = "";
            _flashSet.Clear();
        }

        // 格子闪耀颜色：搜索命中 / 点机器名 = 亮粉；点空闲机型 = 深一号的粉
        internal Color? FlashColorFor(WorkCellData cell)
        {
            if (cell == null) return null;
            if (_flashSet.Contains(cell)) return Color.FromRgb(0xFF, 0x2D, 0x9B);
            if (!string.IsNullOrEmpty(_hlModel) && ComputerUtil.HasModel(cell.Computer, _hlModel))
                return Color.FromRgb(0xFF, 0x2D, 0x9B);
            if (!string.IsNullOrEmpty(_idleModel) && string.IsNullOrEmpty((cell.Name ?? "").Trim())
                && ComputerUtil.HasModel(cell.Computer, _idleModel))
                return Color.FromRgb(0xFF, 0x14, 0x93);
            return null;
        }

        // ================= 搜索 =================
        private void BtnSearch_Click(object sender, RoutedEventArgs e) { DoSearch(); }

        private void BtnSearchClear_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = "";
            ClearSearch();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (searchHint != null)
                searchHint.Visibility = string.IsNullOrEmpty((txtSearch.Text ?? "")) ?
                    Visibility.Visible : Visibility.Collapsed;
            if (string.IsNullOrEmpty((txtSearch.Text ?? "").Trim())) ClearSearch();
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { DoSearch(); e.Handled = true; }
            else if (e.Key == Key.Escape) { txtSearch.Text = ""; ClearSearch(); e.Handled = true; }
            else if (e.Key == Key.Down && _hits.Count > 0)
            {
                _hitIdx = (_hitIdx + 1) % _hits.Count;
                FocusHit(true);
                e.Handled = true;
            }
            else if (e.Key == Key.Up && _hits.Count > 0)
            {
                _hitIdx = (_hitIdx - 1 + _hits.Count) % _hits.Count;
                FocusHit(true);
                e.Handled = true;
            }
        }

        private void DoSearch()
        {
            string q = (txtSearch.Text ?? "").Trim();
            if (q.Length == 0) { ClearSearch(); return; }

            if (!string.Equals(q, _hitQuery, StringComparison.OrdinalIgnoreCase))
            {
                _hitQuery = q;
                _hitIdx = -1;
                RebuildHits(q);
                // 三种高亮互斥：搜索一出，机型/空闲高亮自动灭
                _hlModel = null;
                _idleModel = null;
                _flashSet.Clear();
                foreach (var h in _hits) _flashSet.Add(h.Value);
                RedrawCanvas();
            }
            if (_hits.Count == 0)
            {
                if (searchPopup != null) searchPopup.IsOpen = false;
                Status(Loc.F("没找到「{0}」", q));
                return;
            }
            _hitIdx = (_hitIdx + 1) % _hits.Count;
            FocusHit(true);
        }

        // 跳到某个命中：选中 + 居中 + 重新画结果列表里的选中态
        private void FocusHit(bool center)
        {
            if (_hitIdx < 0 || _hitIdx >= _hits.Count) return;
            var hit = _hits[_hitIdx];
            SelectCell(hit.Key, hit.Value);
            if (center) CenterOnCell(hit.Key, hit.Value);
            Status(Loc.F("命中 {0} / {1} · {2}（↑↓ 或再点搜索换一个）", _hitIdx + 1, _hits.Count,
                   string.IsNullOrEmpty(hit.Value.Name) ? Loc.T("空格位") : hit.Value.Name));
            ShowSearchResults();
        }

        // 搜索框下面的结果下拉：显示命中列表，超过 5 行可上下滚动，点一行直接跳过去
        private void ShowSearchResults()
        {
            if (searchPopup == null || searchResultsHost == null) return;
            searchResultsHost.Children.Clear();
            if (_hits.Count == 0) { searchPopup.IsOpen = false; return; }

            searchPopHint.Text = Loc.F("找到 {0} 个 · 点一行跳过去（↑↓ 也能换）", _hits.Count);

            for (int i = 0; i < _hits.Count; i++)
            {
                int idx = i;
                var b = _hits[i].Key;
                var c = _hits[i].Value;
                bool sel = (i == _hitIdx);
                bool dual = c.Dual;

                var row = new Border
                {
                    Background = new SolidColorBrush(sel ? Color.FromRgb(46, 58, 74) : Color.FromRgb(35, 42, 52)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(52, 61, 74)),
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(7, 5, 7, 5),
                    Margin = new Thickness(0, 0, 0, 3),
                    CornerRadius = new CornerRadius(5),
                    Cursor = Cursors.Hand,
                    Tag = idx
                };
                // 左侧粉色竖条（选中时亮）
                var bar = new Border
                {
                    Width = 3, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 7, 0),
                    Background = new SolidColorBrush(sel ? Color.FromRgb(0xFF, 0x2D, 0x9B)
                        : (dual ? Color.FromRgb(30, 215, 106) : Color.FromRgb(74, 88, 104)))
                };

                var sp = new StackPanel();
                var line1 = new StackPanel { Orientation = Orientation.Horizontal };
                line1.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrEmpty(c.Name) ? Loc.T("(空位)") : c.Name,
                    FontSize = 12, FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(236, 241, 248))
                });
                line1.Children.Add(new TextBlock
                {
                    Text = "  " + CellLocation(b, c),
                    FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(140, 154, 170)),
                    VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 1)
                });
                if (dual)
                    line1.Children.Add(new TextBlock
                    {
                        Text = Loc.T(" 双网"), FontSize = 10, FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(30, 215, 106)),
                        VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 1)
                    });
                sp.Children.Add(line1);

                string detail = "";
                if (!string.IsNullOrEmpty(c.Port)) detail += Loc.T("网线 ") + c.Port;
                string comp = ComputerUtil.Display(c.Computer);
                if (comp.Length > 0) detail += (detail.Length > 0 ? " · " : "") + Loc.F("机器 {0}", comp);
                if (!string.IsNullOrEmpty(c.EmployeeId)) detail += (detail.Length > 0 ? " · " : "") + Loc.F("工号 {0}", c.EmployeeId);
                sp.Children.Add(new TextBlock
                {
                    Text = detail.Length > 0 ? detail : Loc.T("（该格没有填写内容）"),
                    FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(158, 172, 188)),
                    Margin = new Thickness(0, 1, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis
                });

                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.Children.Add(bar);
                Grid.SetColumn(sp, 1);
                g.Children.Add(sp);
                row.Child = g;

                row.MouseEnter += (s, e) => { if (((int)((Border)s).Tag) != _hitIdx) ((Border)s).Background = new SolidColorBrush(Color.FromRgb(43, 52, 64)); };
                row.MouseLeave += (s, e) => { if (((int)((Border)s).Tag) != _hitIdx) ((Border)s).Background = new SolidColorBrush(Color.FromRgb(35, 42, 52)); };
                row.MouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                    _hitIdx = idx;
                    FocusHit(true);
                };

                searchResultsHost.Children.Add(row);
            }

            searchPopup.IsOpen = true;

            // 选中项滚进可视区
            if (_hitIdx >= 0 && _hitIdx < searchResultsHost.Children.Count)
            {
                var el = searchResultsHost.Children[_hitIdx] as FrameworkElement;
                if (el != null) el.BringIntoView();
            }
        }

        // 结果列表里的位置描述：块名 + 第几列/第几行（大格坐标）
        private string CellLocation(BlockInstance b, WorkCellData c)
        {
            if (b == null) return "";
            int idx = b.Cells != null ? b.Cells.IndexOf(c) : -1;
            string name = string.IsNullOrEmpty(b.Name) ? Loc.T("工位块") : b.Name;
            foreach (var slot in b.Layout())
            {
                if (!ReferenceEquals(slot.Cell, c)) continue;
                int col = b.Col / SUB + slot.Col + 1;
                int row = b.Row / SUB + slot.Row + 1;
                return Loc.F("{0} 第{1}列{2}行", name, col, row);
            }
            return idx >= 0 ? Loc.F("{0} 第{1}格", name, idx + 1) : name;
        }

        private void RebuildHits(string q)
        {
            _hits.Clear();
            if (_room == null) return;
            foreach (var b in _room.Blocks)
            {
                if (b.Cells == null) continue;
                foreach (var c in b.Cells)
                    if (MatchCell(c, q)) _hits.Add(new KeyValuePair<BlockInstance, WorkCellData>(b, c));
            }
        }

        private static bool MatchCell(WorkCellData c, string q)
        {
            if (c == null) return false;
            if ((c.Name ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if ((c.EmployeeId ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if ((c.Port ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if ((c.Monitor ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (ComputerUtil.ContainsModel(c.Computer, q)) return true;
            if (c.Machines != null)
                foreach (var m in c.Machines)
                {
                    if ((m.Ip ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if ((m.Mac ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if ((m.EmployeeId ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if ((m.Model ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                    if ((m.Note ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
            return false;
        }

        private void ClearSearch()
        {
            bool had = _flashSet.Count > 0 || _hits.Count > 0;
            if (searchPopup != null) searchPopup.IsOpen = false;
            if (searchResultsHost != null) searchResultsHost.Children.Clear();
            _hits.Clear();
            _hitIdx = -1;
            _hitQuery = "";
            _flashSet.Clear();
            if (had) { RedrawCanvas(); Status(Loc.T("已清空搜索，取消闪耀")); }
        }

        private void CenterOnCell(BlockInstance b, WorkCellData c)
        {
            if (b == null || c == null || roomCanvas == null) return;
            foreach (var slot in b.Layout())
            {
                if (!ReferenceEquals(slot.Cell, c)) continue;
                double x = b.Col * SNAP + slot.Col * CELL + slot.ColSpan * CELL / 2.0;
                double y = b.Row * SNAP + slot.Row * CELL + slot.RowSpan * CELL / 2.0;
                try
                {
                    canvasScroll.UpdateLayout();
                    Point tp = roomCanvas.TranslatePoint(new Point(x, y), canvasScroll);
                    double vw = canvasScroll.ViewportWidth > 0 ? canvasScroll.ViewportWidth : canvasScroll.ActualWidth;
                    double vh = canvasScroll.ViewportHeight > 0 ? canvasScroll.ViewportHeight : canvasScroll.ActualHeight;
                    canvasScroll.ScrollToHorizontalOffset(Math.Max(0, canvasScroll.HorizontalOffset + tp.X - vw / 2));
                    canvasScroll.ScrollToVerticalOffset(Math.Max(0, canvasScroll.VerticalOffset + tp.Y - vh / 2));
                }
                catch { }
                return;
            }
        }

        // ---------- 工位库 ----------
        private void RefreshLibrary()
        {
            _libRefreshing = true;
            var list = new List<TemplateVm>();
            for (int i = 0; i < _app.Library.Count; i++)
                list.Add(new TemplateVm { T = _app.Library[i], Index = i + 1 });
            object keep = libList.SelectedItem;
            libList.ItemsSource = list;
            _libRefreshing = false;
        }

        private void SelectTemplate(BlockTemplate t)
        {
            if (t == null) return;
            _suppress = true;
            txtLibName.Text = t.Name;
            numLibRows.Text = t.Rows.ToString(CultureInfo.InvariantCulture);
            numLibCols.Text = t.Cols.ToString(CultureInfo.InvariantCulture);
            chkLeader.IsChecked = t.HasLeader;
            _libLeaderVertical = t.HasLeader ? t.LeaderVertical : true;
            _libLeaderPos = t.HasLeader ? t.LeaderPos : 0;
            int ci = ColorIndex(t.Color);
            libColor.SelectedIndex = ci >= 0 ? ci : 0;
            _suppress = false;
            UpdateLeaderUI();
            UpdateShapeLocks();
            UpdateLibHint();
        }

        private static readonly string[] COLORS = ThemePreset.BlockPalette;
        private static int ColorIndex(string hex)
        {
            for (int i = 0; i < COLORS.Length; i++)
                if (string.Equals(COLORS[i], hex, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        private TemplateVm SelectedTemplateVm() { return libList.SelectedItem as TemplateVm; }

        private void LibList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_libRefreshing) return;
            var vm = SelectedTemplateVm();
            if (vm != null) SelectTemplate(vm.T);
        }

        private void LibList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
            if (item == null) { _libDragCandidate = null; return; }   // 点空白处不参与排序
            if (!item.IsSelected) item.IsSelected = true;
            _libDragStart = e.GetPosition(libList);
            _libDragCandidate = libList.SelectedItem as TemplateVm;
            _libReorderOn = false;
        }

        // 在列表里上下拖 = 调整顺序；拖出列表 = 变成拖到房间放置
        private void LibList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                if (_libReorderOn) FinishLibReorder(false, 0);
                return;
            }
            if (_libDragCandidate == null) return;

            Point p = e.GetPosition(libList);
            if (!_libReorderOn &&
                Math.Abs(p.X - _libDragStart.X) < 6 && Math.Abs(p.Y - _libDragStart.Y) < 6) return;

            bool inside = p.X > -24 && p.Y > -24 &&
                          p.X < libList.ActualWidth + 24 && p.Y < libList.ActualHeight + 24;

            if (!_libReorderOn)
            {
                if (inside)
                {
                    _libReorderOn = true;
                }
                else { StartLibDragOut(); return; }
            }

            if (!inside) { StartLibDragOut(); return; }

            LibInsertLineY(p.Y);
        }

        private void LibList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_libReorderOn) return;
            FinishLibReorder(true, e.GetPosition(libList).Y);
            e.Handled = true;
        }

        // ---- 工位库顺序拖动 ----
        private int LibInsertIndex(double y)
        {
            int idx = 0;
            for (int i = 0; i < libList.Items.Count; i++)
            {
                var c = libList.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (c == null) continue;
                Point p = c.TranslatePoint(new Point(0, 0), libList);
                if (y > p.Y + c.ActualHeight / 2) idx = i + 1;
                else { idx = i; break; }
            }
            return idx;
        }

        private void LibInsertLineY(double y)
        {
            double top = 0;
            int idx = LibInsertIndex(y);
            if (idx <= 0) top = 0;
            else
            {
                var c = libList.ItemContainerGenerator.ContainerFromIndex(Math.Min(idx - 1, libList.Items.Count - 1)) as FrameworkElement;
                if (c != null) top = c.TranslatePoint(new Point(0, 0), libList).Y + c.ActualHeight;
            }
            libDropShift.Y = Math.Max(0, Math.Min(libList.ActualHeight - 1, top));
            libDropLine.Visibility = Visibility.Visible;
        }

        private void HideLibDropLine() { libDropLine.Visibility = Visibility.Collapsed; }

        private void FinishLibReorder(bool apply, double y)
        {
            bool was = _libReorderOn;
            _libReorderOn = false;
            HideLibDropLine();
            var vm = _libDragCandidate;
            _libDragCandidate = null;
            if (!was || !apply || vm == null || vm.T == null) return;

            int from = _app.Library.IndexOf(vm.T);
            if (from < 0) return;
            int to = LibInsertIndex(y);
            if (to > from) to--;
            if (to < 0) to = 0;
            if (to > _app.Library.Count - 1) to = _app.Library.Count - 1;
            if (to == from) return;
            _app.Library.RemoveAt(from);
            _app.Library.Insert(to, vm.T);
            RefreshLibrary();
            libList.SelectedIndex = to;
            Status(Loc.F("工位库顺序已调整：第 {0} 位 · {1}", to + 1, vm.T.Name));
        }

        private void StartLibDragOut()
        {
            var vm = _libDragCandidate;
            _libReorderOn = false;
            _libDragCandidate = null;
            HideLibDropLine();
            if (vm == null || vm.T == null) return;
            DragDrop.DoDragDrop(libList, vm.T.Id, DragDropEffects.Copy);
        }

        private static DependencyObject ParentOf(DependencyObject d)
        {
            if (d == null) return null;
            try { return VisualTreeHelper.GetParent(d); }
            catch
            {
                var fce = d as FrameworkContentElement;
                return fce != null ? fce.Parent : null;
            }
        }

        private static T FindAncestor<T>(DependencyObject d) where T : DependencyObject
        {
            while (d != null && !(d is T)) d = ParentOf(d);
            return d as T;
        }

        // ---------- 设计器：领导位方向 / 位置 ----------
        private void ChkLeader_Click(object sender, RoutedEventArgs e)
        {
            if (_suppress) return;
            UpdateLeaderUI();
            UpdateShapeLocks();
            UpdateLibHint();
        }

        private void BtnLeaderDir_Click(object sender, RoutedEventArgs e)
        {
            _libLeaderVertical = !_libLeaderVertical;
            UpdateLeaderUI();
            UpdateShapeLocks();
            UpdateLibHint();
        }

        private void BtnLeaderPos_Click(object sender, RoutedEventArgs e)
        {
            _libLeaderPos = (_libLeaderPos + 1) % 3;
            UpdateLeaderUI();
            UpdateShapeLocks();
            UpdateLibHint();
        }

        private void UpdateLeaderUI()
        {
            bool has = chkLeader.IsChecked == true;
            leaderRow.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
            btnLeaderDir.Content = _libLeaderVertical ? Loc.T("竖") : Loc.T("横");
            btnLeaderPos.Content = _libLeaderPos == 0 ? Loc.T("单独") : (_libLeaderPos == 1 ? Loc.T("左") : Loc.T("右"));
        }

        // 排 / 列 的锁定规则
        private void UpdateShapeLocks()
        {
            bool has = chkLeader.IsChecked == true;
            if (!has)
            {
                numLibRows.IsEnabled = true;
                numLibCols.IsEnabled = true;
                return;
            }
            if (_libLeaderVertical)
            {
                numLibRows.Text = "2";
                numLibRows.IsEnabled = false;
                if (_libLeaderPos == 0)
                {
                    numLibCols.Text = "1";
                    numLibCols.IsEnabled = false;
                }
                else
                {
                    numLibCols.IsEnabled = true;
                    int c;
                    if (!int.TryParse(numLibCols.Text, out c) || c < 2) numLibCols.Text = "3";
                }
            }
            else
            {
                numLibCols.Text = "2";
                numLibCols.IsEnabled = false;
                if (_libLeaderPos == 0)
                {
                    numLibRows.Text = "1";
                    numLibRows.IsEnabled = false;
                }
                else
                {
                    numLibRows.IsEnabled = true;
                    int r;
                    if (!int.TryParse(numLibRows.Text, out r) || r < 2) numLibRows.Text = "3";
                }
            }
        }

        // 领导位描述：竖/横 × 单独/左/右，六种写法各自成句，翻译才通顺
        internal static string LeaderLabel(bool vertical, int pos)
        {
            if (pos == 0) return Loc.T(vertical ? "领导位 竖·单独" : "领导位 横·单独");
            if (pos == 1) return Loc.T(vertical ? "领导位 竖·左" : "领导位 横·左");
            return Loc.T(vertical ? "领导位 竖·右" : "领导位 横·右");
        }

        private void UpdateLibHint()
        {
            int r, c;
            if (!int.TryParse(numLibRows.Text, out r)) r = 2;
            if (!int.TryParse(numLibCols.Text, out c)) c = 2;
            if (r < 1) r = 1;
            if (c < 1) c = 1;
            bool has = chkLeader.IsChecked == true;
            int cells = has ? Math.Max(1, r * c - 1) : r * c;
            string lead = "";
            if (has) lead = " · " + LeaderLabel(_libLeaderVertical, _libLeaderPos);
            libHint.Text = Loc.F("{0} 排 × {1} 列 · {2} 个工位{3}", r, c, cells, lead);
        }

        private bool TryReadLibEditor(out BlockTemplate t)
        {
            t = null;
            int rows, cols;
            if (!int.TryParse(numLibRows.Text.Trim(), out rows)) rows = 2;
            if (!int.TryParse(numLibCols.Text.Trim(), out cols)) cols = 2;
            bool has = chkLeader.IsChecked == true;

            if (has)
            {
                if (_libLeaderVertical)
                {
                    rows = 2;
                    cols = _libLeaderPos == 0 ? 1 : Math.Max(2, cols);
                }
                else
                {
                    cols = 2;
                    rows = _libLeaderPos == 0 ? 1 : Math.Max(2, rows);
                }
            }
            if (rows < 1 || rows > 10 || cols < 1 || cols > 10)
            {
                MessageBox.Show(Loc.T("排、列范围 1~10"), Loc.T("提示"));
                return false;
            }
            if (rows >= 3 && cols >= 3)
            {
                MessageBox.Show(Loc.T("3×3 不行：中间那个工位的人出不来。\n请把排或列降到 2 以内。"), Loc.T("禁止 3×3"));
                return false;
            }

            string name = txtLibName.Text.Trim();
            if (name.Length == 0) name = Loc.T("工位块");

            t = new BlockTemplate
            {
                Name = name,
                Rows = rows,
                Cols = cols,
                Color = COLORS[Math.Max(0, libColor.SelectedIndex)],
                HasLeader = has,
                LeaderVertical = _libLeaderVertical,
                LeaderPos = _libLeaderPos
            };
            return true;
        }

        private void BtnLibAdd_Click(object sender, RoutedEventArgs e)
        {
            BlockTemplate t;
            if (!TryReadLibEditor(out t)) return;
            _app.Library.Add(t);
            RefreshLibrary();
            libList.SelectedIndex = _app.Library.Count - 1;
            Status(Loc.F("已添加第 {0} 种工位块: {1}", _app.Library.Count, t.Name));
        }

        private void BtnLibUpdate_Click(object sender, RoutedEventArgs e)
        {
            var vm = SelectedTemplateVm();
            if (vm == null) { MessageBox.Show(Loc.T("先在上面的列表里选中一个工位块"), Loc.T("提示")); return; }
            BlockTemplate edit;
            if (!TryReadLibEditor(out edit)) return;
            var old = vm.T;
            old.Name = edit.Name; old.Rows = edit.Rows; old.Cols = edit.Cols; old.Color = edit.Color;
            old.HasLeader = edit.HasLeader; old.LeaderVertical = edit.LeaderVertical; old.LeaderPos = edit.LeaderPos;
            RefreshLibrary();
            for (int i = 0; i < _app.Library.Count; i++)
                if (ReferenceEquals(_app.Library[i], old)) { libList.SelectedIndex = i; break; }
            Status(Loc.T("已更新: ") + old.Name);
        }

        private void BtnLibDel_Click(object sender, RoutedEventArgs e)
        {
            var vm = SelectedTemplateVm();
            if (vm == null) { MessageBox.Show(Loc.T("先在上面的列表里选中一个工位块"), Loc.T("提示")); return; }
            if (MessageBox.Show(Loc.F("删除模板「{0}」？\n已经放到房间里的工位块不受影响。", vm.T.Name), Loc.T("确认"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _app.Library.Remove(vm.T);
            RefreshLibrary();
            if (_app.Library.Count > 0) libList.SelectedIndex = 0;
            Status(Loc.T("已删除模板"));
        }

        // ---------- 拖放放置 ----------
        private void RoomCanvas_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(typeof(string)) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void RoomCanvas_Drop(object sender, DragEventArgs e)
        {
            if (RoomLocked) { Status(Loc.T("房间已锁定，放不了新工位块（F2 里解锁）")); return; }
            if (!e.Data.GetDataPresent(typeof(string))) return;
            string id = (string)e.Data.GetData(typeof(string));
            BlockTemplate t = _app.Library.FirstOrDefault(x => x.Id == id);
            if (t == null) return;

            Point p = e.GetPosition(roomCanvas);
            int maxCol = Math.Max(0, _room.Cols * SUB - t.Cols * SUB);
            int maxRow = Math.Max(0, _room.Rows * SUB - t.Rows * SUB);
            int col = Clamp((int)Math.Round((p.X - t.Cols * CELL / 2) / SNAP), 0, maxCol);
            int row = Clamp((int)Math.Round((p.Y - t.Rows * CELL / 2) / SNAP), 0, maxRow);

            var b = new BlockInstance
            {
                TemplateId = t.Id, Name = t.Name,
                Rows = t.Rows, Cols = t.Cols, Color = t.Color, Col = col, Row = row,
                HasLeader = t.HasLeader, LeaderVertical = t.LeaderVertical, LeaderPos = t.LeaderPos
            };
            b.RebuildCells();
            _room.Blocks.Add(b);
            RedrawCanvas();
            if (b.Cells.Count > 0) SelectCell(b, b.Cells[0]);
            Status(Loc.F("已放置「{0}」", t.Name));
        }

        private static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }

        private void RoomCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is Canvas || e.OriginalSource is System.Windows.Shapes.Line) ClearSelection();
        }

        // ---------- 块拖动（按小格吸附） ----------
        internal void BeginBlockDrag(BlockVisual vis, MouseEventArgs e)
        {
            if (RoomLocked) { Status(Loc.T("房间已锁定，工位块拖不动（F2 里解锁）")); return; }
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) return; // Ctrl 是格子互换
            if (vis.Block.Locked) { Status(Loc.T("该工位块已固定，右键可以解除")); return; }
            _dragBlock = true;
            _dragVisual = vis;
            Point p = e.GetPosition(roomCanvas);
            _dragOffset = new Point(p.X - vis.Block.Col * SNAP, p.Y - vis.Block.Row * SNAP);
            vis.CaptureMouse();
        }

        internal void MoveBlockDrag(BlockVisual vis, MouseEventArgs e)
        {
            if (!_dragBlock || _dragVisual != vis) return;
            Point p = e.GetPosition(roomCanvas);
            var b = vis.Block;
            int maxCol = Math.Max(0, _room.Cols * SUB - b.Cols * SUB);
            int maxRow = Math.Max(0, _room.Rows * SUB - b.Rows * SUB);
            int col = Clamp((int)Math.Round((p.X - _dragOffset.X) / SNAP), 0, maxCol);
            int row = Clamp((int)Math.Round((p.Y - _dragOffset.Y) / SNAP), 0, maxRow);
            b.Col = col; b.Row = row;
            Canvas.SetLeft(vis, col * SNAP);
            Canvas.SetTop(vis, row * SNAP);
        }

        internal void EndBlockDrag(BlockVisual vis)
        {
            if (!_dragBlock || _dragVisual != vis) return;
            _dragBlock = false;
            vis.ReleaseMouseCapture();
            Status(Loc.F("位置: 小格 ({0},{1})", vis.Block.Col, vis.Block.Row));
        }

        // ---------- Ctrl 拖动单元格：两个格子的内容互换 ----------
        // 领导位拖到小格子、小格子拖到领导位都可以；工位块锁不锁都能拖。
        // 这是交换「内容」，不是挪动工位块位置，房间锁定时也允许。
        internal void BeginCellDrag(UIElement src, BlockInstance b, WorkCellData c)
        {
            if (b == null || c == null || src == null) return;
            var payload = new CellDragPayload { Block = b, Cell = c };
            Cursor old = Cursor;
            Cursor = Cursors.Hand;
            try
            {
                DragDrop.DoDragDrop(src, new DataObject(typeof(CellDragPayload), payload), DragDropEffects.Move);
            }
            catch (Exception ex) { App.Log("CELL DRAG EX: " + ex); }
            finally { Cursor = old; }
            RenderBlock(b);
        }

        internal void SwapCellData(BlockInstance fromBlock, WorkCellData from, BlockInstance toBlock, WorkCellData to)
        {
            if (from == null || to == null || ReferenceEquals(from, to)) return;
            from.SwapWith(to);
            RenderBlock(fromBlock);
            RenderBlock(toBlock);
            RefreshModelStats();
            if (_selCell != null && _selBlock != null) SelectCell(_selBlock, _selCell);
            Status(Loc.T("已互换两个格子：人名/型号/显示器/工号/IP/MAC 都跟着走，网线口号留在原位"));
        }

        // ---------- 选中与属性 ----------
        private void SelectCell(BlockInstance b, WorkCellData c)
        {
            var prevBlock = _selBlock;
            if (b == null || c == null) { ClearSelection(); return; }
            _selBlock = b; _selCell = c;

            if (_cellPanel != null) _cellPanel.Bind(b, c, RoomLocked);

            if (prevBlock != null && !ReferenceEquals(prevBlock, b)) RenderBlock(prevBlock);
            RenderBlock(b);
        }

        internal void SelectCellExternal(BlockInstance b, WorkCellData c) { SelectCell(b, c); }

        private void ClearSelection()
        {
            var prev = _selBlock;
            _selBlock = null; _selCell = null;
            if (_cellPanel != null) _cellPanel.Bind(null, null, RoomLocked);
            if (prev != null) RenderBlock(prev);
        }

        // 浮层里的编辑动作统一从这里回流：重画该块 + 刷新统计 + 刷新标题
        internal void CellEdited(BlockInstance b, WorkCellData c)
        {
            if (b != null) RenderBlock(b);
            RefreshModelStats();
            if (_cellPanel != null) _cellPanel.RefreshTitle();
        }

        internal void DeleteSelectedBlockFromPanel() { DeleteSelectedBlock(); }

        // 双击工位：就地编辑。房间锁定只锁布局，工位内容随时可编辑。
        internal void OpenCellEditor(BlockInstance b, WorkCellData c)
        {
            SelectCell(b, c);
            if (_cellEditor != null) { try { _cellEditor.Close(); } catch { } _cellEditor = null; }
            Point sp;
            try { sp = ToDip(PointToScreen(Mouse.GetPosition(this))); }
            catch { sp = new Point(400, 300); }
            string title = Loc.F("{0} · 第 {1} 格{2}", b.Name, b.Cells.IndexOf(c) + 1, c.IsLeader ? Loc.T("（领导位）") : "");
            var w = new CellEditorWindow(c, title, sp, delegate
            {
                CellEdited(b, c);
                if (_cellPanel != null && ReferenceEquals(_selCell, c)) _cellPanel.Bind(b, c, RoomLocked);
            });
            w.Owner = this;
            w.Closed += (s, e) => { _cellEditor = null; RefreshModelStats(); };
            _cellEditor = w;
            w.Show();
        }

        private void DeleteSelectedBlock()
        {
            if (_selBlock == null) return;
            if (RoomLocked)
            {
                Status(Loc.T("房间已锁定，不能删除工位块（F2 里解锁）"));
                return;
            }
            if (_selBlock.Locked)
            {
                // 固定住的块：Delete 键直接不响应，按钮本身也是灰的
                Status(Loc.F("「{0}」已固定，不能删除（右键可解除固定）", _selBlock.Name));
                return;
            }
            if (MessageBox.Show(Loc.F("确定删除「{0}」这个工位块？\n里面的数据会一起删掉。", _selBlock.Name), Loc.T("删除确认"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _room.Blocks.Remove(_selBlock);
            ClearSelection();
            RedrawCanvas();
            Status(Loc.T("已删除工位块"));
        }

        internal void ToggleLock(BlockInstance b)
        {
            if (RoomLocked) { Status(Loc.T("房间已锁定，固定状态也改不了（F2 里解锁）")); return; }
            b.Locked = !b.Locked;
            BlockVisual v;
            if (_visuals.TryGetValue(b.Id, out v)) v.RefreshLockMenu();
            if (ReferenceEquals(b, _selBlock)) SelectCell(b, _selCell);
            Status(b.Locked ? Loc.T("已固定：拖不动、删不掉") : Loc.T("已解除固定"));
        }

        internal void DeleteBlockDirect(BlockInstance b)
        {
            if (RoomLocked) { Status(Loc.T("房间已锁定，不能删除工位块（F2 里解锁）")); return; }
            if (b.Locked)
            {
                Status(Loc.F("「{0}」已固定，不能删除（右键可解除固定）", b.Name));
                return;
            }
            if (MessageBox.Show(Loc.F("确定删除「{0}」？", b.Name), Loc.T("删除确认"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _room.Blocks.Remove(b);
            if (ReferenceEquals(_selBlock, b)) ClearSelection();
            RedrawCanvas();
            Status(Loc.T("已删除工位块"));
        }

        // ---------- 弹窗 ----------
        // 屏幕坐标是物理像素，Window.Left/Top 是 DIP，125% 缩放下必须换算，否则弹窗位置偏移
        private Point ToDip(Point p)
        {
            try
            {
                PresentationSource src = PresentationSource.FromVisual(this);
                if (src != null && src.CompositionTarget != null)
                {
                    Matrix m = src.CompositionTarget.TransformToDevice;
                    if (m.M11 > 0) p.X /= m.M11;
                    if (m.M22 > 0) p.Y /= m.M22;
                }
            }
            catch { }
            return p;
        }

        internal void ShowMachinePopup(UIElement anchor, WorkCellData c)
        {
            new PopupWindow(anchor, c, ToDip(PointToScreenFromCanvas(anchor))).Show();
        }

        private Point PointToScreenFromCanvas(UIElement el)
        {
            try
            {
                Point p = el.TransformToVisual(roomCanvas).Transform(new Point(0, 0));
                return roomCanvas.PointToScreen(new Point(p.X, p.Y + CELL * 0.2));
            }
            catch { return new Point(200, 200); }
        }

        // ---------- 存档 ----------
        // 入口按钮「存档 ▾」：平时点开下拉菜单；进入确认态后再点一次 = 真正覆盖保存（两段式不变）。
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_saveArmed) { DisarmSave(); DoSave(); return; }
            popData.IsOpen = false;
            menuSaveItem.Content = Loc.T("保存当前存档（覆盖）");
            popSave.IsOpen = !popSave.IsOpen;
        }

        private void MenuSave_Click(object sender, RoutedEventArgs e)
        {
            RunAfterMenuClose(delegate
            {
                if (_saveArmed) { DisarmSave(); DoSave(); }
                else ArmSave();
            });
        }

        private void MenuLoad_Click(object sender, RoutedEventArgs e)
        {
            RunAfterMenuClose(delegate { BtnLoad_Click(null, null); });
        }

        private void MenuImportLegacy_Click(object sender, RoutedEventArgs e)
        {
            RunAfterMenuClose(delegate { ImportLegacySave(); });
        }

        // 手动导入 exe 同目录的旧存档：只有用户在这里点了「是」才会读那个文件
        private void ImportLegacySave()
        {
            try
            {
                if (!System.IO.File.Exists(Paths.LegacySaveFile))
                {
                    MessageBox.Show(Loc.T("程序目录下没有找到旧存档。"), Loc.T("导入旧存档"),
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var r = MessageBox.Show(
                    Loc.F("检测到程序目录下有一份旧存档：\n{0}\n\n要把它导入进来吗？\n导入会替换当前的房间数据。",
                        Paths.LegacySaveFile),
                    Loc.T("导入旧存档"), MessageBoxButton.YesNo,
                    MessageBoxImage.Question, MessageBoxResult.No);
                if (r != MessageBoxResult.Yes) return;

                var loaded = Storage.Load(Paths.LegacySaveFile);
                if (loaded == null || loaded.Rooms == null || loaded.Rooms.Count == 0)
                    throw new Exception(Loc.T("存档内没有房间"));
                if (loaded.Library == null) loaded.Library = new List<BlockTemplate>();
                Storage.Migrate(loaded);

                Paths.EnsureDataDir();
                _curSavePath = Paths.SaveFile;
                _app = loaded;
                ClearPendingRoomSettings();
                _room = _app.Rooms.FirstOrDefault(x => x.Id == _app.ActiveRoomId) ?? _app.Rooms[0];
                ClearSelection();
                ClearSearch();
                RebuildModelColors();
                ApplyRoomFieldsFromData();
                RefreshRoomCombo(); RefreshLibrary(); RedrawCanvas(); UpdateSavePathText();
                if (_app.Library.Count > 0) { libList.SelectedIndex = 0; }
                RequestFit();
                Storage.Save(_curSavePath, _app);
                Status(Loc.F("已导入旧存档并保存到 {0}", _curSavePath));
            }
            catch (Exception ex) { MessageBox.Show(Loc.F("读取失败: {0}", ex.Message), Loc.T("错误")); }
        }

        private void ArmSave()
        {
            _saveArmed = true;
            btnSave.Content = Loc.T("确认覆盖？再按一次");
            btnSave.Background = new SolidColorBrush(Color.FromRgb(0x8A, 0x5A, 0x12));
            btnSave.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0xA8, 0x30));
            btnSave.BorderThickness = new Thickness(2);
            btnSave.Foreground = Brushes.White;
            btnSave.FontWeight = FontWeights.Bold;
            btnSave.ToolTip = Loc.F("再按一次就覆盖写入 {0}", _curSavePath);
            menuSaveItem.Content = Loc.T("确认覆盖当前存档");
            Status(Loc.F("注意：存档会覆盖 {0}，再按一次才真正保存（6 秒内有效）", _curSavePath));
            _saveArmTimer.Stop();
            _saveArmTimer.Start();
        }

        private void DisarmSave()
        {
            _saveArmed = false;
            _saveArmTimer.Stop();
            btnSave.Content = Loc.T("存档 ▾");
            btnSave.ClearValue(Button.BackgroundProperty);
            btnSave.ClearValue(Button.BorderBrushProperty);
            btnSave.ClearValue(Button.BorderThicknessProperty);
            btnSave.ClearValue(Button.ForegroundProperty);
            btnSave.ClearValue(Button.FontWeightProperty);
            btnSave.ToolTip = Loc.T("保存存档（覆盖，需确认）/ 读取存档");
            menuSaveItem.Content = Loc.T("保存当前存档（覆盖）");
        }

        private void DoSave()
        {
            try
            {
                ApplyRoomSettings();
                SyncRoom();
                Storage.Save(_curSavePath, _app);

                // 写回后立即读一遍，核对房间数、当前房间、每个房间的块数，防止串房
                int savedCount = _app.Rooms.Count;
                string activeName = _room != null ? _room.Name : "";
                var check = Storage.Load(_curSavePath);
                bool ok = check != null && check.Rooms.Count == savedCount
                          && check.Rooms.Any(r => r.Id == _app.ActiveRoomId && r.Name == activeName);
                if (!ok)
                {
                    MessageBox.Show(Loc.T("存档写回核对失败，请重新保存一次。"), Loc.T("存档校验"));
                    return;
                }
                UpdateSavePathText();
                Status(Loc.F("已保存 {0} 个房间，当前「{1}」· {2}", savedCount, activeName, _curSavePath));
            }
            catch (Exception ex) { MessageBox.Show(Loc.F("保存失败: {0}", ex.Message), Loc.T("错误")); }
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = Loc.T("工位存档 (*.json)|*.json|所有文件 (*.*)|*.*"),
                Title = Loc.T("选择工位存档"),
                InitialDirectory = System.IO.Path.GetDirectoryName(_curSavePath)
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var loaded = Storage.Load(dlg.FileName);
                if (loaded.Rooms == null || loaded.Rooms.Count == 0) throw new Exception(Loc.T("存档内没有房间"));
                if (loaded.Library == null) loaded.Library = new List<BlockTemplate>();
                Storage.Migrate(loaded);
                _curSavePath = dlg.FileName;
                _app = loaded;
                ClearPendingRoomSettings();
                _room = _app.Rooms.FirstOrDefault(r => r.Id == _app.ActiveRoomId) ?? _app.Rooms[0];
                ClearSelection();
                ClearSearch();
                RebuildModelColors();
                ApplyRoomFieldsFromData();
                RefreshRoomCombo(); RefreshLibrary(); RedrawCanvas(); UpdateSavePathText();
                if (_app.Library.Count > 0) { libList.SelectedIndex = 0; }
                RequestFit();
                Status(Loc.T("已读取: ") + dlg.FileName);
            }
            catch (Exception ex) { MessageBox.Show(Loc.F("读取失败: {0}", ex.Message), Loc.T("错误")); }
        }

        private void SyncRoom()
        {
            if (_room == null) return;
            _app.ActiveRoomId = _room.Id;
        }

        // ---------- 导入 / 同步 ----------
        // ---------- 数据菜单：模板 / 导入 / 同步 ----------
        private void BtnData_Click(object sender, RoutedEventArgs e)
        {
            popSave.IsOpen = false;
            popData.IsOpen = !popData.IsOpen;
        }

        // 先收起下拉，再弹出对话框/提示，避免下拉浮层跟着抢焦点
        private void RunAfterMenuClose(Action act)
        {
            popData.IsOpen = false;
            popSave.IsOpen = false;
            Dispatcher.BeginInvoke(new Action(act), DispatcherPriority.Background);
        }

        private void MenuTemplate_Click(object sender, RoutedEventArgs e)
        {
            RunAfterMenuClose(SaveDataTemplate);
        }

        private void MenuImport_Click(object sender, RoutedEventArgs e)
        {
            RunAfterMenuClose(delegate { BtnImport_Click(null, null); });
        }

        private void MenuSync_Click(object sender, RoutedEventArgs e)
        {
            RunAfterMenuClose(delegate { BtnSync_Click(null, null); });
        }

        private void MenuExportPeople_Click(object sender, RoutedEventArgs e)
        {
            RunAfterMenuClose(ExportPeopleTable);
        }

        // 导出当前房间的人员表：列与导入模板完全一致，一人多机就多行，网线口号一起带上
        private void ExportPeopleTable()
        {
            if (_room == null) { Status(Loc.T("没有可导出的房间")); return; }

            int people;
            var rows = BuildPeopleRows(out people);

            if (rows.Count == 0)
            {
                MessageBox.Show(Loc.F("当前房间「{0}」里还没有人名，没什么可导出的。\n\n先把房间里工位的人名填上。", _room.Name),
                    Loc.T("没有人员"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string initDir = System.IO.Path.GetDirectoryName(_curSavePath);
            if (string.IsNullOrEmpty(initDir) || !System.IO.Directory.Exists(initDir))
                initDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            var dlg = new SaveFileDialog
            {
                Filter = Loc.T("Excel 表格 (*.xlsx)|*.xlsx"),
                Title = Loc.T("导出人员表"),
                FileName = "Zogrid-People-" + SafeFileNamePart(_room.Name) + "-" + DateTime.Now.ToString("MMdd") + ".xlsx",
                InitialDirectory = initDir
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                Storage.WritePeople(dlg.FileName, rows);
                Status(Loc.F("已导出 {0} 人 / {1} 行 → {2}", people, rows.Count, dlg.FileName));
                var ask = MessageBox.Show(
                    Loc.F("已导出「{0}」的人名、工号、内外网、MAC、IP、备注、网线口号、显示器型号，共 {1} 人 / {2} 行。\n\n{3}\n\n",
                        _room.Name, people, rows.Count, dlg.FileName) +
                    Loc.T("列和导入模板一致，改完可以直接「导入表格 → 同步到工位」。\n\n现在打开看看吗？"),
                    Loc.T("导出完成"), MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (ask == MessageBoxResult.Yes) System.Diagnostics.Process.Start(dlg.FileName);
            }
            catch (Exception ex) { MessageBox.Show(Loc.F("导出失败: {0}", ex.Message), Loc.T("错误")); }
        }

        // 把当前房间里的人（含每人名下的机器）摊成表格行；people 返回人名个数
        private List<string[]> BuildPeopleRows(out int people)
        {
            people = 0;
            var rows = new List<string[]>();
            if (_room == null || _room.Blocks == null) return rows;

            foreach (var b in _room.Blocks)
            {
                if (b.Cells == null) continue;
                foreach (var cell in b.Cells)
                {
                    string nm = (cell.Name ?? "").Trim();
                    if (nm.Length == 0) continue;
                    people++;
                    string port = (cell.Port ?? "").Trim();
                    string emp = (cell.EmployeeId ?? "").Trim();

                    if (cell.Machines != null && cell.Machines.Count > 0)
                    {
                        foreach (var mi in cell.Machines)
                        {
                            string note = (mi.Note ?? "").Trim();
                            if (note.Length == 0) note = (mi.Model ?? "").Trim();
                            string e = (mi.EmployeeId ?? "").Trim();
                            string mon = (mi.Monitor ?? "").Trim();
                            if (mon.Length == 0) mon = (cell.Monitor ?? "").Trim();
                            rows.Add(new[]
                            {
                                nm, e.Length > 0 ? e : emp,
                                (mi.NetType ?? "").Trim(), (mi.Mac ?? "").Trim(), (mi.Ip ?? "").Trim(),
                                note, port, mon
                            });
                        }
                    }
                    else
                    {
                        // 只有人名（还没同步过机器）：备注里放主机型号，回来还能认得出
                        rows.Add(new[] { nm, emp, "", "", "", (cell.Computer ?? "").Trim(), port, (cell.Monitor ?? "").Trim() });
                    }
                }
            }
            return rows;
        }

        // 文件名里不能出现的字符换成下划线
        private static string SafeFileNamePart(string s)
        {
            if (string.IsNullOrEmpty(s)) return Loc.T("房间");
            var sb = new StringBuilder(s.Length);
            char[] bad = System.IO.Path.GetInvalidFileNameChars();
            foreach (char ch in s)
                sb.Append(Array.IndexOf(bad, ch) >= 0 ? '_' : ch);
            string r = sb.ToString().Trim();
            return r.Length == 0 ? Loc.T("房间") : r;
        }

        private void SaveDataTemplate()
        {
            string initDir = System.IO.Path.GetDirectoryName(_curSavePath);
            if (string.IsNullOrEmpty(initDir) || !System.IO.Directory.Exists(initDir))
                initDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var dlg = new SaveFileDialog
            {
                Filter = Loc.T("Excel 模板 (*.xlsx)|*.xlsx"),
                Title = Loc.T("保存空白数据模板"),
                FileName = "Zogrid-data-template.xlsx",
                InitialDirectory = initDir
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                Storage.WriteTemplate(dlg.FileName);
                Status(Loc.F("模板已生成: {0}", dlg.FileName));
                var ask = MessageBox.Show(
                    Loc.F("空白模板已保存：\n{0}\n\n填好「姓名」以及工号 / 内外网 / MAC / IP / 备注（型号）/ 网线口号 / 显示器型号 后，回到程序：\n数据 ▾ → 导入表格 →（点同步到工位）\n\n现在打开这个模板吗？",
                        dlg.FileName),
                    Loc.T("模板已生成"), MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (ask == MessageBoxResult.Yes) System.Diagnostics.Process.Start(dlg.FileName);
            }
            catch (Exception ex) { MessageBox.Show(Loc.F("模板生成失败: {0}", ex.Message), Loc.T("错误")); }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Excel (*.xlsx)|*.xlsx",
                Title = Loc.T("选择人员/机器表格"),
                InitialDirectory = System.IO.Path.GetDirectoryName(_curSavePath)
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                _machines = Storage.ReadMachines(dlg.FileName);
                int rows = 0;
                foreach (var kv in _machines) rows += kv.Value.Count;
                if (rows == 0)
                {
                    Status(Loc.T("表格里没读到数据"));
                    MessageBox.Show(Loc.T("没读到数据。\n\n请确认：\n1) 第一行是表头，其中一列叫「姓名」\n2) 姓名下面确实填了内容\n\n可以先用「数据 ▾ → 下载空白模板」照着填。"),
                        Loc.T("没有数据"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Status(Loc.F("已读取 {0} 条机器记录（{1} 人），点「同步到工位」填入房间", rows, _machines.Count));
                MessageBox.Show(Loc.F("读取到 {0} 人、共 {1} 条机器记录。\n再点「同步到工位」，会按人名自动填入型号 / IP / MAC / 工号 / 网线口号 / 显示器。",
                    _machines.Count, rows), Loc.T("导入成功"));
            }
            catch (Exception ex) { MessageBox.Show(Loc.F("读取表格失败: {0}", ex.Message), Loc.T("错误")); }
        }

        private void BtnSync_Click(object sender, RoutedEventArgs e)
        {
            if (_machines.Count == 0)
            {
                // 自动化测试用：WS_SYNC_FILE 直接指定表格，跳过选文件对话框
                string envFile = Environment.GetEnvironmentVariable("WS_SYNC_FILE");
                if (!string.IsNullOrEmpty(envFile) && System.IO.File.Exists(envFile))
                {
                    try { _machines = Storage.ReadMachines(envFile); }
                    catch (Exception ex) { MessageBox.Show(Loc.F("读取表格失败: {0}", ex.Message), Loc.T("错误")); return; }
                }
                else
                {
                    var dlg = new OpenFileDialog
                    {
                        Filter = "Excel (*.xlsx)|*.xlsx",
                        InitialDirectory = System.IO.Path.GetDirectoryName(_curSavePath)
                    };
                    if (dlg.ShowDialog() != true) return;
                    try { _machines = Storage.ReadMachines(dlg.FileName); }
                    catch (Exception ex) { MessageBox.Show(Loc.F("读取表格失败: {0}", ex.Message), Loc.T("错误")); return; }
                }
            }

            int matched = 0, filled = 0, fuzzyMatched = 0, portsFilled = 0, monitorsFilled = 0;
            var unmatched = new List<string>();
            int skippedLocked = 0;
            foreach (var room in _app.Rooms)
            {
                if (room.Locked) { skippedLocked++; continue; }
                foreach (var b in room.Blocks)
                    foreach (var cell in b.Cells)
                    {
                        string nm = (cell.Name ?? "").Trim();
                        if (nm.Length == 0) continue;
                        bool exact;
                        List<MachineInfo> ms = SyncMatcher.Find(_machines, nm, out exact);
                        if (ms == null)
                        {
                            if (!unmatched.Contains(nm)) unmatched.Add(nm);
                            continue;
                        }
                        bool fuzzy = !exact;
                        matched++;
                        if (fuzzy) fuzzyMatched++;
                        // 机器分三级：0 固定机正常显示；1 笔记本；2 新档案机/华为PAD/VTM机/电视。
                        // 级别越大越往后排，且级别 1、2 不往格子外面的型号文字里写。
                        // 判断同时看备注和型号，避免备注列识别错位时漏判。
                        string[] lowKeys = { Loc.T("档案机"), Loc.T("华为pad"), "vtm", Loc.T("电视") };
                        Func<MachineInfo, int> rankOf = m =>
                        {
                            if (m == null) return 0;
                            string s = (m.Note ?? "") + " " + (m.Model ?? "");
                            foreach (var k in lowKeys)
                                if (s.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return 2;
                            if (s.IndexOf(Loc.T("笔记本"), StringComparison.Ordinal) >= 0) return 1;
                            return 0;
                        };
                        // OrderBy 是稳定排序，同级之间保持表格原顺序
                        var ordered = ms.OrderBy(rankOf).ToList();
                        // 一个人名下最多认 4 台主机，多的不要
                        var take = new List<MachineInfo>();
                        for (int i = 0; i < ordered.Count && take.Count < ComputerUtil.MaxModels; i++) take.Add(ordered[i]);
                        cell.Machines = take;
                        // 一人两机 → “3010 3060”，中间一个空格，不再带 / 号。
                        // 两台同型号就显示两遍（如 "3080 3080"）——它们是两台机器，型号相同而已，不去重。
                        // 外面只用固定机；万一这个人一台固定机都没有，才退而用笔记本填，避免格子空着。
                        // 级别 2（新档案机/华为PAD/VTM机/电视）永远不在外面显示。
                        var models = new List<string>();
                        foreach (var mi in take)
                        {
                            if (rankOf(mi) != 0) continue;
                            foreach (var md in ComputerUtil.Models(mi.Model))
                                if (models.Count < ComputerUtil.MaxModels) models.Add(md);
                        }
                        if (models.Count == 0)
                            foreach (var mi in take)
                            {
                                if (rankOf(mi) != 1) continue;
                                foreach (var md in ComputerUtil.Models(mi.Model))
                                    if (models.Count < ComputerUtil.MaxModels) models.Add(md);
                            }
                        if (models.Count > 0) { cell.Computer = string.Join(" ", models.ToArray()); filled++; }
                        var emp = take.FirstOrDefault(m => !string.IsNullOrEmpty(m.EmployeeId));
                        if (emp != null && string.IsNullOrEmpty(cell.EmployeeId)) cell.EmployeeId = emp.EmployeeId;
                        // 网线口号：表里有值就写进这个人所在格子的网口（表里是空的就不动，
                        // 免得把墙上已经核过的口号擦掉）。同一个人多台机器时取第一条填了的。
                        var portHit = take.FirstOrDefault(m => !string.IsNullOrEmpty((m.Port ?? "").Trim()));
                        if (portHit != null)
                        {
                            string pv = portHit.Port.Trim();
                            if (pv != cell.Port) { cell.Port = pv; portsFilled++; }
                        }
                        // 显示器型号：同理，表里有值就写到格子底部的显示器文字里，空值不动。
                        // 这份数据跟着存档一起存，下次打开还在。
                        var monHit = take.FirstOrDefault(m => !string.IsNullOrEmpty((m.Monitor ?? "").Trim()));
                        if (monHit != null)
                        {
                            string mv = monHit.Monitor.Trim();
                            if (mv != cell.Monitor) { cell.Monitor = mv; monitorsFilled++; }
                        }
                    }
            }

            RedrawCanvas();
            if (_selBlock != null && _selCell != null) SelectCell(_selBlock, _selCell);
            string lockTip = skippedLocked > 0 ? Loc.F("（跳过 {0} 个已锁房间）", skippedLocked) : "";
            Status(Loc.F("同步完成: 匹配 {0} 个工位（模糊 {1} 个），填入型号 {2}，网线口号 {3}，显示器 {4}{5}",
                matched, fuzzyMatched, filled, portsFilled, monitorsFilled, lockTip));

            string tip = "";
            if (unmatched.Count > 0)
            {
                int show = Math.Min(10, unmatched.Count);
                tip = Loc.F("\n\n没对上的名字（{0} 个）:\n{1}", unmatched.Count,
                            string.Join(Loc.T("、"), unmatched.GetRange(0, show).ToArray()));
                if (unmatched.Count > show) tip += " …";
            }
            MessageBox.Show(Loc.F("同步完成\n匹配工位: {0}（其中模糊 {1}）\n填入型号: {2}\n填入网线口号: {3}\n填入显示器: {4}{5}{6}",
                matched, fuzzyMatched, filled, portsFilled, monitorsFilled, lockTip, tip),
                Loc.T("同步结果"));
        }

        // 顶部工具栏 / 底部机器统计条：不出横向滚动条，鼠标滚轮在条上滚就是左右移动
        // 向上滚往左、向下滚往右；已经到两头就把事件还给外层，不影响别处的滚轮
        private void HScroll_Wheel(object sender, MouseWheelEventArgs e)
        {
            var sv = sender as ScrollViewer;
            if (sv == null || sv.ScrollableWidth <= 0) return;
            double step = (e.Delta / 120.0) * 90;
            double target = sv.HorizontalOffset - step;
            if (target < 0) target = 0;
            if (target > sv.ScrollableWidth) target = sv.ScrollableWidth;
            if (Math.Abs(target - sv.HorizontalOffset) < 0.5) return;
            sv.ScrollToHorizontalOffset(target);
            e.Handled = true;
        }

        // 姓名在表格姓名列里找不到时：去每行的备注（含二维码列）里模糊找这个名字
        // 只回机器信息，格子里的人名绝不动
    }
}
