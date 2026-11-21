using System;
using System.Collections.Generic;
using Npgsql;
using KDS.Entidades;

namespace KDS.Datos
{
    public class PedidoDAL
    {
        private const string ColumnasPedido = "id_pedido, numero_mesa, fecha_hora, estado, notificado_voz";

        public bool InsertarPedido(Pedido nuevoPedido)
        {
            using (NpgsqlConnection con = ConexionBD.ObtenerConexion())
            {
                con.Open();
                using (NpgsqlTransaction trans = con.BeginTransaction())
                {
                    try
                    {
                        string sqlPedido = @"INSERT INTO pedidos (numero_mesa, fecha_hora, estado, notificado_voz)
                                             VALUES (@mesa, @fecha, @estado, FALSE)
                                             RETURNING id_pedido;";

                        int idGenerado;
                        using (NpgsqlCommand cmdPedido = new NpgsqlCommand(sqlPedido, con, trans))
                        {
                            cmdPedido.Parameters.AddWithValue("@mesa", nuevoPedido.NumeroMesa);
                            cmdPedido.Parameters.AddWithValue("@fecha", DateTime.Now);
                            cmdPedido.Parameters.AddWithValue("@estado", string.IsNullOrEmpty(nuevoPedido.Estado) ? "Pendiente" : nuevoPedido.Estado);
                            idGenerado = Convert.ToInt32(cmdPedido.ExecuteScalar());
                        }

                        string sqlDetalle = @"INSERT INTO detalle_pedidos (id_pedido, id_plato, cantidad, notas_especiales)
                                              VALUES (@idPedido, @idPlato, @cantidad, @notas);";

                        foreach (DetallePedido det in nuevoPedido.Detalles)
                        {
                            using (NpgsqlCommand cmdDet = new NpgsqlCommand(sqlDetalle, con, trans))
                            {
                                cmdDet.Parameters.AddWithValue("@idPedido", idGenerado);
                                cmdDet.Parameters.AddWithValue("@idPlato", det.IdPlato);
                                cmdDet.Parameters.AddWithValue("@cantidad", det.Cantidad);
                                cmdDet.Parameters.AddWithValue("@notas", string.IsNullOrWhiteSpace(det.NotasEspeciales) ? (object)DBNull.Value : det.NotasEspeciales);
                                cmdDet.ExecuteNonQuery();
                            }
                        }

                        trans.Commit();
                        nuevoPedido.IdPedido = idGenerado;
                        return true;
                    }
                    catch (Exception)
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        public List<Pedido> ObtenerPedidosCocina()
        {
            return ConsultarPedidos("WHERE estado IN ('Pendiente', 'Preparando')");
        }

        public List<Pedido> ObtenerPedidosListosParaMesero()
        {
            return ConsultarPedidos("WHERE estado = 'Listo'");
        }

        public List<Pedido> ObtenerSolicitudesClientes()
        {
            return ConsultarPedidos("WHERE estado = 'Solicitado'");
        }

        public string ObtenerEstadoPedido(int idPedido)
        {
            string sql = "SELECT estado FROM pedidos WHERE id_pedido = @id;";
            using (NpgsqlConnection con = ConexionBD.ObtenerConexion())
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@id", idPedido);
                con.Open();
                object estado = cmd.ExecuteScalar();
                return estado == null || estado == DBNull.Value ? null : estado.ToString();
            }
        }

        public bool MarcarComoNotificado(int idPedido)
        {
            string sql = "UPDATE pedidos SET notificado_voz = true WHERE id_pedido = @id;";
            using (NpgsqlConnection con = ConexionBD.ObtenerConexion())
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@id", idPedido);
                con.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public bool CambiarEstadoPedido(int idPedido, string nuevoEstado)
        {
            string sql = "UPDATE pedidos SET estado = @estado WHERE id_pedido = @id;";
            using (NpgsqlConnection con = ConexionBD.ObtenerConexion())
            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@estado", nuevoEstado);
                cmd.Parameters.AddWithValue("@id", idPedido);
                con.Open();
                return cmd.ExecuteNonQuery() > 0;
            }
        }

        public bool EliminarPedido(int idPedido)
        {
            using (NpgsqlConnection con = ConexionBD.ObtenerConexion())
            {
                con.Open();
                using (NpgsqlTransaction trans = con.BeginTransaction())
                {
                    try
                    {
                        using (NpgsqlCommand cmdDet = new NpgsqlCommand("DELETE FROM detalle_pedidos WHERE id_pedido = @id;", con, trans))
                        {
                            cmdDet.Parameters.AddWithValue("@id", idPedido);
                            cmdDet.ExecuteNonQuery();
                        }

                        int borrados;
                        using (NpgsqlCommand cmd = new NpgsqlCommand("DELETE FROM pedidos WHERE id_pedido = @id;", con, trans))
                        {
                            cmd.Parameters.AddWithValue("@id", idPedido);
                            borrados = cmd.ExecuteNonQuery();
                        }

                        trans.Commit();
                        return borrados > 0;
                    }
                    catch (Exception)
                    {
                        trans.Rollback();
                        throw;
                    }
                }
            }
        }

        private List<Pedido> ConsultarPedidos(string filtro)
        {
            List<Pedido> pedidos = new List<Pedido>();
            string sql = "SELECT " + ColumnasPedido + " FROM pedidos " + filtro + " ORDER BY fecha_hora ASC;";

            using (NpgsqlConnection con = ConexionBD.ObtenerConexion())
            {
                con.Open();
                using (NpgsqlCommand cmd = new NpgsqlCommand(sql, con))
                using (NpgsqlDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        pedidos.Add(new Pedido
                        {
                            IdPedido = Convert.ToInt32(dr["id_pedido"]),
                            NumeroMesa = Convert.ToInt32(dr["numero_mesa"]),
                            FechaHora = Convert.ToDateTime(dr["fecha_hora"]),
                            Estado = dr["estado"].ToString(),
                            NotificadoVoz = Convert.ToBoolean(dr["notificado_voz"])
                        });
                    }
                }

                foreach (Pedido p in pedidos)
                {
                    p.Detalles = ObtenerDetallesPorPedido(p.IdPedido, con);
                }
            }
            return pedidos;
        }

        private List<DetallePedido> ObtenerDetallesPorPedido(int idPedido, NpgsqlConnection con)
        {
            List<DetallePedido> detalles = new List<DetallePedido>();
            string sql = @"SELECT d.id_detalle, d.id_pedido, d.id_plato, pl.nombre AS nombre_plato, d.cantidad, d.notas_especiales
                           FROM detalle_pedidos d
                           INNER JOIN platos pl ON d.id_plato = pl.id_plato
                           WHERE d.id_pedido = @idPedido;";

            using (NpgsqlCommand cmd = new NpgsqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@idPedido", idPedido);
                using (NpgsqlDataReader dr = cmd.ExecuteReader())
                {
                    while (dr.Read())
                    {
                        detalles.Add(new DetallePedido
                        {
                            IdDetalle = Convert.ToInt32(dr["id_detalle"]),
                            IdPedido = Convert.ToInt32(dr["id_pedido"]),
                            IdPlato = Convert.ToInt32(dr["id_plato"]),
                            NombrePlato = dr["nombre_plato"].ToString(),
                            Cantidad = Convert.ToInt32(dr["cantidad"]),
                            NotasEspeciales = dr["notas_especiales"] == DBNull.Value ? "" : dr["notas_especiales"].ToString()
                        });
                    }
                }
            }
            return detalles;
        }
    }
}
