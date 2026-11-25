using System;
using System.IO;
using System.Windows;

namespace WorkstationStudio
{
    public partial class App : Application
    {
        private static string LogPath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log"); }
        }
        public static void Log(string msg)
        {
            // 正常使用不产生日志文件；排查问题时设置环境变量 WS_DEBUG=1 才会写
            try
            {
                if (Environment.GetEnvironmentVariable("WS_DEBUG") != "1") return;
                File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + "\r\n");
            }
            catch { }
        }
        protected override void OnStartup(StartupEventArgs e)
        {
            // 语言必须在任何界面创建之前定下来
            try { Loc.Load(); } catch { }
            Log("=== STARTUP === lang=" + Loc.Lang);
            this.DispatcherUnhandledException += (s, args) =>
            {
                Log("UNHANDLED: " + args.Exception);
                MessageBox.Show(Loc.F("程序错误: {0}", args.Exception.Message), Loc.T("错误"), MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
                Log("DOMAIN UNHANDLED: " + (args.ExceptionObject != null ? args.ExceptionObject.ToString() : "?"));
            base.OnStartup(e);
            Log("AFTER BASE STARTUP");


        }
    }
}
