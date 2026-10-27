using VisualCommander.Models;

namespace VisualCommander.Services.UndoRedo.Actions
{
    /// <summary>
    /// Acción: mover un token dentro de la línea activa.
    /// Si tokenAEliminar no es null, primero lo elimina (Ctrl+Drop).
    /// </summary>
    public class MoverTokenAction : IUndoableAction
    {
        private readonly LineaComando _linea;
        private readonly string _textoAnterior;
        private readonly string _textoNuevo;

        public string Descripcion { get; }

        public MoverTokenAction(
            LineaComando linea,
            Token tokenOrigen,
            int indiceDestino,
            Token? tokenAEliminar = null)
        {
            _linea = linea;
            _textoAnterior = linea.Texto;

            // Calcular texto nuevo.
            var tokens = linea.Tokens.ToList();
            var indiceOrigen = tokens.IndexOf(tokenOrigen);
            if (indiceOrigen < 0)
            {
                _textoNuevo = _textoAnterior;
                Descripcion = "Mover token (sin cambios)";
                return;
            }

            // Si hay token a eliminar, quitarlo primero.
            if (tokenAEliminar != null)
            {
                var indiceEliminar = tokens.IndexOf(tokenAEliminar);
                if (indiceEliminar >= 0 && indiceEliminar != indiceOrigen)
                {
                    tokens.RemoveAt(indiceEliminar);
                    if (indiceEliminar < indiceOrigen)
                        indiceOrigen--;
                }
                Descripcion = "Sustituir token";
            }
            else
            {
                Descripcion = "Mover token";
            }

            // Quitar el origen.
            tokens.RemoveAt(indiceOrigen);

            // Ajustar destino.
            if (indiceDestino > indiceOrigen) indiceDestino--;
            if (indiceDestino < 0) indiceDestino = 0;
            if (indiceDestino > tokens.Count) indiceDestino = tokens.Count;

            // Insertar en destino.
            tokens.Insert(indiceDestino, tokenOrigen);

            _textoNuevo = string.Join(" ", tokens.Select(t => t.Texto));
        }

        public void Execute() => _linea.ActualizarDesdeTexto(_textoNuevo);
        public void Undo() => _linea.ActualizarDesdeTexto(_textoAnterior);
    }
}