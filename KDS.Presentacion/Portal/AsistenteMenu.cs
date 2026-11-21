using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Models.Beta;
using Anthropic.Models.Beta.Messages;
using KDS.PRE.Vistas;

namespace KDS.PRE.Portal
{
    public record MensajeChat(string Rol, string Texto);

    public record RespuestaBot(string Respuesta, List<PlatoModel> Sugeridos, string Nota, string Fuente);

    public class AsistenteMenu
    {
        private const string Modelo = "claude-opus-5-5";
        private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CO");

        private readonly AnthropicClient? cliente;

        public AsistenteMenu()
        {
            string? clave = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (!string.IsNullOrWhiteSpace(clave))
            {
                cliente = new AnthropicClient { ApiKey = clave };
            }
        }

        public bool UsaClaude => cliente != null;

        public async Task<RespuestaBot> ResponderAsync(string mensaje, IReadOnlyList<MensajeChat> historial, List<PlatoModel> menu, int mesa)
        {
            if (cliente != null)
            {
                try
                {
                    return await PreguntarAClaudeAsync(mensaje, historial, menu, mesa);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Claude no respondió: " + ex.Message);
                }
            }

            return ResponderSinConexion(mensaje, menu);
        }

        private async Task<RespuestaBot> PreguntarAClaudeAsync(string mensaje, IReadOnlyList<MensajeChat> historial, List<PlatoModel> menu, int mesa)
        {
            var carta = new StringBuilder();
            foreach (var p in menu)
            {
                carta.AppendLine($"- id {p.IdPlato}: {p.Nombre} [{CategoriasMenu.De(p)}] (${p.Precio.ToString("N0", Cultura)} COP)");
            }

            string sistema =
                "Eres el mesero virtual de un restaurante colombiano y atiendes a un cliente desde su celular. " +
                $"El cliente está en la mesa {mesa}. Responde en español, en tono cercano y en máximo tres frases cortas. " +
                "Solo puedes recomendar platos de esta carta, con sus precios reales en pesos colombianos:\n" +
                carta +
                "Si el cliente menciona un presupuesto, no sugieras combinaciones que lo superen y di cuánto suman. " +
                "Si pide quitar o cambiar un ingrediente (por ejemplo 'sin cebolla'), escribe esa indicación corta en nota_cocina " +
                "para que viaje con el pedido; si no hay indicación, deja nota_cocina vacía. " +
                "En ids_sugeridos pon solo ids de la carta que recomiendas en esta respuesta. " +
                "No inventes ingredientes ni platos; si no sabes algo de un plato, dilo y ofrece preguntarle al mesero. " +
                "El cliente suele escribir informal, con jerga colombiana, abreviaturas, sin tildes o con errores de ortografía; " +
                "interpreta lo que quiere decir y no le corrijas la forma de escribir. Por ejemplo: 'lucas', 'barras' o 'mil' son miles de pesos " +
                "('25 lucas' = $25.000), 'pa'' es 'para', 'algo pa' la sed' es una bebida, 'algo pa' picar' es un acompañamiento, " +
                "'estoy pelado' significa que tiene poca plata, 'somos 3' significa que el presupuesto se reparte entre tres, " +
                "los diminutivos ('una gaseosita', 'unas papitas') se refieren al plato normal, y 'q', 'xq', 'tmb', 'porfa' son abreviaturas. " +
                "Si el mensaje es ambiguo, haz una sola pregunta corta. Contesta con calidez y naturalidad colombiana, sin exagerar la jerga.";

            var mensajes = new List<BetaMessageParam>();
            foreach (var m in historial.TakeLast(10))
            {
                if (string.IsNullOrWhiteSpace(m.Texto)) continue;
                if (m.Rol == "bot")
                    mensajes.Add(new BetaAssistantMessageParam { Content = m.Texto });
                else
                    mensajes.Add(new BetaUserMessageParam { Content = m.Texto });
            }
            mensajes.Add(new BetaUserMessageParam { Content = mensaje });

            while (mensajes.Count > 1 && mensajes[0].Role != "user")
                mensajes.RemoveAt(0);

            var esquema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
            {
              "type": "object",
              "properties": {
                "respuesta": { "type": "string" },
                "ids_sugeridos": { "type": "array", "items": { "type": "integer" } },
                "nota_cocina": { "type": "string" }
              },
              "required": ["respuesta", "ids_sugeridos", "nota_cocina"],
              "additionalProperties": false
            }
            """)!;

            var parametros = new MessageCreateParams
            {
                Model = Modelo,
                MaxTokens = 2000,
                Betas = [AnthropicBeta.ServerSideFallback2026_07_01],
                Fallbacks = new Default(),
                System = sistema,
                Messages = mensajes,
                OutputConfig = new BetaOutputConfig
                {
                    Effort = Effort.Low,
                    Format = new BetaJsonOutputFormat { Schema = esquema }
                }
            };

            BetaMessage respuesta = await cliente!.Beta.Messages.Create(parametros);

            if (respuesta.StopReason == "refusal")
            {
                return new RespuestaBot("Esa pregunta no la puedo responder, pero con gusto te ayudo a escoger algo de la carta.", new(), "", "claude");
            }

            string json = string.Concat(respuesta.Content
                .Select(b => b.TryPickText(out var t) ? t.Text : string.Empty));

            using var doc = JsonDocument.Parse(json);
            var raiz = doc.RootElement;

            var ids = raiz.GetProperty("ids_sugeridos").EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.Number)
                .Select(e => e.GetInt32())
                .ToHashSet();

            return new RespuestaBot(
                raiz.GetProperty("respuesta").GetString() ?? string.Empty,
                menu.Where(p => ids.Contains(p.IdPlato)).ToList(),
                raiz.GetProperty("nota_cocina").GetString() ?? string.Empty,
                "claude");
        }

        public static RespuestaBot ResponderSinConexion(string mensaje, List<PlatoModel> menu)
        {
            string texto = InterpreteColoquial.Normalizar(mensaje);
            string nota = InterpreteColoquial.LeerNotas(texto);
            string avisoNota = nota.Length > 0 ? $" Le dejo dicho a cocina: {nota}." : string.Empty;

            if (menu.Count == 0)
                return new RespuestaBot("Uy, en este momento no hay platos disponibles. Llama al mesero y él te ayuda.", new(), nota, "local");

            if (InterpreteColoquial.EsSaludo(texto))
                return new RespuestaBot("¡Quiubo! Cuéntame cuánto quieres gastar o qué se te antoja (algo pa' la sed, pa' picar o bien llenador) y te ayudo.", new(), nota, "local");

            if (InterpreteColoquial.EsAgradecimiento(texto) && nota.Length == 0)
                return new RespuestaBot("¡Con gusto! Cuando tengas todo, toca «Ver pedido» y se lo mandamos al mesero.", new(), nota, "local");

            decimal? presupuesto = InterpreteColoquial.LeerPresupuesto(texto);
            int personas = InterpreteColoquial.LeerPersonas(texto);
            var nombrados = InterpreteColoquial.BuscarPlatosNombrados(texto, menu);

            if (nombrados.Count > 0 && presupuesto == null)
            {
                bool variosDistintos = nombrados.Select(p => CategoriasMenu.De(p)).Distinct().Count() == nombrados.Count;
                string cuales = Enumerar(nombrados.Take(3).ToList(), variosDistintos ? "y" : "o");
                string frase = nombrados.Count == 1
                    ? $"¡De una! {cuales}. Tócalo abajo y te lo agrego al pedido."
                    : variosDistintos
                        ? $"¡De una! {cuales}. Tócalos abajo y te los agrego al pedido."
                        : $"Tengo {cuales}. ¿Cuál te provoca?";
                return new RespuestaBot(frase + avisoNota, nombrados.Take(3).ToList(), nota, "local");
            }

            var antojo = InterpreteColoquial.LeerAntojo(texto);
            var candidatos = FiltrarPorAntojo(antojo, menu);

            if (presupuesto is decimal total)
            {
                decimal tope = personas > 1 ? Math.Floor(total / personas) : total;
                string porPersona = personas > 1 ? $" (unos {Pesos(tope)} por persona)" : string.Empty;
                var alcanza = candidatos.Where(p => p.Precio <= tope).OrderByDescending(p => p.Precio).ToList();

                if (alcanza.Count == 0)
                {
                    var masBarato = menu.OrderBy(p => p.Precio).First();
                    return new RespuestaBot(
                        $"Con {Pesos(total)}{porPersona} no alcanza pa' ningún plato, lo más económico es {masBarato.Nombre} a {Pesos(masBarato.Precio)}.",
                        new() { masBarato }, nota, "local");
                }

                var combo = antojo is Antojo.Ninguno or Antojo.Hambre ? MejorPareja(FiltrarPorAntojo(Antojo.Ninguno, menu).Where(p => p.Precio <= tope).ToList(), tope) : null;
                if (combo != null)
                {
                    var (a, b) = combo.Value;
                    return new RespuestaBot(
                        $"Con {Pesos(total)}{porPersona} te alcanza pa' {a.Nombre} y {b.Nombre}, en total {Pesos(a.Precio + b.Precio)}" +
                        (personas > 1 ? " cada uno." : ".") + avisoNota,
                        new() { a, b }, nota, "local");
                }

                var opciones = alcanza.Take(3).ToList();
                return new RespuestaBot($"Con {Pesos(total)}{porPersona} te recomiendo {Enumerar(opciones)}.{avisoNota}", opciones, nota, "local");
            }

            List<PlatoModel> sugeridos;
            string respuesta;

            switch (antojo)
            {
                case Antojo.Barato:
                    sugeridos = menu.OrderBy(p => p.Precio).Take(3).ToList();
                    respuesta = $"Lo más económico es {Enumerar(sugeridos)}.";
                    break;
                case Antojo.Bebida:
                    sugeridos = candidatos.OrderBy(p => p.Precio).Take(3).ToList();
                    respuesta = $"Pa' la sed tengo {Enumerar(sugeridos)}.";
                    break;
                case Antojo.Picar:
                    sugeridos = candidatos.OrderBy(p => p.Precio).Take(3).ToList();
                    respuesta = $"Pa' picar te va bien {Enumerar(sugeridos)}.";
                    break;
                case Antojo.Hambre:
                    sugeridos = candidatos.OrderByDescending(p => p.Precio).Take(2).ToList();
                    respuesta = $"Si vienes con hambre, lo más llenador es {Enumerar(sugeridos)}.";
                    break;
                default:
                    sugeridos = candidatos.OrderByDescending(p => p.Precio).Take(3).ToList();
                    respuesta = $"Te puedo recomendar {Enumerar(sugeridos)}. Si me dices cuánto quieres gastar (por ejemplo «tengo 25 lucas») te armo algo a la medida.";
                    break;
            }

            if (personas > 1)
                respuesta += $" Como son {personas}, agrega {personas} de cada uno.";

            return new RespuestaBot(respuesta + avisoNota, sugeridos, nota, "local");
        }

        private static List<PlatoModel> FiltrarPorAntojo(Antojo antojo, List<PlatoModel> menu)
        {
            string? categoria = antojo switch
            {
                Antojo.Bebida => CategoriasMenu.Bebida,
                Antojo.Picar => CategoriasMenu.Acompanamiento,
                Antojo.Hambre => CategoriasMenu.Comida,
                _ => null
            };

            if (categoria == null) return menu;

            var filtrados = menu.Where(p => CategoriasMenu.De(p) == categoria).ToList();
            return filtrados.Count > 0 ? filtrados : menu;
        }

        private static (PlatoModel, PlatoModel)? MejorPareja(List<PlatoModel> platos, decimal tope)
        {
            (PlatoModel, PlatoModel)? mejor = null;
            decimal mejorSuma = 0;

            for (int i = 0; i < platos.Count; i++)
            {
                for (int j = i + 1; j < platos.Count; j++)
                {
                    decimal suma = platos[i].Precio + platos[j].Precio;
                    bool distintos = CategoriasMenu.De(platos[i]) != CategoriasMenu.De(platos[j]);
                    if (suma <= tope && distintos && suma > mejorSuma)
                    {
                        mejorSuma = suma;
                        mejor = (platos[i], platos[j]);
                    }
                }
            }

            return mejor;
        }

        private static string Enumerar(List<PlatoModel> platos, string union = "o")
        {
            var partes = platos.Select(p => $"{p.Nombre} ({Pesos(p.Precio)})").ToList();
            return partes.Count switch
            {
                0 => "algo de la carta",
                1 => partes[0],
                _ => string.Join(", ", partes.Take(partes.Count - 1)) + $" {union} " + partes[^1]
            };
        }

        private static string Pesos(decimal valor) => "$" + valor.ToString("N0", Cultura);
    }
}
