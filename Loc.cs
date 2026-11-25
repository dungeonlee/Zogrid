using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Markup;

namespace WorkstationStudio
{
    // ===== 统一的本地化入口 =====
    // key 就是简体中文原文，查不到就回退原文，永远不会显示空白或 key 名。
    public static partial class Loc
    {
        public const string ZhHans = "zh-Hans";
        public const string ZhHant = "zh-Hant";
        public const string Ja = "ja";
        public const string Ko = "ko";
        public const string En = "en";
        public const string Fr = "fr";
        public const string De = "de";

        // 语言下拉里显示的名字（各语种自己称呼自己）
        public static readonly string[][] Languages = new string[][]
        {
            new string[] { ZhHans, "简体中文" },
            new string[] { ZhHant, "繁體中文" },
            new string[] { Ja,     "日本語" },
            new string[] { Ko,     "한국어" },
            new string[] { En,     "English" },
            new string[] { Fr,     "Français" },
            new string[] { De,     "Deutsch" }
        };

        private static string _lang = ZhHans;

        public static string Lang
        {
            get { return _lang; }
            set { _lang = string.IsNullOrEmpty(value) ? ZhHans : value; }
        }

        // 中日韩语言下表格列名用中文，其余用英文
        public static bool IsCjk { get { return _lang == ZhHans || _lang == ZhHant || _lang == Ja || _lang == Ko; } }

        // 中文界面（简/繁）：表格列名用中文；其他语言统一用英文列名
        public static bool IsZhLike { get { return _lang == ZhHans || _lang == ZhHant; } }

        public static string T(string zh)
        {
            if (string.IsNullOrEmpty(zh)) return zh == null ? "" : zh;
            if (_lang == ZhHans) return zh;
            Dictionary<string, string> d = CurrentDict();
            string v;
            if (d != null && d.TryGetValue(zh, out v) && v != null) return v;
            return zh;
        }

        // 带占位符的模板，如 F("已保存 {0} 个房间", n)
        public static string F(string zh, params object[] args)
        {
            string tpl = T(zh);
            try { return string.Format(tpl, args); }
            catch { return tpl; }
        }

        private static Dictionary<string, string> CurrentDict()
        {
            switch (_lang)
            {
                case ZhHant: return ZhHantDict;
                case Ja: return JaDict;
                case Ko: return KoDict;
                case En: return EnDict;
                case Fr: return FrDict;
                case De: return DeDict;
            }
            return null;
        }

        // 内外网标签：数据里存的是「内」「外」，各语言换成自己的说法
        public static string NetLabel(string netType)
        {
            if (string.IsNullOrEmpty(netType)) return T("主机");
            if (netType.Contains("内")) return T("内网");
            if (netType.Contains("外")) return T("外网");
            return F("{0}网络", netType);
        }

        // ---------- 语言设置读写：和存档同目录 ----------
        public static void Load()
        {
            Lang = DetectFromSystem();   // 第一次运行按系统语言猜，之后听用户的选择
            try
            {
                string f = Paths.SettingsFile;
                if (!File.Exists(f)) return;
                string s = File.ReadAllText(f).Trim();
                if (s.Length > 0 && s.Length < 20) Lang = s;
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                Paths.EnsureDataDir();
                File.WriteAllText(Paths.SettingsFile, _lang, new System.Text.UTF8Encoding(false));
            }
            catch { }
        }

        // 语言在系统语言里找不到时，按系统文化猜一个；
        // 自带七种语言以外的语言（西语、俄语、阿语等）一律回退英文界面
        public static string DetectFromSystem()
        {
            try
            {
                string n = System.Globalization.CultureInfo.CurrentUICulture.Name.ToLowerInvariant();
                if (n.StartsWith("zh-hant") || n.StartsWith("zh-tw") || n.StartsWith("zh-hk") || n.StartsWith("zh-mo"))
                    return ZhHant;
                if (n.StartsWith("ja")) return Ja;
                if (n.StartsWith("ko")) return Ko;
                if (n.StartsWith("fr")) return Fr;
                if (n.StartsWith("de")) return De;
                if (n.StartsWith("zh")) return ZhHans;
                if (n.StartsWith("en")) return En;
            }
            catch { }
            return En;
        }

        // 当前语言的显示名
        public static string CurrentDisplayName()
        {
            for (int i = 0; i < Languages.Length; i++)
                if (Languages[i][0] == _lang) return Languages[i][1];
            return Languages[0][1];
        }
    }

    // XAML 里用：Content="{l:T Key='房间属性 F2'}"
    [MarkupExtensionReturnType(typeof(string))]
    public class TExtension : MarkupExtension
    {
        public TExtension() { }
        public TExtension(string key) { this.Key = key; }

        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return Loc.T(this.Key);
        }
    }

    // ===== 路径约定：数据统一放在 文档\Zogrid =====
    internal static class Paths
    {
        public static string DataDir
        {
            get
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(docs))
                    docs = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(docs, "Zogrid");
            }
        }

        public static string SaveFile { get { return Path.Combine(DataDir, "zogrid.json"); } }
        public static string SettingsFile { get { return Path.Combine(DataDir, "settings.txt"); } }

        // 老版本存档：exe 同目录的 工位存档.json
        public static string LegacySaveFile
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "工位存档.json"); }
        }

        public static void EnsureDataDir()
        {
            if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
        }
    }
}
