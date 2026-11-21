using System.Windows;
using System.Windows.Controls;

namespace KDS.PRE.Vistas
{
    public partial class VistaAccesoDueno : Window
    {
        private readonly KdsAccesoDueno acceso = new();
        private readonly UsuarioModel? duenoQueCambia;

        public UsuarioModel? DuenoAutenticado { get; private set; }

        public VistaAccesoDueno()
        {
            InitializeComponent();
            PanelClaveActual.Visibility = Visibility.Collapsed;
            Loaded += (_, _) => CargarDuenos();
        }

        public VistaAccesoDueno(UsuarioModel dueno)
        {
            InitializeComponent();
            duenoQueCambia = dueno;
            PanelDueno.Visibility = Visibility.Collapsed;
            TextoTitulo.Text = "Cambiar mi clave";
            TextoAyuda.Text = dueno.Nombre;
            EtiquetaClave.Text = "CLAVE NUEVA";
            BtnEntrar.Content = "GUARDAR";
            Loaded += (_, _) => TxtClaveActual.Focus();
        }

        private void CargarDuenos()
        {
            try
            {
                var duenos = acceso.ObtenerDuenos();
                if (duenos.Count == 0)
                {
                    MostrarError("No hay ningún dueño registrado. Ejecuta BD/actualizacion_menu_web.sql en la base de datos.");
                    BtnEntrar.IsEnabled = false;
                    return;
                }

                CmbDueno.ItemsSource = duenos;
                CmbDueno.SelectedIndex = 0;
                PanelDueno.Visibility = duenos.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (System.Exception ex)
            {
                MostrarError("No se pudo leer la base de datos: " + ex.Message);
                BtnEntrar.IsEnabled = false;
            }
        }

        private void CmbDueno_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbDueno.SelectedItem is not UsuarioModel dueno) return;

            bool primeraVez = !KdsAccesoDueno.TieneClave(dueno);
            TextoTitulo.Text = primeraVez ? "Crea la clave del dueño" : "Entrar a administrar el menú";
            TextoAyuda.Text = primeraVez
                ? $"{dueno.Nombre} todavía no tiene clave. La que escribas aquí será la que se pida para cambiar el menú."
                : $"Escribe la clave de {dueno.Nombre}. Los meseros no pueden cambiar platos ni precios.";
            EtiquetaClave.Text = primeraVez ? "CLAVE NUEVA" : "CLAVE";
            PanelConfirmar.Visibility = primeraVez ? Visibility.Visible : Visibility.Collapsed;
            BtnEntrar.Content = primeraVez ? "CREAR Y ENTRAR" : "ENTRAR";
            TextoError.Visibility = Visibility.Collapsed;
            TxtClave.Clear();
            TxtConfirmar.Clear();
            TxtClave.Focus();
        }

        private void BtnEntrar_Click(object sender, RoutedEventArgs e)
        {
            if (duenoQueCambia != null)
            {
                CambiarClave(duenoQueCambia);
                return;
            }

            if (CmbDueno.SelectedItem is not UsuarioModel dueno) return;

            string resultado = KdsAccesoDueno.TieneClave(dueno)
                ? acceso.Entrar(dueno, TxtClave.Password)
                : acceso.CrearClave(dueno, TxtClave.Password, TxtConfirmar.Password);

            if (resultado != "OK")
            {
                MostrarError(resultado);
                TxtClave.Clear();
                TxtClave.Focus();
                return;
            }

            DuenoAutenticado = dueno;
            DialogResult = true;
        }

        private void CambiarClave(UsuarioModel dueno)
        {
            string resultado = acceso.Entrar(dueno, TxtClaveActual.Password);
            if (resultado == "OK")
                resultado = acceso.CrearClave(dueno, TxtClave.Password, TxtConfirmar.Password);

            if (resultado != "OK")
            {
                MostrarError(resultado);
                return;
            }

            DialogResult = true;
        }

        private void MostrarError(string mensaje)
        {
            TextoError.Text = mensaje;
            TextoError.Visibility = Visibility.Visible;
        }
    }
}
