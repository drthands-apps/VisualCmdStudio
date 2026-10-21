using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace VisualCommander.Models
{
    /// <summary>
    /// Convierte una cadena de comando en una lista de Tokens.
    /// Parseo heurístico: no es perfecto, pero cubre la mayoría de casos.
    /// </summary>
    public static class ComandoParser
    {
        /// <summary>
        /// Parsea un texto de línea de comandos y devuelve la lista de tokens.
        /// </summary>
        /// <param name="texto">La línea tal cual la escribió el usuario.</param>
        /// <param name="catalogo">Catálogo para resolver nombres de comando.</param>
        /// <param name="directorioTrabajo">Para resolver rutas relativas.</param>
        public static List<Token> Parsear(
     string texto,
     IEnumerable<Comando> catalogo,
     string directorioTrabajo)
        {
            var resultado = new List<Token>();
            if (string.IsNullOrWhiteSpace(texto)) return resultado;

            var partes = DividirRespetandoComillas(texto);
            var dict = catalogo.ToDictionary(c => c.Nombre, StringComparer.OrdinalIgnoreCase);

            Comando? comandoActual = null;
            bool primerToken = true;

            foreach (var parte in partes)
            {
                if (string.IsNullOrWhiteSpace(parte)) continue;

                var token = Clasificar(parte, dict, directorioTrabajo, primerToken, comandoActual);

                // Actualizamos el comando activo si el token es un comando.
                if (token.Tipo == TipoToken.Comando && token.Origen is Comando c)
                    comandoActual = c;

                // Si el token es un operador, reseteamos el comando actual.
                if (token.Tipo == TipoToken.Operador)
                    comandoActual = null;

                resultado.Add(token);
                primerToken = false;
            }

            return resultado;
        }
        // ==================== Clasificación de un trozo ====================

        private static Token Clasificar(
    string trozo,
    Dictionary<string, Comando> catalogo,
    string directorioTrabajo,
    bool esPrimero,
    Comando? comandoActual)
        {
            var limpio = trozo.Trim('"');

            // 1) ¿Es un comando del catálogo?
            if (esPrimero && catalogo.TryGetValue(limpio, out var cmd))
            {
                return new Token
                {
                    Tipo = TipoToken.Comando,
                    Texto = cmd.Nombre,
                    Descripcion = $"{cmd.Resumen}\n{cmd.Descripcion}",
                    Color = cmd.ColorFamilia,
                    Icono = cmd.Icono,
                    Origen = cmd,
                    RequiereAdmin = cmd.RequiereAdmin
                };
            }

            // 2) ¿Empieza por / o -? → parámetro
            if (limpio.StartsWith("/") || limpio.StartsWith("-"))
            {
                // Buscamos si el comando actual tiene este parámetro.
                Parametro? param = null;
                if (comandoActual != null)
                {
                    param = comandoActual.Parametros.FirstOrDefault(p =>
                        string.Equals(p.Nombre, limpio, StringComparison.OrdinalIgnoreCase));
                }

                return new Token
                {
                    Tipo = TipoToken.Parametro,
                    Texto = trozo,
                    Color = param?.Tipo == TipoParametro.Lista ? "D9E1F2" : "E2EFDA",
                    Icono = "🔧",
                    Descripcion = param?.Descripcion ?? "Parámetro",
                    Origen = param   // ← puede ser null si el parámetro no está en el comando
                };
            }

            // 3) Operadores
            if (limpio is "|" or "&" or "&&" or "||" or ">" or ">>" or "<" or"^" or "==" or "NEQ" or "EQU")
            {
                return new Token
                {
                    Tipo = TipoToken.Operador,
                    Texto = trozo,
                    Color = "FCE4D6",
                    Icono = "⏵",
                    Descripcion = $"Operador: {limpio}"
                };
            }
            // 4.5) Constantes 
            if (limpio is "NUL" or "CON" or "PRN" or "AUX" or "COM1" or "LPT1")
            {
                return new Token
                {
                    Tipo = TipoToken.Constante,
                    Texto = trozo,
                    Color = "CCDE86",
                    Icono = "▣",
                    Descripcion = $"Constante: {limpio}"
                };
            }

            // 4) Variables
            if (limpio.Contains('%'))
            {
                return new Token
                {
                    Tipo = TipoToken.Variable,
                    Texto = trozo,
                    Color = "E4D7F5",
                    Icono = "📌",
                    Descripcion = "Variable de entorno"
                };
            }
            // 4.5) ¿Es un comodín? (*, ?, [abc], [a-z])
            if (EsComodin(limpio))
            {
                return new Token
                {
                    Tipo = TipoToken.Comodin,
                    Texto = trozo,
                    Color = "FFF2CC",         // amarillo pálido
                    Icono = "◆",              // rombo relleno, distintivo
                    Descripcion = "Comodín de búsqueda"
                };
            }

            // 5) Patrones de archivo
            if (EsPatronArchivo(limpio))
            {
                return new Token
                {
                    Tipo = TipoToken.Texto,
                    Texto = trozo,
                    Color = "FFF2CC",
                    Icono = "🔎",
                    Descripcion = "Patrón de archivos"
                };
            }

            // 6) Rutas
            var comoRuta = IntentarComoRuta(limpio, directorioTrabajo);
            if (comoRuta != null)
            {
                var esCarpeta = Directory.Exists(comoRuta);
                return new Token
                {
                    Tipo = TipoToken.Ruta,
                    Texto = trozo.Contains(' ') ? $"\"{limpio}\"" : trozo,
                    Color = "DEEBF7",
                    Icono = esCarpeta ? "📁" : "📄",
                    Descripcion = (esCarpeta ? "Carpeta: " : "Archivo: ") + comoRuta,
                    Origen = comoRuta
                };
            }
           

            // 7) Texto genérico
            return new Token
            {
                Tipo = TipoToken.Texto,
                Texto = trozo,
                Color = "EDEDED",
                Icono = "📝",
                Descripcion = "Texto"
            };
        }

        /// <summary>Helper para determinar si un string es un patrón de archivo.</summary>
        private static bool EsPatronArchivo(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            // Contiene * o ? pero no es una ruta válida con drive.
            if (!s.Contains('*') && !s.Contains('?')) return false;
            if (s.Length >= 2 && s[1] == ':') return false; // C:\algo\*
            return true;
        }
        /// <summary>Helper para determinar si un string es un comodin.</summary>
        private static bool EsComodin(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (s == "*" || s == "?") return true;
            if (s.StartsWith("[") && s.EndsWith("]")) return true;
            if (s.Contains('*') || s.Contains('?'))
            {
                // Si ya es un patrón de archivo, lo clasificamos como comodín
                // en lugar de Texto. Pero si es una ruta completa (C:\...\*.txt),
                // lo dejamos como Ruta.
                if (s.Length >= 2 && s[1] == ':') return false;
                return true;
            }
            return false;
        }

        // ==================== Detección de rutas ====================

        private static string? IntentarComoRuta(string texto, string directorioTrabajo)
        {
            try
            {
                // Rutas absolutas (C:\...) → siempre se aceptan.
                if (Path.IsPathRooted(texto))
                {
                    var full = Path.GetFullPath(texto);
                    if (Directory.Exists(full) || File.Exists(full)) return full;
                    // Aceptamos igualmente rutas absolutas que no existan:
                    // pueden ser el destino de un copy/move.
                    return full;
                }

                // Rutas relativas → resolvemos contra el directorio de trabajo.
                // Solo las aceptamos si existen, para no confundir "hola"
                // con una ruta relativa.
                var combinada = Path.Combine(directorioTrabajo, texto);
                var fullRelativa = Path.GetFullPath(combinada);
                if (Directory.Exists(fullRelativa) || File.Exists(fullRelativa))
                    return fullRelativa;
            }
            catch
            {
                // Cualquier error de Path → no es una ruta.
            }

            return null;
        }

        // ==================== Tokenizador ====================

        /// <summary>
        /// Divide una línea de comando en tokens, respetando comillas dobles.
        /// Reglas:
        ///   - Mientras haya una comilla abierta, los espacios NO dividen.
        ///   - Una comilla sin cerrar implica que todo el resto es un único token.
        ///   - Las comillas se conservan en el token de salida (CMD las necesita).
        ///   - Se colapsan espacios múltiples consecutivos.
        ///   - Se recortan espacios al principio y al final.
        /// </summary>
        private static List<string> DividirRespetandoComillas(string texto)
        {
            var partes = new List<string>();
            if (string.IsNullOrEmpty(texto)) return partes;

            var sb = new StringBuilder();
            bool dentroComillas = false;

            foreach (var c in texto)
            {
                if (c == '"')
                {
                    // Alterna el estado de comillas.
                    dentroComillas = !dentroComillas;
                    sb.Append(c);
                }
                else if (char.IsWhiteSpace(c) && !dentroComillas)
                {
                    // Espacio fuera de comillas → cierra el token actual si hay algo.
                    if (sb.Length > 0)
                    {
                        partes.Add(sb.ToString());
                        sb.Clear();
                    }
                    // Los espacios múltiples se colapsan: si sb está vacío, no hacemos nada.
                }
                else
                {
                    // Cualquier otro carácter, o un espacio dentro de comillas,
                    // se acumula en el token actual.
                    sb.Append(c);
                }
            }

            // Si quedó algo acumulado (por ejemplo, una comilla sin cerrar),
            // lo añadimos como último token.
            if (sb.Length > 0)
                partes.Add(sb.ToString());

            return partes;
        }
    }
}