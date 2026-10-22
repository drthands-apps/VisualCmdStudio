using System.Collections.Generic;

namespace VisualCommander.Models
{
    /// <summary>
    /// Información sobre un token "pequeño" (constante, variable, comodín, operador).
    /// Se carga desde tokens.json.
    /// </summary>
    public class TokenInfo
    {
        /// <summary>Texto exacto que se inserta en la línea.</summary>
        public string Nombre { get; set; } = "";

        /// <summary>Descripción amplia para el panel de detalles.</summary>
        public string Descripcion { get; set; } = "";

        /// <summary>Ejemplo de uso (opcional).</summary>
        public string? Ejemplo { get; set; }

        /// <summary>Categoría para agrupar visualmente (Sistema, Usuario...).</summary>
        public string Categoria { get; set; } = "";
    }

    /// <summary>
    /// Catálogo completo de tokens pequeños. Se carga desde tokens.json.
    /// </summary>
    public class CatalogoTokens
    {
        public List<TokenInfo> Constantes { get; set; } = new();
        public List<TokenInfo> Variables { get; set; } = new();
        public List<TokenInfo> Comodines { get; set; } = new();
        public List<TokenInfo> Operadores { get; set; } = new();
    }
}