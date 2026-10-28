using System.Windows;

namespace VisualCommander
{
    public partial class DialogoImportacionLimitada : Window
    {
        public bool NoVolverAMostrar { get; private set; }

        public DialogoImportacionLimitada(string extension)
        {
            InitializeComponent();

            if (extension == ".ps1")
            {
                TxtMensaje.Text =
                    "Visual CMD Studio aún no interpreta PowerShell.\n\n" +
                    "El archivo se importará como TEXTO PLANO: cada línea será una " +
                    "línea del plan, pero los cmdlets de PowerShell NO se reconocerán " +
                    "como comandos ni se mostrarán sus parámetros en el constructor visual.\n\n" +
                    "Podrás editar el texto, guardar la sesión y exportarla de nuevo, " +
                    "pero no aprovecharás las ventajas del editor visual en este plan.";
            }
            else
            {
                TxtMensaje.Text =
                    "Visual CMD Studio aún no interpreta Bash.\n\n" +
                    "El archivo se importará como TEXTO PLANO: cada línea será una " +
                    "línea del plan, pero los comandos de Bash NO se reconocerán " +
                    "como comandos ni se mostrarán sus parámetros en el constructor visual.\n\n" +
                    "Podrás editar el texto, guardar la sesión y exportarla de nuevo, " +
                    "pero no aprovecharás las ventajas del editor visual en este plan.";
            }
        }

        private void Importar_Click(object sender, RoutedEventArgs e)
        {
            NoVolverAMostrar = ChkNoVolver.IsChecked == true;
            DialogResult = true;
        }

        private void Cancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}