using System;
using System.Collections.Generic;
using KDS.Datos;
using KDS.Entidades;

namespace KDS.Logica
{
    public class PlatoBLL
    {
        private readonly PlatoDAL _platoDal = new PlatoDAL();

        public List<Plato> ListarTodos()
        {
            return _platoDal.ObtenerTodosLosPlatos();
        }

        public string CrearPlato(Plato p)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.Nombre))
                return "El nombre del plato es obligatorio.";
            if (p.Precio <= 0)
                return "El precio debe ser mayor a cero.";

            try
            {
                return _platoDal.InsertarPlato(p) ? "OK" : "No se pudo registrar el plato.";
            }
            catch (Exception ex) { return "Error BD: " + ex.Message; }
        }

        public string EditarPlato(Plato p)
        {
            if (p == null || p.IdPlato <= 0)
                return "Seleccione un plato válido para modificar.";
            if (string.IsNullOrWhiteSpace(p.Nombre))
                return "El nombre del plato no puede quedar vacío.";
            if (p.Precio <= 0)
                return "El precio debe ser mayor a cero.";

            try
            {
                return _platoDal.ActualizarPlato(p) ? "OK" : "No se pudo actualizar el plato.";
            }
            catch (Exception ex) { return "Error BD: " + ex.Message; }
        }

        public string EliminarPlato(int idPlato)
        {
            if (idPlato <= 0) return "Seleccione un plato válido para eliminar.";
            try
            {
                return _platoDal.EliminarPlato(idPlato) ? "OK" : "No se pudo eliminar el plato.";
            }
            catch (Exception ex) { return "Error BD: " + ex.Message; }
        }
    }
}
