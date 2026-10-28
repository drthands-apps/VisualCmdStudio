using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using VisualCommander.Models;

namespace VisualCommander.Services
{
    /// <summary>
    /// Importa archivos .bat y .cmd al modelo de líneas del editor.
    /// Respeta el contenido original: no descarta REM, @echo, ni líneas
    /// aparentemente inútiles. Solo aplica semántica de CMD donde es
    /// imprescindible (continuaciones con ^, encoding).
    /// </summary>
    public static class ImportadorBat
    {
        /// <summary>
        /// Importa un archivo .bat/.cmd y devuelve la lista de líneas
        /// lista para reconstruir el plan.
        /// </summary>
        public static List<LineaSesion> Importar(string ruta, IEnumerable<Comando> catalogo)
        {
            var texto = LeerArchivo(ruta);
            var lineasCrudas = texto.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            // Unir continuaciones con ^ al final de línea.
            var lineasUnidas = UnirContinuaciones(lineasCrudas);

            var resultado = new List<LineaSesion>();
            var directorioActual = Environment.CurrentDirectory;

            foreach (var linea in lineasUnidas)
            {
                if (string.IsNullOrWhiteSpace(linea)) continue;

                // Parsear para calcular el directorio siguiente.
                var tokens = ComandoParser.Parsear(linea, catalogo, directorioActual);

                // ¿Cambia el directorio para las siguientes líneas?
                var nuevoDir = CalcularDirectorioSiguiente(tokens);

                resultado.Add(new LineaSesion
                {
                    Comando = linea,
                    DirectorioTrabajo = directorioActual
                });

                if (nuevoDir != null)
                    directorioActual = nuevoDir;
            }

            return resultado;
        }

        /// <summary>
        /// Lee el archivo con UTF-8 y fallback a ANSI 1252.
        /// </summary>
        private static string LeerArchivo(string ruta)
        {
            try
            {
                return File.ReadAllText(ruta, Encoding.UTF8);
            }
            catch (DecoderFallbackException)
            {
                return File.ReadAllText(ruta, Encoding.GetEncoding(1252));
            }
        }

        /// <summary>
        /// Une líneas que terminan en ^ con la siguiente.
        /// </summary>
        private static List<string> UnirContinuaciones(IReadOnlyList<string> lineas)
        {
            var resultado = new List<string>();
            var buffer = new StringBuilder();
            bool continuando = false;

            foreach (var linea in lineas)
            {
                if (continuando)
                {
                    buffer.Append(linea);
                }
                else
                {
                    buffer.Clear();
                    buffer.Append(linea);
                }

                if (linea.EndsWith("^"))
                {
                    buffer.Length--;   // quitar el ^
                    continuando = true;
                }
                else
                {
                    resultado.Add(buffer.ToString());
                    continuando = false;
                }
            }

            // Si termina en ^, añadimos el buffer tal cual.
            if (continuando)
                resultado.Add(buffer.ToString());

            return resultado;
        }

        /// <summary>
        /// Reutiliza la lógica de LineaComando: si la línea empieza por
        /// cd o pushd y tiene una Ruta existente, devuelve esa ruta.
        /// </summary>
        private static string? CalcularDirectorioSiguiente(IEnumerable<Token> tokens)
        {
            var lista = tokens.ToList();
            if (lista.Count == 0) return null;

            int i = 0;
            // Saltar prefijos (@).
            while (i < lista.Count && lista[i].Tipo == TipoToken.Prefijo)
                i++;

            if (i >= lista.Count) return null;

            var primero = lista[i].Texto.ToLowerInvariant();
            if (primero != "cd" && primero != "pushd") return null;

            for (i++; i < lista.Count; i++)
            {
                var t = lista[i];
                if (t.Tipo != TipoToken.Ruta) continue;
                if (t.Origen is string ruta && Directory.Exists(ruta))
                    return ruta;
            }

            return null;
        }
    }
}