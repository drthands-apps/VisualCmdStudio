using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using VisualCommander.Models;

namespace VisualCommander
{
    public partial class DialogoExportarSh : Window
    {
        private readonly IList<LineaComando> _lineas;
        private readonly string _rutaSugerida;

        public string RutaSeleccionada => TxtRuta.Text;
        public OpcionesExportacionSh Opciones { get; private set; } = new();

        public DialogoExportarSh(IList<LineaComando> lineas, string rutaSugerida)
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
                Title = "Guardar script Bash",
                Filter = "Scripts Bash (*.sh)|*.sh|Todos los archivos (*.*)|*.*",
                DefaultExt = ".sh",
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
                var contenido = ExportadorSh.GenerarContenido(_lineas, opcionesTemp);
                var lineas = contenido.Split('\n');
                var preview = string.Join("\n", lineas, 0, Math.Min(15, lineas.Length));
                if (lineas.Length > 15) preview += "\n...";
                TxtPreview.Text = preview;
            }
            catch (Exception ex)
            {
                TxtPreview.Text = $"(Error: {ex.Message})";
            }
        }

        private OpcionesExportacionSh LeerOpciones()
        {
            return new OpcionesExportacionSh
            {
                IncluirCabecera = ChkCabecera.IsChecked == true,
                IncluirAdvertencia = ChkAdvertencia.IsChecked == true,
                IncluirDirectorioTrabajo = ChkDirectorio.IsChecked == true
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