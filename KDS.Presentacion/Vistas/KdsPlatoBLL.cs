using System;
using System.Collections.Generic;
using KDS.PRE.Portal;

namespace KDS.PRE.Vistas
{
    public class KdsPlatoBLL
    {
        public List<PlatoModel> ListarTodos()
        {
            return KdsDatosService.ObtenerTodosLosPlatos();
        }

        public string CrearPlato(PlatoModel p)
        {
            string? error = Validar(p, false);
            if (error != null) return error;

            try
            {
                return KdsDatosService.InsertarPlato(p) ? "OK" : "No se pudo registrar el plato.";
            }
            catch (Exception ex) { return "Error BD: " + ex.Message; }
        }

        public string EditarPlato(PlatoModel p)
        {
            string? error = Validar(p, true);
            if (error != null) return error;

            try
            {
                return KdsDatosService.ActualizarPlato(p) ? "OK" : "No se pudo actualizar el plato.";
            }
            catch (Exception ex) { return "Error BD: " + ex.Message; }
        }

        public string EliminarPlato(int idPlato, out bool soloDesactivado)
        {
            soloDesactivado = false;
            if (idPlato <= 0) return "Seleccione un plato válido para eliminar.";

            try
            {
                return KdsDatosService.EliminarPlato(idPlato, out soloDesactivado) ? "OK" : "No se pudo eliminar el plato.";
            }
            catch (Exception ex) { return "Error BD: " + ex.Message; }
        }

        private static string? Validar(PlatoModel p, bool esEdicion)
        {
            if (p == null) return "Datos del plato incompletos.";
            if (esEdicion && p.IdPlato <= 0) return "Seleccione un plato válido para modificar.";
            if (string.IsNullOrWhiteSpace(p.Nombre)) return "El nombre del plato es obligatorio.";
            if (p.Nombre.Trim().Length > 100) return "El nombre no puede pasar de 100 caracteres.";
            if (p.Precio <= 0) return "El precio debe ser mayor a cero.";
            if (!CategoriasMenu.EsValida(p.Categoria)) return "Elige si el plato es Comida, Bebida o Acompañamiento.";
            return null;
        }
    }
}
