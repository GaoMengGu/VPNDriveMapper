using System;
using System.Threading;
using System.Windows.Forms;

namespace VPNDriveMapper
{
    static class Program
    {
        private static Mutex _mutex;

        [STAThread]
        static void Main()
        {
            bool createdNew;
            _mutex = new Mutex(true, "VPNDriveMapper_SingleInstance", out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("程序已在运行中", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayApplicationContext());
        }
    }
}
