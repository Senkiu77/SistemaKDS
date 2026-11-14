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
    }
}