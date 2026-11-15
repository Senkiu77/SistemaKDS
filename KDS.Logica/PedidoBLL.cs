using KDS.Datos;
using KDS.Entidades;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDS.Logica
{
    /// <summary>
    /// Reglas de negocio del KDS. Es el único punto de acceso que la capa
    /// de Presentación debe usar (nunca llama directo a KDS.Datos).
    /// </summary>
    public class PedidoBLL
    {
        private readonly PedidoDAL _pedidoDal = new PedidoDAL();
        private readonly PlatoDAL _platoDal = new PlatoDAL();
        private readonly UsuarioDAL _usuarioDal = new UsuarioDAL();
        private readonly VozService _vozService = new VozService();

        // Estados válidos del ciclo de vida de un pedido.
        private static readonly string[] _estadosPermitidos =
        {
            "Pendiente", "Preparando", "Listo", "Entregado"
        };

        // 1. Para llenar el ComboBox de meseros en FrmMeseroPOS.
        public List<Usuario> ObtenerMeseros()
        {
            return _usuarioDal.ObtenerMeseros();
        }

        // 2. Para llenar el ComboBox del menú en FrmMeseroPOS.
        public List<Plato> ObtenerMenu()
        {
            return _platoDal.ObtenerPlatosActivos();
        }

        // 3. Valida y envía el pedido del mesero hacia PostgreSQL.
        //    Retorna "OK" si todo salió bien; de lo contrario, el mensaje de error.
        public string RegistrarPedido(Pedido p)
        {
            if (p == null)
                return "El pedido no es válido.";

            if (p.NumeroMesa <= 0)
                return "Seleccione un número de mesa mayor a cero.";

            if (p.Detalles == null || p.Detalles.Count == 0)
                return "Debe agregar al menos un plato a la comanda.";

            // Ningún renglón puede tener cantidad cero o negativa.
            foreach (var det in p.Detalles)
            {
                if (det == null || det.Cantidad <= 0)
                    return "La cantidad de cada plato debe ser mayor a cero.";
            }

            bool exito = _pedidoDal.InsertarPedido(p);
            return exito ? "OK" : "No se pudo guardar el pedido en la base de datos.";
        }

        // 4. Invocado cada 3 segundos por el Timer de FrmCocinaKDS:
        //    trae los pedidos activos y canta únicamente los nuevos.
        public List<Pedido> ProcesarColaCocina()
        {
            List<Pedido> activos = _pedidoDal.ObtenerPedidosCocina();

            foreach (var p in activos)
            {
                if (!p.NotificadoVoz)
                {
                    _vozService.CantarPedido(p);                 // Lee el pedido en voz alta
                    _pedidoDal.MarcarComoNotificado(p.IdPedido); // Evita repetirlo en BD
                    p.NotificadoVoz = true;                      // Y también en memoria
                }
            }

            return activos;
        }

        // 5. Cambia el estado de un pedido (cocina: Preparando/Listo; mesero: Entregado).
        public bool ActualizarEstado(int idPedido, string nuevoEstado)
        {
            if (idPedido <= 0)
                return false;

            if (string.IsNullOrWhiteSpace(nuevoEstado) ||
                System.Array.IndexOf(_estadosPermitidos, nuevoEstado) < 0)
                return false;

            return _pedidoDal.CambiarEstadoPedido(idPedido, nuevoEstado);
        }

        // 6. Pedidos en estado "Listo" para la lista de entrega de FrmMeseroPOS.
        public List<Pedido> ObtenerPedidosListosParaMesero()
        {
            return _pedidoDal.ObtenerPedidosListosParaMesero();
        }
    }
}
