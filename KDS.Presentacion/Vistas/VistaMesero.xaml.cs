using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using KDS.PRE.Portal;

namespace KDS.PRE.Vistas
{
    public partial class VistaMesero : UserControl
    {
        private const string MeseroPorDefecto = "Mesero de turno";

        private readonly KdsPedidoBLL pedidoBll = new();
        private readonly ObservableCollection<LineaComanda> lineas = new();
        private readonly CultureInfo cultura = CultureInfo.GetCultureInfo("es-CO");
        private readonly DispatcherTimer timerSincronizacion = new() { Interval = TimeSpan.FromSeconds(3) };

        private List<PlatoModel> listaPlatosBD = new();
        private PedidoCocina? ultimoPedidoEnviado;
        private int cantidadSolicitudes = -1;

        public VistaMesero()
        {
            InitializeComponent();

            ListaComanda.ItemsSource = lineas;
            ActualizarResumen();

            timerSincronizacion.Tick += (_, _) => Sincronizar();

            Loaded += Vista_Loaded;
            Unloaded += (_, _) => timerSincronizacion.Stop();
        }

        private void Vista_Loaded(object sender, RoutedEventArgs e)
        {
            CargarMeserosDesdeBD();
            CargarPlatosDesdeBD();
            Sincronizar();
            ActualizarNumeroPedido();

            timerSincronizacion.Start();
        }

        private void Sincronizar()
        {
            ActualizarPedidosListos();
            CargarSolicitudesClientes();
        }

        private void CargarMeserosDesdeBD()
        {
            SelectorMesero.Items.Clear();

            try
            {
                foreach (var m in pedidoBll.ObtenerMeseros())
                {
                    SelectorMesero.Items.Add(new ComboBoxItem { Content = m.Nombre, Tag = m.IdUsuario });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cargando meseros: {ex.Message}");
            }

            if (SelectorMesero.Items.Count == 0)
                SelectorMesero.Items.Add(new ComboBoxItem { Content = MeseroPorDefecto });

            SelectorMesero.SelectedIndex = 0;
        }

        private void CargarPlatosDesdeBD()
        {
            try
            {
                listaPlatosBD = pedidoBll.ObtenerMenu();
            }
            catch (Exception ex)
            {
                listaPlatosBD = new();
                MessageBox.Show(
                    "No se pudo cargar el menú desde PostgreSQL. Revisa que la base bd_kds esté encendida.\n\n" + ex.Message,
                    "Sin conexión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            GenerarTarjetasPlatos();

            var vigentes = listaPlatosBD.Select(p => p.IdPlato.ToString()).ToHashSet();
            foreach (var linea in lineas.Where(l => !vigentes.Contains(l.Codigo)).ToList())
                lineas.Remove(linea);

            AplicarFiltros();
            ActualizarResumen();
        }

        private void GenerarTarjetasPlatos()
        {
            GrillaProductos.Children.Clear();

            foreach (var plato in listaPlatosBD)
            {
                var boton = new ToggleButton
                {
                    Tag = plato,
                    Style = (Style)FindResource("TarjetaProducto"),
                    ToolTip = "Agregar a la comanda"
                };
                boton.Click += Producto_Click;

                var panel = new StackPanel();

                panel.Children.Add(new Border
                {
                    Height = 80,
                    Background = Pincel("#30271B"),
                    CornerRadius = new CornerRadius(8),
                    Child = new TextBlock
                    {
                        Text = CategoriasMenu.Icono(plato),
                        FontSize = 48,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                });

                panel.Children.Add(new TextBlock
                {
                    Text = plato.Nombre.ToUpper(cultura),
                    Foreground = Brushes.White,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 12, 0, 5)
                });

                panel.Children.Add(new TextBlock
                {
                    Text = CategoriasMenu.De(plato),
                    Foreground = Pincel("#999999"),
                    FontSize = 10
                });

                panel.Children.Add(new TextBlock
                {
                    Text = "$ " + plato.Precio.ToString("N0", cultura),
                    Foreground = Pincel("#F5A623"),
                    FontSize = 17,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 12, 0, 0)
                });

                boton.Content = panel;
                GrillaProductos.Children.Add(boton);
            }
        }

        private static SolidColorBrush Pincel(string color)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }

        private void Producto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton { Tag: PlatoModel plato })
            {
                AgregarProducto(plato.IdPlato.ToString(), plato.Nombre, plato.Precio);
            }
        }

        private string ObtenerMeseroSeleccionado()
        {
            return (SelectorMesero.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? MeseroPorDefecto;
        }

        private void SelectorMesero_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListaPedidosListos != null)
                ActualizarPedidosListos();
        }

        private static PedidoCocina ConvertirPedido(PedidoModel p, string atendidoPor, EstadoPedido estado)
        {
            var productos = p.Detalles.Select(d => new LineaComanda(
                d.IdPlato.ToString(),
                d.NombrePlato,
                d.PrecioUnitario,
                d.Cantidad,
                d.NotasEspeciales));

            var pedido = new PedidoCocina(p.IdPedido, $"Mesa {p.NumeroMesa:00}", atendidoPor, productos, string.Empty, p.FechaHora);
            pedido.CambiarEstado(estado);
            return pedido;
        }

        private void ActualizarPedidosListos()
        {
            try
            {
                string mesero = ObtenerMeseroSeleccionado();
                var listos = pedidoBll.ObtenerPedidosListosParaMesero()
                    .Select(p => ConvertirPedido(p, mesero, EstadoPedido.Listo))
                    .ToList();

                ListaPedidosListos.ItemsSource = listos;
                TextoListosMesero.Text = $"LISTOS PARA RECOGER: {listos.Count}";
                MensajeSinListos.Visibility = listos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo listos: {ex.Message}");
            }
        }

        private void CargarSolicitudesClientes()
        {
            try
            {
                var solicitudes = pedidoBll.ObtenerSolicitudesClientes()
                    .Select(p => ConvertirPedido(p, "Menú web", EstadoPedido.Pendiente))
                    .ToList();

                ListaSolicitudesCliente.ItemsSource = solicitudes;
                TextoSolicitudes.Text = $"SOLICITUDES DEL CLIENTE: {solicitudes.Count}";
                MensajeSinSolicitudes.Visibility = solicitudes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                if (cantidadSolicitudes >= 0 && solicitudes.Count > cantidadSolicitudes)
                    System.Media.SystemSounds.Asterisk.Play();

                cantidadSolicitudes = solicitudes.Count;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo solicitudes: {ex.Message}");
            }
        }

        private void BtnAprobarSolicitud_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: PedidoCocina solicitud }) return;

            try
            {
                if (pedidoBll.AprobarSolicitudCliente(solicitud.Numero))
                {
                    MessageBox.Show(
                        $"{solicitud.Mesa} aprobada y enviada a cocina.",
                        "Solicitud aprobada",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Esa solicitud ya no existe.", "Solicitud", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo aprobar la solicitud:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            CargarSolicitudesClientes();
        }

        private void BtnRechazarSolicitud_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: PedidoCocina solicitud }) return;

            var respuesta = MessageBox.Show(
                $"¿Rechazar la solicitud de la {solicitud.Mesa}? El cliente verá que no fue aceptada.",
                "Rechazar solicitud",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (respuesta != MessageBoxResult.Yes) return;

            try
            {
                pedidoBll.RechazarSolicitudCliente(solicitud.Numero);
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo rechazar la solicitud:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            CargarSolicitudesClientes();
            ActualizarNumeroPedido();
        }

        private void BtnAdminPlatos_Click(object sender, RoutedEventArgs e)
        {
            var ventana = Window.GetWindow(this);
            var acceso = new VistaAccesoDueno { Owner = ventana };
            if (acceso.ShowDialog() != true || acceso.DuenoAutenticado == null)
                return;

            var admin = new VistaAdminPlatos(acceso.DuenoAutenticado) { Owner = ventana };
            admin.ShowDialog();

            if (admin.HuboCambios)
                CargarPlatosDesdeBD();
        }

        private void BtnAbrirCocina_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow ventana)
                ventana.AbrirMonitorCocina();
        }

        private void BtnEntregar_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: PedidoCocina pedido }) return;

            try
            {
                pedidoBll.ActualizarEstado(pedido.Numero, "Entregado");
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo marcar como entregado:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            ActualizarPedidosListos();
        }

        private void BtnTicketListo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: PedidoCocina pedido })
                MostrarTicket(pedido);
        }

        private void MostrarTicket(PedidoCocina pedido)
        {
            var ticket = new VistaTicket(pedido.Productos, pedido.Observaciones, pedido.Numero, pedido.Mesa, pedido.Fecha, pedido.Mesero)
            {
                Owner = Window.GetWindow(this)
            };
            ticket.ShowDialog();
        }

        private void BusquedaProductos_TextChanged(object sender, TextChangedEventArgs e)
        {
            AplicarFiltros();
        }

        private void Categoria_Checked(object sender, RoutedEventArgs e)
        {
            AplicarFiltros();
        }

        private string CategoriaSeleccionada()
        {
            foreach (var radio in new[] { CategoriaComida, CategoriaAcompanamiento, CategoriaBebida })
            {
                if (radio?.IsChecked == true) return radio.Tag?.ToString() ?? string.Empty;
            }
            return string.Empty;
        }

        private void AplicarFiltros()
        {
            if (GrillaProductos == null || BusquedaProductos == null)
                return;

            string busqueda = BusquedaProductos.Text.Trim();
            string categoria = CategoriaSeleccionada();
            int visibles = 0;

            foreach (UIElement elem in GrillaProductos.Children)
            {
                if (elem is not ToggleButton { Tag: PlatoModel plato } btn) continue;

                bool coincideTexto = busqueda.Length == 0 ||
                    cultura.CompareInfo.IndexOf(plato.Nombre, busqueda, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
                bool coincideCategoria = categoria.Length == 0 || CategoriasMenu.De(plato) == categoria;

                bool mostrar = coincideTexto && coincideCategoria;
                btn.Visibility = mostrar ? Visibility.Visible : Visibility.Collapsed;
                if (mostrar) visibles++;
            }

            TextoCantidadMenu.Text = visibles == 1 ? "1 PRODUCTO" : $"{visibles} PRODUCTOS";
            if (MensajeSinResultados != null)
                MensajeSinResultados.Visibility = visibles == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AgregarProducto(string codigo, string nombre, decimal precio)
        {
            var existente = lineas.FirstOrDefault(linea => linea.Codigo == codigo);

            if (existente != null)
            {
                CambiarCantidad(existente, existente.Cantidad + 1);
                return;
            }

            lineas.Add(new LineaComanda(codigo, nombre, precio, 1));
            ActualizarResumen();
        }

        private void SumarProducto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: LineaComanda linea })
                CambiarCantidad(linea, linea.Cantidad + 1);
        }

        private void RestarProducto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: LineaComanda linea })
                CambiarCantidad(linea, linea.Cantidad - 1);
        }

        private void CambiarCantidad(LineaComanda linea, int cantidad)
        {
            int indice = lineas.IndexOf(linea);
            if (indice < 0) return;

            if (cantidad <= 0)
                lineas.RemoveAt(indice);
            else
                lineas[indice] = new LineaComanda(linea.Codigo, linea.Nombre, linea.PrecioUnitario, cantidad, linea.Notas);

            ActualizarResumen();
        }

        private void ActualizarResumen()
        {
            int cantidad = lineas.Sum(linea => linea.Cantidad);
            decimal total = lineas.Sum(linea => linea.Subtotal);

            TextoTotal.Text = "$ " + total.ToString("N0", cultura);
            TextoCantidad.Text = cantidad == 1 ? "1 PRODUCTO" : cantidad + " PRODUCTOS";

            MensajeComandaVacia.Visibility = cantidad == 0 ? Visibility.Visible : Visibility.Collapsed;
            ListaComanda.Visibility = cantidad == 0 ? Visibility.Collapsed : Visibility.Visible;

            if (GrillaProductos == null) return;

            foreach (UIElement elem in GrillaProductos.Children)
            {
                if (elem is ToggleButton { Tag: PlatoModel plato } btn)
                    btn.IsChecked = lineas.Any(l => l.Codigo == plato.IdPlato.ToString());
            }
        }

        private void ActualizarNumeroPedido()
        {
            int siguiente = pedidoBll.SiguienteNumeroPedido();
            TextoNumeroPedido.Text = siguiente > 0 ? $"PEDIDO #{siguiente:000}" : "PEDIDO NUEVO";
        }

        private void BtnLimpiarComanda_Click(object sender, RoutedEventArgs e)
        {
            lineas.Clear();
            ObservacionesPedido.Clear();
            ActualizarResumen();
        }

        private void BtnInicio_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow ventana)
                ventana.VolverAlInicio();
        }

        private string ObtenerMesaSeleccionada()
        {
            return (SelectorMesa.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Mesa 01";
        }

        private int ObtenerNumeroMesa()
        {
            string digitos = new(ObtenerMesaSeleccionada().Where(char.IsDigit).ToArray());
            return int.TryParse(digitos, out int num) && num > 0 ? num : 1;
        }

        private void BtnEnviarCocina_Click(object sender, RoutedEventArgs e)
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

            int numeroMesa = ObtenerNumeroMesa();
            string observaciones = ObservacionesPedido.Text.Trim();

            var nuevoPedido = new PedidoModel
            {
                NumeroMesa = numeroMesa,
                FechaHora = DateTime.Now,
                Estado = "Pendiente"
            };

            foreach (var l in lineas)
            {
                int.TryParse(l.Codigo, out int idPlato);
                nuevoPedido.Detalles.Add(new DetallePedidoModel
                {
                    IdPlato = idPlato,
                    NombrePlato = l.Nombre,
                    Cantidad = l.Cantidad,
                    NotasEspeciales = l.Notas ?? string.Empty,
                    PrecioUnitario = l.PrecioUnitario
                });
            }

            if (observaciones.Length > 0)
            {
                var primero = nuevoPedido.Detalles[0];
                primero.NotasEspeciales = string.IsNullOrWhiteSpace(primero.NotasEspeciales)
                    ? $"Obs. pedido: {observaciones}"
                    : $"{primero.NotasEspeciales}. Obs. pedido: {observaciones}";
            }

            string resultado = pedidoBll.RegistrarPedido(nuevoPedido);

            if (resultado != "OK")
            {
                MessageBox.Show(
                    $"No se pudo guardar el pedido:\n{resultado}",
                    "Error de base de datos",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            ultimoPedidoEnviado = new PedidoCocina(
                nuevoPedido.IdPedido,
                ObtenerMesaSeleccionada(),
                ObtenerMeseroSeleccionado(),
                lineas,
                observaciones,
                nuevoPedido.FechaHora);

            lineas.Clear();
            ObservacionesPedido.Clear();
            ActualizarResumen();
            ActualizarNumeroPedido();

            MessageBox.Show(
                $"Pedido #{nuevoPedido.IdPedido:000} (Mesa {numeroMesa:00}) enviado a cocina.",
                "Pedido enviado",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void BtnVerTicket_Click(object sender, RoutedEventArgs e)
        {
            if (lineas.Count > 0)
            {
                int numero = pedidoBll.SiguienteNumeroPedido();
                var ticket = new VistaTicket(lineas, ObservacionesPedido.Text, numero, ObtenerMesaSeleccionada(), mesero: ObtenerMeseroSeleccionado())
                {
                    Owner = Window.GetWindow(this)
                };
                ticket.ShowDialog();
            }
            else if (ultimoPedidoEnviado != null)
            {
                MostrarTicket(ultimoPedidoEnviado);
            }
            else
            {
                MessageBox.Show(
                    "Agrega productos o envía un pedido para consultar su ticket.",
                    "Sin ticket disponible",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
    }
}
