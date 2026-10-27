using VisualCommander.Models;

namespace VisualCommander.Services.UndoRedo.Actions
{
    /// <summary>
    /// Acción: cambiar el texto de una línea.
    /// Sirve para agregar token, eliminar token, y cualquier cambio
    /// que se traduzca en un texto anterior → texto nuevo.
    /// </summary>
    public class CambiarTextoLineaAction : IUndoableAction
    {
        private readonly LineaComando _linea;
        private readonly string _textoAnterior;
        private readonly string _textoNuevo;

        public string Descripcion { get; }

        public CambiarTextoLineaAction(
            LineaComando linea,
            string textoAnterior,
            string textoNuevo,
            string descripcion)
        {
            _linea = linea;
            _textoAnterior = textoAnterior;
            _textoNuevo = textoNuevo;
            Descripcion = descripcion;
        }

        public void Execute()
        {
            _linea.ActualizarDesdeTexto(_textoNuevo);
        }

        public void Undo()
        {
            _linea.ActualizarDesdeTexto(_textoAnterior);
        }
    }
}
