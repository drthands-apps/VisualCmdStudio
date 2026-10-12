using System.Collections.Generic;
using System.Linq;
using System.Windows;
using VisualCommander.Models;

namespace VisualCommander
{
    /// <summary>Elemento de la lista de líneas peligrosas, para el binding.</summary>
    public class LineaPeligrosaVista
    {
        public string Encabezado { get; set; } = "";
        public string Directorio { get; set; } = "";
        public string Comando { get; set; } = "";
        public string Efectos { get; set; } = "";
    }

    public partial class ConfirmarPlanDialog : Window
    {
        public bool NoPreguntarMas => ChkNoPreguntar.IsChecked == true;

        public ConfirmarPlanDialog(List<(LineaComando Linea, ResultadoSimulacion Sim)> lineas)
        {
            InitializeComponent();

            TxtSubtitulo.Text =
                $"{lineas.Count} línea(s) contienen comandos peligrosos.";

            var items = new List<LineaPeligrosaVista>();
            foreach (var (linea, sim) in lineas)
            {
                items.Add(new LineaPeligrosaVista
                {
                    Encabezado = $"Línea {linea.NumeroLinea} — {sim.Comando}",
                    Directorio = $"Se ejecutará en: {linea.DirectorioTrabajo}",
                    Comando = linea.Texto,
                    Efectos = sim.Resumen
                });
            }

            ListaLineasPeligrosas.ItemsSource = items;
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Ejecutar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}