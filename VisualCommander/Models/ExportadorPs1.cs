using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace VisualCommander.Models
{
    /// <summary>Opciones de exportación a .ps1.</summary>
    public class OpcionesExportacionPs1
    {
        /// <summary>Incluir cabecera con metadatos.</summary>
        public bool IncluirCabecera { get; set; } = true;

        /// <summary>Incluir advertencia sobre la política de ejecución.</summary>
        public bool IncluirAdvertenciaPolitica { get; set; } = true;

        /// <summary>Incluir comentario con el directorio de trabajo.</summary>
        public bool IncluirDirectorioTrabajo { get; set; } = true;

        /// <summary>Añadir "Read-Host" al final para pausar.</summary>
        public bool IncluirPausaFinal { get; set; } = false;
    }

    /// <summary>
    /// Genera archivos .ps1 (PowerShell) a partir de un plan de líneas.
    /// </summary>
    public static class ExportadorPs1
    {
        /// <summary>
        /// Genera el contenido del archivo .ps1 para las líneas indicadas.
        /// </summary>
        public static string GenerarContenido(
            IList<LineaComando> lineas,
            OpcionesExportacionPs1 opciones)
        {
            var sb = new StringBuilder();

            // ==== Cabecera de metadatos ====
            if (opciones.IncluirCabecera)
            {
                sb.AppendLine("# ============================================================");
                sb.AppendLine("# Visual CMD Studio - Script generado automáticamente");
                sb.AppendLine($"# Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"# Usuario: {Environment.UserName}");
                sb.AppendLine($"# Equipo: {Environment.MachineName}");
                sb.AppendLine($"# Líneas: {lineas.Count}");
                sb.AppendLine("# ============================================================");
            }

            // ==== Advertencia sobre política ====
            if (opciones.IncluirAdvertenciaPolitica)
            {
                sb.AppendLine("# ADVERTENCIA: Windows bloquea los scripts .ps1 por defecto.");
                sb.AppendLine("# Para ejecutarlo, abre PowerShell y ejecuta:");
                sb.AppendLine("#   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass");
                sb.AppendLine("#   .\\nombre-del-script.ps1");
                sb.AppendLine("# O firma digitalmente el script.");
                sb.AppendLine("# ============================================================");
                sb.AppendLine();
            }

            // ==== Directorio de trabajo ====
            var primeraConTexto = lineas.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l.Texto));
            if (primeraConTexto != null && opciones.IncluirDirectorioTrabajo)
            {
                var dir = primeraConTexto.DirectorioTrabajo;
                sb.AppendLine();
                sb.AppendLine("# --- Cambiar al directorio de trabajo ---");
                sb.AppendLine($"Set-Location -Path \"{dir}\"");
            }

            // ==== Comandos ====
            sb.AppendLine();
            sb.AppendLine("# --- Comandos ---");

            foreach (var linea in lineas)
            {
                var texto = linea.Texto;
                if (string.IsNullOrWhiteSpace(texto))
                    continue;

                sb.AppendLine(texto);
            }

            // ==== Pie ====
            sb.AppendLine();
            sb.AppendLine("# --- Fin del script ---");

            if (opciones.IncluirPausaFinal)
            {
                sb.AppendLine("Read-Host \"Pulsa Enter para salir\"");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Guarda el contenido en el archivo indicado.
        /// PowerShell requiere UTF-8 con BOM para manejar bien los acentos.
        /// </summary>
        public static void GuardarEnArchivo(
            string ruta,
            string contenido)
        {
            // Normalizar saltos de línea a CRLF (Windows).
            contenido = contenido.Replace("\r\n", "\n").Replace("\n", "\r\n");

            // UTF-8 con BOM (importante para PowerShell).
            File.WriteAllText(ruta, contenido, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
    }
}