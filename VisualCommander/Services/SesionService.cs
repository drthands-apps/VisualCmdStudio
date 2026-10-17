using System;
using System.IO;
using System.Text.Json;
using VisualCommander.Models;

namespace VisualCommander.Services
{
    /// <summary>
    /// Servicio para guardar y cargar sesiones en archivos .vcmd.
    /// </summary>
    public static class SesionService
    {
        /// <summary>Extensión de archivo de sesión.</summary>
        public const string Extension = ".vcmd";

        /// <summary>Filtro para diálogos de archivo.</summary>
        public const string Filtro =
            "Sesiones de Visual CMD Studio (*.vcmd)|*.vcmd|Todos los archivos (*.*)|*.*";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Guarda una sesión en el archivo indicado.
        /// Lanza excepción si hay error.
        /// </summary>
        public static void Guardar(string ruta, SesionVcmd sesion)
        {
            sesion.Fecha = DateTime.UtcNow;

            var json = JsonSerializer.Serialize(sesion, JsonOptions);

            var carpeta = Path.GetDirectoryName(ruta);
            if (!string.IsNullOrEmpty(carpeta) && !Directory.Exists(carpeta))
                Directory.CreateDirectory(carpeta);

            File.WriteAllText(ruta, json);
        }

        /// <summary>
        /// Carga una sesión desde el archivo indicado.
        /// Devuelve null si el archivo no existe.
        /// Lanza excepción si el JSON está corrupto.
        /// </summary>
        public static SesionVcmd? Cargar(string ruta)
        {
            if (!File.Exists(ruta))
                return null;

            var json = File.ReadAllText(ruta);
            return JsonSerializer.Deserialize<SesionVcmd>(json, JsonOptions);
        }
    }
}