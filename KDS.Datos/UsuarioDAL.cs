using KDS.Entidades;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDS.Datos
{
    public class UsuarioDAL
    {
        public List<Usuario> ObtenerMeseros()
        {
            List<Usuario> lista = new List<Usuario>();
            string sql = "SELECT id_usuario, nombre, rol FROM usuarios WHERE rol = 'Mesero' ORDER BY nombre;";

            using (NpgsqlConnection con = ConexionBD.ObtenerConexion())
            {
                using (NpgsqlCommand cmd = new NpgsqlCommand(sql, con))
                {
                    con.Open();
                    using (NpgsqlDataReader dr = cmd.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            lista.Add(new Usuario
                            {
                                IdUsuario = Convert.ToInt32(dr["id_usuario"]),
                                Nombre = dr["nombre"].ToString(),
                                Rol = dr["rol"].ToString()
                            });
                        }
                    }
                }
            }
            return lista;
        }
    }
}
