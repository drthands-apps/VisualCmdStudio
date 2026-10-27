namespace VisualCommander.Services.UndoRedo
{
    /// <summary>
    /// Representa una acción del usuario que puede deshacerse y rehacerse.
    /// </summary>
    public interface IUndoableAction
    {
        /// <summary>Descripción legible de la acción (para la UI).</summary>
        string Descripcion { get; }

        /// <summary>Ejecuta la acción (o la rehace).</summary>
        void Execute();

        /// <summary>Deshace la acción.</summary>
        void Undo();
    }
}