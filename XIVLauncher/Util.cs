using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace XIVLauncher
{
    public static class Util
    {
        public static void ShowError(string message, string caption, [CallerMemberName] string callerName = "", [CallerLineNumber] int callerLineNumber = 0)
        {
            MessageBox.Show($"{message}\n\n{callerName} L{callerLineNumber}", caption, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}