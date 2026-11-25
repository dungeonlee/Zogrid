using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WorkstationStudio
{
    // 房间布局打印：按房间数据画一张黑白线稿（不截屏、不跟屏幕配色），
    // 走系统打印对话框，纸面白底黑线，彩打也只会出灰阶。
    internal static class RoomPrinter
    {
        private const double CELL = 78;          // 与画布保持一致
        private const double SNAP = CELL / 2;

        private static readonly Brush Ink = Brushes.Black;
        private static readonly Brush InkSoft = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A));
        private static readonly Brush InkFaint = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x6E));
        private static readonly Brush GridMajor = new SolidColorBrush(Color.FromRgb(0xC9, 0xC9, 0xC9));
        private static readonly Brush BlockFill = new SolidColorBrush(Color.FromRgb(0xF7, 0xF7, 0xF7));
        private static readonly Brush BlockEdge = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22));
        private static readonly Brush CellLine = new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A));
        private static readonly Brush Rule = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));

        public static void Print(Window owner, RoomData room)
        {
            if (room == null) return;
            var dlg = new PrintDialog();
            try
            {
                if (dlg.ShowDialog() != true) return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.F("打不开打印机对话框：{0}", ex.Message), Loc.T("打印"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            double pw = dlg.PrintableAreaWidth, ph = dlg.PrintableAreaHeight;
            if (double.IsNaN(pw) || pw < 100) pw = 760;
            if (double.IsNaN(ph) || ph < 100) ph = 1040;

            DrawingVisual dv;
            try { dv = Build(room, pw, ph); }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.F("生成打印内容失败：{0}", ex.Message), Loc.T("打印"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                dlg.PrintVisual(dv, Loc.F("房间布局 {0}", room.Name ?? ""));
            }
            catch (Exception ex)
            {
                MessageBox.Show(Loc.F("打印失败：{0}", ex.Message), Loc.T("打印"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        internal static DrawingVisual Build(RoomData room, double pw, double ph)
        {
            var dv = new DrawingVisual();
            double cols = Math.Max(1, room.Cols), rows = Math.Max(1, room.Rows);
            double roomW = cols * CELL, roomH = rows * CELL;

            // 统计
            int total = 0, used = 0;
            var blocks = room.Blocks ?? new List<BlockInstance>();
            foreach (var b in blocks)
            {
                if (b == null) continue;
                foreach (var slot in b.Layout())
                {
                    if (slot.Cell == null) continue;
                    total++;
                    if (!string.IsNullOrEmpty((slot.Cell.Name ?? "").Trim())) used++;
                }
            }

            using (DrawingContext dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, pw, ph));

                double margin = 30;
                double headH = 60, footH = 26;
                double availW = Math.Max(120, pw - margin * 2);
                double availH = Math.Max(120, ph - margin * 2 - headH - footH);
                double scale = Math.Min(availW / roomW, availH / roomH);
                if (scale > 1.7) scale = 1.7;

                // ---- 页眉 ----
                var title = Text(room.Name ?? Loc.T("未命名房间"), 21, true, Ink);
                dc.DrawText(title, new Point(margin, margin - 4));

                string stat = string.Format(CultureInfo.InvariantCulture,
                    Loc.T("{0} 列 × {1} 行    工位 {2} 个    已用 {3}    空闲 {4}"),
                    (int)cols, (int)rows, total, used, Math.Max(0, total - used));
                dc.DrawText(Text(stat, 10.5, false, InkSoft), new Point(margin, margin + 25));

                var stamp = Text(DateTime.Now.ToString("yyyy-MM-dd HH:mm"), 10, false, InkFaint);
                dc.DrawText(stamp, new Point(pw - margin - stamp.Width, margin - 2));

                var kind = Text(Loc.T("工位布局图"), 10.5, false, InkFaint);
                dc.DrawText(kind, new Point(pw - margin - kind.Width, margin + 17));

                dc.DrawLine(new Pen(Rule, 1.2), new Point(margin, margin + 44),
                    new Point(pw - margin, margin + 44));

                // ---- 地板 ----
                double x0 = margin + Math.Max(0, (availW - roomW * scale) / 2);
                double y0 = margin + headH + Math.Max(0, (availH - roomH * scale) / 2);

                dc.PushTransform(new TranslateTransform(x0, y0));
                dc.PushTransform(new ScaleTransform(scale, scale));

                double hair = 1.0 / scale;   // 线宽按缩放折算，纸上始终约 1pt
                dc.DrawRectangle(Brushes.White, new Pen(BlockEdge, 1.6 * hair),
                    new Rect(0, 0, roomW, roomH));

                // 格线：外框描粗，格子线细
                for (int c = 1; c < cols; c++)
                {
                    double x = c * CELL;
                    dc.DrawLine(new Pen(GridMajor, 1.0 * hair), new Point(x, 0), new Point(x, roomH));
                }
                for (int r = 1; r < rows; r++)
                {
                    double y = r * CELL;
                    dc.DrawLine(new Pen(GridMajor, 1.0 * hair), new Point(0, y), new Point(roomW, y));
                }

                // 工位块
                foreach (var b in blocks)
                {
                    if (b == null) continue;
                    int bc = Math.Max(1, b.Cols), br = Math.Max(1, b.Rows);
                    var rect = new Rect(b.Col * SNAP, b.Row * SNAP, bc * CELL, br * CELL);
                    dc.DrawRoundedRectangle(BlockFill, new Pen(BlockEdge, 1.6 * hair), rect, 5, 5);

                    foreach (var slot in b.Layout())
                    {
                        if (slot.Cell == null) continue;
                        int sc = Math.Max(0, slot.Col), sr = Math.Max(0, slot.Row);
                        double sw = Math.Max(1, slot.ColSpan) * CELL, sh = Math.Max(1, slot.RowSpan) * CELL;
                        var cr = new Rect(rect.X + sc * CELL + 2.5, rect.Y + sr * CELL + 2.5,
                            sw - 5, sh - 5);
                        DrawCell(dc, slot.Cell, cr, hair, slot.IsLeader);
                    }
                }

                // 空房间也要说清楚
                if (total == 0)
                {
                    var empty = Text(Loc.T("这块地板上还没有放工位块"), 12, false, InkFaint);
                    dc.DrawText(empty, new Point((roomW - empty.Width) / 2, roomH / 2 - empty.Height / 2));
                }

                dc.Pop();
                dc.Pop();

                // ---- 页脚 ----
                double fy = ph - margin - 14;
                var foot = Text(Loc.F("实心圆点 = 该工位有固态硬盘   ·   由 {0} 按房间布局生成", AppInfo.EnName), 9, false, InkFaint);
                dc.DrawText(foot, new Point(margin, fy));
                if (scale < 0.999)
                {
                    var sc = Text(Loc.F("缩放 {0}%", (int)Math.Round(scale * 100)), 9, false, InkFaint);
                    dc.DrawText(sc, new Point(pw - margin - sc.Width, fy));
                }
            }
            return dv;
        }

        private static void DrawCell(DrawingContext dc, WorkCellData cell, Rect r, double hair, bool leader)
        {
            var pen = new Pen(leader ? Ink : CellLine, (leader ? 2.0 : 1.0) * hair);
            dc.DrawRectangle(Brushes.White, pen, r);

            double pad = 4;
            double inner = Math.Max(12, r.Width - pad * 2);

            // 网口号（顶部）
            string port = (cell.Port ?? "").Trim();
            if (cell.Dual) port = (port.Length > 0 ? port + " " : "") + Loc.T("双线");
            if (port.Length > 0)
            {
                var ft = Fit(port, 8, false, InkSoft, inner);
                dc.DrawText(ft, new Point(r.X + (r.Width - Math.Min(ft.Width, inner)) / 2, r.Y + 3));
            }

            // 中间：人名 + 主机型号
            var mid = new List<FormattedText>();
            string name = (cell.Name ?? "").Trim();
            string comp = ComputerUtil.Display(cell.Computer);
            if (name.Length > 0) mid.Add(Fit(name, leader ? 17 : 15, true, Ink, inner));
            if (comp.Length > 0) mid.Add(Fit(comp, 11, false, Ink, inner));

            if (mid.Count > 0)
            {
                double totalH = 0;
                foreach (var f in mid) totalH += f.Height;
                double y = r.Y + (r.Height - totalH) / 2;
                foreach (var f in mid)
                {
                    double w = Math.Min(f.Width, inner);
                    dc.DrawText(f, new Point(r.X + (r.Width - w) / 2, y));
                    y += f.Height;
                }
            }

            // 显示器（底部）
            string mon = (cell.Monitor ?? "").Trim();
            if (mon.Length > 0)
            {
                var ft = Fit(mon, 8, false, InkFaint, inner);
                dc.DrawText(ft, new Point(r.X + (r.Width - Math.Min(ft.Width, inner)) / 2,
                    r.Y + r.Height - ft.Height - 2));
            }

            // 固态：屏幕上是红点，纸面上是实心黑点
            if (HasSolid(cell))
                dc.DrawEllipse(Ink, null, new Point(r.X + r.Width - 7.5, r.Y + r.Height - 7.5), 2.6, 2.6);
        }

        private static bool HasSolid(WorkCellData cell)
        {
            if (cell == null || cell.Machines == null) return false;
            foreach (var m in cell.Machines)
                if (m != null && m.IsSolid) return true;
            return false;
        }

        private static FormattedText Text(string s, double size, bool bold, Brush brush)
        {
            var tf = new Typeface(new FontFamily("Microsoft YaHei, Segoe UI"),
                FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
            return new FormattedText(s ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                tf, size, brush, 1.0);
        }

        private static FormattedText Fit(string s, double size, bool bold, Brush brush, double maxW)
        {
            var ft = Text(s, size, bold, brush);
            if (maxW > 10)
            {
                ft.MaxTextWidth = maxW;
                ft.MaxLineCount = 1;
                ft.Trimming = TextTrimming.CharacterEllipsis;
            }
            return ft;
        }
    }
}
