using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace KDS.PRE.Vistas
{
    public partial class VistaMesero : UserControl
    {
        private PedidoCocina? ultimoPedidoEnviado;
        private readonly ObservableCollection<LineaComanda> lineas = new();
        private readonly CultureInfo cultura =
            CultureInfo.GetCultureInfo("es-CO");

        public VistaMesero()
        {
            InitializeComponent();

            ListaComanda.ItemsSource = lineas;

            ActualizarResumen();
            AplicarFiltros();

            Loaded += Vista_Loaded;
            Unloaded += Vista_Unloaded;
        }

        private void Vista_Loaded(object sender, RoutedEventArgs e)
        {
            PedidosEnMemoria.Cambio -= Pedidos_Cambio;
            PedidosEnMemoria.Cambio += Pedidos_Cambio;
            ActualizarPedidosListos();
        }

        private void Vista_Unloaded(object sender, RoutedEventArgs e)
        {
            PedidosEnMemoria.Cambio -= Pedidos_Cambio;
        }

        private void Pedidos_Cambio(object? sender, EventArgs e)
        {
            ActualizarPedidosListos();
        }

        private string ObtenerMeseroSeleccionado()
        {
            return (SelectorMesero.SelectedItem as ComboBoxItem)?
                .Content?.ToString() ?? "Carlos Mesero";
        }

        private void SelectorMesero_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (ListaPedidosListos != null)
                ActualizarPedidosListos();
        }

        private void ActualizarPedidosListos()
        {
            var listos = PedidosEnMemoria.Listos
                .Where(p => p.Mesero == ObtenerMeseroSeleccionado())
                .ToList();

            ListaPedidosListos.ItemsSource = listos;

            TextoListosMesero.Text =
                $"LISTOS PARA RECOGER: {listos.Count}";

            MensajeSinListos.Visibility = listos.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void BtnAbrirCocina_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is KDS.PRE.MainWindow ventana)
                ventana.AbrirMonitorCocina();
        }

        private void BtnEntregar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: PedidoCocina pedido })
                PedidosEnMemoria.Entregar(pedido);
        }

        private void BtnTicketListo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: PedidoCocina pedido })
            {
                var ticket = new VistaTicket(
                    pedido.Productos,
                    pedido.Observaciones,
                    pedido.Numero,
                    pedido.Mesa,
                    pedido.Fecha,
                    pedido.Mesero)
                {
                    Owner = Window.GetWindow(this)
                };

                ticket.ShowDialog();
            }
        }

        private void BusquedaProductos_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            AplicarFiltros();
        }

        private void Categoria_Checked(object sender, RoutedEventArgs e)
        {
            AplicarFiltros();
        }

        private void AplicarFiltros()
        {
            if (BusquedaProductos == null ||
                TextoCantidadMenu == null ||
                MensajeSinResultados == null ||
                CategoriaHamburguesas == null ||
                CategoriaAcompanamientos == null ||
                CategoriaBebidas == null ||
                CategoriaAdicionales == null ||
                TarjetaClasica == null ||
                TarjetaDoble == null ||
                TarjetaCombo == null ||
                TarjetaEspecial == null ||
                TarjetaPapas == null ||
                TarjetaGaseosa == null ||
                TarjetaQueso == null)
            {
                return;
            }

            string busqueda = BusquedaProductos.Text.Trim();

            FiltrarTarjeta(
                TarjetaClasica,
                "PLÁTANO CLÁSICA Hamburguesa de plátano",
                busqueda,
                CategoriaHamburguesas.IsChecked == true);

            FiltrarTarjeta(
                TarjetaDoble,
                "DOBLE PLÁTANO Doble porción de sabor",
                busqueda,
                CategoriaHamburguesas.IsChecked == true);

            FiltrarTarjeta(
                TarjetaCombo,
                "COMBO BURGER Hamburguesa y acompañamiento",
                busqueda,
                CategoriaHamburguesas.IsChecked == true);

            FiltrarTarjeta(
                TarjetaEspecial,
                "BURGER ESPECIAL Una combinación especial",
                busqueda,
                CategoriaHamburguesas.IsChecked == true);

            FiltrarTarjeta(
                TarjetaPapas,
                "PAPAS FRITAS Porción de papas crujientes",
                busqueda,
                CategoriaAcompanamientos.IsChecked == true);

            FiltrarTarjeta(
                TarjetaGaseosa,
                "GASEOSA PERSONAL Bebida personal de 400 ml",
                busqueda,
                CategoriaBebidas.IsChecked == true);

            FiltrarTarjeta(
                TarjetaQueso,
                "QUESO EXTRA Porción adicional de queso",
                busqueda,
                CategoriaAdicionales.IsChecked == true);

            var tarjetas = new[]
            {
                TarjetaClasica,
                TarjetaDoble,
                TarjetaCombo,
                TarjetaEspecial,
                TarjetaPapas,
                TarjetaGaseosa,
                TarjetaQueso
            };

            int visibles = tarjetas.Count(
                tarjeta => tarjeta.Visibility == Visibility.Visible);

            TextoCantidadMenu.Text = visibles == 0
                ? "SIN RESULTADOS"
                : visibles == 1
                    ? "1 PRODUCTO"
                    : visibles + " PRODUCTOS";

            MensajeSinResultados.Visibility = visibles == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void FiltrarTarjeta(
            ToggleButton tarjeta,
            string texto,
            string busqueda,
            bool categoriaCoincide)
        {
            bool textoCoincide = cultura.CompareInfo.IndexOf(
                texto,
                busqueda,
                CompareOptions.IgnoreCase |
                CompareOptions.IgnoreNonSpace) >= 0;

            tarjeta.Visibility = categoriaCoincide && textoCoincide
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void Producto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton tarjeta ||
                tarjeta.Tag is not string codigo)
            {
                return;
            }

            switch (codigo)
            {
                case "clasica":
                    AgregarProducto(codigo, "PLÁTANO CLÁSICA", 15000m);
                    break;
                case "doble":
                    AgregarProducto(codigo, "DOBLE PLÁTANO", 20000m);
                    break;
                case "combo":
                    AgregarProducto(codigo, "COMBO BURGER", 24000m);
                    break;
                case "especial":
                    AgregarProducto(codigo, "BURGER ESPECIAL", 22000m);
                    break;
                case "papas":
                    AgregarProducto(codigo, "PAPAS FRITAS", 6000m);
                    break;
                case "gaseosa":
                    AgregarProducto(codigo, "GASEOSA PERSONAL", 4000m);
                    break;
                case "queso":
                    AgregarProducto(codigo, "QUESO EXTRA", 3000m);
                    break;
            }
        }

        private void AgregarProducto(
            string codigo,
            string nombre,
            decimal precio)
        {
            var existente = lineas.FirstOrDefault(
                linea => linea.Codigo == codigo);

            if (existente == null)
            {
                lineas.Add(new LineaComanda(
                    codigo, nombre, precio, 1));
            }
            else
            {
                CambiarCantidad(existente, existente.Cantidad + 1);
                return;
            }

            ActualizarResumen();
        }

        private void SumarProducto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button boton &&
                boton.Tag is LineaComanda linea)
            {
                CambiarCantidad(linea, linea.Cantidad + 1);
            }
        }

        private void RestarProducto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button boton &&
                boton.Tag is LineaComanda linea)
            {
                CambiarCantidad(linea, linea.Cantidad - 1);
            }
        }

        private void CambiarCantidad(LineaComanda linea, int cantidad)
        {
            int indice = lineas.IndexOf(linea);

            if (indice < 0)
                return;

            if (cantidad <= 0)
            {
                lineas.RemoveAt(indice);
            }
            else
            {
                lineas[indice] = new LineaComanda(
                    linea.Codigo,
                    linea.Nombre,
                    linea.PrecioUnitario,
                    cantidad,
                    linea.Notas);
            }

            ActualizarResumen();
        }

        private void ActualizarResumen()
        {
            int cantidad = lineas.Sum(linea => linea.Cantidad);
            decimal total = lineas.Sum(linea => linea.Subtotal);

            TextoTotal.Text = "$ " + total.ToString("N0", cultura);

            TextoCantidad.Text = cantidad == 1
                ? "1 PRODUCTO"
                : cantidad + " PRODUCTOS";

            MensajeComandaVacia.Visibility = cantidad == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            ListaComanda.Visibility = cantidad == 0
                ? Visibility.Collapsed
                : Visibility.Visible;

            TarjetaClasica.IsChecked =
                lineas.Any(linea => linea.Codigo == "clasica");
            TarjetaDoble.IsChecked =
                lineas.Any(linea => linea.Codigo == "doble");
            TarjetaCombo.IsChecked =
                lineas.Any(linea => linea.Codigo == "combo");
            TarjetaEspecial.IsChecked =
                lineas.Any(linea => linea.Codigo == "especial");
            TarjetaPapas.IsChecked =
                lineas.Any(linea => linea.Codigo == "papas");
            TarjetaGaseosa.IsChecked =
                lineas.Any(linea => linea.Codigo == "gaseosa");
            TarjetaQueso.IsChecked =
                lineas.Any(linea => linea.Codigo == "queso");

            TextoNumeroPedido.Text =
                $"PEDIDO #{PedidosEnMemoria.ProximoNumero:000}";
        }

        private void BtnLimpiarComanda_Click(
            object sender,
            RoutedEventArgs e)
        {
            lineas.Clear();
            ObservacionesPedido.Clear();
            ActualizarResumen();
        }

        private void BtnInicio_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is KDS.PRE.MainWindow ventana)
                ventana.VolverAlInicio();
        }

        private string ObtenerMesaSeleccionada()
        {
            return SelectorMesa.SelectedItem is ComboBoxItem item
                ? item.Content?.ToString() ?? "Mesa 01"
                : "Mesa 01";
        }

        private void BtnEnviarCocina_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (lineas.Count == 0)
            {
                MessageBox.Show(
                    "Agrega al menos un producto antes de enviar el pedido.",
                    "Comanda vacía",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var pedido = PedidosEnMemoria.Enviar(
                lineas,
                ObtenerMesaSeleccionada(),
                ObservacionesPedido.Text,
                ObtenerMeseroSeleccionado());

            ultimoPedidoEnviado = pedido;

            lineas.Clear();
            ObservacionesPedido.Clear();
            ActualizarResumen();

            MessageBox.Show(
                $"Pedido #{pedido.Numero:000} enviado a cocina.",
                "Pedido enviado",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void BtnVerTicket_Click(
            object sender,
            RoutedEventArgs e)
        {
            VistaTicket ticket;

            if (lineas.Count > 0)
            {
                ticket = new VistaTicket(
                    lineas,
                    ObservacionesPedido.Text,
                    PedidosEnMemoria.ProximoNumero,
                    ObtenerMesaSeleccionada(),
                    mesero: ObtenerMeseroSeleccionado());
            }
            else if (ultimoPedidoEnviado is PedidoCocina pedido)
            {
                ticket = new VistaTicket(
                    pedido.Productos,
                    pedido.Observaciones,
                    pedido.Numero,
                    pedido.Mesa,
                    pedido.Fecha,
                    pedido.Mesero);
            }
            else
            {
                MessageBox.Show(
                    "Agrega productos o envía un pedido para consultar su ticket.",
                    "Sin ticket disponible",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            ticket.Owner = Window.GetWindow(this);
            ticket.ShowDialog();
        }
    }
}