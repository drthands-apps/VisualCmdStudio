using System;
using System.Collections.Generic;
using System.Text;

namespace VisualCommander.Models
{
    /// <summary>
    /// Configuración persistente de la aplicación.
    /// Se guarda entre sesiones en un archivo JSON en AppData.
    /// </summary>
    public class Configuracion
    {
        /// <summary>
        /// Modo silencioso: silencia avisos durante la construcción/edición.
        /// NO silencia avisos antes de ejecutar.
        /// </summary>
        public bool ModoSilencioso { get; set; } = false;
    }
}
