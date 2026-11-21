using System;
using System.Collections.Generic;
using KDS.Datos;
using KDS.Entidades;

namespace KDS.Logica
{
    public class PedidoBLL
    {
        private readonly PedidoDAL _pedidoDal = new PedidoDAL();
        private readonly PlatoDAL _platoDal = new PlatoDAL();
        private readonly UsuarioDAL _usuarioDal = new UsuarioDAL();
        private readonly VozService _vozService = new VozService();

        private static readonly string[] _estadosPermitidos =
        {
            "Solicitado", "Pendiente", "Preparando", "Listo", "Entregado"
        };

        public List<Usuario> ObtenerMeseros()
        {
            return _usuarioDal.ObtenerMeseros();
        }

        public List<Plato> ObtenerMenu()
        {
            return _platoDal.ObtenerPlatosActivos();
        }

        public string RegistrarPedido(Pedido p)
        {
            if (p == null)
                return "El pedido no es válido.";

            if (p.NumeroMesa <= 0)
                return "Seleccione un número de mesa mayor a cero.";

            if (p.Detalles == null || p.Detalles.Count == 0)
                return "Debe agregar al menos un plato a la comanda.";

            foreach (var det in p.Detalles)
            {
                if (det == null || det.Cantidad <= 0)
                    return "La cantidad de cada plato debe ser mayor a cero.";
            }

            try
            {
                return _pedidoDal.InsertarPedido(p) ? "OK" : "No se pudo guardar el pedido en la base de datos.";
            }
            catch (Exception ex)
            {
                return "Error BD: " + ex.Message;
            }
        }

        public string RegistrarSolicitudCliente(Pedido p)
        {
            if (p == null || p.NumeroMesa <= 0 || p.Detalles == null || p.Detalles.Count == 0)
                return "Datos de solicitud incompletos.";

            p.Estado = "Solicitado";
            p.NotificadoVoz = false;
            return RegistrarPedido(p);
        }

        public List<Pedido> ProcesarColaCocina()
        {
            List<Pedido> activos = _pedidoDal.ObtenerPedidosCocina();

            foreach (var p in activos)
            {
                if (!p.NotificadoVoz)
                {
                    _vozService.CantarPedido(p);
                    _pedidoDal.MarcarComoNotificado(p.IdPedido);
                    p.NotificadoVoz = true;
                }
            }

            return activos;
        }

        public bool ActualizarEstado(int idPedido, string nuevoEstado)
        {
            if (idPedido <= 0)
                return false;

            if (string.IsNullOrWhiteSpace(nuevoEstado) || Array.IndexOf(_estadosPermitidos, nuevoEstado) < 0)
                return false;

            return _pedidoDal.CambiarEstadoPedido(idPedido, nuevoEstado);
        }

        public List<Pedido> ObtenerPedidosListosParaMesero()
        {
            return _pedidoDal.ObtenerPedidosListosParaMesero();
        }

        public List<Pedido> ObtenerSolicitudesClientes()
        {
            return _pedidoDal.ObtenerSolicitudesClientes();
        }

        public bool AprobarSolicitudCliente(int idPedido)
        {
            return ActualizarEstado(idPedido, "Pendiente");
        }

        public bool RechazarSolicitudCliente(int idPedido)
        {
            return idPedido > 0 && _pedidoDal.EliminarPedido(idPedido);
        }
    }
}
