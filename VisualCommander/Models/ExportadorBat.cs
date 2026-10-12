using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace VisualCommander.Models
{
    /// <summary>Opciones de exportación a .bat.</summary>
    public class OpcionesExportacionBat
    {
        public bool CodificacionUtf8 { get; set; } = true;   // true=UTF-8 BOM, false=ANSI
        public bool IncluirPause { get; set; } = true;
        public bool IncluirCabecera { get; set; } = true;
    }

    /// <summary>
    /// Genera archivos .bat a partir de un plan de líneas.
    /// El formato sigue las buenas prácticas de scripts de Windows.
    /// </summary>
    public static class ExportadorBat
    {
        /// <summary>
        /// Genera el contenido del archivo .bat para las líneas indicadas.
        /// </summary>
        public static string GenerarContenido(
            IList<LineaComando> lineas,
            OpcionesExportacionBat opciones)
        {
            var sb = new StringBuilder();

            // ==== Cabecera ====
            sb.AppendLine("@echo off");
            sb.AppendLine("chcp 65001 >nul");

            if (opciones.IncluirCabecera)
            {
                sb.AppendLine();
                sb.AppendLine("REM ============================================================");
                sb.AppendLine("REM Visual CMD Studio - Archivo batch generado automáticamente");
                sb.AppendLine($"REM Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"REM Usuario: {Environment.UserName}");
                sb.AppendLine($"REM Equipo: {Environment.MachineName}");
                sb.AppendLine($"REM Codificación: {(opciones.CodificacionUtf8 ? "UTF-8 con BOM" : "ANSI (Windows-1252)")}");
                sb.AppendLine($"REM Líneas: {lineas.Count}");
                sb.AppendLine("REM ============================================================");
            }

            // ==== Directorio de trabajo inicial ====
            // Usamos el de la primera línea no vacía como punto de partida.
            var primeraConTexto = lineas.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l.Texto));
            if (primeraConTexto != null)
            {
                var dir = primeraConTexto.DirectorioTrabajo;

                // Cambiar de unidad (C:, D:...) si aplica.
                if (dir.Length >= 2 && dir[1] == ':')
                {
                    var unidad = dir.Substring(0, 2);
                    sb.AppendLine();
                    sb.AppendLine($"REM --- Cambiar al directorio de trabajo ---");
                    sb.AppendLine(unidad);
                    sb.AppendLine($"cd /D \"{dir}\"");
                }
            }

            // ==== Comandos ====
            sb.AppendLine();
            sb.AppendLine("REM --- Comandos ---");

            foreach (var linea in lineas)
            {
                var texto = linea.Texto;
                if (string.IsNullOrWhiteSpace(texto))
                    continue;   // las líneas vacías se omiten en el .bat

                sb.AppendLine(texto);
            }

            // ==== Pie ====
            sb.AppendLine();
            sb.AppendLine("REM --- Fin del script ---");

            if (opciones.IncluirPause)
                sb.AppendLine("pause");

            return sb.ToString();
        }

        /// <summary>
        /// Guarda el contenido en el archivo indicado con la codificación elegida.
        /// </summary>
        public static void GuardarEnArchivo(
            string ruta,
            string contenido,
            OpcionesExportacionBat opciones)
        {
            // Aseguramos terminadores CRLF (formato Windows).
            contenido = contenido.Replace("\r\n", "\n").Replace("\n", "\r\n");

            if (opciones.CodificacionUtf8)
            {
                // UTF-8 con BOM.
                File.WriteAllText(ruta, contenido, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            }
            else
            {
                // ANSI (Windows-1252).
                var encoding = Encoding.GetEncoding(1252);
                File.WriteAllText(ruta, contenido, encoding);
            }
        }
    }
}