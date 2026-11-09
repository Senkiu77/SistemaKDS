using System;
using System.Windows.Forms;

namespace KDS.Presentacion
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Aquí le dices con cuál formulario arrancar (puedes cambiarlo luego)
            Application.Run(new FrmMeseroPOS());
        }
    }
}