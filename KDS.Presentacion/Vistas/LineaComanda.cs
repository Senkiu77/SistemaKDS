using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace KDS.PRE.Vistas;

public sealed class LineaComanda : INotifyPropertyChanged
{
    private static readonly CultureInfo Cultura =
        CultureInfo.GetCultureInfo("es-CO");

    private string notas;

    public string Codigo { get; }
    public string Nombre { get; }
    public decimal PrecioUnitario { get; }
    public int Cantidad { get; }

    public decimal Subtotal => PrecioUnitario * Cantidad;

    public string PrecioUnitarioTexto =>
        "$ " + PrecioUnitario.ToString("N0", Cultura) + " c/u";

    public string SubtotalTexto =>
        "$ " + Subtotal.ToString("N0", Cultura);

    public string Notas
    {
        get => notas;
        set
        {
            if (notas == value)
                return;

            notas = value ?? string.Empty;

            OnPropertyChanged();
            OnPropertyChanged(nameof(TieneNotas));
        }
    }

    public bool TieneNotas => !string.IsNullOrWhiteSpace(Notas);

    public LineaComanda(
        string codigo,
        string nombre,
        decimal precioUnitario,
        int cantidad,
        string notas = "")
    {
        Codigo = codigo;
        Nombre = nombre;
        PrecioUnitario = precioUnitario;
        Cantidad = cantidad;
        this.notas = notas;
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