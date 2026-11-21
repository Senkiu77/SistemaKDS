using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace KDS.PRE.Vistas;

public enum EstadoPedido
{
    Pendiente,
    EnPreparacion,
    Listo,
    Entregado
}

public sealed class PedidoCocina : INotifyPropertyChanged
{
    public int Numero { get; }
    public string Mesa { get; }
    public string Mesero { get; }
    public DateTime Fecha { get; }
    public IReadOnlyList<LineaComanda> Productos { get; }
    public string Observaciones { get; }

    public EstadoPedido Estado { get; private set; } =
        EstadoPedido.Pendiente;

    public string NumeroTexto => $"PEDIDO #{Numero:000}";

    public decimal Total => Productos.Sum(p => p.Subtotal);

    public string TotalTexto =>
        "$ " + Total.ToString(
            "N0",
            CultureInfo.GetCultureInfo("es-CO"));

    public string Resumen => $"#{Numero:000} · {Mesa}";

    public string DetalleEntrega =>
        $"{Mesero} · {Productos.Sum(p => p.Cantidad)} productos · {TotalTexto}";

    public string ProductosTexto =>
        string.Join("\n", Productos.Select(p =>
            p.TieneNotas ? $"{p.Cantidad} × {p.Nombre} ({p.Notas})" : $"{p.Cantidad} × {p.Nombre}"));

    public int MinutosEspera =>
        Math.Max(0, (int)(DateTime.Now - Fecha).TotalMinutes);

    public string TiempoTexto =>
        $"Espera: {MinutosEspera} min · {SemaforoTexto}";

    public string SemaforoTexto =>
        MinutosEspera < 5
            ? "A tiempo"
            : MinutosEspera < 10
                ? "En atención"
                : "Demorado";

    public string ColorPrincipal =>
        MinutosEspera < 5
            ? "#39D98A"
            : MinutosEspera < 10
                ? "#F5A623"
                : "#FF6464";

    public string ColorFondo =>
        MinutosEspera < 5
            ? "#183C2A"
            : MinutosEspera < 10
                ? "#30271B"
                : "#3C1D1D";

    public string ColorEstado => Estado switch
    {
        EstadoPedido.Pendiente => "#F5A623",
        EstadoPedido.EnPreparacion => "#4DA3FF",
        _ => "#39D98A"
    };

    public string EstadoTexto => Estado switch
    {
        EstadoPedido.Pendiente => "● PENDIENTE",
        EstadoPedido.EnPreparacion => "● PREPARANDO",
        EstadoPedido.Listo => "● LISTO PARA ENTREGAR",
        _ => "● ENTREGADO"
    };

    public string AccionTexto =>
        Estado == EstadoPedido.Pendiente
            ? "INICIAR PREPARACIÓN"
            : "MARCAR COMO LISTO";

    internal PedidoCocina(
        int numero,
        string mesa,
        string mesero,
        IEnumerable<LineaComanda> productos,
        string observaciones,
        DateTime? fecha = null)
    {
        Numero = numero;
        Mesa = mesa;
        Mesero = mesero;
        Fecha = fecha ?? DateTime.Now;
        Observaciones = observaciones.Trim();

        Productos = productos
            .Select(p => new LineaComanda(
                p.Codigo,
                p.Nombre,
                p.PrecioUnitario,
                p.Cantidad,
                p.Notas))
            .ToList()
            .AsReadOnly();
    }

    internal void CambiarEstado(EstadoPedido estado)
    {
        Estado = estado;

        foreach (string nombre in new[]
        {
            nameof(Estado),
            nameof(EstadoTexto),
            nameof(ColorEstado),
            nameof(AccionTexto)
        })
        {
            OnPropertyChanged(nombre);
        }
    }

    public void ActualizarTiempo()
    {
        foreach (string nombre in new[]
        {
            nameof(MinutosEspera),
            nameof(TiempoTexto),
            nameof(SemaforoTexto),
            nameof(ColorPrincipal),
            nameof(ColorFondo)
        })
        {
            OnPropertyChanged(nombre);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? nombre = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nombre));
    }
}
