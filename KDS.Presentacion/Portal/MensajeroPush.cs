using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace KDS.PRE.Portal
{
    public record ClavesVapid(string Publica, string Privada);

    public class EnvioPushFallido : Exception
    {
        public HttpStatusCode Estado { get; }

        public EnvioPushFallido(HttpStatusCode estado, string detalle)
            : base($"El servicio de notificaciones respondió {(int)estado}: {detalle}")
        {
            Estado = estado;
        }
    }

    public class MensajeroPush
    {
        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

        private readonly ClavesVapid claves;
        private readonly string contacto;

        public MensajeroPush(ClavesVapid claves, string contacto)
        {
            this.claves = claves;
            this.contacto = contacto;
        }

        public static ClavesVapid GenerarClaves()
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var p = ecdsa.ExportParameters(true);
            return new ClavesVapid(Base64Url(PuntoPublico(p)), Base64Url(p.D!));
        }

        public async Task EnviarAsync(SuscripcionBeeper destino, string contenido)
        {
            byte[] cuerpo = Cifrar(Encoding.UTF8.GetBytes(contenido), DeBase64Url(destino.P256dh), DeBase64Url(destino.Auth));

            using var solicitud = new HttpRequestMessage(HttpMethod.Post, destino.Endpoint)
            {
                Content = new ByteArrayContent(cuerpo)
            };
            solicitud.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            solicitud.Content.Headers.ContentEncoding.Add("aes128gcm");
            solicitud.Headers.TryAddWithoutValidation("TTL", "3600");
            solicitud.Headers.TryAddWithoutValidation("Urgency", "high");
            solicitud.Headers.TryAddWithoutValidation("Authorization", $"vapid t={FirmarJwt(destino.Endpoint)}, k={claves.Publica}");

            using var respuesta = await Http.SendAsync(solicitud);
            if (!respuesta.IsSuccessStatusCode)
                throw new EnvioPushFallido(respuesta.StatusCode, await respuesta.Content.ReadAsStringAsync());
        }

        public static byte[] Cifrar(byte[] mensaje, byte[] clavePublicaCelular, byte[] secretoAuth)
        {
            using var celular = ECDiffieHellman.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = clavePublicaCelular[1..33], Y = clavePublicaCelular[33..65] }
            });
            using var servidor = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            byte[] publicaServidor = PuntoPublico(servidor.ExportParameters(false));
            byte[] secretoCompartido = servidor.DeriveRawSecretAgreement(celular.PublicKey);

            byte[] infoClave = Unir(Encoding.ASCII.GetBytes("WebPush: info\0"), clavePublicaCelular, publicaServidor);
            byte[] ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, secretoCompartido, 32, secretoAuth, infoClave);

            byte[] sal = RandomNumberGenerator.GetBytes(16);
            byte[] claveContenido = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, sal, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
            byte[] nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, sal, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

            byte[] claro = Unir(mensaje, new byte[] { 2 });
            byte[] cifrado = new byte[claro.Length];
            byte[] etiqueta = new byte[16];
            using (var aes = new AesGcm(claveContenido, 16))
                aes.Encrypt(nonce, claro, cifrado, etiqueta);

            byte[] tamanoRegistro = { 0, 0, 16, 0 };
            return Unir(sal, tamanoRegistro, new[] { (byte)publicaServidor.Length }, publicaServidor, cifrado, etiqueta);
        }

        private string FirmarJwt(string endpoint)
        {
            var uri = new Uri(endpoint);
            string audiencia = uri.GetLeftPart(UriPartial.Authority);
            long vence = DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds();

            string encabezado = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { typ = "JWT", alg = "ES256" }));
            string datos = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { aud = audiencia, exp = vence, sub = contacto }));
            string sinFirma = encabezado + "." + datos;

            byte[] publica = DeBase64Url(claves.Publica);
            using var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                D = DeBase64Url(claves.Privada),
                Q = new ECPoint { X = publica[1..33], Y = publica[33..65] }
            });
            byte[] firma = ecdsa.SignData(Encoding.ASCII.GetBytes(sinFirma), HashAlgorithmName.SHA256);
            return sinFirma + "." + Base64Url(firma);
        }

        private static byte[] PuntoPublico(ECParameters p) => Unir(new byte[] { 4 }, p.Q.X!, p.Q.Y!);

        private static byte[] Unir(params byte[][] partes) => partes.SelectMany(p => p).ToArray();

        public static string Base64Url(byte[] datos) =>
            Convert.ToBase64String(datos).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public static byte[] DeBase64Url(string texto)
        {
            string b = texto.Replace('-', '+').Replace('_', '/');
            return Convert.FromBase64String(b.PadRight(b.Length + (4 - b.Length % 4) % 4, '='));
        }
    }
}
