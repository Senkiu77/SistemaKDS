using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using KDS.PRE.Vistas;

namespace KDS.PRE.Portal
{
    public class PortalClienteService
    {
        private const int MaxMesas = 50;
        private const int MaxCantidadPorPlato = 20;

        private static readonly JsonSerializerOptions Json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private readonly KdsPedidoBLL pedidoBll = new();
        private readonly AsistenteMenu asistente = new();
        private readonly BeeperService beeper = new();
        private readonly Dictionary<string, (string Contenido, string Tipo)> archivos = new();
        private HttpListener? listener;
        private bool escuchandoEstados;

        public string Direccion { get; private set; } = string.Empty;
        public bool Activo => listener?.IsListening == true;
        public bool UsaClaude => asistente.UsaClaude;
        public bool BeeperConPush => beeper.Disponible;
        public string? Error { get; private set; }

        public void IniciarServidor(string url = "http://localhost:8080/")
        {
            if (Activo) return;

            try
            {
                CargarArchivos();
                beeper.Preparar();

                if (!escuchandoEstados)
                {
                    KdsPedidoBLL.EstadoCambiado += (id, estado) => _ = beeper.AvisarCambioAsync(id, estado);
                    escuchandoEstados = true;
                }

                listener = new HttpListener();
                listener.Prefixes.Add(url);
                listener.Start();
                Direccion = url;
                Error = null;
                _ = Task.Run(EscucharPeticiones);
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                listener = null;
            }
        }

        public void Detener()
        {
            try
            {
                listener?.Stop();
                listener?.Close();
            }
            catch (ObjectDisposedException)
            {
            }
            listener = null;
        }

        private async Task EscucharPeticiones()
        {
            while (listener?.IsListening == true)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await listener.GetContextAsync();
                }
                catch (Exception) when (listener?.IsListening != true)
                {
                    return;
                }
                catch (HttpListenerException)
                {
                    continue;
                }

                _ = Task.Run(() => AtenderAsync(ctx));
            }
        }

        private async Task AtenderAsync(HttpListenerContext ctx)
        {
            var req = ctx.Request;
            string ruta = req.Url?.AbsolutePath.TrimEnd('/').ToLowerInvariant() ?? string.Empty;

            try
            {
                switch (req.HttpMethod, ruta)
                {
                    case ("GET", ""):
                        await ResponderArchivo(ctx, "/index.html");
                        break;
                    case ("GET", "/index.html"):
                    case ("GET", "/sw.js"):
                    case ("GET", "/manifest.json"):
                    case ("GET", "/icono.svg"):
                        await ResponderArchivo(ctx, ruta);
                        break;
                    case ("GET", "/api/push/clave"):
                        await ResponderJson(ctx, 200, new { disponible = beeper.Disponible, clave = beeper.ClavePublica });
                        break;
                    case ("POST", "/api/push/suscribir"):
                        await SuscribirBeeper(ctx);
                        break;
                    case ("GET", "/api/menu"):
                        await ResponderJson(ctx, 200, ArmarMenu());
                        break;
                    case ("GET", "/api/info"):
                        await ResponderJson(ctx, 200, new { iaClaude = asistente.UsaClaude });
                        break;
                    case ("GET", "/api/estado"):
                        await ConsultarEstado(ctx);
                        break;
                    case ("POST", "/api/bot"):
                        await AtenderBot(ctx);
                        break;
                    case ("POST", "/api/solicitud"):
                        await RegistrarSolicitud(ctx);
                        break;
                    case ("GET", "/favicon.ico"):
                        ctx.Response.StatusCode = 204;
                        ctx.Response.Close();
                        break;
                    default:
                        await ResponderJson(ctx, 404, new { error = "No encontramos esa página." });
                        break;
                }
            }
            catch (JsonException)
            {
                await ResponderJson(ctx, 400, new { error = "La solicitud llegó incompleta." });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Portal: " + ex.Message);
                await ResponderJson(ctx, 500, new { error = "No pudimos conectarnos con el restaurante. Intenta de nuevo." });
            }
        }

        private object ArmarMenu()
        {
            return pedidoBll.ObtenerMenu().Select(p => new
            {
                id = p.IdPlato,
                nombre = p.Nombre,
                precio = p.Precio,
                categoria = CategoriasMenu.De(p),
                icono = CategoriasMenu.Icono(p)
            });
        }

        private async Task AtenderBot(HttpListenerContext ctx)
        {
            var datos = await LeerCuerpo<PeticionBot>(ctx);
            string mensaje = (datos?.Mensaje ?? string.Empty).Trim();

            if (mensaje.Length == 0)
            {
                await ResponderJson(ctx, 400, new { error = "Escribe una pregunta para el asistente." });
                return;
            }

            if (mensaje.Length > 500) mensaje = mensaje[..500];

            int mesa = Math.Clamp(datos!.Mesa, 0, MaxMesas);
            var historial = (datos.Historial ?? new())
                .OfType<ItemHistorial>()
                .Select(h => new MensajeChat(h.Rol == "bot" ? "bot" : "cliente", (h.Texto ?? string.Empty).Trim()))
                .ToList();

            var menu = pedidoBll.ObtenerMenu();
            var r = await asistente.ResponderAsync(mensaje, historial, menu, mesa);

            await ResponderJson(ctx, 200, new
            {
                respuesta = r.Respuesta,
                nota = r.Nota,
                fuente = r.Fuente,
                sugeridos = r.Sugeridos.Select(p => new { id = p.IdPlato, nombre = p.Nombre, precio = p.Precio })
            });
        }

        private async Task RegistrarSolicitud(HttpListenerContext ctx)
        {
            var datos = await LeerCuerpo<PeticionSolicitud>(ctx);

            if (datos == null || datos.Mesa < 1 || datos.Mesa > MaxMesas)
            {
                await ResponderJson(ctx, 400, new { error = "Selecciona una mesa válida." });
                return;
            }

            var menu = pedidoBll.ObtenerMenu().ToDictionary(p => p.IdPlato);
            var pedido = new PedidoModel { NumeroMesa = datos.Mesa };

            foreach (var item in datos.Items ?? new())
            {
                if (item == null || !menu.TryGetValue(item.IdPlato, out var plato)) continue;
                if (item.Cantidad < 1 || item.Cantidad > MaxCantidadPorPlato) continue;

                string nota = (item.Nota ?? string.Empty).Trim();
                if (nota.Length > 200) nota = nota[..200];

                pedido.Detalles.Add(new DetallePedidoModel
                {
                    IdPlato = plato.IdPlato,
                    NombrePlato = plato.Nombre,
                    PrecioUnitario = plato.Precio,
                    Cantidad = item.Cantidad,
                    NotasEspeciales = nota
                });
            }

            if (pedido.Detalles.Count == 0)
            {
                await ResponderJson(ctx, 400, new { error = "Tu carrito está vacío o los platos ya no están disponibles." });
                return;
            }

            string resultado = pedidoBll.RegistrarSolicitudCliente(pedido);
            if (resultado != "OK")
            {
                await ResponderJson(ctx, 500, new { error = resultado });
                return;
            }

            await ResponderJson(ctx, 200, new
            {
                idPedido = pedido.IdPedido,
                mensaje = $"Listo, tu pedido de la mesa {pedido.NumeroMesa} le llegó al mesero."
            });
        }

        private async Task ConsultarEstado(HttpListenerContext ctx)
        {
            int.TryParse(ctx.Request.QueryString["id"], out int id);
            string? estado = pedidoBll.ConsultarEstado(id);

            await ResponderJson(ctx, 200, new { estado = estado ?? "Rechazado" });
        }

        private async Task SuscribirBeeper(HttpListenerContext ctx)
        {
            var datos = await LeerCuerpo<PeticionSuscripcion>(ctx);

            bool valida = datos != null
                && Uri.TryCreate(datos.Endpoint, UriKind.Absolute, out var destino)
                && destino.Scheme == Uri.UriSchemeHttps
                && !string.IsNullOrWhiteSpace(datos.Keys?.P256dh)
                && !string.IsNullOrWhiteSpace(datos.Keys?.Auth);

            if (!valida || !beeper.Disponible)
            {
                await ResponderJson(ctx, 400, new { error = "Este celular no permite avisos push; deja la página abierta y te avisamos igual." });
                return;
            }

            string? estado = pedidoBll.ConsultarEstado(datos!.IdPedido);
            if (estado == null || estado == "Entregado")
            {
                await ResponderJson(ctx, 404, new { error = "Ese pedido ya no está activo." });
                return;
            }

            beeper.Suscribir(datos.IdPedido, new SuscripcionBeeper(datos.Endpoint!, datos.Keys!.P256dh!, datos.Keys.Auth!));
            await ResponderJson(ctx, 200, new { ok = true, estado });
        }

        private Task ResponderArchivo(HttpListenerContext ctx, string ruta)
        {
            var (contenido, tipo) = archivos[ruta];
            if (ruta == "/sw.js") ctx.Response.Headers["Service-Worker-Allowed"] = "/";
            return Responder(ctx, 200, contenido, tipo);
        }

        private static async Task<T?> LeerCuerpo<T>(HttpListenerContext ctx)
        {
            if (ctx.Request.ContentLength64 > 64 * 1024)
                throw new JsonException("Cuerpo demasiado grande");

            using var lector = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
            string cuerpo = await lector.ReadToEndAsync();
            return string.IsNullOrWhiteSpace(cuerpo) ? default : JsonSerializer.Deserialize<T>(cuerpo, Json);
        }

        private static Task ResponderJson(HttpListenerContext ctx, int estado, object datos)
        {
            return Responder(ctx, estado, JsonSerializer.Serialize(datos, Json), "application/json; charset=utf-8");
        }

        private static async Task Responder(HttpListenerContext ctx, int estado, string contenido, string tipo)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(contenido);
                ctx.Response.StatusCode = estado;
                ctx.Response.ContentType = tipo;
                ctx.Response.Headers["Cache-Control"] = "no-store";
                ctx.Response.ContentLength64 = bytes.Length;
                await ctx.Response.OutputStream.WriteAsync(bytes);
                ctx.Response.Close();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or IOException)
            {
            }
        }

        private void CargarArchivos()
        {
            archivos["/index.html"] = (LeerRecurso("menu.html"), "text/html; charset=utf-8");
            archivos["/sw.js"] = (LeerRecurso("sw.js"), "application/javascript; charset=utf-8");
            archivos["/manifest.json"] = (LeerRecurso("manifest.json"), "application/manifest+json; charset=utf-8");
            archivos["/icono.svg"] = (LeerRecurso("icono.svg"), "image/svg+xml");
        }

        private static string LeerRecurso(string nombre)
        {
            var ensamblado = Assembly.GetExecutingAssembly();
            using var recurso = ensamblado.GetManifestResourceStream("KDS.PRE.Portal." + nombre)
                ?? throw new InvalidOperationException($"No se encontró {nombre} dentro del programa.");
            using var lector = new StreamReader(recurso, Encoding.UTF8);
            return lector.ReadToEnd();
        }

        private class PeticionBot
        {
            public string? Mensaje { get; set; }
            public int Mesa { get; set; }
            public List<ItemHistorial?>? Historial { get; set; }
        }

        private class ItemHistorial
        {
            public string? Rol { get; set; }
            public string? Texto { get; set; }
        }

        private class PeticionSolicitud
        {
            public int Mesa { get; set; }
            public List<ItemSolicitud?>? Items { get; set; }
        }

        private class ItemSolicitud
        {
            public int IdPlato { get; set; }
            public int Cantidad { get; set; }
            public string? Nota { get; set; }
        }

        private class PeticionSuscripcion
        {
            public int IdPedido { get; set; }
            public string? Endpoint { get; set; }
            public ClavesSuscripcion? Keys { get; set; }
        }

        private class ClavesSuscripcion
        {
            public string? P256dh { get; set; }
            public string? Auth { get; set; }
        }
    }
}
