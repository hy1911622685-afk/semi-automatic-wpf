using System.Diagnostics;

namespace MyAsset.Wpf
{
    public static class ProcessHelper
    {
        public static void KillProcess(string processName, bool exactMatch = false, int waitForExitMilliseconds = 3000)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return;

            int currentProcessId = Process.GetCurrentProcess().Id;
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (process.Id == currentProcessId)
                        continue;

                    if (!IsProcessNameMatch(process.ProcessName, processName, exactMatch))
                        continue;

                    process.Kill();
                    process.WaitForExit(waitForExitMilliseconds);
                }
                catch (System.Exception ex)
                {
                    MyLog.WriteLog("ProcessHelper", $"结束进程 {processName} 失败: {ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private static bool IsProcessNameMatch(string currentName, string targetName, bool exactMatch)
        {
            if (string.IsNullOrWhiteSpace(currentName))
                return false;

            return exactMatch
                ? string.Equals(currentName, targetName, System.StringComparison.OrdinalIgnoreCase)
                : currentName.IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
