using System.Windows;
using VisualCommander.ViewModels;

namespace VisualCommander
{
    public partial class DialogoOpciones : Window
    {
        private readonly MainViewModel _viewModel;

        public DialogoOpciones(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;

            // Cargar estado actual.
            ChkModoSilencioso.IsChecked = _viewModel.ModoSilencioso;
        }

        private void Aceptar_Click(object sender, RoutedEventArgs e)
        {
            // Aplicar cambios.
            _viewModel.ModoSilencioso = ChkModoSilencioso.IsChecked == true;

            // TODO futuro: aplicar codificación por defecto, idioma, etc.

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