using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;

namespace KDS.Datos
{
    public class ConexionBD
    {
        private static readonly string cadena = "Host=localhost;Port=5432;Username=postgres;Password=1234;Database=bd_kds;";

        public static NpgsqlConnection ObtenerConexion()
        {
            return new NpgsqlConnection(cadena);
        }
    }
}
