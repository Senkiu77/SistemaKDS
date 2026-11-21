using System;
using System.Collections.Generic;
using Npgsql;
using KDS.Entidades;

namespace KDS.Datos
{
    public class PlatoDAL
    {
        public List<Plato> ObtenerPlatosActivos()
        {
            return ConsultarPlatos("SELECT id_plato, nombre, precio, activo FROM platos WHERE activo = true ORDER BY nombre;");
        }

        public List<Plato> ObtenerTodosLosPlatos()
        {
            return ConsultarPlatos("SELECT id_plato, nombre, precio, activo FROM platos ORDER BY id_plato ASC;");
        }

        public bool InsertarPlato(Plato plato)
        {
            string sql = "INSERT INTO platos (nombre, precio, activo) VALUES (@nombre, @precio, @activo);";
            using (NpgsqlConnection con = ConexionBD.ObtenerConexion())
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@nombre", plato.Nombre.Trim());
                cmd.Parameters.AddWithValue("@precio", plato.Precio);
                cmd.Parameters.AddWithValue("@activo", plato.Activo);
                con.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public bool ActualizarPlato(Plato plato)
        {
            string sql = "UPDATE platos SET nombre = @nombre, precio = @precio, activo = @activo WHERE id_plato = @id;";
            using (NpgsqlConnection con = ConexionBD.ObtenerConexion())
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@nombre", plato.Nombre.Trim());
                cmd.Parameters.AddWithValue("@precio", plato.Precio);
                cmd.Parameters.AddWithValue("@activo", plato.Activo);
                cmd.Parameters.AddWithValue("@id", plato.IdPlato);
                con.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public bool EliminarPlato(int idPlato)
        {
            using (NpgsqlConnection con = ConexionBD.ObtenerConexion())
            {
                con.Open();

                long usos;
                using (NpgsqlCommand cmdUso = new NpgsqlCommand("SELECT COUNT(*) FROM detalle_pedidos WHERE id_plato = @id;", con))
                {
                    cmdUso.Parameters.AddWithValue("@id", idPlato);
                    usos = Convert.ToInt64(cmdUso.ExecuteScalar());
                }

                string sql = usos > 0
                    ? "UPDATE platos SET activo = false WHERE id_plato = @id;"
                    : "DELETE FROM platos WHERE id_plato = @id;";

                using (NpgsqlCommand cmd = new NpgsqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@id", idPlato);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        private List<Plato> ConsultarPlatos(string sql)
        {
            List<Plato> lista = new List<Plato>();
            using (NpgsqlConnection con = ConexionBD.ObtenerConexion())
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
            return lista;
        }
    }
}
