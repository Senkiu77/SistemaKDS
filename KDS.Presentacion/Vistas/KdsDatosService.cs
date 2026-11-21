using System;
using System.Collections.Generic;
using Npgsql;

namespace KDS.PRE.Vistas
{
    public class PlatoModel
    {
        public int IdPlato { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public bool Activo { get; set; }
    }

    public class UsuarioModel
    {
        public int IdUsuario { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public string? ClaveHash { get; set; }
    }

    public class DetallePedidoModel
    {
        public int IdDetalle { get; set; }
        public int IdPedido { get; set; }
        public int IdPlato { get; set; }
        public string NombrePlato { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public string NotasEspeciales { get; set; } = string.Empty;
        public decimal PrecioUnitario { get; set; }
    }

    public class PedidoModel
    {
        public int IdPedido { get; set; }
        public int NumeroMesa { get; set; }
        public DateTime FechaHora { get; set; } = DateTime.Now;
        public string Estado { get; set; } = "Pendiente";
        public bool NotificadoVoz { get; set; }
        public List<DetallePedidoModel> Detalles { get; set; } = new();
        public int MinutosEspera => Math.Max(0, (int)(DateTime.Now - FechaHora).TotalMinutes);
    }

    public static class KdsDatosService
    {
        private static readonly string CadenaConexion =
            Environment.GetEnvironmentVariable("KDS_CONEXION")
            ?? "Host=localhost;Port=5432;Username=postgres;Password=1234;Database=bd_kds;";

        public static string? ActualizarEstructura()
        {
            try
            {
                using var recurso = typeof(KdsDatosService).Assembly.GetManifestResourceStream("KDS.PRE.BD.actualizacion.sql");
                if (recurso == null) return "No se encontró el script de actualización.";
                using var lector = new System.IO.StreamReader(recurso);
                string script = lector.ReadToEnd();

                using var con = AbrirConexion();
                using var cmd = new NpgsqlCommand(script, con);
                cmd.ExecuteNonQuery();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static string? ProbarConexion()
        {
            try
            {
                using var con = AbrirConexion();
                using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM platos;", con);
                cmd.ExecuteScalar();
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static NpgsqlConnection AbrirConexion()
        {
            var con = new NpgsqlConnection(CadenaConexion);
            con.Open();
            return con;
        }

        public static List<UsuarioModel> ObtenerMeseros()
        {
            var lista = new List<UsuarioModel>();
            using var con = AbrirConexion();
            const string sql = "SELECT id_usuario, nombre, rol FROM usuarios WHERE rol = 'Mesero' ORDER BY nombre ASC;";
            using var cmd = new NpgsqlCommand(sql, con);
            using var dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                lista.Add(new UsuarioModel
                {
                    IdUsuario = Convert.ToInt32(dr["id_usuario"]),
                    Nombre = dr["nombre"].ToString() ?? string.Empty,
                    Rol = dr["rol"].ToString() ?? string.Empty
                });
            }
            return lista;
        }

        public static List<UsuarioModel> ObtenerDuenos()
        {
            var lista = new List<UsuarioModel>();
            using var con = AbrirConexion();
            const string sql = "SELECT id_usuario, nombre, rol, clave_hash FROM usuarios WHERE rol = 'Admin' ORDER BY nombre ASC;";
            using var cmd = new NpgsqlCommand(sql, con);
            using var dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                lista.Add(new UsuarioModel
                {
                    IdUsuario = Convert.ToInt32(dr["id_usuario"]),
                    Nombre = dr["nombre"].ToString() ?? string.Empty,
                    Rol = dr["rol"].ToString() ?? string.Empty,
                    ClaveHash = dr["clave_hash"] == DBNull.Value ? null : dr["clave_hash"].ToString()
                });
            }
            return lista;
        }

        public static bool GuardarClaveDueno(int idUsuario, string claveHash)
        {
            using var con = AbrirConexion();
            const string sql = "UPDATE usuarios SET clave_hash = @clave WHERE id_usuario = @id AND rol = 'Admin';";
            using var cmd = new NpgsqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@clave", claveHash);
            cmd.Parameters.AddWithValue("@id", idUsuario);
            return cmd.ExecuteNonQuery() > 0;
        }

        public static List<PlatoModel> ObtenerPlatosActivos()
        {
            return ConsultarPlatos("SELECT id_plato, nombre, precio, categoria, activo FROM platos WHERE activo = true ORDER BY nombre ASC;");
        }

        public static List<PlatoModel> ObtenerTodosLosPlatos()
        {
            return ConsultarPlatos("SELECT id_plato, nombre, precio, categoria, activo FROM platos ORDER BY id_plato ASC;");
        }

        private static List<PlatoModel> ConsultarPlatos(string sql)
        {
            var lista = new List<PlatoModel>();
            using var con = AbrirConexion();
            using var cmd = new NpgsqlCommand(sql, con);
            using var dr = cmd.ExecuteReader();
            while (dr.Read())
            {
                lista.Add(new PlatoModel
                {
                    IdPlato = Convert.ToInt32(dr["id_plato"]),
                    Nombre = dr["nombre"].ToString() ?? string.Empty,
                    Precio = Convert.ToDecimal(dr["precio"]),
                    Categoria = dr["categoria"].ToString() ?? string.Empty,
                    Activo = Convert.ToBoolean(dr["activo"])
                });
            }
            return lista;
        }

        public static bool InsertarPlato(PlatoModel plato)
        {
            using var con = AbrirConexion();
            const string sql = "INSERT INTO platos (nombre, precio, categoria, activo) VALUES (@nombre, @precio, @categoria, @activo);";
            using var cmd = new NpgsqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@nombre", plato.Nombre.Trim());
            cmd.Parameters.AddWithValue("@precio", plato.Precio);
            cmd.Parameters.AddWithValue("@categoria", plato.Categoria);
            cmd.Parameters.AddWithValue("@activo", plato.Activo);
            return cmd.ExecuteNonQuery() > 0;
        }

        public static bool ActualizarPlato(PlatoModel plato)
        {
            using var con = AbrirConexion();
            const string sql = "UPDATE platos SET nombre = @nombre, precio = @precio, categoria = @categoria, activo = @activo WHERE id_plato = @id;";
            using var cmd = new NpgsqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@nombre", plato.Nombre.Trim());
            cmd.Parameters.AddWithValue("@precio", plato.Precio);
            cmd.Parameters.AddWithValue("@categoria", plato.Categoria);
            cmd.Parameters.AddWithValue("@activo", plato.Activo);
            cmd.Parameters.AddWithValue("@id", plato.IdPlato);
            return cmd.ExecuteNonQuery() > 0;
        }

        public static bool EliminarPlato(int idPlato, out bool soloDesactivado)
        {
            using var con = AbrirConexion();

            using (var cmdUso = new NpgsqlCommand("SELECT COUNT(*) FROM detalle_pedidos WHERE id_plato = @id;", con))
            {
                cmdUso.Parameters.AddWithValue("@id", idPlato);
                soloDesactivado = Convert.ToInt64(cmdUso.ExecuteScalar()) > 0;
            }

            string sql = soloDesactivado
                ? "UPDATE platos SET activo = false WHERE id_plato = @id;"
                : "DELETE FROM platos WHERE id_plato = @id;";

            using var cmd = new NpgsqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@id", idPlato);
            return cmd.ExecuteNonQuery() > 0;
        }

        public static bool InsertarPedido(PedidoModel pedido)
        {
            if (pedido == null || pedido.Detalles.Count == 0) return false;

            using var con = AbrirConexion();
            using var tx = con.BeginTransaction();

            try
            {
                const string sqlPedido = @"
                    INSERT INTO pedidos (numero_mesa, fecha_hora, estado, notificado_voz)
                    VALUES (@mesa, @fecha, @estado, @notificado)
                    RETURNING id_pedido;";

                int idGenerado;
                using (var cmd = new NpgsqlCommand(sqlPedido, con, tx))
                {
                    cmd.Parameters.AddWithValue("@mesa", pedido.NumeroMesa);
                    cmd.Parameters.AddWithValue("@fecha", pedido.FechaHora);
                    cmd.Parameters.AddWithValue("@estado", string.IsNullOrEmpty(pedido.Estado) ? "Pendiente" : pedido.Estado);
                    cmd.Parameters.AddWithValue("@notificado", pedido.NotificadoVoz);
                    idGenerado = Convert.ToInt32(cmd.ExecuteScalar());
                }

                const string sqlDetalle = @"
                    INSERT INTO detalle_pedidos (id_pedido, id_plato, cantidad, notas_especiales)
                    VALUES (@idPedido, @idPlato, @cantidad, @notas);";

                foreach (var det in pedido.Detalles)
                {
                    using var cmdDet = new NpgsqlCommand(sqlDetalle, con, tx);
                    cmdDet.Parameters.AddWithValue("@idPedido", idGenerado);
                    cmdDet.Parameters.AddWithValue("@idPlato", det.IdPlato);
                    cmdDet.Parameters.AddWithValue("@cantidad", det.Cantidad);
                    cmdDet.Parameters.AddWithValue("@notas",
                        string.IsNullOrWhiteSpace(det.NotasEspeciales) ? DBNull.Value : det.NotasEspeciales.Trim());
                    cmdDet.ExecuteNonQuery();
                }

                tx.Commit();
                pedido.IdPedido = idGenerado;
                return true;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public static List<PedidoModel> ObtenerPedidosCocina()
        {
            return ConsultarPedidos("p.estado IN ('Pendiente', 'Preparando')");
        }

        public static List<PedidoModel> ObtenerPedidosListosParaMesero()
        {
            return ConsultarPedidos("p.estado = 'Listo'");
        }

        public static List<PedidoModel> ObtenerSolicitudesClientes()
        {
            return ConsultarPedidos("p.estado = 'Solicitado'");
        }

        private static List<PedidoModel> ConsultarPedidos(string filtro)
        {
            var pedidos = new Dictionary<int, PedidoModel>();

            using var con = AbrirConexion();

            string sql = $@"
                SELECT p.id_pedido, p.numero_mesa, p.fecha_hora, p.estado, p.notificado_voz,
                       d.id_detalle, d.id_plato, pl.nombre AS nombre_plato, pl.precio, d.cantidad, d.notas_especiales
                FROM pedidos p
                INNER JOIN detalle_pedidos d ON p.id_pedido = d.id_pedido
                INNER JOIN platos pl ON d.id_plato = pl.id_plato
                WHERE {filtro}
                ORDER BY p.fecha_hora ASC, p.id_pedido ASC, d.id_detalle ASC;";

            using var cmd = new NpgsqlCommand(sql, con);
            using var dr = cmd.ExecuteReader();

            while (dr.Read())
            {
                int idPed = Convert.ToInt32(dr["id_pedido"]);
                if (!pedidos.TryGetValue(idPed, out var ped))
                {
                    ped = new PedidoModel
                    {
                        IdPedido = idPed,
                        NumeroMesa = Convert.ToInt32(dr["numero_mesa"]),
                        FechaHora = Convert.ToDateTime(dr["fecha_hora"]),
                        Estado = dr["estado"].ToString() ?? string.Empty,
                        NotificadoVoz = Convert.ToBoolean(dr["notificado_voz"])
                    };
                    pedidos[idPed] = ped;
                }

                ped.Detalles.Add(new DetallePedidoModel
                {
                    IdDetalle = Convert.ToInt32(dr["id_detalle"]),
                    IdPedido = idPed,
                    IdPlato = Convert.ToInt32(dr["id_plato"]),
                    NombrePlato = dr["nombre_plato"].ToString() ?? string.Empty,
                    PrecioUnitario = Convert.ToDecimal(dr["precio"]),
                    Cantidad = Convert.ToInt32(dr["cantidad"]),
                    NotasEspeciales = dr["notas_especiales"] is DBNull ? string.Empty : dr["notas_especiales"].ToString() ?? string.Empty
                });
            }

            return new List<PedidoModel>(pedidos.Values);
        }

        public static string? ObtenerEstadoPedido(int idPedido)
        {
            using var con = AbrirConexion();
            using var cmd = new NpgsqlCommand("SELECT estado FROM pedidos WHERE id_pedido = @id;", con);
            cmd.Parameters.AddWithValue("@id", idPedido);
            return cmd.ExecuteScalar() as string;
        }

        public static int ObtenerSiguienteNumeroPedido()
        {
            using var con = AbrirConexion();
            const string sql = @"
                SELECT GREATEST(
                    COALESCE(pg_sequence_last_value(pg_get_serial_sequence('pedidos', 'id_pedido')), 0),
                    COALESCE((SELECT MAX(id_pedido) FROM pedidos), 0)) + 1;";
            using var cmd = new NpgsqlCommand(sql, con);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static bool MarcarComoNotificado(int idPedido)
        {
            using var con = AbrirConexion();
            const string sql = "UPDATE pedidos SET notificado_voz = true WHERE id_pedido = @id;";
            using var cmd = new NpgsqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@id", idPedido);
            return cmd.ExecuteNonQuery() > 0;
        }

        public static bool CambiarEstadoPedido(int idPedido, string nuevoEstado)
        {
            using var con = AbrirConexion();
            const string sql = "UPDATE pedidos SET estado = @estado WHERE id_pedido = @id;";
            using var cmd = new NpgsqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@estado", nuevoEstado);
            cmd.Parameters.AddWithValue("@id", idPedido);
            return cmd.ExecuteNonQuery() > 0;
        }

        public static bool EliminarPedido(int idPedido)
        {
            using var con = AbrirConexion();
            using var tx = con.BeginTransaction();

            try
            {
                using (var cmdDet = new NpgsqlCommand("DELETE FROM detalle_pedidos WHERE id_pedido = @id;", con, tx))
                {
                    cmdDet.Parameters.AddWithValue("@id", idPedido);
                    cmdDet.ExecuteNonQuery();
                }

                int borrados;
                using (var cmd = new NpgsqlCommand("DELETE FROM pedidos WHERE id_pedido = @id AND estado = 'Solicitado';", con, tx))
                {
                    cmd.Parameters.AddWithValue("@id", idPedido);
                    borrados = cmd.ExecuteNonQuery();
                }

                if (borrados == 0)
                {
                    tx.Rollback();
                    return false;
                }

                tx.Commit();
                return true;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }
}
