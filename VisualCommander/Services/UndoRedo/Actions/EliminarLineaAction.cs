using System.Collections.ObjectModel;
using VisualCommander.Models;

namespace VisualCommander.Services.UndoRedo.Actions
{
    /// <summary>
    /// Acción: eliminar una línea en una posición concreta.
    /// Al deshacer, se reinserta en la misma posición.
    /// </summary>
    public class EliminarLineaAction : IUndoableAction
    {
        private readonly ObservableCollection<LineaComando> _lineas;
        private readonly LineaComando _linea;
        private readonly int _indice;

        public string Descripcion => $"Eliminar línea {_indice + 1}";

        public EliminarLineaAction(
            ObservableCollection<LineaComando> lineas,
            LineaComando linea,
            int indice)
        {
            _lineas = lineas;
            _linea = linea;
            _indice = indice;
        }

        public void Execute()
        {
            _lineas.Remove(_linea);
        }

        public void Undo()
        {
            // Reinsertar en la posición original.
            int idx = _indice;
            if (idx < 0) idx = 0;
            if (idx > _lineas.Count) idx = _lineas.Count;
            _lineas.Insert(idx, _linea);
        }
    }
}