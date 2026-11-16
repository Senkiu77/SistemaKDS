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

        public void VolverAlInicio()
        {
            ContenidoPrincipal.Visibility = Visibility.Collapsed;
            ContenidoPrincipal.Content = null;
            PantallaInicio.Visibility = Visibility.Visible;
        }
    }
}