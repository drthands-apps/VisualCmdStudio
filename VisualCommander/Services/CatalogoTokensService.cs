using System.IO;
using System.Text.Json;
using VisualCommander.Models;

namespace VisualCommander.Services
{
    /// <summary>
    /// Servicio para cargar el catálogo de tokens pequeños desde tokens.json.
    /// </summary>
    public static class CatalogoTokensService
    {
        public static CatalogoTokens Cargar()
        {
            var ruta = Path.Combine(System.AppContext.BaseDirectory, "Data", "tokens.json");
            if (!File.Exists(ruta))
                ruta = Path.Combine(System.AppContext.BaseDirectory, "tokens.json");

            if (!File.Exists(ruta))
            {
                return new CatalogoTokens();
            }

            try
            {
                var json = File.ReadAllText(ruta);
                var opciones = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                return JsonSerializer.Deserialize<CatalogoTokens>(json, opciones)
                       ?? new CatalogoTokens();
            }
            catch (JsonException ex)
            {
              
                return new CatalogoTokens();
            }
            catch (Exception ex)
            {
                
                return new CatalogoTokens();
            }
        }
    }
}