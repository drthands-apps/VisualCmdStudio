using System.Collections.Generic;
using System.Windows;
using VisualCommander.Models;

namespace VisualCommander
{
    public partial class ConfirmarCierreDialog : Window
    {
        /// <summary>¿El usuario eligió cerrar igualmente?</summary>
        public bool CerrarIgualmente { get; private set; }

        public ConfirmarCierreDialog(IEnumerable<Token> dummiesRequeridos)
        {
            InitializeComponent();

            var textos = new List<string>();
            foreach (var d in dummiesRequeridos)
                textos.Add($"• [{d.TextoDummy}]");

            ListaDummiesRequeridos.ItemsSource = textos;
        }

        private void SeguirEditando_Click(object sender, RoutedEventArgs e)
        {
            CerrarIgualmente = false;
            DialogResult = false;
            Close();
        }

        private void CerrarIgualmente_Click(object sender, RoutedEventArgs e)
        {
            CerrarIgualmente = true;
            DialogResult = true;
            Close();
        }
    }
}