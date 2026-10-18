using System;
using System.IO;
using System.Text;
using System.Windows;
using VisualCommander.Models;

namespace VisualCommander
{
    public partial class DialogoExportarBat : Window
    {
        private readonly System.Collections.Generic.IList<LineaComando> _lineas;
        private readonly string _rutaSugerida;

        private readonly string _extension = ".bat";

        public string RutaSeleccionada => TxtRuta.Text;
        public OpcionesExportacionBat Opciones { get; private set; } = new();

        public DialogoExportarBat(
            IList<LineaComando> lineas,
            string rutaSugerida,
            string extension = ".bat")
        {
            InitializeComponent();

            _lineas = lineas;
            _rutaSugerida = rutaSugerida;
            _extension = extension;

            TxtRuta.Text = rutaSugerida;

            // Ajustar título según extensión.
            if (extension == ".cmd")
                Title = "Exportar como archivo CMD";
            else
                Title = "Exportar como archivo batch";

            ActualizarPreview();
        }

        private void Examinar_Click(object sender, RoutedEventArgs e)
        {
            var filtro = _extension == ".cmd"
                ? "Scripts CMD (*.cmd)|*.cmd|Todos los archivos (*.*)|*.*"
                : "Archivos batch (*.bat)|*.bat|Todos los archivos (*.*)|*.*";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = $"Guardar archivo {_extension}",
                Filter = filtro,
                DefaultExt = _extension,
                FileName = Path.GetFileName(_rutaSugerida),
                InitialDirectory = Path.GetDirectoryName(_rutaSugerida)
            };

            if (dlg.ShowDialog() == true)
            {
                TxtRuta.Text = dlg.FileName;
                ActualizarPreview();
            }
        }

        private void ActualizarPreview()
        {
            try
            {
                var opcionesTemp = LeerOpciones();
                var contenido = ExportadorBat.GenerarContenido(_lineas, opcionesTemp);

                // Mostramos las primeras 10 líneas.
                var lineas = contenido.Split('\n');
                var preview = string.Join("\n", lineas, 0, Math.Min(12, lineas.Length));
                if (lineas.Length > 12) preview += "\n...";

                TxtPreview.Text = preview;
            }
            catch (Exception ex)
            {
                TxtPreview.Text = $"(Error generando preview: {ex.Message})";
            }
        }

        private OpcionesExportacionBat LeerOpciones()
        {
            return new OpcionesExportacionBat
            {
                CodificacionUtf8 = RbUtf8.IsChecked == true,
                IncluirCabecera = ChkCabecera.IsChecked == true,
                IncluirPause = ChkPause.IsChecked == true
            };
        }

        private void Exportar_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtRuta.Text))
            {
                MessageBox.Show("Indica una ruta para el archivo.",
                    "Ruta vacía", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Opciones = LeerOpciones();
            DialogResult = true;
            Close();
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}