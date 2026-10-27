using System;
using System.Collections.ObjectModel;
using VisualCommander.Models;

namespace VisualCommander.Services.UndoRedo.Actions
{
    /// <summary>
    /// Acción: mover una línea de una posición a otra.
    /// </summary>
    public class MoverLineaAction : IUndoableAction
    {
        private readonly ObservableCollection<LineaComando> _lineas;
        private readonly LineaComando _linea;
        private readonly int _indiceOrigen;
        private readonly int _indiceDestino;

        public string Descripcion =>
            $"Mover línea {_indiceOrigen + 1} → {_indiceDestino + 1}";

        public MoverLineaAction(
            ObservableCollection<LineaComando> lineas,
            LineaComando linea,
            int indiceOrigen,
            int indiceDestino)
        {
            _lineas = lineas;
            _linea = linea;
            _indiceOrigen = indiceOrigen;
            _indiceDestino = indiceDestino;
        }

        public void Execute()
        {
            // Mover al destino.
            var indiceActual = _lineas.IndexOf(_linea);
            if (indiceActual < 0) return;

            _lineas.Move(indiceActual, _indiceDestino);
        }

        public void Undo()
        {
            // Mover al origen.
            var indiceActual = _lineas.IndexOf(_linea);
            if (indiceActual < 0) return;

            _lineas.Move(indiceActual, _indiceOrigen);
        }
    }
}