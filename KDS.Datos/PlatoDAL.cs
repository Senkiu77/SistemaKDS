using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using KDS.Entidades;

namespace KDS.Datos
{
    public class PlatoDAL
    {
        public List<Plato> ObtenerPlatosActivos()
        {
            List<Plato> lista = new List<Plato>();
            string sql = "SELECT id_plato, nombre, precio, activo FROM platos WHERE activo = true ORDER BY nombre;";

            using (NpgsqlConnection con = ConexionBD.ObtenerConexion())
            {
                using (NpgsqlCommand cmd = new NpgsqlCommand(sql, con))
                {
                    con.Open();
                    using (NpgsqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            lista.Add(new Plato
                            {
                                IdPlato = Convert.ToInt32(dr["id_plato"]),
                                Nombre = dr["nombre"].ToString(),
                                Precio = Convert.ToDecimal(dr["precio"]),
                                Activo = Convert.ToBoolean(dr["activo"])
                            });
                        }
                    }
                }
            }
            return lista;
        }
    }
}
