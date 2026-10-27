using System.Collections.ObjectModel;
using VisualCommander.Models;
using VisualCommander.Services.UndoRedo;

public class PegarLineaAction : IUndoableAction
{
    private readonly ObservableCollection<LineaComando> _lineas;
    private readonly LineaComando _linea;
    private readonly int _indice;

    public string Descripcion => $"Pegar línea {_indice + 1}";

    public PegarLineaAction(
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
        int idx = _indice;
        if (idx < 0) idx = 0;
        if (idx > _lineas.Count) idx = _lineas.Count;
        _lineas.Insert(idx, _linea);
    }

    public void Undo()
    {
        _lineas.Remove(_linea);
    }
}