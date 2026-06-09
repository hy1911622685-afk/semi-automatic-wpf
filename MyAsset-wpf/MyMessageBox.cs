using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;

namespace MyAsset.Wpf
{
    public enum MyDialogResult
    {
        None,
        OK,
        Cancel,
        Yes,
        No
    }

    public static class MyMessageBox
    {
        public static Window Owner { get; set; }

        public static void ShowInfo(string content, string head = "提示")
        {
            Show(content, head, MessageBoxImage.Information);
        }

        public static void ShowSuccess(string content, string head = "操作成功")
        {
            Show(content, head, MessageBoxImage.Information);
        }

        public static void ShowWarn(string content, string head = "警告")
        {
            Show(content, head, MessageBoxImage.Warning);
        }

        public static void ShowError(string content, string head = "错误")
        {
            Show(content, head, MessageBoxImage.Error);
        }

        public static MyDialogResult ShowQuery(string content, string head = "确认操作")
        {
            var result = ShowCore(content, head, MessageBoxButton.OKCancel, MessageBoxImage.Question);
            return result == MessageBoxResult.OK ? MyDialogResult.OK : MyDialogResult.Cancel;
        }

        private static void Show(string content, string head, MessageBoxImage image)
        {
            if (string.IsNullOrWhiteSpace(content))
                return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                ShowCore(content, head, MessageBoxButton.OK, image);
            else
                dispatcher.BeginInvoke((Action)(() => ShowCore(content, head, MessageBoxButton.OK, image)));
        }

        private static MessageBoxResult ShowCore(string content, string head, MessageBoxButton button, MessageBoxImage image)
        {
            if (string.IsNullOrWhiteSpace(content))
                return MessageBoxResult.None;

            MessageBoxResult ShowDialog()
            {
                string title = string.IsNullOrWhiteSpace(head) ? "提示" : head;
                return Owner == null
                    ? MessageBox.Show(content, title, button, image, MessageBoxResult.OK)
                    : MessageBox.Show(Owner, content, title, button, image, MessageBoxResult.OK);
            }

            var dispatcher = Application.Current?.Dispatcher;
            return dispatcher == null || dispatcher.CheckAccess()
                ? ShowDialog()
                : dispatcher.Invoke(ShowDialog);
        }
    }

    public class MyLog
    {
        private static readonly object LockObject = new object();
        private static string _logPath = Path.Combine(Environment.CurrentDirectory, "Log");

        public static string LogPath
        {
            get => _logPath;
            set => _logPath = value;
        }

        public static void WriteLog(string message)
        {
            lock (LockObject)
            {
                string dirPath = _logPath ?? Path.Combine(Environment.CurrentDirectory, "Log");
                Directory.CreateDirectory(dirPath);
                string filePath = Path.Combine(dirPath, DateTime.Now.ToShortDateString().Replace("/", "-") + ".txt");
                File.AppendAllText(filePath, DateTime.Now.ToString(CultureInfo.CurrentCulture) + " : " + message, Encoding.UTF8);
            }
        }

        public static void WriteLog(string head, string message)
        {
            lock (LockObject)
            {
                string dirPath = _logPath ?? Path.Combine(Environment.CurrentDirectory, "Log");
                Directory.CreateDirectory(dirPath);

                string formatted = DateTime.Now.ToString(CultureInfo.CurrentCulture) + " --- " + head + " : " + message + "\r\n";
                string filePath = Path.Combine(dirPath, DateTime.Now.ToShortDateString().Replace("/", "-") + ".txt");
                File.AppendAllText(filePath, formatted, Encoding.UTF8);
            }
        }
    }
}
