using System.Linq;
using KDS.PRE.Vistas;

namespace KDS.PRE.Portal
{
    public static class CategoriasMenu
    {
        public const string Comida = "Comida";
        public const string Bebida = "Bebida";
        public const string Acompanamiento = "Acompañamiento";

        public static readonly string[] Todas = { Comida, Acompanamiento, Bebida };

        public static bool EsValida(string? categoria) => categoria != null && Todas.Contains(categoria);

        public static string De(PlatoModel plato) =>
            EsValida(plato.Categoria) ? plato.Categoria : Sugerir(plato.Nombre);

        public static string Sugerir(string nombre)
        {
            string n = nombre.ToUpperInvariant();

            if (Contiene(n, "LIMONADA", "BEBIDA", "GASEOSA", "JUGO", "AGUA", "CERVEZA", "CAFÉ", "CAFE", "MALTEADA", "SODA",
                    "COCA", "COLA", "PEPSI", "SPRITE", "POSTOB", "TINTO", "CHOCOLATE", "AVENA", "VINO", "MILO", "NECTAR", "TÉ HELADO"))
                return Bebida;
            if (Contiene(n, "PAPAS", "PAPITAS", "YUCA", "AROS", "ENSALADA", "PATACÓN", "PATACON", "NUGGETS",
                    "EXTRA", "ADICIONAL", "QUESO", "TOCINETA", "SALSA"))
                return Acompanamiento;
            return Comida;
        }

        public static string Icono(PlatoModel plato)
        {
            string n = plato.Nombre.ToUpperInvariant();
            if (n.Contains("PIZZA")) return "🍕";
            if (n.Contains("PASTA") || n.Contains("LASAGNA")) return "🍝";
            if (n.Contains("POLLO") || n.Contains("NUGGETS")) return "🍗";
            if (n.Contains("COSTILLA")) return "🍖";
            if (n.Contains("QUESO")) return "🧀";

            return De(plato) switch
            {
                Bebida => "🥤",
                Acompanamiento => "🍟",
                _ => "🍔"
            };
        }

        private static bool Contiene(string texto, params string[] palabras) => palabras.Any(texto.Contains);
    }
}
