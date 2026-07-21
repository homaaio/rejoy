using System;
using System.Drawing;
using System.Windows.Forms;

namespace DualKey
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // Segoe UI is the font every native Windows dialog uses; the WinForms
            // default (Microsoft Sans Serif) is what makes hand-built forms look dated.
            Application.SetDefaultFont(new Font("Segoe UI", 9f));
            Application.Run(new MainForm());
        }
    }
}
