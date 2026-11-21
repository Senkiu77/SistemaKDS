using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using KDS.PRE.Vistas;

namespace KDS.PRE.Portal
{
    public enum Antojo
    {
        Ninguno,
        Bebida,
        Picar,
        Hambre,
        Barato
    }

    public static class InterpreteColoquial
    {
        private static readonly (string Patron, string Reemplazo)[] Expresiones =
        {
            (@"\bpa['´`]?(?=\s|$)", "para"),
            (@"\bq\b", "que"),
            (@"\bxq\b|\bpq\b", "porque"),
            (@"\btmb\b|\btb\b|\btbn\b", "tambien"),
            (@"\bxfa\b|\bporfa\b|\bporfis\b", "por favor"),
            (@"\bkiero\b|\bqiero\b", "quiero"),
            (@"\bbaratic[oa]s?\b|\bbaratit[oa]s?\b", "barato"),
            (@"\bha?m?bu?rgu?e?s+(a|ita)s?\b|\bburg(u)?er(s)?\b|\bburguesa\b|\bamburguesa\b", "hamburguesa"),
            (@"\bgaseosita?s?\b|\bgaseocita?s?\b|\bgasiosa\b|\bcoca\s?cola\b|\bcocacola\b|\bpostobon\b", "gaseosa"),
            (@"\bpapita?s\b|\bpapas?\s+a\s+la\s+francesa\b", "papas"),
            (@"\blimonadit[ao]\b", "limonada"),
            (@"\bjuguit[oa]s?\b", "jugo"),
            (@"\bcervecit[ao]s?\b|\bpola(s)?\b", "cerveza"),
            (@"\bquesit[oa]\b", "queso"),
            (@"\bcebollita\b|\bcebolla\s+cabezona\b", "cebolla"),
            (@"\btomatico\b|\btomatito\b", "tomate")
        };

        private static readonly Dictionary<string, int> Numeros = new()
        {
            ["un"] = 1, ["uno"] = 1, ["una"] = 1, ["dos"] = 2, ["tres"] = 3, ["cuatro"] = 4, ["cinco"] = 5,
            ["seis"] = 6, ["siete"] = 7, ["ocho"] = 8, ["nueve"] = 9, ["diez"] = 10, ["once"] = 11,
            ["doce"] = 12, ["trece"] = 13, ["catorce"] = 14, ["quince"] = 15, ["dieciseis"] = 16,
            ["diecisiete"] = 17, ["dieciocho"] = 18, ["diecinueve"] = 19, ["veinte"] = 20,
            ["veintiuno"] = 21, ["veintiun"] = 21, ["veintidos"] = 22, ["veintitres"] = 23,
            ["veinticuatro"] = 24, ["veinticinco"] = 25, ["veintiseis"] = 26, ["veintisiete"] = 27,
            ["veintiocho"] = 28, ["veintinueve"] = 29, ["treinta"] = 30, ["cuarenta"] = 40,
            ["cincuenta"] = 50, ["sesenta"] = 60, ["setenta"] = 70, ["ochenta"] = 80, ["noventa"] = 90,
            ["cien"] = 100
        };

        private static readonly string[] PalabrasVacias =
        {
            "de", "la", "el", "los", "las", "con", "y", "en", "para", "una", "uno", "un", "del", "al"
        };

        public static string Normalizar(string texto)
        {
            string t = QuitarTildes(texto.ToLowerInvariant());
            t = Regex.Replace(t, @"([a-zñ])\1{2,}", "$1");
            foreach (var (patron, reemplazo) in Expresiones)
                t = Regex.Replace(t, patron, reemplazo);
            return Regex.Replace(t, @"\s+", " ").Trim();
        }

        public static decimal? LeerPresupuesto(string t)
        {
            var conUnidad = Regex.Match(t, @"(\d+(?:[.,]\d+)?)\s*(lucas?|barras?|mil|k|lks)\b");
            if (conUnidad.Success && decimal.TryParse(conUnidad.Groups[1].Value.Replace(',', '.'),
                    NumberStyles.Number, CultureInfo.InvariantCulture, out decimal miles))
            {
                return miles * 1000;
            }

            var completo = Regex.Match(t, @"\$?\s*(\d{1,3}(?:[.,]\d{3})+|\d{4,7})\b");
            if (completo.Success && decimal.TryParse(Regex.Replace(completo.Groups[1].Value, @"[.,]", ""), out decimal valor))
                return valor;

            var enLetras = Regex.Match(t, @"\b([a-z]+)(?:\s+y\s+([a-z]+))?\s+(lucas?|barras?|mil)\b");
            if (enLetras.Success && Numeros.TryGetValue(enLetras.Groups[1].Value, out int baseNum))
            {
                int extra = 0;
                if (enLetras.Groups[2].Success) Numeros.TryGetValue(enLetras.Groups[2].Value, out extra);
                return (baseNum + extra) * 1000m;
            }

            if (Regex.IsMatch(t, @"\b(una|un)\s+(luca|barra)\b")) return 1000;

            return null;
        }

        public static string LeerNotas(string t)
        {
            var notas = new List<string>();
            var patrones = new[]
            {
                @"\bsin\s+(?:nada\s+de\s+)?([a-zñ]+(?:\s+(?!y\b|con\b|por\b|que\b|para\b)[a-zñ]+)?)",
                @"\bno\s+(?:le\s+)?(?:eche|echen|ponga|pongan|heche|hechen|pongas|eches)\s+([a-zñ]+)",
                @"\bque\s+no\s+(?:tenga|lleve|traiga)\s+([a-zñ]+)",
                @"\bbien\s+(asad[oa]|cocid[oa]|tostad[oa]|frit[oa])",
                @"\bextra\s+([a-zñ]+)",
                @"\b(?:con\s+)?(?:harto|hartica|bastante|mucha|mucho)\s+([a-zñ]+)"
            };

            foreach (string patron in patrones)
            {
                foreach (Match m in Regex.Matches(t, patron))
                {
                    string cosa = m.Groups[1].Value.Trim();
                    if (cosa.Length < 3 || cosa is "hambre" or "plata" or "sed" or "afan") continue;

                    string nota = patron.Contains("bien") ? "bien " + cosa
                        : patron.Contains("extra") ? "extra " + cosa
                        : patron.Contains("harto") ? "con bastante " + cosa
                        : "sin " + cosa;

                    if (!notas.Contains(nota)) notas.Add(nota);
                }
            }

            return string.Join(", ", notas);
        }

        public static Antojo LeerAntojo(string t)
        {
            if (Regex.IsMatch(t, @"\b(barato|economic[oa]|poca plata|poquita plata|no tengo (mucha )?plata|estoy pelad[oa]|ando pelad[oa]|lo mas bajo)\b"))
                return Antojo.Barato;
            if (Regex.IsMatch(t, @"\b(sed|tomar|beber|bebida|refresco|gaseosa|jugo|limonada|cerveza|algo frio|algo helado)\b"))
                return Antojo.Bebida;
            if (Regex.IsMatch(t, @"\b(picar|picadita|picoteo|acompanamiento|acompanar|papas|entrada|algo ligero|algo suave)\b"))
                return Antojo.Picar;
            if (Regex.IsMatch(t, @"\b(hambre|hambriento|muerto de hambre|llenador|contundente|algo grande|bien servido|que llene|hamburguesa)\b"))
                return Antojo.Hambre;
            return Antojo.Ninguno;
        }

        public static bool EsSaludo(string t) =>
            Regex.IsMatch(t, @"^(hola|holi|buenas|buenos dias|buenas tardes|buenas noches|quiubo|quibo|que mas|q mas|ey|hey|alo)\b[\s!.,?]*$");

        public static bool EsAgradecimiento(string t) =>
            Regex.IsMatch(t, @"\b(gracias|muchas gracias|mil gracias|listo|de una|bacano|chevere|perfecto)\b") && t.Length < 40;

        public static int LeerPersonas(string t)
        {
            var m = Regex.Match(t, @"\b(?:somos|para)\s+(\d+|[a-z]+)\s*(?:personas|amigos|parceros|ninos|adultos)?\b");
            if (!m.Success) return 1;
            string v = m.Groups[1].Value;
            if (int.TryParse(v, out int n) || Numeros.TryGetValue(v, out n))
                return n is > 1 and <= 10 ? n : 1;
            return 1;
        }

        public static List<PlatoModel> BuscarPlatosNombrados(string t, List<PlatoModel> menu)
        {
            var palabrasCliente = Palabras(t);
            var encontrados = new List<(PlatoModel Plato, int Coincidencias)>();

            foreach (var plato in menu)
            {
                var palabrasPlato = Palabras(Normalizar(plato.Nombre));
                if (palabrasPlato.Count == 0) continue;

                int coincidencias = palabrasPlato.Count(pp => palabrasCliente.Any(pc => SePareceA(pc, pp)));
                bool nombreCompleto = coincidencias == palabrasPlato.Count;
                bool palabraFuerte = palabrasPlato.Any(pp => pp.Length >= 5 && palabrasCliente.Any(pc => SePareceA(pc, pp)));

                if (nombreCompleto || palabraFuerte)
                    encontrados.Add((plato, coincidencias));
            }

            if (encontrados.Count == 0) return new();
            int mejor = encontrados.Max(e => e.Coincidencias);
            return encontrados.Where(e => e.Coincidencias == mejor).Select(e => e.Plato).ToList();
        }

        private static List<string> Palabras(string texto)
        {
            return Regex.Matches(texto, @"[a-zñ]+")
                .Select(m => m.Value)
                .Where(p => p.Length >= 3 && !PalabrasVacias.Contains(p))
                .ToList();
        }

        private static bool SePareceA(string a, string b)
        {
            if (a == b) return true;
            if (a.Length < 4 || b.Length < 4) return false;
            if (a.TrimEnd('s') == b.TrimEnd('s')) return true;
            int tolerancia = Math.Max(a.Length, b.Length) >= 7 ? 2 : 1;
            return Math.Abs(a.Length - b.Length) <= tolerancia && Distancia(a, b) <= tolerancia;
        }

        private static int Distancia(string a, string b)
        {
            var previa = new int[b.Length + 1];
            var actual = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) previa[j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                actual[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int costo = a[i - 1] == b[j - 1] ? 0 : 1;
                    actual[j] = Math.Min(Math.Min(actual[j - 1] + 1, previa[j] + 1), previa[j - 1] + costo);
                }
                (previa, actual) = (actual, previa);
            }

            return previa[b.Length];
        }

        private static string QuitarTildes(string texto)
        {
            var sb = new StringBuilder();
            foreach (char c in texto.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark || c == '̃')
                    sb.Append(c);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
