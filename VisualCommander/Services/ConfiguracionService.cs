using System;
using System.IO;
using System.Text.Json;
using VisualCommander.Models;

namespace VisualCommander.Services
{
    /// <summary>
    /// Servicio para cargar y guardar la configuración de la aplicación.
    /// La configuración se guarda en %APPDATA%\VisualCmdStudio\config.json.
    /// </summary>
    public static class ConfiguracionService
    {
        private static readonly string CarpetaConfig =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VisualCmdStudio");

        private static readonly string RutaConfig =
            Path.Combine(CarpetaConfig, "config.json");

        /// <summary>
        /// Carga la configuración desde disco. Si no existe o hay error,
        /// devuelve una configuración por defecto.
        /// </summary>
        public static Configuracion Cargar()
        {
            try
            {
                if (!File.Exists(RutaConfig))
                    return new Configuracion();

                var json = File.ReadAllText(RutaConfig);
                var config = JsonSerializer.Deserialize<Configuracion>(json);
                return config ?? new Configuracion();
            }
            catch
            {
                // Si hay cualquier error, devolvemos la config por defecto.
                return new Configuracion();
            }
        }

        /// <summary>
        /// Guarda la configuración en disco. Crea la carpeta si no existe.
        /// </summary>
        public static void Guardar(Configuracion config)
        {
            try
            {
                if (!Directory.Exists(CarpetaConfig))
                    Directory.CreateDirectory(CarpetaConfig);

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(RutaConfig, json);
            }
            catch
            {
                // Silencioso: si falla el guardado, no pasa nada crítico.
            }
        }
    }
}