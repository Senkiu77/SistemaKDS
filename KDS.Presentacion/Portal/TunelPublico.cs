using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace KDS.PRE.Portal
{
    public class TunelPublico
    {
        private static readonly Regex EnlaceCloudflare = new(@"https://[a-z0-9-]+\.trycloudflare\.com", RegexOptions.Compiled);

        private Process? proceso;

        public string? Enlace { get; private set; }
        public string Modo { get; private set; } = "Sin túnel";
        public bool EsPermanente { get; private set; }
        public string? Error { get; private set; }

        public event Action? Cambio;

        public void Iniciar(int puerto)
        {
            string? fijo = Environment.GetEnvironmentVariable("KDS_LINK_PUBLICO");
            string? dominioNgrok = Environment.GetEnvironmentVariable("KDS_NGROK_DOMINIO");

            try
            {
                if (!string.IsNullOrWhiteSpace(fijo))
                {
                    Modo = "Enlace propio";
                    EsPermanente = true;
                    Enlace = LimpiarEnlace(fijo);
                    Cambio?.Invoke();
                }
                else if (!string.IsNullOrWhiteSpace(dominioNgrok))
                {
                    IniciarNgrok(LimpiarEnlace(dominioNgrok), puerto);
                }
                else if (BuscarPrograma("cloudflared") is string cloudflared)
                {
                    IniciarCloudflare(cloudflared, puerto);
                }
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                Cambio?.Invoke();
            }
        }

        public void Detener()
        {
            var abierto = proceso;
            proceso = null;
            try
            {
                if (abierto is { HasExited: false }) abierto.Kill(true);
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void IniciarNgrok(string enlace, int puerto)
        {
            Modo = "ngrok";
            EsPermanente = true;

            string carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SistemaKDS");
            Directory.CreateDirectory(carpeta);
            string politica = Path.Combine(carpeta, "ngrok-politica.yml");
            File.WriteAllText(politica,
                "on_http_request:\n" +
                "  - actions:\n" +
                "      - type: add-headers\n" +
                "        config:\n" +
                "          headers:\n" +
                $"            host: localhost:{puerto}\n");

            string ngrok = BuscarPrograma("ngrok")
                ?? throw new FileNotFoundException("No encontré ngrok. Instálalo con: winget install ngrok.ngrok");
            proceso = Lanzar(ngrok, $"http {puerto} --url {enlace} --traffic-policy-file \"{politica}\" --log stdout");

            _ = Task.Run(async () =>
            {
                await Task.Delay(4000);
                if (proceso == null) return;
                if (proceso.HasExited)
                {
                    Error = "ngrok se cerró. Revisa que hayas puesto el authtoken (ngrok config add-authtoken ...) y que el dominio sea el tuyo.";
                }
                else
                {
                    Enlace = enlace;
                }
                Cambio?.Invoke();
            });
        }

        private void IniciarCloudflare(string cloudflared, int puerto)
        {
            Modo = "Cloudflare rápido";
            EsPermanente = false;
            proceso = Lanzar(cloudflared, $"tunnel --url http://localhost:{puerto} --http-host-header localhost:{puerto}");
        }

        private Process Lanzar(string programa, string argumentos)
        {
            var p = new Process
            {
                StartInfo = new ProcessStartInfo(programa, argumentos)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            p.OutputDataReceived += (_, e) => LeerLinea(e.Data);
            p.ErrorDataReceived += (_, e) => LeerLinea(e.Data);
            p.Exited += (_, _) =>
            {
                if (proceso != p) return;
                Enlace = null;
                Error ??= $"{Path.GetFileNameWithoutExtension(programa)} se cerró.";
                Cambio?.Invoke();
            };

            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            return p;
        }

        private void LeerLinea(string? linea)
        {
            if (linea == null || Enlace != null || Modo != "Cloudflare rápido") return;

            var m = EnlaceCloudflare.Match(linea);
            if (m.Success)
            {
                Enlace = m.Value;
                Cambio?.Invoke();
            }
        }

        private static string LimpiarEnlace(string texto)
        {
            string t = texto.Trim().TrimEnd('/');
            return t.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? t : "https://" + t;
        }

        private static string? BuscarPrograma(string programa)
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var carpetas = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator).ToList();
            carpetas.Add(Path.Combine(local, "Microsoft", "WinGet", "Links"));
            string paquetes = Path.Combine(local, "Microsoft", "WinGet", "Packages");
            if (Directory.Exists(paquetes))
                carpetas.AddRange(Directory.GetDirectories(paquetes, "*" + programa + "*"));
            carpetas.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), programa));
            carpetas.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), programa));

            foreach (string carpeta in carpetas.Where(c => !string.IsNullOrWhiteSpace(c)))
            {
                foreach (string nombre in new[] { programa + ".exe", programa })
                {
                    string ruta = Path.Combine(carpeta.Trim(), nombre);
                    if (File.Exists(ruta)) return ruta;
                }
            }
            return null;
        }
    }
}
