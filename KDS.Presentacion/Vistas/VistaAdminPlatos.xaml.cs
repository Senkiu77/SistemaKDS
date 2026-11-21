using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KDS.PRE.Portal;

namespace KDS.PRE.Vistas
{
    public partial class VistaAdminPlatos : Window
    {
        private readonly KdsPlatoBLL platoBll = new();
        private readonly UsuarioModel dueno;
        private int idSeleccionado;

        public bool HuboCambios { get; private set; }

        public VistaAdminPlatos(UsuarioModel dueno)
        {
            InitializeComponent();
            this.dueno = dueno;
            TextoDueno.Text = "DUEÑO · " + dueno.Nombre.ToUpper();
            Loaded += (_, _) => CargarPlatos();
        }

        private void CargarPlatos(int? seleccionar = null)
        {
            try
            {
                var platos = platoBll.ListarTodos();
                GrillaPlatos.ItemsSource = platos;
                TextoEstado.Text = $"{platos.Count} platos registrados.";

                if (seleccionar is int id)
                {
                    GrillaPlatos.SelectedItem = platos.FirstOrDefault(p => p.IdPlato == id);
                }
            }
            catch (Exception ex)
            {
                TextoEstado.Text = "No se pudo leer la base de datos: " + ex.Message;
            }
        }

        private void GrillaPlatos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GrillaPlatos.SelectedItem is not PlatoModel plato)
                return;

            idSeleccionado = plato.IdPlato;
            TxtNombre.Text = plato.Nombre;
            TxtPrecio.Text = plato.Precio.ToString("0");
            ChkActivo.IsChecked = plato.Activo;
            MarcarCategoria(CategoriasMenu.De(plato));
            TituloFormulario.Text = $"EDITAR PLATO #{plato.IdPlato}";
            BtnModificar.IsEnabled = true;
            BtnEliminar.IsEnabled = true;
        }

        private void TxtPrecio_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private bool LeerFormulario(out PlatoModel plato)
        {
            plato = new PlatoModel();

            string precioTexto = TxtPrecio.Text.Replace(".", "").Replace(",", "").Replace("$", "").Trim();
            if (!decimal.TryParse(precioTexto, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal precio))
            {
                MessageBox.Show(this, "Escribe un precio numérico, por ejemplo 28000.", "Precio no válido",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPrecio.Focus();
                return false;
            }

            string? categoria = CategoriaElegida();
            if (categoria == null)
            {
                MessageBox.Show(this, "Elige si el plato es Comida, Bebida o Acompañamiento.", "Falta la categoría",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                OpcionComida.Focus();
                return false;
            }

            plato = new PlatoModel
            {
                IdPlato = idSeleccionado,
                Nombre = TxtNombre.Text.Trim(),
                Precio = precio,
                Categoria = categoria,
                Activo = ChkActivo.IsChecked == true
            };
            return true;
        }

        private void BtnAgregar_Click(object sender, RoutedEventArgs e)
        {
            if (!LeerFormulario(out var plato)) return;

            string resultado = platoBll.CrearPlato(plato);
            if (resultado != "OK")
            {
                MessageBox.Show(this, resultado, "No se pudo agregar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            HuboCambios = true;
            Limpiar();
            CargarPlatos();
            TextoEstado.Text = $"«{plato.Nombre}» quedó en el menú y en el portal web.";
        }

        private void BtnModificar_Click(object sender, RoutedEventArgs e)
        {
            if (idSeleccionado <= 0)
            {
                MessageBox.Show(this, "Selecciona un plato de la lista.", "Sin selección",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!LeerFormulario(out var plato)) return;

            string resultado = platoBll.EditarPlato(plato);
            if (resultado != "OK")
            {
                MessageBox.Show(this, resultado, "No se pudo guardar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            HuboCambios = true;
            CargarPlatos(plato.IdPlato);
            TextoEstado.Text = $"Cambios de «{plato.Nombre}» guardados.";
        }

        private void BtnEliminar_Click(object sender, RoutedEventArgs e)
        {
            if (GrillaPlatos.SelectedItem is not PlatoModel plato)
            {
                MessageBox.Show(this, "Selecciona un plato de la lista.", "Sin selección",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var respuesta = MessageBox.Show(this,
                $"¿Eliminar «{plato.Nombre}» del menú?\n\nSi ya aparece en pedidos anteriores, solo se marcará como agotado para no perder el historial.",
                "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (respuesta != MessageBoxResult.Yes) return;

            string resultado = platoBll.EliminarPlato(plato.IdPlato, out bool soloDesactivado);
            if (resultado != "OK")
            {
                MessageBox.Show(this, resultado, "No se pudo eliminar", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            HuboCambios = true;
            Limpiar();
            CargarPlatos();
            TextoEstado.Text = soloDesactivado
                ? $"«{plato.Nombre}» tiene pedidos guardados, así que quedó como agotado."
                : $"«{plato.Nombre}» se eliminó del menú.";
        }

        private void BtnLimpiar_Click(object sender, RoutedEventArgs e)
        {
            Limpiar();
        }

        private void Limpiar()
        {
            idSeleccionado = 0;
            GrillaPlatos.SelectedItem = null;
            TxtNombre.Clear();
            TxtPrecio.Clear();
            ChkActivo.IsChecked = true;
            MarcarCategoria(null);
            TituloFormulario.Text = "NUEVO PLATO";
            BtnModificar.IsEnabled = false;
            BtnEliminar.IsEnabled = false;
            TxtNombre.Focus();
        }

        private RadioButton[] OpcionesCategoria => new[] { OpcionComida, OpcionBebida, OpcionAcompanamiento };

        private string? CategoriaElegida() =>
            OpcionesCategoria.FirstOrDefault(o => o.IsChecked == true)?.Tag as string;

        private void MarcarCategoria(string? categoria)
        {
            foreach (var opcion in OpcionesCategoria)
                opcion.IsChecked = categoria != null && (string)opcion.Tag == categoria;
        }

        private void BtnCambiarClave_Click(object sender, RoutedEventArgs e)
        {
            var cambio = new VistaAccesoDueno(dueno) { Owner = this };
            if (cambio.ShowDialog() == true)
                TextoEstado.Text = "Tu clave quedó cambiada.";
        }
    }
}
