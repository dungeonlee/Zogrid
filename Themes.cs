using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WorkstationStudio
{
    // ===== 配色方案 =====
    // 全部走护眼浅淡 / 低饱和路线，不用红蓝强对比
    public class ThemePreset
    {
        public string Key;
        public string Name;
        public string Note;
        public bool Light;

        public string Floor;
        public string Grid;

        public Color CellBg;
        public Color CellBgLeader;
        public Color CellBorder;
        public Color CellBorderLeader;

        public Color NameFg;
        public Color CompFg;
        public Color MonFg;
        public Color PortFg;
        public Color PortDualFg;
        public Color EyeFg;
        public Color SelBorder;

        public Color CanvasBg;
        public Color HintFg;

        public double BlockFillAlpha;
        public double BlockEdgeAlpha;

        public static readonly List<ThemePreset> All = new List<ThemePreset>();

        // 工位块本身的备选颜色：低饱和，深浅两套底色上都不刺眼
        public static readonly string[] BlockPalette =
        {
            "#6E93B8", "#6FA695", "#9B8CB5", "#B79A6E", "#B98A8A", "#8CA37C"
        };
        public static readonly string[] BlockPaletteNames =
        {
            Loc.T("雾蓝"), Loc.T("青瓷"), Loc.T("藕紫"), Loc.T("砂金"), Loc.T("陶粉"), Loc.T("豆绿")
        };

        static ThemePreset()
        {
            All.Add(new ThemePreset
            {
                Key = "graphite", Name = Loc.T("石墨夜"), Note = Loc.T("深灰底 · 低亮 · 夜里看最省眼"), Light = false,
                Floor = "#1E232B", Grid = "#2C333F",
                CellBg = C("#262D37"), CellBgLeader = C("#302D25"),
                CellBorder = C("#3B4553"), CellBorderLeader = C("#7A6636"),
                NameFg = C("#E4EAF2"), CompFg = C("#8CCFE8"), MonFg = C("#93BFA9"),
                PortFg = C("#C89A9A"), PortDualFg = C("#8FC7A4"), EyeFg = C("#6FA8C4"),
                SelBorder = C("#56AEDA"), CanvasBg = C("#14181D"), HintFg = C("#5A6572"),
                BlockFillAlpha = 26, BlockEdgeAlpha = 150
            });

            All.Add(new ThemePreset
            {
                Key = "bamboo", Name = Loc.T("墨竹夜"), Note = Loc.T("深绿灰底 · 长时间盯屏不累"), Light = false,
                Floor = "#1C2420", Grid = "#2A352F",
                CellBg = C("#232C27"), CellBgLeader = C("#2F2C24"),
                CellBorder = C("#38453D"), CellBorderLeader = C("#77653C"),
                NameFg = C("#E2EBE4"), CompFg = C("#9AD3C4"), MonFg = C("#9BC0A4"),
                PortFg = C("#C39C97"), PortDualFg = C("#93C7A2"), EyeFg = C("#74A895"),
                SelBorder = C("#62B39B"), CanvasBg = C("#131715"), HintFg = C("#5C6B63"),
                BlockFillAlpha = 26, BlockEdgeAlpha = 145
            });

            All.Add(new ThemePreset
            {
                Key = "paper", Name = Loc.T("宣纸米"), Note = Loc.T("暖米底 · 像纸一样柔和"), Light = true,
                Floor = "#F1EADC", Grid = "#DCCFBB",
                CellBg = C("#FBF6ED"), CellBgLeader = C("#FBF0D9"),
                CellBorder = C("#D8CBB6"), CellBorderLeader = C("#C6A85C"),
                NameFg = C("#3A342B"), CompFg = C("#2F6076"), MonFg = C("#4C6B57"),
                PortFg = C("#8C5F4E"), PortDualFg = C("#3F7355"), EyeFg = C("#94A6A8"),
                SelBorder = C("#4F86A8"), CanvasBg = C("#E4DCCD"), HintFg = C("#9A8F7C"),
                BlockFillAlpha = 40, BlockEdgeAlpha = 175
            });

            All.Add(new ThemePreset
            {
                Key = "green", Name = Loc.T("护眼绿"), Note = Loc.T("淡豆绿底 · 公认最护眼"), Light = true,
                Floor = "#E6EFE2", Grid = "#CBDCC4",
                CellBg = C("#F4F9F1"), CellBgLeader = C("#F8F2DC"),
                CellBorder = C("#CADCC2"), CellBorderLeader = C("#C2A65E"),
                NameFg = C("#2C3A2E"), CompFg = C("#2A6272"), MonFg = C("#3F6B4C"),
                PortFg = C("#8A5F5A"), PortDualFg = C("#336E4F"), EyeFg = C("#7FA090"),
                SelBorder = C("#4A8C6A"), CanvasBg = C("#DCE7D8"), HintFg = C("#87977F"),
                BlockFillAlpha = 40, BlockEdgeAlpha = 175
            });

            All.Add(new ThemePreset
            {
                Key = "mist", Name = Loc.T("雾霭蓝"), Note = Loc.T("冷调浅灰蓝 · 清爽不刺目"), Light = true,
                Floor = "#E7ECF2", Grid = "#CFD8E3",
                CellBg = C("#F6F8FB"), CellBgLeader = C("#FAF3E4"),
                CellBorder = C("#CDD7E2"), CellBorderLeader = C("#C0A96E"),
                NameFg = C("#2B333D"), CompFg = C("#2E5F7E"), MonFg = C("#456A62"),
                PortFg = C("#855F63"), PortDualFg = C("#3A6E5C"), EyeFg = C("#8AA0AD"),
                SelBorder = C("#4B82A8"), CanvasBg = C("#DBE2EA"), HintFg = C("#8593A1"),
                BlockFillAlpha = 40, BlockEdgeAlpha = 175
            });

            All.Add(new ThemePreset
            {
                Key = "lilac", Name = Loc.T("紫金"), Note = Loc.T("淡紫底 · 领导位描金"), Light = true,
                Floor = "#ECE7F1", Grid = "#D6CBE0",
                CellBg = C("#F8F5FB"), CellBgLeader = C("#FAF0DC"),
                CellBorder = C("#D6CBE0"), CellBorderLeader = C("#C9A94F"),
                NameFg = C("#342E3D"), CompFg = C("#4C4E86"), MonFg = C("#5C6470"),
                PortFg = C("#855D6B"), PortDualFg = C("#45705F"), EyeFg = C("#978CA6"),
                SelBorder = C("#7A6BA0"), CanvasBg = C("#E1DAE8"), HintFg = C("#8D8299"),
                BlockFillAlpha = 40, BlockEdgeAlpha = 175
            });

            Current = All[0];
        }

        public static ThemePreset Current;

        public static ThemePreset Get(string key)
        {
            if (!string.IsNullOrEmpty(key))
                foreach (var t in All)
                    if (string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase)) return t;
            return All[0];
        }

        public static ThemePreset MatchByFloor(string floor)
        {
            if (!string.IsNullOrEmpty(floor))
                foreach (var t in All)
                    if (string.Equals(t.Floor, floor, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        public static Color C(string hex)
        {
            try { return (Color)ColorConverter.ConvertFromString(hex); }
            catch { return Colors.Gray; }
        }

        public static SolidColorBrush B(Color c) { return new SolidColorBrush(c); }

        public static SolidColorBrush B(string hex, double opacity)
        {
            var b = new SolidColorBrush(C(hex));
            b.Opacity = opacity;
            return b;
        }
    }

    // ===== 焦点归位：子窗口/模态窗关闭后把焦点交还应用内窗口，防止整个程序跑后台 =====
    internal static class FocusHelp
    {
        // 当前活动的浮层（非主窗口），作为模态窗的 Owner，保证 ShowDialog 关闭后焦点回到浮层，
        // 而不是跳到主窗口再导致浮层失焦、关浮层时整个程序退到后台。
        public static Window ActiveOwner(Window fallback)
        {
            var app = Application.Current;
            if (app != null)
                foreach (Window w in app.Windows)
                    if (w.IsVisible && w.IsActive && !ReferenceEquals(w, app.MainWindow)) return w;
            return fallback;
        }

        public static void Restore()
        {
            var app = Application.Current;
            if (app == null) return;
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    foreach (Window w in app.Windows)
                        if (w.IsVisible && w.IsActive) return;   // 已有活动窗口，不抢焦点
                    if (app.MainWindow != null && app.MainWindow.IsVisible) app.MainWindow.Activate();
                }
                catch { }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
    }

    // ===== 圆饼色轮：自定取色（HSV 圆盘，点哪儿取哪儿） =====
    // 角度=色相，半径=饱和度，明度固定偏亮；另配一条明度滑杆。
    public class ColorWheel : UserControl
    {
        public event Action<string> ColorPicked;
        private string _hex;
        private TextBlock _hexText;
        private Border _preview;
        private Slider _valSlider;
        private WriteableBitmap _bmp;
        private const int SIZE = 132;

        public ColorWheel(string current)
        {
            _hex = string.IsNullOrEmpty(current) ? "#3FA7D6" : current;
            var root = new StackPanel { Orientation = Orientation.Horizontal };
            // 圆盘
            _bmp = new WriteableBitmap(SIZE, SIZE, 96, 96, PixelFormats.Bgra32, null);
            RenderWheel(1.0);
            var img = new Image
            {
                Source = _bmp, Width = SIZE, Height = SIZE,
                Stretch = Stretch.Fill, Cursor = Cursors.Cross
            };
            img.MouseLeftButtonDown += (s, e) => PickAt(e.GetPosition(img));
            img.MouseMove += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) PickAt(e.GetPosition(img)); };

            var left = new StackPanel();
            left.Children.Add(img);
            left.Children.Add(new TextBlock
            {
                Text = Loc.T("圆盘取色 · 外圈更艳 中心更灰"), FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromRgb(147, 160, 176)),
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0)
            });
            root.Children.Add(left);

            var right = new StackPanel { Margin = new Thickness(10, 0, 0, 0), Width = 116, VerticalAlignment = VerticalAlignment.Center };
            _preview = new Border
            {
                Height = 30, CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(ThemePreset.C(_hex)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(90, 100, 116)), BorderThickness = new Thickness(1)
            };
            right.Children.Add(_preview);
            _hexText = new TextBlock
            {
                Text = _hex.ToUpperInvariant(), FontSize = 12, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 235, 242)),
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 8)
            };
            right.Children.Add(_hexText);
            right.Children.Add(new TextBlock
            {
                Text = Loc.T("明度"), FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(147, 160, 176))
            });
            _valSlider = new Slider
            {
                Minimum = 0.25, Maximum = 1.0, Value = 1.0,
                SmallChange = 0.05, LargeChange = 0.2, Margin = new Thickness(0, 2, 0, 0)
            };
            _valSlider.ValueChanged += (s, e) => { RenderWheel(_valSlider.Value); ApplyHex(); };
            right.Children.Add(_valSlider);
            var use = new Button
            {
                Content = Loc.T("用这个色"), Height = 26, Margin = new Thickness(0, 8, 0, 0), FontSize = 11.5,
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromRgb(46, 110, 142)),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(63, 167, 214)), BorderThickness = new Thickness(1)
            };
            use.Click += (s, e) => { if (ColorPicked != null) ColorPicked(_hex); };
            right.Children.Add(use);
            root.Children.Add(right);

            this.Content = root;
        }

        private void RenderWheel(double value)
        {
            int w = SIZE, h = SIZE;
            var px = new byte[w * h * 4];
            double cx = (w - 1) / 2.0, cy = (h - 1) / 2.0, radius = cx - 2;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double dx = x - cx, dy = y - cy;
                    double r = Math.Sqrt(dx * dx + dy * dy);
                    int i = (y * w + x) * 4;
                    if (r > radius) { px[i + 3] = 0; continue; }
                    double hue = (Math.Atan2(dy, dx) * 180.0 / Math.PI + 360.0) % 360.0;
                    double sat = Math.Min(1.0, r / radius);
                    Color c = HsvToRgb(hue, sat, value);
                    px[i] = c.B; px[i + 1] = c.G; px[i + 2] = c.R; px[i + 3] = 255;
                }
            _bmp.WritePixels(new Int32Rect(0, 0, w, h), px, w * 4, 0);
        }

        private void PickAt(Point p)
        {
            double cx = (SIZE - 1) / 2.0, cy = (SIZE - 1) / 2.0, radius = cx - 2;
            double dx = p.X - cx, dy = p.Y - cy;
            if (Math.Sqrt(dx * dx + dy * dy) > radius) return;
            double hue = (Math.Atan2(dy, dx) * 180.0 / Math.PI + 360.0) % 360.0;
            double sat = Math.Min(1.0, Math.Sqrt(dx * dx + dy * dy) / radius);
            Color c = HsvToRgb(hue, sat, _valSlider.Value);
            _hex = string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
            ApplyHex();
        }

        private void ApplyHex()
        {
            _hexText.Text = _hex.ToUpperInvariant();
            _preview.Background = new SolidColorBrush(ThemePreset.C(_hex));
        }

        public static Color HsvToRgb(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360 / 60.0;
            int sector = (int)Math.Floor(h) % 6;
            double f = h - Math.Floor(h);
            double p = v * (1 - s), q = v * (1 - f * s), t = v * (1 - (1 - f) * s);
            double r, g, b;
            switch (sector)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }
            return Color.FromRgb((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
        }
    }

    // ===== 配色选择窗 =====
    public class ThemeWindow : Window
    {
        public ThemePreset Selected { get; private set; }

        public ThemeWindow(string currentKey)
        {
            Title = Loc.T("房间配色");
            Width = 466; SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None; AllowsTransparency = true;
            Background = Brushes.Transparent; ShowInTaskbar = false;

            var wrap = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 31, 39)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(63, 70, 84)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16), Margin = new Thickness(12)
            };
            var sp = new StackPanel();
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            head.Children.Add(new TextBlock
            {
                Text = Loc.T("选一套配色"), FontSize = 15, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 235, 242))
            });
            var close = new Button
            {
                Content = "×", Width = 22, Height = 22, Padding = new Thickness(0),
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(147, 160, 176)), Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            close.Click += (s, e) => { this.DialogResult = false; this.Close(); };
            DockPanel.SetDock(close, Dock.Right);
            head.Children.Add(close);
            sp.Children.Add(head);
            sp.Children.Add(new TextBlock
            {
                Text = Loc.T("地板、格线、工位格底色一起换；机型文字颜色不跟着变，只在底部机器统计面板里设。"),
                FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(147, 160, 176)),
                Margin = new Thickness(0, 0, 0, 12), TextWrapping = TextWrapping.Wrap
            });

            foreach (var t in ThemePreset.All)
            {
                bool isCur = string.Equals(t.Key, currentKey, StringComparison.OrdinalIgnoreCase);
                var card = MakeCard(t, isCur);
                sp.Children.Add(card);
            }

            wrap.Child = sp;
            this.Content = wrap;
            this.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { this.DialogResult = false; this.Close(); } };
        }

        private Border MakeCard(ThemePreset t, bool isCurrent)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(33, 39, 48)),
                BorderBrush = new SolidColorBrush(isCurrent
                    ? Color.FromRgb(86, 174, 218) : Color.FromRgb(50, 58, 70)),
                BorderThickness = new Thickness(isCurrent ? 1.5 : 1),
                CornerRadius = new CornerRadius(7), Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 8), Cursor = Cursors.Hand
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };

            // 预览：地板 / 格线 / 工位格 / 领导位
            var preview = new Border
            {
                Width = 84, Height = 50, CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(ThemePreset.C(t.Floor)),
                BorderBrush = new SolidColorBrush(ThemePreset.C(t.Grid)), BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 12, 0)
            };
            var pv = new Grid();
            var cell1 = new Border
            {
                Background = new SolidColorBrush(t.CellBg),
                BorderBrush = new SolidColorBrush(t.CellBorder), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3), Margin = new Thickness(7, 9, 0, 20), HorizontalAlignment = HorizontalAlignment.Left,
                Width = 30
            };
            var cell2 = new Border
            {
                Background = new SolidColorBrush(t.CellBgLeader),
                BorderBrush = new SolidColorBrush(t.CellBorderLeader), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 9, 7, 20), HorizontalAlignment = HorizontalAlignment.Right,
                Width = 30
            };
            pv.Children.Add(cell1);
            pv.Children.Add(cell2);
            preview.Child = pv;
            row.Children.Add(preview);

            var txt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            txt.Children.Add(new TextBlock
            {
                Text = t.Name + (isCurrent ? Loc.T("   ✓ 当前") : ""),
                FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 238, 246))
            });
            txt.Children.Add(new TextBlock
            {
                Text = t.Note, FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(147, 160, 176)),
                Margin = new Thickness(0, 2, 0, 0)
            });
            row.Children.Add(txt);
            card.Child = row;

            card.MouseLeftButtonDown += (s, e) =>
            {
                Selected = t;
                this.DialogResult = true;
                this.Close();
            };
            card.MouseEnter += (s, e) => card.Background = new SolidColorBrush(Color.FromRgb(44, 52, 63));
            card.MouseLeave += (s, e) => card.Background = new SolidColorBrush(Color.FromRgb(33, 39, 48));
            return card;
        }

        public static ThemePreset Pick(Window owner, string currentKey)
        {
            var w = new ThemeWindow(currentKey);
            Window host = FocusHelp.ActiveOwner(owner);
            if (host != null && host.IsVisible) w.Owner = host;
            w.ShowDialog();
            FocusHelp.Restore();
            return w.Selected;
        }
    }

    // ===== 选色窗：只点选，不手填 =====
    public class ColorPickWindow : Window
    {
        public string Result { get; private set; }

        private static readonly string[] Rows =
        {
            "#14181D #1C2420 #1E232B #232A34 #262B33 #2A2F39 #303740 #3B4553",
            "#FBF6ED #F5F0E6 #F1EADC #EFE7D8 #EDE3D2 #E8DCC6 #E4D6BE #DCCFBB",
            "#F4F9F1 #E9F1E4 #E6EFE2 #DCE7D8 #D6E5CF #CBDCC4 #C3D6BA #BFD3B7",
            "#F6F8FB #EEF2F7 #E7ECF2 #DBE2EA #DDE6EF #CFD8E3 #C9D6E2 #B7C6D4",
            "#F8F5FB #F1EBF6 #ECE7F1 #E1DAE8 #E9DFF0 #D6CBE0 #DCCEE6 #CBBBD9",
            "#8C9AA8 #A4B0BC #B7A98E #C6A85C #9C8F7C #87977F #8D8299 #97A6B4"
        };

        public ColorPickWindow(string current)
        {
            Title = Loc.T("选颜色");
            Width = 386; SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None; AllowsTransparency = true;
            Background = Brushes.Transparent; ShowInTaskbar = false;

            var wrap = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 31, 39)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(63, 70, 84)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14), Margin = new Thickness(12)
            };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = Loc.T("选一个颜色"), FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 235, 242)), Margin = new Thickness(0, 0, 0, 8)
            });

            foreach (string line in Rows)
            {
                var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
                foreach (string hex in line.Split(' '))
                {
                    if (hex.Length != 7) continue;
                    bool isCur = string.Equals(hex, current, StringComparison.OrdinalIgnoreCase);
                    var sw = new Border
                    {
                        Width = 32, Height = 26, CornerRadius = new CornerRadius(4),
                        Background = new SolidColorBrush(ThemePreset.C(hex)),
                        BorderBrush = new SolidColorBrush(isCur
                            ? Color.FromRgb(86, 174, 218) : Color.FromRgb(70, 78, 92)),
                        BorderThickness = new Thickness(isCur ? 2 : 1),
                        Margin = new Thickness(0, 0, 4, 4), Cursor = Cursors.Hand, Tag = hex,
                        ToolTip = hex
                    };
                    sw.MouseLeftButtonDown += (s, e) =>
                    {
                        Result = (string)((Border)s).Tag;
                        this.DialogResult = true;
                        this.Close();
                    };
                    row.Children.Add(sw);
                }
                sp.Children.Add(row);
            }

            // 并列圆饼色轮：自定取色
            sp.Children.Add(new TextBlock
            {
                Text = Loc.T("或自定颜色"), FontSize = 11, Margin = new Thickness(0, 8, 0, 4),
                Foreground = new SolidColorBrush(Color.FromRgb(147, 160, 176))
            });
            var wheel = new ColorWheel(current);
            wheel.ColorPicked += hex => { Result = hex; this.DialogResult = true; this.Close(); };
            sp.Children.Add(wheel);

            var cancel = new Button
            {
                Content = Loc.T("取消"), Height = 28, HorizontalAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(14, 0, 14, 0), Margin = new Thickness(0, 6, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(38, 45, 55)),
                Foreground = new SolidColorBrush(Color.FromRgb(210, 218, 228)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 68, 82)), BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            cancel.Click += (s, e) => { this.DialogResult = false; this.Close(); };
            sp.Children.Add(cancel);

            wrap.Child = sp;
            this.Content = wrap;
            this.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { this.DialogResult = false; this.Close(); } };
        }

        public static string Pick(Window owner, string current)
        {
            var w = new ColorPickWindow(current);
            Window host = FocusHelp.ActiveOwner(owner);
            if (host != null && host.IsVisible) w.Owner = host;
            w.ShowDialog();
            FocusHelp.Restore();
            return w.Result;
        }
    }

    // ===== 机型颜色（只在机器统计面板里设定） =====
    public class ModelColorWindow : Window
    {
        public string Result { get; private set; }

        private static readonly string[] Rows =
        {
            "#1F6FB2 #0E7C66 #5B4B9E #9A6B1F #A3392F #3F7A34 #8A3E86 #1C6378",
            "#3D8FD1 #2A9E86 #7C6BC4 #BF8A34 #C25A4E #5E9A4A #A85CA4 #3E86A0",
            "#C0392B #16A085 #6C5CE7 #D68910 #E07A5F #4C9A2A #9B59B6 #2980B9",
            "#D9534F #27AE60 #8E44AD #E67E22 #C0635B #6FA83C #B07CC6 #3498DB",
            "#7F8C8D #95A5A6 #566573 #34495E #B7950B #A04000 #1E8449 #1A5276"
        };

        public ModelColorWindow(string model, string current)
        {
            Title = Loc.T("机型颜色");
            Width = 400; SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None; AllowsTransparency = true;
            Background = Brushes.Transparent; ShowInTaskbar = false;

            var wrap = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 31, 39)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(63, 70, 84)),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14), Margin = new Thickness(12)
            };
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = Loc.F("机型「{0}」的文字颜色", model), FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(230, 235, 242)), Margin = new Thickness(0, 0, 0, 3)
            });
            sp.Children.Add(new TextBlock
            {
                Text = Loc.T("只改这个机型在工位格里的字色，房间配色不会覆盖它。"),
                FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(147, 160, 176)),
                Margin = new Thickness(0, 0, 0, 10)
            });

            foreach (string line in Rows)
            {
                var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
                foreach (string hex in line.Split(' '))
                {
                    if (hex.Length != 7) continue;
                    bool isCur = string.Equals(hex, current, StringComparison.OrdinalIgnoreCase);
                    var sw = new Border
                    {
                        Width = 36, Height = 28, CornerRadius = new CornerRadius(4),
                        Background = new SolidColorBrush(ThemePreset.C(hex)),
                        BorderBrush = new SolidColorBrush(isCur
                            ? Color.FromRgb(86, 174, 218) : Color.FromRgb(70, 78, 92)),
                        BorderThickness = new Thickness(isCur ? 2 : 1),
                        Margin = new Thickness(0, 0, 4, 4), Cursor = Cursors.Hand, Tag = hex,
                        ToolTip = hex
                    };
                    sw.MouseLeftButtonDown += (s, e) =>
                    {
                        Result = (string)((Border)s).Tag;
                        this.DialogResult = true;
                        this.Close();
                    };
                    row.Children.Add(sw);
                }
                sp.Children.Add(row);
            }

            // 并列圆饼色轮：自定取色
            sp.Children.Add(new TextBlock
            {
                Text = Loc.T("或自定颜色"), FontSize = 11, Margin = new Thickness(0, 8, 0, 4),
                Foreground = new SolidColorBrush(Color.FromRgb(147, 160, 176))
            });
            var wheel = new ColorWheel(current);
            wheel.ColorPicked += hex => { Result = hex; this.DialogResult = true; this.Close(); };
            sp.Children.Add(wheel);

            var cancel = new Button
            {
                Content = Loc.T("取消"), Height = 28, HorizontalAlignment = HorizontalAlignment.Right,
                Padding = new Thickness(14, 0, 14, 0), Margin = new Thickness(0, 6, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(38, 45, 55)),
                Foreground = new SolidColorBrush(Color.FromRgb(210, 218, 228)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 68, 82)), BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            cancel.Click += (s, e) => { this.DialogResult = false; this.Close(); };
            sp.Children.Add(cancel);

            wrap.Child = sp;
            this.Content = wrap;
            this.PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { this.DialogResult = false; this.Close(); } };
        }

        public static string Pick(Window owner, string model, string current)
        {
            var w = new ModelColorWindow(model, current);
            Window host = FocusHelp.ActiveOwner(owner);
            if (host != null && host.IsVisible) w.Owner = host;
            w.ShowDialog();
            FocusHelp.Restore();
            return w.Result;
        }
    }

    // ===== Ctrl 拖动单元格用的数据包 =====
    public class CellDragPayload
    {
        public BlockInstance Block;
        public WorkCellData Cell;
    }
}
