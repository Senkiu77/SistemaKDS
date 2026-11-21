using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace KDS.PRE
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer temporizador = null!;
        private Random aleatorio = new Random();

        private double posicionX = 50;
        private double posicionY = 50;

        private double velocidadX = 3.5;
        private double velocidadY = 2.5;

        private Color[] colores =
        {
            Colors.White,
            Colors.DeepSkyBlue,
            Colors.LimeGreen,
            Colors.Orange,
            Colors.MediumPurple,
            Colors.HotPink,
            Colors.Yellow
        };

        private KDS.PRE.Vistas.VistaMesero? vistaMesero;
        private Window? monitorCocina;

        public MainWindow()
        {
            InitializeComponent();

            Loaded += IniciarAnimacion;
            Loaded += (_, _) => MostrarEstadoPortal();
            Loaded += (_, _) => MostrarTunel();
            Loaded += async (_, _) => await RevisarBaseDeDatos();
            App.Tunel.Cambio += () => Dispatcher.BeginInvoke(MostrarTunel);
            Closed += (_, _) => temporizador?.Stop();
        }

        private void IniciarAnimacion(object sender, RoutedEventArgs e)
        {
            posicionX = (AreaAnimacion.ActualWidth - LogoKDS.Width) / 2;
            posicionY = (AreaAnimacion.ActualHeight - LogoKDS.Height) / 2;

            Canvas.SetLeft(LogoKDS, posicionX);
            Canvas.SetTop(LogoKDS, posicionY);

            temporizador = new DispatcherTimer();
            temporizador.Interval = TimeSpan.FromMilliseconds(16);
            temporizador.Tick += MoverLogo;
            temporizador.Start();
        }

        private void MoverLogo(object? sender, EventArgs e)
        {
            posicionX += velocidadX;
            posicionY += velocidadY;

            if (posicionX <= 0)
            {
                posicionX = 0;
                velocidadX = Math.Abs(velocidadX);
                CambiarColor();
            }

            if (posicionX + LogoKDS.Width >= AreaAnimacion.ActualWidth)
            {
                posicionX = AreaAnimacion.ActualWidth - LogoKDS.Width;
                velocidadX = -Math.Abs(velocidadX);
                CambiarColor();
            }

            if (posicionY <= 0)
            {
                posicionY = 0;
                velocidadY = Math.Abs(velocidadY);
                CambiarColor();
            }

            if (posicionY + LogoKDS.Height >= AreaAnimacion.ActualHeight)
            {
                posicionY = AreaAnimacion.ActualHeight - LogoKDS.Height;
                velocidadY = -Math.Abs(velocidadY);
                CambiarColor();
            }

            Canvas.SetLeft(LogoKDS, posicionX);
            Canvas.SetTop(LogoKDS, posicionY);
        }

        private void CambiarColor()
        {
            int indice = aleatorio.Next(colores.Length);
            LogoKDS.Fill = new SolidColorBrush(colores[indice]);
        }

        private void BtnMesero_Click(object sender, RoutedEventArgs e)
        {
            vistaMesero ??= new KDS.PRE.Vistas.VistaMesero();

            PantallaInicio.Visibility = Visibility.Collapsed;
            ContenidoPrincipal.Content = vistaMesero;
            ContenidoPrincipal.Visibility = Visibility.Visible;
        }

        private void BtnCocina_Click(object sender, RoutedEventArgs e)
        {
            AbrirMonitorCocina();
        }

        public void AbrirMonitorCocina()
        {
            if (monitorCocina != null)
            {
                if (monitorCocina.WindowState == WindowState.Minimized)
                {
                    monitorCocina.WindowState = WindowState.Normal;
                }

                monitorCocina.Activate();
                return;
            }

            monitorCocina = new Window
            {
                Title = "KDS — Monitor de cocina",
                Width = 850,
                Height = 750,
                MinWidth = 460,
                MinHeight = 500,
                Background = new SolidColorBrush(Color.FromRgb(8, 8, 8)),
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new KDS.PRE.Vistas.VistaCocina()
            };

            monitorCocina.Closed += (_, _) => monitorCocina = null;
            monitorCocina.Show();
        }

        private void MostrarEstadoPortal()
        {
            var portal = App.Portal;

            if (portal.Activo)
            {
                string ia = portal.UsaClaude ? "ASISTENTE CON CLAUDE" : "ASISTENTE SIN CONEXIÓN A IA";
                TextoPortal.Text = $"MENÚ WEB ACTIVO: {portal.Direccion.TrimEnd('/')}  ·  {ia}";
                TextoPortal.Foreground = new SolidColorBrush(Color.FromRgb(57, 217, 138));
            }
            else
            {
                TextoPortal.Text = "MENÚ WEB APAGADO: " + (portal.Error ?? "no se pudo abrir el puerto 8080");
                TextoPortal.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
            }
        }

        private void TextoPortal_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!App.Portal.Activo) return;

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = App.Portal.Direccion + "?mesa=1",
                UseShellExecute = true
            });
        }

        private async System.Threading.Tasks.Task RevisarBaseDeDatos()
        {
            string? error = App.ErrorBaseDatos
                ?? await System.Threading.Tasks.Task.Run(KDS.PRE.Vistas.KdsDatosService.ProbarConexion);
            if (error == null) return;

            MessageBox.Show(this,
                "No se pudo conectar a la base de datos bd_kds.\n\n" + error +
                "\n\nRevisa que PostgreSQL esté encendido y que en pgAdmin la base se llame bd_kds, con usuario postgres y clave 1234 en el puerto 5432. " +
                "Si tu servidor usa otro puerto, cámbialo en Vistas/KdsDatosService.cs. " +
                "El programa actualiza las tablas solo al abrir; si el error sigue, ejecuta BD/actualizacion_menu_web.sql en pgAdmin.",
                "Base de datos", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void MostrarTunel()
        {
            var tunel = App.Tunel;

            if (tunel.Enlace != null)
            {
                string tipo = tunel.EsPermanente ? "FIJO" : "CAMBIA CADA VEZ QUE ABRES EL PROGRAMA";
                TextoTunel.Text = $"LINK PARA LOS NFC: {tunel.Enlace}/?mesa=1  ·  {tipo}  ·  CLIC PARA COPIAR";
                TextoTunel.Foreground = new SolidColorBrush(tunel.EsPermanente ? Color.FromRgb(57, 217, 138) : Color.FromRgb(245, 166, 35));
            }
            else if (tunel.Error != null)
            {
                TextoTunel.Text = "LINK PARA LOS NFC: " + tunel.Error;
                TextoTunel.Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100));
            }
            else if (tunel.Modo == "Sin túnel")
            {
                TextoTunel.Text = "LINK PARA LOS NFC: NO HAY TÚNEL CONFIGURADO (VER README)";
            }
            else
            {
                TextoTunel.Text = $"LINK PARA LOS NFC: ABRIENDO TÚNEL ({tunel.Modo.ToUpper()})...";
            }
        }

        private void TextoTunel_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (App.Tunel.Enlace == null) return;

            string enlace = App.Tunel.Enlace + "/?mesa=1";
            try
            {
                Clipboard.SetText(enlace);
                TextoTunel.Text = $"COPIADO: {enlace}  ·  CAMBIA EL NÚMERO DE MESA AL FINAL PARA CADA STICKER";
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                TextoTunel.Text = $"LINK PARA LOS NFC: {enlace}";
            }
        }

        public void VolverAlInicio()
        {
            ContenidoPrincipal.Visibility = Visibility.Collapsed;
            ContenidoPrincipal.Content = null;
            PantallaInicio.Visibility = Visibility.Visible;
        }
    }
}
