using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace KDS.PRE.Vistas
{
    public class KdsAccesoDueno
    {
        private const int Iteraciones = 100_000;
        private const int IntentosPermitidos = 5;
        private static int intentosFallidos;
        private static DateTime bloqueadoHasta = DateTime.MinValue;

        public List<UsuarioModel> ObtenerDuenos() => KdsDatosService.ObtenerDuenos();

        public static bool TieneClave(UsuarioModel dueno) => !string.IsNullOrEmpty(dueno.ClaveHash);

        public string CrearClave(UsuarioModel dueno, string clave, string confirmacion)
        {
            if (clave.Length < 4) return "La clave debe tener al menos 4 caracteres.";
            if (clave != confirmacion) return "Las dos claves no son iguales.";

            try
            {
                string hash = Proteger(clave);
                if (!KdsDatosService.GuardarClaveDueno(dueno.IdUsuario, hash)) return "No se pudo guardar la clave.";
                dueno.ClaveHash = hash;
                return "OK";
            }
            catch (Exception ex) { return "Error BD: " + ex.Message; }
        }

        public string Entrar(UsuarioModel dueno, string clave)
        {
            int segundos = (int)Math.Ceiling((bloqueadoHasta - DateTime.Now).TotalSeconds);
            if (segundos > 0) return $"Demasiados intentos. Espera {segundos} segundos.";

            if (TieneClave(dueno) && Coincide(clave, dueno.ClaveHash!))
            {
                intentosFallidos = 0;
                return "OK";
            }

            intentosFallidos++;
            if (intentosFallidos >= IntentosPermitidos)
            {
                intentosFallidos = 0;
                bloqueadoHasta = DateTime.Now.AddSeconds(30);
                return "Clave incorrecta. Por seguridad hay que esperar 30 segundos.";
            }
            int quedan = IntentosPermitidos - intentosFallidos;
            return quedan == 1 ? "Clave incorrecta. Te queda 1 intento." : $"Clave incorrecta. Te quedan {quedan} intentos.";
        }

        private static string Proteger(string clave)
        {
            byte[] sal = RandomNumberGenerator.GetBytes(16);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(clave), sal, Iteraciones, HashAlgorithmName.SHA256, 32);
            return $"pbkdf2${Iteraciones}${Convert.ToBase64String(sal)}${Convert.ToBase64String(hash)}";
        }

        private static bool Coincide(string clave, string guardado)
        {
            var partes = guardado.Split('$');
            if (partes.Length != 4 || partes[0] != "pbkdf2" || !int.TryParse(partes[1], out int iteraciones)) return false;

            byte[] sal = Convert.FromBase64String(partes[2]);
            byte[] esperado = Convert.FromBase64String(partes[3]);
            byte[] calculado = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(clave), sal, iteraciones, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
    }
}
