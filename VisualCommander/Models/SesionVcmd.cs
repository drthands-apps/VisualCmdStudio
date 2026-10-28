using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VisualCommander.Models
{
    /// <summary>
    /// Representa el contenido serializable de una sesión de Visual CMD Studio.
    /// Se guarda en archivos .vcmd en formato JSON legible.
    /// </summary>
    public class SesionVcmd
    {
        /// <summary>Versión del formato. Para futuras migraciones.</summary>
        public string Version { get; set; } = "1.0";

        /// <summary>Nombre de la aplicación que generó el archivo.</summary>
        public string Aplicacion { get; set; } = "Visual CMD Studio";

        /// <summary>
        /// Entorno de trabajo del plan (cmd, powershell, bash).
        /// En v1.0 solo se usa "cmd". Preparado para v2.0.
        /// </summary>
        public string Entorno { get; set; } = "cmd";


        /// <summary>Fecha de creación (ISO 8601 UTC).</summary>
        public DateTime Fecha { get; set; } = DateTime.UtcNow;

        /// <summary>Nombre del creador del archivo. Informativo.</summary>
        public string? Creador { get; set; }

        /// <summary>Modo silencioso activo al guardar.</summary>
        public bool ModoSilencioso { get; set; } = false;

        /// <summary>Índice de la línea activa al guardar (0-based).</summary>
        public int LineaActivaIndice { get; set; } = 0;

        /// <summary>Líneas del plan, en orden.</summary>
        public List<LineaSesion> Lineas { get; set; } = new();

        /// <summary>
        /// Firma criptográfica del contenido. Null si no está firmado.
        /// Implementación completa pendiente (Tier 3).
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public FirmaVcmd? Firma { get; set; }
    }

    /// <summary>
    /// Una línea del plan serializada para un archivo .vcmd.
    /// </summary>
    public class LineaSesion
    {
        /// <summary>Directorio de trabajo de la línea.</summary>
        [JsonPropertyOrder(1)]
        public string DirectorioTrabajo { get; set; } = "";

        /// <summary>Texto del comando.</summary>
        [JsonPropertyOrder(2)]
        public string Comando { get; set; } = "";
    }

    /// <summary>
    /// Firma criptográfica de un archivo .vcmd.
    /// Implementación criptográfica pendiente (Tier 3).
    /// </summary>
    public class FirmaVcmd
    {
        /// <summary>Algoritmo usado (ej. "RSA-SHA256").</summary>
        public string Algoritmo { get; set; } = "RSA-SHA256";

        /// <summary>Identificador del verificador (hash de la clave pública).</summary>
        public string VerificadorId { get; set; } = "";

        /// <summary>Clave pública del verificador en base64.</summary>
        public string ClavePublica { get; set; } = "";

        /// <summary>Valor de la firma en base64.</summary>
        public string Valor { get; set; } = "";

        /// <summary>Fecha de la firma.</summary>
        public DateTime FechaFirma { get; set; } = DateTime.UtcNow;
    }
}