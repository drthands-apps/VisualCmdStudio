using System;
using System.IO;
using System.Text.Json;
using VisualCommander.Models;

namespace VisualCommander.Services
{
    /// <summary>
    /// Servicio para cargar/guardar el registro de uso de comandos.
    /// </summary>
    public static class UsoService
    {
        private static readonly string CarpetaConfig =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VisualCmdStudio");

        private static readonly string RutaUso =
            Path.Combine(CarpetaConfig, "uso.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Carga el registro de uso desde disco. Si no existe, devuelve uno vacío.
        /// </summary>
        public static RegistroUso Cargar()
        {
            try
            {
                if (!File.Exists(RutaUso))
                    return new RegistroUso();

                var json = File.ReadAllText(RutaUso);
                var reg = JsonSerializer.Deserialize<RegistroUso>(json, JsonOptions);
                return reg ?? new RegistroUso();
            }
            catch
            {
                return new RegistroUso();
            }
        }

        /// <summary>
        /// Guarda el registro de uso. Silencioso si falla.
        /// </summary>
        public static void Guardar(RegistroUso reg)
        {
            try
            {
                if (!Directory.Exists(CarpetaConfig))
                    Directory.CreateDirectory(CarpetaConfig);

                reg.Actualizado = DateTime.UtcNow;

                var json = JsonSerializer.Serialize(reg, JsonOptions);
                File.WriteAllText(RutaUso, json);
            }
            catch
            {
                // Silencioso.
            }
        }
    }
}