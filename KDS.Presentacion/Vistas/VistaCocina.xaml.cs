using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace KDS.PRE.Vistas;

public partial class VistaCocina : UserControl
{
    private readonly DispatcherTimer actualizador = new()
    {
        Interval = TimeSpan.FromSeconds(3)
    };

    public VistaCocina()
    {
        InitializeComponent();

        actualizador.Tick += (_, _) => ActualizarTiempos();

        Loaded += Vista_Loaded;
        Unloaded += Vista_Unloaded;

        ActualizarPantalla();
    }

    private void Vista_Loaded(object sender, RoutedEventArgs e)
    {
        PedidosEnMemoria.Cambio -= Pedidos_Cambio;
        PedidosEnMemoria.Cambio += Pedidos_Cambio;

        ActualizarPantalla();
        actualizador.Start();
    }

    private void Vista_Unloaded(object sender, RoutedEventArgs e)
    {
        actualizador.Stop();
        PedidosEnMemoria.Cambio -= Pedidos_Cambio;
    }

    private void Pedidos_Cambio(object? sender, EventArgs e)
    {
        ActualizarPantalla();
    }

    private void ActualizarTiempos()
    {
        foreach (var pedido in PedidosEnMemoria.EnCocina)
            pedido.ActualizarTiempo();
    }

    private void ActualizarPantalla()
    {
        var pedidos = PedidosEnMemoria.EnCocina.ToList();

        ListaPedidos.ItemsSource = pedidos;

        TextoPendientes.Text = "● PENDIENTES: " +
            pedidos.Count(p => p.Estado == EstadoPedido.Pendiente);

        TextoPreparacion.Text = "● EN PREPARACIÓN: " +
            pedidos.Count(p => p.Estado == EstadoPedido.EnPreparacion);

        TextoListos.Text =
            "● LISTOS EN MESERO: " + PedidosEnMemoria.Listos.Count;

        TextoTotalPedidos.Text = pedidos.Count == 1
            ? "1 PEDIDO"
            : pedidos.Count + " PEDIDOS";

        MensajeSinPedidos.Visibility = pedidos.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        ScrollPedidos.Visibility = pedidos.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;

        ActualizarTiempos();
    }

    private void BtnAvanzarEstado_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is Button { Tag: PedidoCocina pedido })
            PedidosEnMemoria.Avanzar(pedido);
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