using System.Collections.ObjectModel;
using VisualCommander.Models;

namespace VisualCommander.Services.UndoRedo.Actions
{
    /// <summary>
    /// Acción: añadir una línea nueva en una posición concreta.
    /// </summary>
    public class AgregarLineaAction : IUndoableAction
    {
        private readonly ObservableCollection<LineaComando> _lineas;
        private readonly LineaComando _linea;
        private readonly int _indice;

        public string Descripcion => $"Añadir línea {_indice + 1}";

        public AgregarLineaAction(
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
            // Insertar en la posición.
            int idx = _indice;
            if (idx < 0) idx = 0;
            if (idx > _lineas.Count) idx = _lineas.Count;
            _lineas.Insert(idx, _linea);
        }

        public void Undo()
        {
            // Quitar la línea.
            _lineas.Remove(_linea);
        }
    }
}