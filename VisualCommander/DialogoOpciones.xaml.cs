using System.Windows;
using VisualCommander.Models;
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
            DataContext = viewModel;

            // Cargar estado.
            ChkModoSilencioso.IsChecked = _viewModel.ModoSilencioso;

            if (_viewModel.EsModoAprendiz)
                RbAprendiz.IsChecked = true;
            else
                RbExperto.IsChecked = true;
        }

        private void RbAprendiz_Checked(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null && _viewModel.EsModoExperto)
                _viewModel.ModoTrabajo = ModoTrabajo.Aprendiz;
        }

        private void RbExperto_Checked(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null && _viewModel.EsModoAprendiz)
                _viewModel.ModoTrabajo = ModoTrabajo.Experto;
        }

        private void Aceptar_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ModoSilencioso = ChkModoSilencioso.IsChecked == true;
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