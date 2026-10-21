using System.Collections.Generic;

namespace VisualCommander.Models
{
   
    /// 
    /// <summary>
    /// Definición de un argumento esperado por un comando.
    /// Se declara en el JSON y determina cómo se generan los dummies guía.
    /// </summary>
    public class ArgumentoComando
    {
        /// <summary>Posición ordinal dentro de la línea (1 = primero).</summary>
        public int Posicion { get; set; }

        /// <summary>Nombre interno del argumento (ej. "origen", "destino").</summary>
        public string Nombre { get; set; } = "";

        /// <summary>Texto descriptivo que se muestra en el dummie.</summary>
        public string Dummy { get; set; } = "";

        /// <summary>Tipo de argumento esperado (para validación futura).</summary>
        public string Tipo { get; set; } = "Texto";

        /// <summary>¿Es obligatorio para que el comando sea válido?</summary>
        public bool Requerido { get; set; } = true;

        /// <summary>Descripción para el tooltip del dummie.</summary>
        public string Descripcion { get; set; } = "";
    }
    /// <summary>
    /// Representa un comando de Windows con su metadata completa.
    /// </summary>
    public class Comando
    {
        /// <summary>Identificador único, normalmente el propio nombre (ej. "dir").</summary>
        public string Id { get; set; } = "";

        /// <summary>Nombre visible del comando (ej. "dir").</summary>
        public string Nombre { get; set; } = "";

        /// <summary>Familia a la que pertenece (agrupa en la lista).</summary>
        public string Familia { get; set; } = "";

        /// <summary>Descripción breve para la lista.</summary>
        public string Resumen { get; set; } = "";

        /// <summary>Descripción larga para el panel de descripción.</summary>
        public string Descripcion { get; set; } = "";

        /// <summary>Sintaxis general.</summary>
        public string Sintaxis { get; set; } = "";
        /// <summary>
        /// Número de veces que este comando se ha usado.
        /// Se actualiza desde el MainViewModel al insertar el comando en un plan.
        /// No se serializa en el JSON del catálogo.
        /// </summary>
        public int Usos { get; set; }

        /// <summary>
        /// Argumentos que este comando espera, en orden.
        /// Se usan para generar dummies guía al insertar el comando.
        /// </summary>
        public List<ArgumentoComando> Argumentos { get; set; } = new();

        /// <summary>Indica si el comando puede ser destructivo (borra, formatea...).</summary>
        public bool Peligroso { get; set; }

        /// <summary>Lista de parámetros/modificadores válidos para este comando.</summary>
        public List<Parametro> Parametros { get; set; } = new();

        /// <summary>Ejemplos de uso.</summary>
        public List<string> Ejemplos { get; set; } = new();

        // ================== NUEVO (Paso 3.1) ==================

        /// <summary>
        /// Color de la familia en formato hexadecimal SIN el '#' (ej. "F4B400").
        /// Lo usa la vista para pintar el bloque/burbuja del comando.
        /// </summary>
        public string ColorFamilia { get; set; } = "CCCCCC";

        /// <summary>
        /// Icono o emoji identificativo (opcional, ej. "📁", "🌐").
        /// </summary>
        public string? Icono { get; set; }

        /// <summary>
        /// Tipos de parámetro que acepta este comando.
        /// Si está vacío, se asume que acepta cualquier tipo definido en su lista de Parametros.
        /// </summary>
        public List<TipoParametro> AceptaTipos { get; set; } = new();

        // ================== FIN NUEVO ==================

        /// <summary>
        /// Texto que se muestra en la lista. Propiedad calculada: no se guarda en JSON.
        /// </summary>
        public string TextoLista => $"{Nombre,-10} · {Resumen}";

        /// <summary>
        /// Si es true, el comando requiere permisos de administrador.
        /// Al ejecutarlo, se solicitará elevación de UAC solo para ese comando.
        /// </summary>
        public bool RequiereAdmin { get; set; }
    }
}