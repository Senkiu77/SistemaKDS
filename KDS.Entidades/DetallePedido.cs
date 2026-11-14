using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KDS.Entidades
{
    public class DetallePedido
    {
        public int IdDetalle { get; set; }
        public int IdPedido { get; set; }
        public int IdPlato { get; set; }
        public string NombrePlato { get; set; }
        public int Cantidad { get; set; }
        public string NotasEspeciales { get; set; }
    }
}
