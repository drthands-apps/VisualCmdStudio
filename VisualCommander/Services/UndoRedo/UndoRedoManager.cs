using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VisualCommander.Services.UndoRedo
{
    /// <summary>
    /// Gestor global de undo/redo. Mantiene dos stacks:
    /// - Undo: acciones que se pueden deshacer.
    /// - Redo: acciones que se pueden rehacer (después de un Undo).
    /// </summary>
    public class UndoRedoManager : INotifyPropertyChanged
    {
        private const int MaxStackSize = 100;

        private readonly Stack<IUndoableAction> _undoStack = new();
        private readonly Stack<IUndoableAction> _redoStack = new();

        /// <summary>¿Hay acciones que se puedan deshacer?</summary>
        public bool PuedeDeshacer => _undoStack.Count > 0;

        /// <summary>¿Hay acciones que se puedan rehacer?</summary>
        public bool PuedeRehacer => _redoStack.Count > 0;

        /// <summary>Descripción de la próxima acción a deshacer (o null).</summary>
        public string? DescripcionDeshacer =>
            _undoStack.Count > 0 ? _undoStack.Peek().Descripcion : null;

        /// <summary>Descripción de la próxima acción a rehacer (o null).</summary>
        public string? DescripcionRehacer =>
            _redoStack.Count > 0 ? _redoStack.Peek().Descripcion : null;

        /// <summary>
        /// Ejecuta una acción y la añade al stack de undo.
        /// Limpia el stack de redo (una nueva acción invalida el rehacer).
        /// </summary>
        public void Ejecutar(IUndoableAction action)
        {
            action.Execute();

            _undoStack.Push(action);
            _redoStack.Clear();

            // Acotar el tamaño del stack.
            if (_undoStack.Count > MaxStackSize)
            {
                // Stack no permite quitar el del fondo fácilmente.
                // Convertimos a lista, cortamos, y volvemos a apilar.
                var lista = new List<IUndoableAction>(_undoStack);
                lista = lista.GetRange(0, MaxStackSize);
                _undoStack.Clear();
                for (int i = lista.Count - 1; i >= 0; i--)
                    _undoStack.Push(lista[i]);
            }

            OnPropertyChanged(nameof(PuedeDeshacer));
            OnPropertyChanged(nameof(PuedeRehacer));
            OnPropertyChanged(nameof(DescripcionDeshacer));
            OnPropertyChanged(nameof(DescripcionRehacer));
        }

        /// <summary>Deshace la última acción.</summary>
        public bool Deshacer()
        {
            if (!PuedeDeshacer) return false;

            var action = _undoStack.Pop();
            action.Undo();
            _redoStack.Push(action);

            OnPropertyChanged(nameof(PuedeDeshacer));
            OnPropertyChanged(nameof(PuedeRehacer));
            OnPropertyChanged(nameof(DescripcionDeshacer));
            OnPropertyChanged(nameof(DescripcionRehacer));
            return true;
        }

        /// <summary>Rehace la última acción deshecha.</summary>
        public bool Rehacer()
        {
            if (!PuedeRehacer) return false;

            var action = _redoStack.Pop();
            action.Execute();
            _undoStack.Push(action);

            OnPropertyChanged(nameof(PuedeDeshacer));
            OnPropertyChanged(nameof(PuedeRehacer));
            OnPropertyChanged(nameof(DescripcionDeshacer));
            OnPropertyChanged(nameof(DescripcionRehacer));
            return true;
        }

        /// <summary>Limpia ambos stacks (por ejemplo, al cargar una sesión).</summary>
        public void Limpiar()
        {
            _undoStack.Clear();
            _redoStack.Clear();

            OnPropertyChanged(nameof(PuedeDeshacer));
            OnPropertyChanged(nameof(PuedeRehacer));
            OnPropertyChanged(nameof(DescripcionDeshacer));
            OnPropertyChanged(nameof(DescripcionRehacer));
        }

        // ==== INotifyPropertyChanged ====

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? nombre = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
    }
}