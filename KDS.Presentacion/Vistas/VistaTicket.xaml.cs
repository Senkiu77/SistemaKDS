using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace KDS.PRE.Vistas
{
    public partial class VistaTicket : Window
    {
        private readonly List<LineaComanda> detalle;
        private readonly string observacionesPedido;
        private readonly DateTime fechaTicket;
        private readonly int numeroPedido;
        private readonly string mesaPedido;
        private readonly string meseroPedido;

        private readonly CultureInfo cultura =
            CultureInfo.GetCultureInfo("es-CO");

        public VistaTicket()
            : this(Array.Empty<LineaComanda>(), string.Empty, 1)
        {
        }

        public VistaTicket(
            IEnumerable<LineaComanda> productos,
            string observaciones,
            int numero = 1,
            string mesa = "Mesa 01",
            DateTime? fecha = null,
            string mesero = "Carlos Mesero")
        {
            InitializeComponent();

            numeroPedido = numero;
            TextoNumeroTicket.Text = $"Pedido: #{numeroPedido:000}";

            meseroPedido = mesero;
            TextoMeseroTicket.Text = "Atendido por: " + meseroPedido;

            mesaPedido = mesa;
            TextoMesaTicket.Text = mesaPedido;

            detalle = productos
                .Select(linea => new LineaComanda(
                    linea.Codigo,
                    linea.Nombre,
                    linea.PrecioUnitario,
                    linea.Cantidad,
                    linea.Notas))
                .ToList();

            observacionesPedido =
                (observaciones ?? string.Empty).Trim();

            fechaTicket = fecha ?? DateTime.Now;

            ProductosTicket.ItemsSource = detalle;

            TextoFecha.Text = fechaTicket.ToString(
                "dd/MM/yyyy HH:mm",
                cultura);

            TotalTicket.Text = FormatearDinero(
                detalle.Sum(linea => linea.Subtotal));

            CantidadTicket.Text = "Cantidad de productos: " +
                detalle.Sum(linea => linea.Cantidad);

            ObservacionesTicket.Text = observacionesPedido;

            PanelObservaciones.Visibility =
                string.IsNullOrWhiteSpace(observacionesPedido)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
        }

        private string FormatearDinero(decimal valor)
        {
            return "$ " + valor.ToString("N0", cultura);
        }

        private void BtnImprimir_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (detalle.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "El ticket no tiene productos para imprimir.",
                    "Ticket vacío",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                var dialogo = new PrintDialog();

                if (dialogo.ShowDialog() != true)
                    return;

                var documento = CrearDocumentoImprimible(
                    dialogo.PrintableAreaWidth,
                    dialogo.PrintableAreaHeight);

                var paginador =
                    ((IDocumentPaginatorSource)documento)
                    .DocumentPaginator;

                dialogo.PrintDocument(
                    paginador,
                    $"KDS - Resumen del pedido #{numeroPedido:000}");
            }
            catch (Exception error)
            {
                MessageBox.Show(
                    this,
                    "No fue posible enviar el ticket a impresión.\n\n" +
                    error.Message,
                    "Error de impresión",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private FlowDocument CrearDocumentoImprimible(
            double ancho,
            double alto)
        {
            var documento = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                Foreground = Brushes.Black,
                Background = Brushes.White,
                PageWidth = ancho,
                PageHeight = alto,
                PagePadding = new Thickness(16),
                ColumnWidth = ancho
            };

            documento.Blocks.Add(CrearParrafo(
                "KDS", 24, true, TextAlignment.Center));

            documento.Blocks.Add(CrearParrafo(
                "RESUMEN DEL PEDIDO", 12, true, TextAlignment.Center));

            documento.Blocks.Add(CrearParrafo(
                fechaTicket.ToString("dd/MM/yyyy HH:mm", cultura),
                10, false, TextAlignment.Center));

            documento.Blocks.Add(CrearParrafo(
                $"Pedido: #{numeroPedido:000}    |    {mesaPedido}",
                11, true));

            documento.Blocks.Add(CrearParrafo(
                "Atendido por: " + meseroPedido, 10));

            foreach (var linea in detalle)
            {
                documento.Blocks.Add(CrearParrafo(
                    linea.Cantidad + " × " + linea.Nombre,
                    11, true));

                documento.Blocks.Add(CrearParrafo(
                    "Unidad: " + FormatearDinero(linea.PrecioUnitario) +
                    "\nSubtotal: " + FormatearDinero(linea.Subtotal),
                    10));

                if (linea.TieneNotas)
                {
                    documento.Blocks.Add(CrearParrafo(
                        "Nota: " + linea.Notas, 10));
                }
            }

            documento.Blocks.Add(CrearParrafo(
                "TOTAL: " +
                FormatearDinero(detalle.Sum(linea => linea.Subtotal)),
                16, true));

            documento.Blocks.Add(CrearParrafo(
                "Cantidad de productos: " +
                detalle.Sum(linea => linea.Cantidad),
                10));

            if (!string.IsNullOrWhiteSpace(observacionesPedido))
            {
                documento.Blocks.Add(CrearParrafo(
                    "OBSERVACIONES", 11, true));

                documento.Blocks.Add(CrearParrafo(
                    observacionesPedido, 10));
            }

            documento.Blocks.Add(CrearParrafo(
                "¡Gracias por tu pedido!",
                12, true, TextAlignment.Center));

            documento.Blocks.Add(CrearParrafo(
                "Este resumen no es una factura de venta.",
                9, false, TextAlignment.Center));

            return documento;
        }

        private static Paragraph CrearParrafo(
            string texto,
            double tamaño,
            bool negrita = false,
            TextAlignment alineacion = TextAlignment.Left)
        {
            return new Paragraph(new Run(texto))
            {
                FontSize = tamaño,
                FontWeight = negrita
                    ? FontWeights.Bold
                    : FontWeights.Normal,
                TextAlignment = alineacion,
                Margin = new Thickness(0, 0, 0, 10)
            };
        }
    }
}