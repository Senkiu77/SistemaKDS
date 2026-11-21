using System;
using System.Collections.Generic;

namespace KDS.PRE.Vistas
{
    public class KdsPedidoBLL
    {
        public static event Action<int, string>? EstadoCambiado;

        private static readonly string[] EstadosPermitidos =
        {
            "Solicitado", "Pendiente", "Preparando", "Listo", "Entregado"
        };

        public List<UsuarioModel> ObtenerMeseros()
        {
            return KdsDatosService.ObtenerMeseros();
        }

        public List<PlatoModel> ObtenerMenu()
        {
            return KdsDatosService.ObtenerPlatosActivos();
        }

        public string RegistrarPedido(PedidoModel p)
        {
            if (p == null) return "El pedido no es válido.";
            if (p.NumeroMesa <= 0) return "Seleccione un número de mesa mayor a cero.";
            if (p.Detalles == null || p.Detalles.Count == 0) return "Debe agregar al menos un plato a la comanda.";

            foreach (var det in p.Detalles)
            {
                if (det == null || det.Cantidad <= 0)
                    return "La cantidad de cada plato debe ser mayor a cero.";
            }

            try
            {
                return KdsDatosService.InsertarPedido(p) ? "OK" : "No se pudo guardar el pedido en la base de datos.";
            }
            catch (Exception ex)
            {
                return $"Error en base de datos: {ex.Message}";
            }
        }

        public string RegistrarSolicitudCliente(PedidoModel p)
        {
            if (p == null || p.NumeroMesa <= 0 || p.Detalles == null || p.Detalles.Count == 0)
                return "Datos de solicitud incompletos.";

            p.Estado = "Solicitado";
            p.NotificadoVoz = false;
            p.FechaHora = DateTime.Now;
            return RegistrarPedido(p);
        }

        public List<PedidoModel> ProcesarColaCocina()
        {
            List<PedidoModel> activos = KdsDatosService.ObtenerPedidosCocina();

            foreach (var p in activos)
            {
                if (!p.NotificadoVoz)
                {
                    KdsVozService.CantarPedido(p);
                    KdsDatosService.MarcarComoNotificado(p.IdPedido);
                    p.NotificadoVoz = true;
                }
            }

            return activos;
        }

        public bool ActualizarEstado(int idPedido, string nuevoEstado)
        {
            if (idPedido <= 0) return false;
            if (string.IsNullOrWhiteSpace(nuevoEstado) || Array.IndexOf(EstadosPermitidos, nuevoEstado) < 0)
                return false;

            bool cambiado = KdsDatosService.CambiarEstadoPedido(idPedido, nuevoEstado);
            if (cambiado) EstadoCambiado?.Invoke(idPedido, nuevoEstado);
            return cambiado;
        }

        public List<PedidoModel> ObtenerPedidosListosParaMesero()
        {
            return KdsDatosService.ObtenerPedidosListosParaMesero();
        }

        public List<PedidoModel> ObtenerSolicitudesClientes()
        {
            return KdsDatosService.ObtenerSolicitudesClientes();
        }

        public bool AprobarSolicitudCliente(int idPedido)
        {
            return ActualizarEstado(idPedido, "Pendiente");
        }

        public bool RechazarSolicitudCliente(int idPedido)
        {
            bool rechazado = idPedido > 0 && KdsDatosService.EliminarPedido(idPedido);
            if (rechazado) EstadoCambiado?.Invoke(idPedido, "Rechazado");
            return rechazado;
        }

        public string? ConsultarEstado(int idPedido)
        {
            return idPedido > 0 ? KdsDatosService.ObtenerEstadoPedido(idPedido) : null;
        }

        public int SiguienteNumeroPedido()
        {
            try
            {
                return KdsDatosService.ObtenerSiguienteNumeroPedido();
            }
            catch
            {
                return 0;
            }
        }
    }
}
