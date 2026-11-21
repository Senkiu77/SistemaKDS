using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace KDS.PRE.Vistas;

public partial class VistaCocina : UserControl
{
    private readonly KdsPedidoBLL pedidoBll = new();
    private readonly DispatcherTimer actualizador = new()
    {
        Interval = TimeSpan.FromSeconds(3)
    };

    public VistaCocina()
    {
        InitializeComponent();

        actualizador.Tick += (_, _) => ActualizarPantallaDesdeBD();

        Loaded += Vista_Loaded;
        Unloaded += Vista_Unloaded;

        ActualizarPantallaDesdeBD();
    }

    private void Vista_Loaded(object sender, RoutedEventArgs e)
    {
        ActualizarPantallaDesdeBD();
        actualizador.Start();
    }

    private void Vista_Unloaded(object sender, RoutedEventArgs e)
    {
        actualizador.Stop();
    }

    private void ActualizarPantallaDesdeBD()
    {
        try
        {
            List<PedidoModel> pedidosBD = pedidoBll.ProcesarColaCocina();

            var pedidosVisuales = new List<PedidoCocina>();

            foreach (var p in pedidosBD)
            {
                var lineas = p.Detalles.Select(d => new LineaComanda(
                    d.IdPlato.ToString(),
                    d.NombrePlato,
                    d.PrecioUnitario,
                    d.Cantidad,
                    d.NotasEspeciales)).ToList();

                var estadoVisual = p.Estado == "Preparando"
                    ? EstadoPedido.EnPreparacion
                    : EstadoPedido.Pendiente;

                var pedCocina = new PedidoCocina(
                    p.IdPedido,
                    $"Mesa {p.NumeroMesa:00}",
                    "Mesero",
                    lineas,
                    "",
                    p.FechaHora);

                pedCocina.CambiarEstado(estadoVisual);
                pedidosVisuales.Add(pedCocina);
            }

            ListaPedidos.ItemsSource = pedidosVisuales;

            TextoPendientes.Text = "● PENDIENTES: " +
                pedidosVisuales.Count(p => p.Estado == EstadoPedido.Pendiente);

            TextoPreparacion.Text = "● EN PREPARACIÓN: " +
                pedidosVisuales.Count(p => p.Estado == EstadoPedido.EnPreparacion);

            var listosBD = pedidoBll.ObtenerPedidosListosParaMesero();
            TextoListos.Text = "● LISTOS: " + listosBD.Count;

            TextoTotalPedidos.Text = pedidosVisuales.Count == 1
                ? "1 PEDIDO"
                : pedidosVisuales.Count + " PEDIDOS";

            MensajeSinPedidos.Visibility = pedidosVisuales.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            ScrollPedidos.Visibility = pedidosVisuales.Count == 0
                ? Visibility.Collapsed
                : Visibility.Visible;

            foreach (var p in pedidosVisuales)
            {
                p.ActualizarTiempo();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error actualizando cocina: {ex.Message}");
        }
    }

    private void BtnAvanzarEstado_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is Button { Tag: PedidoCocina pedido })
        {
            try
            {
                string nuevoEstado = pedido.Estado == EstadoPedido.Pendiente
                    ? "Preparando"
                    : "Listo";

                pedidoBll.ActualizarEstado(pedido.Numero, nuevoEstado);
                ActualizarPantallaDesdeBD();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al avanzar estado: {ex.Message}");
            }
        }
    }

    private void BtnVerTicket_Click(
        object sender,
        RoutedEventArgs e)
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

    private void BtnInicio_Click(
        object sender,
        RoutedEventArgs e)
    {
        var ventana = Window.GetWindow(this);

        if (ventana is KDS.PRE.MainWindow principal)
            principal.VolverAlInicio();
        else
            ventana?.Close();
    }
}
