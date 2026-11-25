using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WorkstationStudio
{
    // ===== 关于窗：顶部大图标 + 居中排版 + 开源免费条款 =====
    public class AboutWindow : Window
    {
        private static readonly Color Pink = Color.FromRgb(0xFF, 0x2D, 0x9B);
        private static readonly Color PinkDim = Color.FromRgb(0x7A, 0x1F, 0x52);

        public AboutWindow(Window owner)
        {
            if (owner != null) this.Owner = owner;

            StackPanel body;
            var card = PanelUi.CardWindow(this, Loc.T("关于"), out body);
            this.Content = card;
            this.Width = 458;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            this.Title = Loc.T("关于 ") + AppInfo.FullName;

            // ---- 顶部大图标：取 ico 里最大的那一帧，避免缩放糊 ----
            var icon = new Image
            {
                Source = LoadAppIcon(),
                Width = 96, Height = 96,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 10),
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
            if (icon.Source != null) body.Children.Add(icon);

            // 软件名 + 版本：名字白色加粗，版本粉色，横排居中
            var head = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            };
            head.Children.Add(new TextBlock
            {
                Text = AppInfo.Name, FontSize = 23, FontWeight = FontWeights.Bold,
                Foreground = PanelUi.B(PanelUi.Text), VerticalAlignment = VerticalAlignment.Bottom
            });
            head.Children.Add(new TextBlock
            {
                Text = AppInfo.Version, FontSize = 15, FontWeight = FontWeights.SemiBold,
                Foreground = PanelUi.B(Pink), Margin = new Thickness(8, 0, 0, 4),
                VerticalAlignment = VerticalAlignment.Bottom
            });
            body.Children.Add(head);

            body.Children.Add(new TextBlock
            {
                Text = AppInfo.EnName + " · " + AppInfo.Tagline,
                FontSize = 11, Foreground = PanelUi.B(PanelUi.Sub),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            });

            body.Children.Add(new TextBlock
            {
                Text = Loc.F("界面语言：{0}", Loc.CurrentDisplayName()),
                FontSize = 10.5, Foreground = PanelUi.B(PanelUi.Sub),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            });

            // 分隔线
            body.Children.Add(new Rectangle
            {
                Height = 1, Fill = PanelUi.B(PanelUi.Line),
                Margin = new Thickness(2, 0, 2, 13), Opacity = 0.9
            });

            body.Children.Add(Line(Loc.F("Copyright © {0} {1}　一切权利归作者所有。", AppInfo.CopyrightYear, AppInfo.Author),
                PanelUi.Text, 12.5, new Thickness(0, 0, 0, 10)));

            body.Children.Add(Line(Loc.T("本软件开源免费：源代码公开，可自由查看、编译与使用。"), PanelUi.Sub, 11.5, new Thickness(0, 0, 0, 5)));
            body.Children.Add(Line(Loc.T("可商用：院校、企业、网吧等均可免费用于机房、工位与座位管理。"), PanelUi.Sub, 11.5, new Thickness(0, 0, 0, 5)));
            body.Children.Add(Line(Loc.T("但不得收费、不得卖钱：不得以本软件或修改版收取费用、出售或捆绑销售。"), PanelUi.Sub, 11.5, new Thickness(0, 0, 0, 5)));
            body.Children.Add(Line(Loc.T("该限制同样适用于后续改版、二次开发，及 Mac 等其他平台版本。"), PanelUi.Sub, 11.5, new Thickness(0, 0, 0, 5)));
            body.Children.Add(Line(Loc.T("按「现状」提供，不附带任何担保；使用造成的损失作者不承担责任。"), PanelUi.Sub, 11.5, new Thickness(0, 0, 0, 14)));

            // 底部小徽标 + 关闭
            var pill = new Border
            {
                Background = PanelUi.B(Color.FromRgb(0x2A, 0x1C, 0x26)),
                BorderBrush = PanelUi.B(PinkDim), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 3, 10, 3),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            };
            pill.Child = new TextBlock
            {
                Text = Loc.T("开源 · 免费 · 可商用 · 禁止售卖"),
                FontSize = 10.5, Foreground = PanelUi.B(Color.FromRgb(0xFF, 0xB3, 0xD9))
            };
            body.Children.Add(pill);

            var close = new Button
            {
                Content = Loc.T("关闭"), Height = 28, FontSize = 12, Cursor = Cursors.Hand,
                Background = PanelUi.B(Color.FromRgb(58, 71, 86)),
                Foreground = PanelUi.B(PanelUi.Text),
                BorderBrush = PanelUi.B(PanelUi.Line), BorderThickness = new Thickness(1)
            };
            close.Style = PanelUi.FlatButton();
            close.Click += (s, e) => this.Close();
            body.Children.Add(close);

            this.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { e.Handled = true; this.Close(); }
            };
        }

        // 从 exe 内嵌资源里取 ico 最大帧（256×256），比 BitmapImage 直接取首帧清楚
        private static ImageSource LoadAppIcon()
        {
            try
            {
                var res = Application.GetResourceStream(new Uri("pack://application:,,,/app.ico", UriKind.Absolute));
                if (res != null)
                {
                    var dec = new IconBitmapDecoder(res.Stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    BitmapFrame best = null;
                    foreach (var f in dec.Frames)
                        if (best == null || f.PixelWidth > best.PixelWidth) best = f;
                    if (best != null) return best;
                }
            }
            catch { }
            try { return new BitmapImage(new Uri("pack://application:,,,/app.ico", UriKind.Absolute)); }
            catch { return null; }
        }

        private static TextBlock Line(string text, Color color, double size, Thickness margin)
        {
            return new TextBlock
            {
                Text = text, FontSize = size, Foreground = PanelUi.B(color),
                TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = margin
            };
        }
    }
}
