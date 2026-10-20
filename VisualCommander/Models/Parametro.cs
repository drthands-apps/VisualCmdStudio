using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VisualCommander.Models
{
    /// <summary>
    /// Tipo de parámetro, determina cómo se pide al usuario y cómo se pinta.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TipoParametro
    {
        Bandera,   // Sin valor: /S, /B, /A ...
        Texto,     // Valor de texto libre
        Ruta,      // Ruta de archivo o carpeta (con selector)
        Numero,    // Valor numérico
        Lista      // Elegir de un conjunto cerrado (usa ValoresPosibles)
    }

    /// <summary>
    /// Representa un modificador o argumento válido de un comando.
    /// </summary>
    public class Parametro
    {
        /// <summary>Texto que se inserta en la línea (ej. "/S", "/A:", "ruta").</summary>
        public string Nombre { get; set; } = "";

        /// <summary>Descripción mostrada en la ayuda.</summary>
        public string Descripcion { get; set; } = "";

        /// <summary>Tipo del parámetro.</summary>
        public TipoParametro Tipo { get; set; } = TipoParametro.Bandera;

        /// <summary>¿Es obligatorio para que el comando funcione?</summary>
        public bool Requerido { get; set; }

        /// <summary>Valor por defecto si lo hay.</summary>
        public string? ValorPorDefecto { get; set; }

        /// <summary>Lista de valores válidos si Tipo == Lista.</summary>
        public string[]? ValoresPosibles { get; set; }

        /// <summary>
        /// Diccionario de descripciones de los valores posibles.
        /// Clave: valor. Valor: descripción.
        /// </summary>
        public Dictionary<string, string>? DescripcionValores { get; set; }

        // ================== NUEVO (Paso 3.1) ==================

        /// <summary>
        /// Si es true, este parámetro solo puede aparecer una vez en la línea.
        /// Por defecto true, que es el caso habitual.
        /// </summary>
        public bool Unico { get; set; } = true;

        /// <summary>
        /// Nombres de parámetros con los que es incompatible.
        /// Ejemplo: en "dir", "/W" es incompatible con "/B".
        /// </summary>
        public List<string> IncompatibleCon { get; set; } = new();

        /// <summary>
        /// Nombres de parámetros que exige que estén presentes si este se usa.
        /// Ejemplo: "/LOG" requiere que exista "/R".
        /// </summary>
        public List<string> Requiere { get; set; } = new();

        // ================== FIN NUEVO ==================

        /// <summary>
        /// Texto que se muestra en la lista de modificadores de la derecha.
        /// Propiedad calculada.
        /// </summary>
        public string TextoMostrado =>
            string.IsNullOrEmpty(Descripcion)
                ? Nombre
                : $"{Nombre,-8}  ·  {Descripcion}";
    }
}