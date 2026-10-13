using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VisualCommander.Models
{
    /// <summary>Tipo de token. Determina cómo se pinta y qué acepta.</summary>
    public enum TipoToken
    {
        Comando,
        Parametro,
        Operador,
        Variable,
        Constante,
        Ruta,
        Texto,
        Separador
    }

    /// <summary>
    /// Una pieza atómica del comando que el usuario va componiendo.
    /// </summary>
    public class Token : INotifyPropertyChanged
    {
        /// <summary>Tipo del token.</summary>
        public TipoToken Tipo { get; set; }

        private string _texto = "";
        /// <summary>Texto literal del token.</summary>
        public string Texto
        {
            get => _texto;
            set
            {
                if (_texto == value) return;
                _texto = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TextoMostrado));
            }
        }

        /// <summary>Descripción corta, para tooltips y ayuda.</summary>
        public string? Descripcion { get; set; }

        /// <summary>Color del bloque en formato hex SIN "#".</summary>
        public string Color { get; set; } = "CCCCCC";

        /// <summary>Icono o emoji opcional.</summary>
        public string? Icono { get; set; }

        /// <summary>
        /// Referencia opcional al objeto de origen (Comando, Parametro...).
        /// Nos servirá luego para validar y para reconstruir la línea al editar.
        /// </summary>
        public object? Origen { get; set; }

        /// <summary>Texto que se muestra dentro del bloque en la UI.</summary>
        public string TextoMostrado =>
            Tipo == TipoToken.Separador ? "␣" : Texto;

        /// <summary>
        /// ¿Es este token editable in-place (doble clic)?
        /// Los tokens de tipo Texto y Ruta son editables.
        /// </summary>
        public bool EsEditable => Tipo == TipoToken.Texto || Tipo == TipoToken.Ruta;

        /// <summary>
        /// ¿Está este token en modo edición in-place?
        /// Propiedad de UI, no de negocio. Se usa para el DataTrigger.
        /// </summary>
        private bool _estoyEditando;
        public bool EstoyEditando
        {
            get => _estoyEditando;
            set
            {
                if (_estoyEditando == value) return;
                _estoyEditando = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Texto original guardado al entrar en edición. Se usa para restaurar con Escape.
        /// </summary>
        public string? TextoAntesDeEditar { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? nombre = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
    }
}