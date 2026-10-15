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
    public class LineaIncompletaVista
    {
        public string Encabezado { get; set; } = "";
        public string Comando { get; set; } = "";
        public string DummiesFaltantes { get; set; } = "";
    }

    public partial class ConfirmarPlanDialog : Window
    {
        public bool NoPreguntarMas => ChkNoPreguntar.IsChecked == true;

        public ConfirmarPlanDialog(
            List<(LineaComando Linea, ResultadoSimulacion Sim)> lineasPeligrosas,
            List<LineaComando> lineasIncompletas)
        {
            InitializeComponent();

            int total = lineasPeligrosas.Count + lineasIncompletas.Count;

            TxtSubtitulo.Text = $"{total} situación(es) requieren tu atención.";

            // Sección 1: peligrosas
            if (lineasPeligrosas.Count > 0)
            {
                var items = new List<LineaPeligrosaVista>();
                foreach (var (linea, sim) in lineasPeligrosas)
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
            else
            {
                TituloPeligrosas.Visibility = Visibility.Collapsed;
                ListaLineasPeligrosas.Visibility = Visibility.Collapsed;
            }
            // Sección 2: incompletas
            if (lineasIncompletas.Count > 0)
            {
                var items = new List<LineaIncompletaVista>();
                foreach (var linea in lineasIncompletas)
                {
                    var faltantes = linea.DummiesRequeridos
                        .Select(d => $"[{d.TextoDummy}]")
                        .ToList();

                    items.Add(new LineaIncompletaVista
                    {
                        Encabezado = $"Línea {linea.NumeroLinea}",
                        Comando = linea.Texto,
                        DummiesFaltantes = "Faltan: " + string.Join(", ", faltantes)
                    });
                }
                ListaLineasIncompletas.ItemsSource = items;
            }
            else
            {
                TituloIncompletas.Visibility = Visibility.Collapsed;
                ListaLineasIncompletas.Visibility = Visibility.Collapsed;
            }
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