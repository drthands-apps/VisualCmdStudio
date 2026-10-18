using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace VisualCommander.Models
{
    /// <summary>Opciones de exportación a .sh.</summary>
    public class OpcionesExportacionSh
    {
        public bool IncluirCabecera { get; set; } = true;
        public bool IncluirAdvertencia { get; set; } = true;
        public bool IncluirDirectorioTrabajo { get; set; } = true;
    }

    /// <summary>
    /// Genera archivos .sh (Bash) a partir de un plan de líneas.
    /// Este formato es "para revisión": los comandos son de Windows
    /// y no se ejecutarán directamente en Bash sin traducción.
    /// </summary>
    public static class ExportadorSh
    {
        public static string GenerarContenido(
            IList<LineaComando> lineas,
            OpcionesExportacionSh opciones)
        {
            var sb = new StringBuilder();

            // Shebang obligatorio.
            sb.AppendLine("#!/bin/bash");

            // Cabecera de metadatos.
            if (opciones.IncluirCabecera)
            {
                sb.AppendLine("# ============================================================");
                sb.AppendLine("# Visual CMD Studio - Script generado automáticamente");
                sb.AppendLine($"# Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"# Usuario: {Environment.UserName}");
                sb.AppendLine($"# Líneas: {lineas.Count}");
                sb.AppendLine("# ============================================================");
            }

            // Advertencia.
            if (opciones.IncluirAdvertencia)
            {
                sb.AppendLine("# ⚠ ADVERTENCIA IMPORTANTE");
                sb.AppendLine("# ============================================================");
                sb.AppendLine("# Este script contiene COMANDOS DE WINDOWS (dir, copy, del, etc.).");
                sb.AppendLine("# Los comandos de Windows NO funcionan en Bash nativo de Linux/macOS.");
                sb.AppendLine("#");
                sb.AppendLine("# Opciones para ejecutarlo:");
                sb.AppendLine("#   1. Usar WSL (Windows Subsystem for Linux) con interop.");
                sb.AppendLine("#   2. Usar Git Bash en Windows.");
                sb.AppendLine("#   3. TRADUCIR manualmente los comandos:");
                sb.AppendLine("#      dir  → ls       copy → cp      del → rm");
                sb.AppendLine("#      move → mv       md   → mkdir   type → cat");
                sb.AppendLine("#      cls  → clear    ren  → mv");
                sb.AppendLine("#");
                sb.AppendLine("# La traducción automática está prevista para una fase futura.");
                sb.AppendLine("# ============================================================");
            }

            // Directorio de trabajo.
            var primeraConTexto = lineas.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l.Texto));
            if (primeraConTexto != null && opciones.IncluirDirectorioTrabajo)
            {
                sb.AppendLine();
                sb.AppendLine("# --- Directorio de trabajo ---");
                sb.AppendLine($"# Ruta original de Windows: {primeraConTexto.DirectorioTrabajo}");
                sb.AppendLine("# En WSL se traduce a /mnt/c/... ; ajústala si es necesario.");
                sb.AppendLine($"# cd \"{primeraConTexto.DirectorioTrabajo}\"");
            }

            // Comandos.
            sb.AppendLine();
            sb.AppendLine("# --- Comandos ---");

            foreach (var linea in lineas)
            {
                if (string.IsNullOrWhiteSpace(linea.Texto))
                    continue;
                sb.AppendLine(linea.Texto);
            }

            // Pie.
            sb.AppendLine();
            sb.AppendLine("# --- Fin del script ---");

            return sb.ToString();
        }

        /// <summary>
        /// Guarda el contenido en el archivo indicado.
        /// Bash requiere LF (no CRLF) y UTF-8 SIN BOM.
        /// </summary>
        public static void GuardarEnArchivo(string ruta, string contenido)
        {
            // Normalizar saltos de línea a LF (formato Unix).
            contenido = contenido.Replace("\r\n", "\n");

            // UTF-8 SIN BOM (Bash no quiere BOM).
            File.WriteAllText(ruta, contenido, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }
}