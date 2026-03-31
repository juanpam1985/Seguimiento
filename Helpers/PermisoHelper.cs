using System;
using System.Linq;
using Seguimiento.Data;

namespace Seguimiento.Helpers
{
    public static class PermisoHelper
    {
        public static bool TienePermiso(ApplicationDbContext db, int? rolId, string moduloClave, string accion)
        {
            if (db == null)
                return false;

            if (rolId == null || rolId <= 0)
                return false;

            if (string.IsNullOrWhiteSpace(moduloClave))
                return false;

            if (string.IsNullOrWhiteSpace(accion))
                return false;

            moduloClave = moduloClave.Trim().ToUpper();
            accion = accion.Trim().ToUpper();

            var permiso = (from rp in db.RolPermisos
                           join m in db.Modulos on rp.ModuloId equals m.Id
                           where rp.RolId == rolId
                                 && m.Activo
                                 && m.Clave.ToUpper() == moduloClave
                           select new
                           {
                               rp.PuedeVer,
                               rp.PuedeCrear,
                               rp.PuedeEditar,
                               rp.PuedeEliminar
                           })
                           .FirstOrDefault();

            if (permiso == null)
                return false;

            switch (accion)
            {
                case "VER":
                    return permiso.PuedeVer;

                case "CREAR":
                    return permiso.PuedeCrear;

                case "EDITAR":
                    return permiso.PuedeEditar;

                case "ELIMINAR":
                    return permiso.PuedeEliminar;

                default:
                    return false;
            }
        }
    }
}