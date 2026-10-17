using System.Windows;

namespace VisualCommander
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            System.Text.Encoding.RegisterProvider(
                System.Text.CodePagesEncodingProvider.Instance);

            base.OnStartup(e);
        }
        /// <summary>
        /// Limpia los archivos temporales de ejecuciones anteriores.
        /// Se llama al arrancar la app.
        /// </summary>
        private static void LimpiarTemporalesAntiguos()
        {
            try
            {
                var carpetaTemp = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VisualCmdStudio", "temp");

                if (!System.IO.Directory.Exists(carpetaTemp))
                    return;

                foreach (var archivo in System.IO.Directory.GetFiles(carpetaTemp))
                {
                    try { System.IO.File.Delete(archivo); } catch { }
                }
            }
            catch { }
        }
    }
}