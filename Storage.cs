using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace WorkstationStudio
{
    public static class Storage
    {
        // ---------- 存档 / 读档 ----------
        public static void Save(string path, AppData data)
        {
            var ser = new DataContractJsonSerializer(typeof(AppData));
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                ser.WriteObject(fs, data);
            }
        }

        public static AppData Load(string path)
        {
            var ser = new DataContractJsonSerializer(typeof(AppData));
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return (AppData)ser.ReadObject(fs);
            }
        }

        // 旧存档升级：v1 的领导位是“固定竖在左列”，位置单位是“大格”
        // v2 起领导位有方向/位置，位置单位改为“小格”（半格）
        public static void Migrate(AppData d)
        {
            if (d == null) return;
            if (d.ModelColors == null) d.ModelColors = new List<ModelColorEntry>();
            if (d.Library == null) d.Library = new List<BlockTemplate>();

            // 老存档没有配色方案：按地板色猜一套，猜不到就用石墨夜
            if (d.Rooms != null)
                foreach (var r in d.Rooms)
                {
                    if (!string.IsNullOrEmpty(r.ThemeKey)) continue;
                    if (r.Blocks == null) r.Blocks = new List<BlockInstance>();
                    var t = ThemePreset.MatchByFloor(r.FloorColor);
                    r.ThemeKey = t != null ? t.Key : "graphite";
                }

            if (d.Version >= 2) return;

            if (d.Library != null)
                foreach (var t in d.Library)
                {
                    if (t.HasLeader)
                    {
                        t.LeaderVertical = true;
                        t.LeaderPos = 1;
                        t.Rows = 2;
                        t.Cols = Math.Max(2, t.LeaderCols + 1);
                    }
                }

            if (d.Rooms != null)
                foreach (var r in d.Rooms)
                {
                    if (r.Blocks == null) continue;
                    foreach (var b in r.Blocks)
                    {
                        if (b.HasLeader)
                        {
                            b.LeaderVertical = true;
                            b.LeaderPos = 1;
                            if (b.Rows <= 0) b.Rows = 2;
                            if (b.Cols <= 1 && b.Cells != null && b.Cells.Count > 2)
                                b.Cols = b.Cells.Count / b.Rows + 1;
                        }
                        if (b.Rows <= 0) b.Rows = 2;
                        if (b.Cols <= 0) b.Cols = 2;
                        b.Col *= 2;
                        b.Row *= 2;
                        b.RebuildCells();
                    }
                }
            d.Version = 2;
        }

        // ---------- 读 xlsx（标准库，无第三方依赖） ----------
        // 表头自动识别：姓名 / id(工号) / MAC / IP / 内外 / 网线口号 / 显示器型号 / 二维码 / 备注
        // 没有表头才退回老版固定列位：0人名 1工号 2MAC 3IP 4内外 5网线口号(老版是财务编码) 6二维码 7备注
        private class ColMap
        {
            public int Name = 0, EmployeeId = 1, Mac = 2, Ip = 3, NetType = 4, Port = 5, QrCode = 6, Note = 7, Monitor = -1;

            public static ColMap Legacy { get { return new ColMap(); } }

            public static ColMap FromHeader(Dictionary<int, string> row)
            {
                // 中英文表头都认：中文界面生成中文表头，其他语言生成英文表头，两边都要能导回来
                bool hasName = false;
                foreach (var kv in row)
                {
                    string t = (kv.Value ?? "").Trim();
                    string tl = t.ToLowerInvariant();
                    if (t == "姓名" || t == "名字" || t == "人员" || t == "员工姓名" || t == "员工"
                        || tl == "name" || tl == "full name" || tl == "employee name"
                        || t == "氏名" || t == "이름")
                    { hasName = true; break; }
                }
                if (!hasName) return null;

                var m = new ColMap { Name = -1, EmployeeId = -1, Mac = -1, Ip = -1, NetType = -1, Port = -1, Monitor = -1, QrCode = -1, Note = -1 };
                foreach (var kv in row)
                {
                    string t = (kv.Value ?? "").Trim().ToLowerInvariant().Replace(" ", "");
                    if (t.Length == 0) continue;
                    int c = kv.Key;

                    if (t == "姓名" || t == "名字" || t == "人员" || t == "员工姓名" || t == "员工"
                        || t == "name" || t == "fullname" || t == "employeename" || t == "氏名" || t == "이름")
                    { if (m.Name < 0) m.Name = c; }
                    else if (t.Contains("mac") || t.Contains("物理地址") || t.Contains("physical")) { if (m.Mac < 0) m.Mac = c; }
                    else if (t == "ip" || t.Contains("ip地址") || t.Contains("ipaddress")) { if (m.Ip < 0) m.Ip = c; }
                    else if (t.Contains("二维码") || t.Contains("扫码") || t.Contains("条码") || t.Contains("qr")) { if (m.QrCode < 0) m.QrCode = c; }
                    // 网线口号（含“网口号/端口号”叫法）；老模板写的“财务编码”也认，直接当网线口号用
                    else if (t.Contains("网线") || t.Contains("网口") || t.Contains("端口") || t.Contains("财务")
                             || t.Contains("port") || t.Contains("outlet") || t.Contains("jack")) { if (m.Port < 0) m.Port = c; }
                    // 显示器要放在“备注/型号”之前判断，否则“显示器型号”会被当成备注列
                    else if (t.Contains("显示器") || t.Contains("屏幕") || t.Contains("螢幕")
                             || t.Contains("monitor") || t.Contains("display")) { if (m.Monitor < 0) m.Monitor = c; }
                    else if (t.Contains("备注") || t.Contains("说明") || t.Contains("机器")
                             || t.Contains("型号") || t.Contains("配置") || t.Contains("描述")
                             || t.Contains("note") || t.Contains("remark") || t.Contains("comment")
                             || t.Contains("model") || t.Contains("config") || t.Contains("desc")) { if (m.Note < 0) m.Note = c; }
                    else if (t.Contains("内外") || t.Contains("网络") || t.Contains("网别") || t.Contains("内网")
                             || t.Contains("lan") || t.Contains("wan") || t.Contains("network")) { if (m.NetType < 0) m.NetType = c; }
                    else if (t == "id" || t.Contains("工号") || t.Contains("员工号") || t.Contains("编号")
                             || t.Contains("employeeid") || t.Contains("staffid") || t.Contains("empid")) { if (m.EmployeeId < 0) m.EmployeeId = c; }
                }
                return m;
            }
        }

        // ---------- 生成表格：空白导入模板 / 导出人员表 ----------
        // 手写一个最小 xlsx（inlineStr，不依赖 sharedStrings），表头文字与 ReadMachines 的列名识别规则对齐。
        // “网线口号”允许带空格（如“3F-12 A”），全程按文本读写，不会把中间空格吃掉。
        // 中文界面用中文表头；其他语言统一用英文表头（不是直译成日/韩/法/德，
        // 免得对方看不懂，也方便和别的系统对接）。导入时两种表头都认。
        private static readonly string[] ZhHeaders =
            { "姓名", "工号", "内外网", "MAC地址", "IP地址", "备注（型号/配置）", "网线口号", "显示器型号" };
        private static readonly string[] EnHeaders =
            { "Name", "Employee ID", "LAN/WAN", "MAC", "IP", "Note (Model/Config)", "Port Label", "Monitor Model" };

        public static string[] PeopleHeaders
        {
            get { return Loc.IsZhLike ? ZhHeaders : EnHeaders; }
        }

        private static readonly int[] PeopleWidths = { 12, 14, 11, 21, 17, 30, 20, 20 };

        public static void WriteTemplate(string path)
        {
            WriteSheet(path, Loc.T("工位数据"), PeopleHeaders, PeopleWidths, null);
        }

        // 导出人员表：列与导入模板完全一致，一人多机就多行，改完可以直接再导入同步
        public static void WritePeople(string path, List<string[]> rows)
        {
            WriteSheet(path, Loc.T("人员表"), PeopleHeaders, PeopleWidths, rows);
        }

        public static void WriteSheet(string path, string sheetName, string[] headers, int[] widths, List<string[]> rows)
        {
            string sheetXml = BuildSheet(headers, widths, rows);

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(path)) File.Delete(path);

            using (Package pkg = Package.Open(path, FileMode.Create))
            {
                PackagePart wb = pkg.CreatePart(new Uri("/xl/workbook.xml", UriKind.Relative),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml", CompressionOption.Normal);
                PackagePart st = pkg.CreatePart(new Uri("/xl/styles.xml", UriKind.Relative),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml", CompressionOption.Normal);
                PackagePart sh = pkg.CreatePart(new Uri("/xl/worksheets/sheet1.xml", UriKind.Relative),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml", CompressionOption.Normal);

                pkg.CreateRelationship(new Uri("xl/workbook.xml", UriKind.Relative), TargetMode.Internal,
                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
                wb.CreateRelationship(new Uri("/xl/styles.xml", UriKind.Relative), TargetMode.Internal,
                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles");
                PackageRelationship relSheet = wb.CreateRelationship(new Uri("/xl/worksheets/sheet1.xml", UriKind.Relative),
                    TargetMode.Internal, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet");

                string workbookXml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"" +
                    " xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                    "<sheets><sheet name=\"" + XmlEsc(string.IsNullOrEmpty(sheetName) ? Loc.T("工位数据") : sheetName) +
                    "\" sheetId=\"1\" r:id=\"" + relSheet.Id + "\"/></sheets>" +
                    "</workbook>";

                WritePart(wb, workbookXml);
                WritePart(st, TemplateStyles);
                WritePart(sh, sheetXml);
            }
        }

        // 中日韩界面用等线，其他语言用 Calibri（英文表格里等线不好看，而且很多机器没这字体）
        private static string SheetFont { get { return Loc.IsCjk ? "等线" : "Calibri"; } }

        private static readonly string TemplateStyles =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<fonts count=\"2\">" +
            "<font><sz val=\"11\"/><color theme=\"1\"/><name val=\"" + SheetFont + "\"/><family val=\"2\"/></font>" +
            "<font><b/><sz val=\"11\"/><color rgb=\"FF1F2A37\"/><name val=\"" + SheetFont + "\"/><family val=\"2\"/></font>" +
            "</fonts>" +
            "<fills count=\"3\">" +
            "<fill><patternFill patternType=\"none\"/></fill>" +
            "<fill><patternFill patternType=\"gray125\"/></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE8EEF6\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
            "</fills>" +
            "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"2\">" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/>" +
            "</cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>";

        private static string BuildSheet(string[] headers, int[] widths, List<string[]> rows)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            // 冻结表头行
            sb.Append("<sheetViews><sheetView tabSelected=\"1\" workbookViewId=\"0\">");
            sb.Append("<pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/>");
            sb.Append("</sheetView></sheetViews>");
            sb.Append("<sheetFormatPr defaultRowHeight=\"16.5\"/>");
            sb.Append("<cols>");
            for (int i = 0; i < headers.Length; i++)
                sb.Append("<col min=\"").Append(i + 1).Append("\" max=\"").Append(i + 1)
                  .Append("\" width=\"").Append(widths[i]).Append("\" customWidth=\"1\"/>");
            sb.Append("</cols>");
            sb.Append("<sheetData><row r=\"1\" ht=\"26\" customHeight=\"1\">");
            for (int i = 0; i < headers.Length; i++)
                sb.Append("<c r=\"").Append(ColName(i)).Append("1\" t=\"inlineStr\" s=\"1\"><is><t>")
                  .Append(XmlEsc(headers[i])).Append("</t></is></c>");
            sb.Append("</row>");

            // 数据行：全部按文本写，MAC/IP/网线口号里的前导 0 与空格都保得住
            if (rows != null)
            {
                for (int r = 0; r < rows.Count; r++)
                {
                    string[] row = rows[r];
                    sb.Append("<row r=\"").Append(r + 2).Append("\">");
                    for (int c = 0; c < headers.Length; c++)
                    {
                        string v = (row != null && c < row.Length) ? (row[c] ?? "") : "";
                        if (v.Length == 0) continue;
                        sb.Append("<c r=\"").Append(ColName(c)).Append(r + 2)
                          .Append("\" t=\"inlineStr\" s=\"0\"><is><t xml:space=\"preserve\">")
                          .Append(XmlEsc(v)).Append("</t></is></c>");
                    }
                    sb.Append("</row>");
                }
            }

            sb.Append("</sheetData>");
            sb.Append("<autoFilter ref=\"A1:").Append(ColName(headers.Length - 1)).Append("1\"/>");
            sb.Append("</worksheet>");
            return sb.ToString();
        }

        private static void WritePart(PackagePart part, string xml)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(xml);
            using (Stream s = part.GetStream(FileMode.Create, FileAccess.Write))
                s.Write(bytes, 0, bytes.Length);
        }

        private static string XmlEsc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }

        private static string ColName(int idx)
        {
            string s = "";
            int n = idx + 1;
            while (n > 0) { int r = (n - 1) % 26; s = (char)('A' + r) + s; n = (n - 1) / 26; }
            return s;
        }

        public static Dictionary<string, List<MachineInfo>> ReadMachines(string path)
        {
            var result = new Dictionary<string, List<MachineInfo>>(StringComparer.OrdinalIgnoreCase);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

            using (Package pkg = Package.Open(path, FileMode.Open, FileAccess.Read))
            {
                List<string> sst = ReadSharedStrings(pkg, ns);
                PackagePart sheet = FindFirstSheet(pkg);
                if (sheet == null) return result;

                XDocument doc;
                using (Stream s = sheet.GetStream())
                    doc = XDocument.Load(s);

                // 整张表先读成 行 → (列号 → 文本)
                var rows = new List<Dictionary<int, string>>();
                foreach (XElement row in doc.Descendants(ns + "row"))
                {
                    var cells = new Dictionary<int, string>();
                    foreach (XElement c in row.Elements(ns + "c"))
                    {
                        int col = ColIndex((string)c.Attribute("r"));
                        string type = (string)c.Attribute("t");
                        string val = "";
                        if (type == "inlineStr")
                        {
                            var isEl = c.Element(ns + "is");
                            if (isEl != null)
                            {
                                var sb = new StringBuilder();
                                foreach (XElement t in isEl.Descendants(ns + "t")) sb.Append(t.Value);
                                val = sb.ToString();
                            }
                        }
                        else
                        {
                            XElement v = c.Element(ns + "v");
                            if (v != null)
                            {
                                val = v.Value;
                                if (type == "s")
                                {
                                    int idx;
                                    if (int.TryParse(val, out idx) && idx >= 0 && idx < sst.Count)
                                        val = sst[idx];
                                }
                            }
                        }
                        if (col >= 0) cells[col] = val;
                    }
                    rows.Add(cells);
                }

                // 前 5 行里找表头，按表头文字定位列
                ColMap map = null;
                int start = 0;
                for (int i = 0; i < rows.Count && i < 5; i++)
                {
                    var m = ColMap.FromHeader(rows[i]);
                    if (m != null) { map = m; start = i + 1; break; }
                }
                if (map == null) { map = ColMap.Legacy; start = 0; }
                if (map.Name < 0) map.Name = 0;

                for (int i = start; i < rows.Count; i++)
                {
                    var cells = rows[i];
                    string name = Get(cells, map.Name).Trim();
                    if (name.Length == 0) continue;
                    if (name == "姓名" || name == "名字" || name == "人员"
                        || name.Equals("name", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("full name", StringComparison.OrdinalIgnoreCase)) continue;

                    var m = new MachineInfo();
                    m.EmployeeId = Get(cells, map.EmployeeId).Trim();
                    m.Mac = Get(cells, map.Mac).Trim();
                    m.Ip = Get(cells, map.Ip).Trim();
                    m.NetType = Get(cells, map.NetType).Trim();
                    // 网线口号：只去首尾空白，中间的空格原样留着（“3F-12 A”不该被压成“3F-12A”）
                    m.Port = Get(cells, map.Port).Trim();
                    m.QrCode = Get(cells, map.QrCode).Trim();
                    m.Monitor = Get(cells, map.Monitor).Trim();
                    m.Note = Get(cells, map.Note).Trim();
                    m.Model = ParseModel(m.Note);

                    // 整行几乎全空就跳过；只填了网线口号 / 显示器的也算有效行
                    if (m.EmployeeId.Length == 0 && m.Mac.Length == 0 && m.Ip.Length == 0
                        && m.Note.Length == 0 && m.Port.Length == 0 && m.Monitor.Length == 0) continue;

                    if (!result.ContainsKey(name)) result[name] = new List<MachineInfo>();
                    result[name].Add(m);
                }
            }
            return result;
        }

        // 从备注里抠型号：
        //   "3020  内网+480固态" → 3020
        //   "外网3080 9H1CD73"    → 3080
        //   "5080(固态)"          → 5080
        //   "内锐捷机 H1QPA..."   → 锐捷机（没有机型号时取第一个词）
        public static string ParseModel(string note)
        {
            if (string.IsNullOrEmpty(note)) return "";
            string s = note.Trim();

            Match mt = Regex.Match(s, @"(?<!\d)([3-7]\d{3})(?!\d)");
            if (mt.Success) return mt.Groups[1].Value;

            mt = Regex.Match(s, @"(?<!\d)(\d{3,4})(?!\d)");
            if (mt.Success) return mt.Groups[1].Value;

            string t = s.TrimStart('内', '外').Trim();
            int sp = t.IndexOfAny(new[] { ' ', '\t', '，', ',', '；', ';' });
            if (sp > 0) t = t.Substring(0, sp).Trim();
            if (t.Length > 14) t = t.Substring(0, 14);
            return t;
        }

        private static string Get(Dictionary<int, string> cells, int i)
        {
            string v;
            return cells.TryGetValue(i, out v) ? v : "";
        }

        private static int ColIndex(string refStr)
        {
            if (string.IsNullOrEmpty(refStr)) return -1;
            int n = 0;
            foreach (char ch in refStr)
            {
                if (ch >= 'A' && ch <= 'Z') n = n * 26 + (ch - 'A' + 1);
                else if (ch >= 'a' && ch <= 'z') n = n * 26 + (ch - 'a' + 1);
                else break;
            }
            return n - 1;
        }

        private static List<string> ReadSharedStrings(Package pkg, XNamespace ns)
        {
            var list = new List<string>();
            foreach (PackagePart part in pkg.GetParts())
            {
                if (!part.Uri.ToString().EndsWith("sharedStrings.xml", StringComparison.OrdinalIgnoreCase)) continue;
                using (Stream s = part.GetStream())
                {
                    XDocument doc = XDocument.Load(s);
                    foreach (XElement si in doc.Descendants(ns + "si"))
                    {
                        var sb = new StringBuilder();
                        foreach (XElement t in si.Descendants(ns + "t")) sb.Append(t.Value);
                        list.Add(sb.ToString());
                    }
                }
                break;
            }
            return list;
        }

        private static PackagePart FindFirstSheet(Package pkg)
        {
            foreach (PackagePart part in pkg.GetParts())
            {
                string u = part.Uri.ToString();
                if (u.StartsWith("/xl/worksheets/") && u.EndsWith(".xml") &&
                    !u.Contains("_rels"))
                    return part;
            }
            return null;
        }
    }
}
