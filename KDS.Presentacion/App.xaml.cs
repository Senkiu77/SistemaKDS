using System.Windows;
using KDS.PRE.Portal;

namespace KDS.PRE
{
    public partial class App : Application
    {
        public static PortalClienteService Portal { get; } = new();
        public static TunelPublico Tunel { get; } = new();
        public static string? ErrorBaseDatos { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ErrorBaseDatos = KDS.PRE.Vistas.KdsDatosService.ActualizarEstructura();
            Portal.IniciarServidor("http://localhost:8080/");
            if (Portal.Activo) Tunel.Iniciar(8080);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Tunel.Detener();
            Portal.Detener();
            base.OnExit(e);
        }
    }
}
