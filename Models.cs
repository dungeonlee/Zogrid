using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace WorkstationStudio
{
    // 版本号唯一出处：改这里即可，窗口标题与 exe 属性同步（AssemblyInfo.cs 里的版本号也要一起改）
    internal static class AppInfo
    {
        // 品牌名跟着界面语言走：中文叫「卓格」，其他语言直接用 Zogrid
        public static string Name { get { return Loc.T("卓格"); } }
        public const string EnName = "Zogrid";
        public const string Version = "1.3";
        public static string FullName { get { return Name + " " + Version; } }
        public static string Tagline { get { return Loc.T("工位 · 机房 · 座位布局管理"); } }
        public const string Author = "Michael";
        public const string CopyrightYear = "2026";
    }

    // 一台机器（表格里一个人可能有内网/外网两条记录）
    [DataContract]
    public class MachineInfo
    {
        [DataMember] public string Model { get; set; }
        [DataMember] public string EmployeeId { get; set; }
        [DataMember] public string Ip { get; set; }
        [DataMember] public string Mac { get; set; }
        [DataMember] public string NetType { get; set; }
        // 网线口号（表格里“网线口号”那一列，可以带空格，如“3F-12 A”）
        [DataMember] public string Port { get; set; }
        // 显示器型号（表格里“显示器型号”那一列）
        [DataMember] public string Monitor { get; set; }
        // 旧版模板里叫“财务编码”的列，保留只为兼容老存档，不再显示
        [DataMember] public string Finance { get; set; }
        [DataMember] public string QrCode { get; set; }
        [DataMember] public string Note { get; set; }
        // 固态标记：SolidSet=false 时按备注里有没有“固态”自动判断
        [DataMember] public bool Solid { get; set; }
        [DataMember] public bool SolidSet { get; set; }

        public MachineInfo()
        {
            Model = ""; EmployeeId = ""; Ip = ""; Mac = ""; NetType = "";
            Port = ""; Finance = ""; QrCode = ""; Note = ""; Monitor = "";
            Solid = false; SolidSet = false;
        }

        public bool IsSolid
        {
            get
            {
                if (SolidSet) return Solid;
                return (Note ?? "").IndexOf("固态", StringComparison.Ordinal) >= 0;
            }
        }

        public void SetSolid(bool on)
        {
            SolidSet = true;
            Solid = on;
        }
    }

    // 工位单元
    [DataContract]
    public class WorkCellData
    {
        [DataMember] public string Port { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public string Computer { get; set; }
        [DataMember] public string Monitor { get; set; }
        [DataMember] public string EmployeeId { get; set; }
        [DataMember] public bool Dual { get; set; }
        [DataMember] public bool IsLeader { get; set; }
        [DataMember] public List<MachineInfo> Machines { get; set; }

        public WorkCellData()
        {
            Port = ""; Name = ""; Computer = ""; Monitor = "";
            EmployeeId = ""; Dual = false; IsLeader = false;
            Machines = new List<MachineInfo>();
        }

        public bool IsEmpty
        {
            get
            {
                return string.IsNullOrEmpty(Port) && string.IsNullOrEmpty(Name)
                    && string.IsNullOrEmpty(Computer) && string.IsNullOrEmpty(Monitor);
            }
        }

        // 交换两个格子的“内容”，领导位身份跟着格子走，不跟着人走
        // 网线口号（含单/双网）焊在墙上，不跟着人走；主机型号/显示器/工号/隐藏信息跟着走
        public void SwapWith(WorkCellData o)
        {
            if (o == null || ReferenceEquals(o, this)) return;
            string n = Name, c = Computer, mo = Monitor, e = EmployeeId;
            var ms = Machines;
            Name = o.Name; Computer = o.Computer; Monitor = o.Monitor;
            EmployeeId = o.EmployeeId; Machines = o.Machines;
            o.Name = n; o.Computer = c; o.Monitor = mo;
            o.EmployeeId = e; o.Machines = ms;
            if (Machines == null) Machines = new List<MachineInfo>();
            if (o.Machines == null) o.Machines = new List<MachineInfo>();
        }
    }

    // 领导位：方向 + 位置
    //  LeaderVertical = true  领导竖占两格（上下）
    //  LeaderPos: 0=单独(只有领导，占两格) 1=左/上 2=右
    public class CellSlot
    {
        public WorkCellData Cell;
        public int Col;
        public int Row;
        public int ColSpan = 1;
        public int RowSpan = 1;
        public bool IsLeader;
    }

    // 工位库模板（一种工位块形状）
    [DataContract]
    public class BlockTemplate
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Label { get; set; }          // 旧档兼容，已不再使用（标号改为自动）
        [DataMember] public string Name { get; set; }
        [DataMember] public int Rows { get; set; }
        [DataMember] public int Cols { get; set; }
        [DataMember] public string Color { get; set; }
        [DataMember] public bool HasLeader { get; set; }
        [DataMember] public bool LeaderVertical { get; set; }
        [DataMember] public int LeaderPos { get; set; }
        [DataMember] public int LeaderCols { get; set; }        // 旧档兼容

        public BlockTemplate()
        {
            Id = Guid.NewGuid().ToString("N");
            Label = ""; Name = Loc.T("工位块"); Rows = 2; Cols = 2; Color = "#6E93B8";
            HasLeader = false; LeaderVertical = true; LeaderPos = 0; LeaderCols = 2;
        }

        public int CellCount
        {
            get
            {
                int r = Math.Max(1, Rows), c = Math.Max(1, Cols);
                return HasLeader ? Math.Max(1, r * c - 1) : r * c;
            }
        }

        public BlockTemplate Clone()
        {
            return new BlockTemplate
            {
                Id = Id, Label = Label, Name = Name, Rows = Rows, Cols = Cols, Color = Color,
                HasLeader = HasLeader, LeaderVertical = LeaderVertical,
                LeaderPos = LeaderPos, LeaderCols = LeaderCols
            };
        }
    }

    // 放到房间里的工位块实例
    [DataContract]
    public class BlockInstance
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string TemplateId { get; set; }
        [DataMember] public string Label { get; set; }
        [DataMember] public string Name { get; set; }
        // Col/Row 单位为“小格”（= 半格），房间一个格子 = 2 小格
        [DataMember] public int Col { get; set; }
        [DataMember] public int Row { get; set; }
        [DataMember] public string Color { get; set; }
        [DataMember] public bool Locked { get; set; }
        [DataMember] public bool HasLeader { get; set; }
        [DataMember] public bool LeaderVertical { get; set; }
        [DataMember] public int LeaderPos { get; set; }
        [DataMember] public int Rows { get; set; }
        [DataMember] public int Cols { get; set; }
        [DataMember] public List<WorkCellData> Cells { get; set; }

        public BlockInstance()
        {
            Id = Guid.NewGuid().ToString("N");
            TemplateId = ""; Label = ""; Name = "";
            Col = 0; Row = 0; Color = "#6E93B8";
            Locked = false; HasLeader = false; LeaderVertical = true; LeaderPos = 0;
            Rows = 2; Cols = 2;
            Cells = new List<WorkCellData>();
        }

        public int CellCount
        {
            get
            {
                int r = Math.Max(1, Rows), c = Math.Max(1, Cols);
                return HasLeader ? Math.Max(1, r * c - 1) : r * c;
            }
        }

        public void RebuildCells()
        {
            if (Cells == null) Cells = new List<WorkCellData>();
            int need = CellCount;
            while (Cells.Count < need) Cells.Add(new WorkCellData());
            while (Cells.Count > need) Cells.RemoveAt(Cells.Count - 1);
            for (int i = 0; i < Cells.Count; i++) Cells[i].IsLeader = (HasLeader && i == 0);
        }

        // 计算每个格子落在网格的哪一格（领导位跨格）
        public List<CellSlot> Layout()
        {
            var list = new List<CellSlot>();
            if (Cells == null) Cells = new List<WorkCellData>();
            int rows = Math.Max(1, Rows), cols = Math.Max(1, Cols);
            int idx = 0;

            if (HasLeader && Cells.Count > 0)
            {
                idx = 1;
                if (LeaderVertical)
                {
                    int lc = (LeaderPos == 2) ? cols - 1 : 0;
                    list.Add(new CellSlot { Cell = Cells[0], Col = lc, Row = 0, ColSpan = 1, RowSpan = rows, IsLeader = true });
                    for (int r = 0; r < rows; r++)
                        for (int c = 0; c < cols; c++)
                        {
                            if (c == lc) continue;
                            if (idx >= Cells.Count) continue;
                            list.Add(new CellSlot { Cell = Cells[idx++], Col = c, Row = r });
                        }
                }
                else
                {
                    list.Add(new CellSlot { Cell = Cells[0], Col = 0, Row = 0, ColSpan = cols, RowSpan = 1, IsLeader = true });
                    for (int r = 1; r < rows; r++)
                        for (int c = 0; c < cols; c++)
                        {
                            if (idx >= Cells.Count) continue;
                            list.Add(new CellSlot { Cell = Cells[idx++], Col = c, Row = r });
                        }
                }
            }
            else
            {
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        if (idx >= Cells.Count) continue;
                        list.Add(new CellSlot { Cell = Cells[idx++], Col = c, Row = r });
                    }
            }
            return list;
        }
    }

    // 房间
    [DataContract]
    public class RoomData
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public int Cols { get; set; }
        [DataMember] public int Rows { get; set; }
        [DataMember] public string FloorColor { get; set; }
        [DataMember] public string GridColor { get; set; }
        [DataMember] public string ThemeKey { get; set; }
        [DataMember] public bool Locked { get; set; }
        [DataMember] public List<BlockInstance> Blocks { get; set; }

        public RoomData()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = Loc.T("房间") + "1"; Cols = 16; Rows = 12;
            FloorColor = "#1E232B";
            GridColor = "#2C333F";
            ThemeKey = "graphite";
            Blocks = new List<BlockInstance>();
        }
    }

    // 表格匹配：先按姓名精确找，再按“包含 / 备注里出现 / 差一个字”兜底
    // 只负责找机器信息，绝不改格子里的人名
    public static class SyncMatcher
    {
        public static List<MachineInfo> Find(Dictionary<string, List<MachineInfo>> table, string cellName)
        {
            bool exact;
            return Find(table, cellName, out exact);
        }

        public static List<MachineInfo> Find(Dictionary<string, List<MachineInfo>> table, string cellName, out bool exact)
        {
            exact = false;
            if (table == null || table.Count == 0) return null;
            string nm = (cellName ?? "").Trim();
            if (nm.Length == 0) return null;

            List<MachineInfo> hit;
            if (table.TryGetValue(nm, out hit) && hit != null && hit.Count > 0)
            {
                exact = true;
                return hit;
            }

            string key = Norm(nm);
            if (key.Length < 2) return null;   // 单字太重名，宁可不猜

            List<MachineInfo> best = null;
            int bestScore = 0;
            foreach (var kv in table)
            {
                if (kv.Value == null || kv.Value.Count == 0) continue;
                string tn = Norm(kv.Key);
                int score = 0;

                // 只认“名字里有 / 备注里写着”，差一个字（潘雨丝 vs 潘语丝）一律不认，
                // 让用户自己发现写错了；表里本来就有差一个字的人名（丁浩 / 丁淏）
                if (tn.Length > 0)
                {
                    if (tn == key) score = 100;
                    else if (tn.Length >= 2 && (tn.IndexOf(key, StringComparison.Ordinal) >= 0 ||
                                                key.IndexOf(tn, StringComparison.Ordinal) >= 0)) score = 90;
                }

                if (score < 85)
                {
                    foreach (var m in kv.Value)
                    {
                        string note = Norm(m.Note);
                        string qr = Norm(m.QrCode);
                        if (note.Length > 0 && note.IndexOf(key, StringComparison.Ordinal) >= 0) { score = 85; break; }
                        if (qr.Length > 0 && qr.IndexOf(key, StringComparison.Ordinal) >= 0) { score = 85; break; }
                    }
                }

                if (score > bestScore) { bestScore = score; best = kv.Value; }
            }

            // 模糊结果只认“像一个人”的：机器数 ≤ 4（超过 4 台基本是会议室/公共设备那一堆）
            if (best != null && best.Count > 4) return null;

            return bestScore >= 85 ? best : null;
        }

        // 只留字母数字和汉字，去掉空格、括号、连字符之类，方便模糊比对
        public static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char ch in s)
            {
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

    }

    // 整份存档
    [DataContract]
    public class AppData
    {
        [DataMember] public int Version { get; set; }
        [DataMember] public List<RoomData> Rooms { get; set; }
        [DataMember] public string ActiveRoomId { get; set; }
        [DataMember] public List<BlockTemplate> Library { get; set; }
        [DataMember] public List<ModelColorEntry> ModelColors { get; set; }

        public AppData()
        {
            Version = 2;
            Rooms = new List<RoomData>();
            Library = new List<BlockTemplate>();
            ModelColors = new List<ModelColorEntry>();
            ActiveRoomId = "";
        }
    }

    // 机型 -> 显示颜色（唯一入口：底部机器统计面板里选）
    [DataContract]
    public class ModelColorEntry
    {
        [DataMember] public string Model { get; set; }
        [DataMember] public string Color { get; set; }
        public ModelColorEntry() { Model = ""; Color = ""; }
    }

    // 主机型号文本解析：跟网页版一致
    //  - 括号及括号内容不算机型（(固态240) 之类）
    //  - + 、/ 、空格、换行 都是分隔符
    //  - 一个人名下最多认 4 个主机型号，多的不管
    public static class ComputerUtil
    {
        public const int MaxModels = 4;

        public class Seg
        {
            public string Text;
            public bool IsModel;
        }

        public static List<Seg> Parse(string text)
        {
            var segs = new List<Seg>();
            if (string.IsNullOrEmpty(text)) return segs;
            var re = new System.Text.RegularExpressions.Regex(@"[（(][^）)]*[）)]|[+/\s]+");
            int last = 0, models = 0;
            System.Text.RegularExpressions.Match m;
            while ((m = re.Match(text, last)).Success)
            {
                if (m.Index > last && models < MaxModels)
                {
                    segs.Add(new Seg { Text = text.Substring(last, m.Index - last), IsModel = true });
                    models++;
                }
                string tok = m.Value;
                bool paren = tok.Length > 0 && (tok[0] == '（' || tok[0] == '(');
                if (paren && models < MaxModels) segs.Add(new Seg { Text = tok, IsModel = false });
                last = m.Index + m.Length;
            }
            if (last < text.Length && models < MaxModels)
                segs.Add(new Seg { Text = text.Substring(last), IsModel = true });
            return segs;
        }

        // 只取机型列表
        public static List<string> Models(string text)
        {
            var r = new List<string>();
            foreach (var s in Parse(text))
            {
                if (!s.IsModel) continue;
                string t = (s.Text ?? "").Trim();
                if (t.Length > 0) r.Add(t);
            }
            return r;
        }

        // 统一显示成“3020 5080”，中间一个空格，最多 4 个
        public static string Display(string text)
        {
            var parts = new List<string>();
            foreach (var s in Parse(text))
            {
                string t = (s.Text ?? "").Trim();
                if (t.Length > 0) parts.Add(t);
            }
            return string.Join(" ", parts.ToArray());
        }

        public static bool ContainsModel(string computer, string q)
        {
            foreach (var m in Models(computer))
                if (m.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // 精确到某个机型
        public static bool HasModel(string computer, string model)
        {
            foreach (var m in Models(computer))
                if (string.Equals(m, model, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
