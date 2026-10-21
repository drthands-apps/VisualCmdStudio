using System;
using System.Collections.Generic;

namespace VisualCommander.Models
{
    /// <summary>
    /// Registro de uso de comandos. Persiste entre sesiones en
    /// %APPDATA%\VisualCmdStudio\uso.json.
    /// </summary>
    public class RegistroUso
    {
        /// <summary>Versión del formato.</summary>
        public string Version { get; set; } = "1.0";

        /// <summary>Fecha de última actualización.</summary>
        public DateTime Actualizado { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Diccionario nombreComando → contador de usos.
        /// Los comandos con más usos aparecen primero en modo "Frecuencia".
        /// </summary>
        public Dictionary<string, int> Comandos { get; set; } = new();
    }
}