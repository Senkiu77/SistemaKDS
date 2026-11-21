using System;
using System.Collections.Generic;

namespace KDS.Entidades
{
    public class Pedido
    {
        public int IdPedido { get; set; }
        public int NumeroMesa { get; set; }
        public DateTime FechaHora { get; set; } = DateTime.Now;
        public string Estado { get; set; } = "Pendiente";
        public bool NotificadoVoz { get; set; } = false;
        public List<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();

        public int MinutosEspera => (int)(DateTime.Now - FechaHora).TotalMinutes;

        public override string ToString()
        {
            int platos = 0;
            foreach (DetallePedido d in Detalles) platos += d.Cantidad;
            return $"Mesa {NumeroMesa} · {platos} platos · {FechaHora:HH:mm}";
        }
    }
}
