using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using VisualCommander.Models;

namespace VisualCommander
{
    public partial class DialogoExportarPs1 : Window
    {
        private readonly IList<LineaComando> _lineas;
        private readonly string _rutaSugerida;

        public string RutaSeleccionada => TxtRuta.Text;
        public OpcionesExportacionPs1 Opciones { get; private set; } = new();

        public DialogoExportarPs1(
            IList<LineaComando> lineas,
            string rutaSugerida)
        {
            InitializeComponent();
            _lineas = lineas;
            _rutaSugerida = rutaSugerida;
            TxtRuta.Text = rutaSugerida;

            ActualizarPreview();
        }

        private void Examinar_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Guardar script PowerShell",
                Filter = "Scripts PowerShell (*.ps1)|*.ps1|Todos los archivos (*.*)|*.*",
                DefaultExt = ".ps1",
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
                var contenido = ExportadorPs1.GenerarContenido(_lineas, opcionesTemp);

                var lineas = contenido.Split('\n');
                var preview = string.Join("\n", lineas, 0, Math.Min(15, lineas.Length));
                if (lineas.Length > 15) preview += "\n...";

                TxtPreview.Text = preview;
            }
            catch (Exception ex)
            {
                TxtPreview.Text = $"(Error generando preview: {ex.Message})";
            }
        }

        private OpcionesExportacionPs1 LeerOpciones()
        {
            return new OpcionesExportacionPs1
            {
                IncluirCabecera = ChkCabecera.IsChecked == true,
                IncluirAdvertenciaPolitica = ChkAdvertencia.IsChecked == true,
                IncluirDirectorioTrabajo = ChkDirectorio.IsChecked == true,
                IncluirPausaFinal = ChkPausa.IsChecked == true
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