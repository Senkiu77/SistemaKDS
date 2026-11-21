using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace KDS.PRE.Portal
{
    public record SuscripcionBeeper(string Endpoint, string P256dh, string Auth);

    public class BeeperService
    {
        private const string Contacto = "https://github.com/Senkiu77/SistemaKDS";

        private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

        private readonly string carpeta;
        private readonly string archivoSuscripciones;
        private readonly ConcurrentDictionary<int, List<SuscripcionBeeper>> suscripciones = new();
        private readonly object candadoArchivo = new();
        private ClavesVapid? vapid;
        private MensajeroPush? mensajero;

        public BeeperService(string? carpetaDatos = null)
        {
            carpeta = carpetaDatos ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SistemaKDS");
            archivoSuscripciones = Path.Combine(carpeta, "beeper.json");
        }

        public string ClavePublica => vapid?.Publica ?? string.Empty;
        public bool Disponible => vapid != null;

        public void Preparar()
        {
            try
            {
                Directory.CreateDirectory(carpeta);
                vapid = CargarOGenerarClaves();
                mensajero = new MensajeroPush(vapid, Contacto);
                CargarSuscripciones();
            }
            catch (Exception ex)
            {
                vapid = null;
                mensajero = null;
                System.Diagnostics.Debug.WriteLine("Beeper sin notificaciones push: " + ex.Message);
            }
        }

        public void Suscribir(int idPedido, SuscripcionBeeper suscripcion)
        {
            var lista = suscripciones.GetOrAdd(idPedido, _ => new List<SuscripcionBeeper>());
            lock (lista)
            {
                lista.RemoveAll(s => s.Endpoint == suscripcion.Endpoint);
                lista.Add(suscripcion);
            }
            GuardarSuscripciones();
        }

        public async Task AvisarCambioAsync(int idPedido, string estado)
        {
            if (mensajero == null || !suscripciones.TryGetValue(idPedido, out var lista)) return;

            var aviso = estado switch
            {
                "Listo" => new { titulo = "¡Tu pedido está listo! 🍽️", cuerpo = $"Pedido #{idPedido}: acércate a recogerlo o espera al mesero.", tipo = "listo" },
                "Pendiente" => new { titulo = "Pedido aprobado ✅", cuerpo = $"El mesero aprobó tu pedido #{idPedido} y ya está en cocina.", tipo = "aprobado" },
                "Preparando" => new { titulo = "Estamos cocinando 👨‍🍳", cuerpo = $"Tu pedido #{idPedido} ya se está preparando.", tipo = "preparando" },
                "Rechazado" => new { titulo = "Pedido no aceptado", cuerpo = $"El mesero no pudo aceptar el pedido #{idPedido}. Llámalo para ayudarte.", tipo = "rechazado" },
                _ => null
            };

            if (aviso == null)
            {
                if (estado == "Entregado") Olvidar(idPedido);
                return;
            }

            string contenido = JsonSerializer.Serialize(new { aviso.titulo, aviso.cuerpo, aviso.tipo, pedido = idPedido, url = "/" });

            SuscripcionBeeper[] destinos;
            lock (lista) destinos = lista.ToArray();

            var vencidas = new List<string>();
            foreach (var s in destinos)
            {
                try
                {
                    await mensajero.EnviarAsync(s, contenido);
                }
                catch (EnvioPushFallido ex) when (ex.Estado is HttpStatusCode.Gone or HttpStatusCode.NotFound)
                {
                    vencidas.Add(s.Endpoint);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("No se pudo avisar al celular: " + ex.Message);
                }
            }

            bool terminado = estado == "Rechazado";
            if (terminado)
            {
                suscripciones.TryRemove(idPedido, out _);
            }
            else if (vencidas.Count > 0)
            {
                lock (lista) lista.RemoveAll(s => vencidas.Contains(s.Endpoint));
            }

            if (terminado || vencidas.Count > 0) GuardarSuscripciones();
        }

        public void Olvidar(int idPedido)
        {
            if (suscripciones.TryRemove(idPedido, out _)) GuardarSuscripciones();
        }

        private ClavesVapid CargarOGenerarClaves()
        {
            string archivo = Path.Combine(carpeta, "vapid.json");

            if (File.Exists(archivo))
            {
                var guardadas = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(archivo));
                if (guardadas != null && guardadas.TryGetValue("publica", out var pub) && guardadas.TryGetValue("privada", out var priv))
                    return new ClavesVapid(pub, priv);
            }

            var nuevas = MensajeroPush.GenerarClaves();
            File.WriteAllText(archivo, JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["publica"] = nuevas.Publica,
                ["privada"] = nuevas.Privada
            }, Json));

            return nuevas;
        }

        private void CargarSuscripciones()
        {
            if (!File.Exists(archivoSuscripciones)) return;

            var guardadas = JsonSerializer.Deserialize<Dictionary<int, List<SuscripcionBeeper>>>(File.ReadAllText(archivoSuscripciones));
            if (guardadas == null) return;

            foreach (var (id, lista) in guardadas)
                suscripciones[id] = lista;
        }

        private void GuardarSuscripciones()
        {
            try
            {
                var copia = suscripciones.ToDictionary(par => par.Key, par =>
                {
                    lock (par.Value) return par.Value.ToList();
                });

                lock (candadoArchivo)
                    File.WriteAllText(archivoSuscripciones, JsonSerializer.Serialize(copia, Json));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("No se pudo guardar beeper.json: " + ex.Message);
            }
        }
    }
}
