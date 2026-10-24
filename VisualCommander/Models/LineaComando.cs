using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace VisualCommander.Models
{
    public enum EstadoEjecucion
    {
        Pendiente,   // no se ha ejecutado todavía
        Ejecutando,  // se está ejecutando ahora mismo
        Exito,       // terminó con código 0
        Error,       // terminó con código ≠ 0
        Cancelada    // se detuvo por el usuario
    }

    /// <summary>
    /// Una línea del plan. El TEXTO es la fuente de verdad. Los Tokens
    /// son una proyección calculada que se usa para pintar la vista visual.
    /// </summary>
    public class LineaComando : INotifyPropertyChanged
    {
        // ==== Fuente de verdad: el texto ====

        private string _texto = "";
        /// <summary>
        /// Texto de la línea. Es la fuente de verdad. Cuando cambia,
        /// se re-proyectan los tokens automáticamente.
        /// </summary>
        /// 
        public string Texto
        {
            get => _texto;
            set
            {
                if (_texto == value) return;
                _texto = value ?? "";
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayText));

                // Al cambiar el texto, el estado de ejecución deja de ser válido.
                if (Estado != EstadoEjecucion.Pendiente)
                    Estado = EstadoEjecucion.Pendiente;

                ReprojectarTokens();
            }
        }

        /// <summary>Texto para mostrar en selectores; evita cadenas vacías.</summary>
        public string DisplayText =>
            string.IsNullOrWhiteSpace(Texto) ? "(línea vacía)" : Texto;

        // ==== Proyección: los tokens ====

        /// <summary>
        /// Tokens derivados del texto. Se reconstruye cada vez que el
        /// texto cambia. NO se debe modificar directamente.
        /// </summary>
        public ObservableCollection<Token> Tokens { get; } = new();

        /// <summary>
        /// Dummies (placeholders visuales) que se muestran en el constructor.
        /// NO forman parte del texto ni del modelo persistente.
        /// Son solo una ayuda visual para que el usuario sepa qué argumentos faltan.
        /// </summary>
        public ObservableCollection<Token> Dummies { get; } = new();

        /// <summary>
        /// Colección combinada de tokens reales + dummies, en el orden
        /// que se muestra en el constructor visual.
        /// Los dummies van DESPUÉS de todos los tokens reales.
        /// Solo UI: no afecta al texto ni al modelo persistente.
        /// </summary>
        public ObservableCollection<Token> BloquesVisibles { get; } = new();
        /// <summary>Modificadores del comando activo, para el panel derecho.</summary>
        public ObservableCollection<ParametroVista> Modificadores { get; } = new();

        /// <summary>
        /// ¿Hay algún dummie requerido sin rellenar?
        /// Se usa para bloquear el cierre (con confirmación).
        /// </summary>
        public bool TieneDummiesRequeridos =>
            Dummies.Any(d => d.EsRequerido);

        /// <summary>
        /// Lista de dummies requeridos sin rellenar.
        /// Se usa para mostrar el detalle en el diálogo de confirmación.
        /// </summary>
        public IEnumerable<Token> DummiesRequeridos =>
            Dummies.Where(d => d.EsRequerido);

        // ==== Estado de la línea ====

        private bool _esActiva;
        public bool EsActiva
        {
            get => _esActiva;
            set
            {
                if (_esActiva == value) return;
                _esActiva = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EsCerrada));
            }
        }

        public bool EsCerrada => !EsActiva;

        private EstadoEjecucion _estado = EstadoEjecucion.Pendiente;

        /// <summary>
        /// Evento que se dispara cuando aparece un comando nuevo en los tokens
        /// (por ejemplo, al escribir el comando en el campo CMD).
        /// </summary>
        public event Action<Comando>? ComandoReconocido;
        public EstadoEjecucion Estado
        {
            get => _estado;
            set
            {
                if (_estado == value) return;
                _estado = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EstadoSimbolo));
                OnPropertyChanged(nameof(EstadoColor));
            }
        }

        /// <summary>Símbolo para mostrar en la UI según el estado.</summary>
        public string EstadoSimbolo => Estado switch
        {
            EstadoEjecucion.Pendiente => "○",
            EstadoEjecucion.Ejecutando => "▶",
            EstadoEjecucion.Exito => "✓",
            EstadoEjecucion.Error => "✗",
            EstadoEjecucion.Cancelada => "⊘",
            _ => "○"
        };

        /// <summary>Color hex para pintar el símbolo.</summary>
        public string EstadoColor => Estado switch
        {
            EstadoEjecucion.Pendiente => "888888",
            EstadoEjecucion.Ejecutando => "4A90D9",
            EstadoEjecucion.Exito => "27AE60",
            EstadoEjecucion.Error => "C0392B",
            EstadoEjecucion.Cancelada => "E67E22",
            _ => "888888"
        };

        /// <summary>¿Hay que resetear el estado al empezar una nueva ejecución?</summary>
        public void ResetearEstado()
        {
            Estado = EstadoEjecucion.Pendiente;
        }



        private int _numeroLinea;
        public int NumeroLinea
        {
            get => _numeroLinea;
            set
            {
                if (_numeroLinea == value) return;
                _numeroLinea = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TextContador));
            }
        }

        private int _totalLineas;
        public int TotalLineas
        {
            get => _totalLineas;
            set
            {
                if (_totalLineas == value) return;
                _totalLineas = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TextContador));
            }
        }

        public string TextContador => $"{NumeroLinea}/{TotalLineas}";

        // ==== Directorio de trabajo ====

        private string _directorioTrabajo = Environment.CurrentDirectory;
        public string DirectorioTrabajo
        {
            get => _directorioTrabajo;
            set
            {
                if (_directorioTrabajo == value) return;
                _directorioTrabajo = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PromptCmd));
                OnPropertyChanged(nameof(PromptCmdCorto));   // ← nuevo
                OnPropertyChanged(nameof(NivelPeligroDirectorio));
            }
        }

        public string PromptCmd => DirectorioTrabajo + ">";

        public int NivelPeligroDirectorio
        {
            get
            {
                var dir = DirectorioTrabajo.TrimEnd('\\').ToLowerInvariant();
                if (dir.Length == 2 && dir[1] == ':') return 2;

                string[] criticos =
                {
                    @"c:\windows",
                    @"c:\program files",
                    @"c:\program files (x86)",
                    @"c:\programdata",
                    @"c:\users\default",
                    @"c:\$recycle.bin"
                };
                foreach (var c in criticos)
                    if (dir == c || dir.StartsWith(c + "\\")) return 1;

                return 0;
            }
        }

     

        // ==== Comando activo ====

        private Token? _comandoActivo;
        public Token? ComandoActivo
        {
            get => _comandoActivo;
            private set
            {
                if (ReferenceEquals(_comandoActivo, value)) return;
                _comandoActivo = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TituloModificadores));
                ReconstruirModificadores();
            }
        }

        public string TituloModificadores =>
            ComandoActivo is null
                ? "MODIFICADORES (sin comando activo)"
                : $"MODIFICADORES DE: {ComandoActivo.Texto.ToUpperInvariant()}";

        // ==== Sincronización ====

        /// <summary>
        /// Bandera que impide ciclos cuando una actualización en curso
        /// dispara otra actualización desde el otro lado.
        /// </summary>
        private bool _sincronizando;

        /// <summary>¿Se está aplicando un cambio ahora mismo?</summary>
        public bool Sincronizando => _sincronizando;

        // ==== Parser inyectado (para la proyección) ====

        /// <summary>
        /// Función que convierte un texto en una lista de tokens.
        /// El MainViewModel la asigna al cargar el catálogo.
        /// </summary>
        public static Func<string, string, IEnumerable<Token>>? Proyector { get; set; }

       

        /// <summary>Estado actual del undo (rehacer).</summary>
        private readonly Stack<string> _rehacer = new();








        // ==== Constructor ====

        public LineaComando()
        {
            // Apilamos el estado inicial (vacío) para poder deshacer hasta aquí.
            ConfirmarEstado();
        }

        // ==== API pública (compatible con lo que ya existe) ====
        /// <summary>
        /// ¿Requiere esta línea permisos de administrador?
        /// Es true si algún token de tipo Comando lo requiere.
        /// </summary>
        public bool RequiereAdmin =>
            Tokens.Any(t => t.Tipo == TipoToken.Comando && t.RequiereAdmin);

        /// <summary>
        /// Reemplaza el texto por otro y re-proyecta. Es la operación
        /// principal de cambio de contenido.
        /// </summary>
        public void ActualizarDesdeTexto(string nuevoTexto)
        {
            if (_sincronizando) return;
            try
            {
                _sincronizando = true;
                Texto = nuevoTexto;
            }
            finally
            {
                _sincronizando = false;
            }
        }

        /// <summary>
        /// Reemplaza el texto a partir de una secuencia de tokens.
        /// Construye el texto uniendo los textos de los tokens con espacios.
        /// </summary>
        public void ActualizarDesdeTokens(IEnumerable<Token> tokens)
        {
            var nuevoTexto = string.Join(" ",
                tokens.Where(t => t.Tipo != TipoToken.Separador)
                      .Select(t => t.Texto));
            ActualizarDesdeTexto(nuevoTexto);
        }

        // --- Compatibilidad con la API anterior ---

        /// <summary>Añade un token al final de la línea (equivalente a añadir la palabra al final del texto).</summary>
        public void AgregarToken(Token token)
        {
            var nuevos = Tokens.ToList();
            nuevos.Add(token);
            ActualizarDesdeTokens(nuevos);
        }

        /// <summary>Inserta un token en una posición (para uso futuro).</summary>
        public void InsertarToken(Token token, int indice)
        {
            var nuevos = Tokens.ToList();
            if (indice < 0) indice = 0;
            if (indice > nuevos.Count) indice = nuevos.Count;
            nuevos.Insert(indice, token);
            ActualizarDesdeTokens(nuevos);
        }

        /// <summary>Quita un token. Si era un parámetro marcado, desmarca el checkbox.</summary>
        public void QuitarToken(Token token)
        {
            if (token.Tipo == TipoToken.Parametro && token.Origen is Parametro p)
            {
                var pv = Modificadores.FirstOrDefault(m => m.Parametro == p);
                if (pv is { EstaSeleccionado: true })
                {
                    pv.EstaSeleccionado = false;
                    return;
                }
            }

            var nuevos = Tokens.Where(t => !ReferenceEquals(t, token)).ToList();
            ActualizarDesdeTokens(nuevos);
        }

        /// <summary>Vacía la línea.</summary>
        public void Limpiar()
        {
            ActualizarDesdeTexto("");
            Modificadores.Clear();
            ComandoActivo = null;
        }

        /// <summary>
        /// Reemplaza los tokens por una nueva lista. Mantenido por
        /// compatibilidad con el código anterior (parser desde texto).
        /// </summary>
        public void ReemplazarCon(IEnumerable<Token> nuevos)
        {
            ActualizarDesdeTokens(nuevos);
        }

        /// <summary>
        /// Devuelve el directorio que debería tener la SIGUIENTE línea,
        /// si esta contiene un `cd` o `pushd` con ruta válida.
        /// </summary>
        public string? CalcularDirectorioSiguiente()
        {
            if (Tokens.Count == 0) return null;

            var primero = Tokens[0].Texto.ToLowerInvariant();
            if (primero != "cd" && primero != "pushd") return null;

            for (int i = 1; i < Tokens.Count; i++)
            {
                var t = Tokens[i];
                if (t.Tipo != TipoToken.Ruta) continue;
                if (t.Origen is string ruta && Directory.Exists(ruta))
                    return ruta;
            }

            return null;
        }

        /// <summary>
        /// Crea una copia independiente de esta línea.
        /// </summary>
        public LineaComando Clonar()
        {
            var copia = new LineaComando
            {
                DirectorioTrabajo = DirectorioTrabajo,
                Texto = Texto   // ← re-proyecta automáticamente
            };
            // El constructor ya apiló "". Ahora apilamos el estado real.
            copia.ConfirmarEstado();
            return copia;
        }

        // ==== Proyección (interno) ====

        /// <summary>
        /// Recalcula la colección de tokens a partir del texto actual.
        /// Se llama automáticamente cuando cambia el Texto.
        /// </summary>
        private void ReprojectarTokens()
        {
            if (Proyector == null) return;

            // Guardar comandos antiguos antes de reproyectar.
            var comandosAntiguos = Tokens
                .Where(t => t.Tipo == TipoToken.Comando && t.Origen is Comando)
                .Select(t => ((Comando)t.Origen!).Nombre)
                .ToHashSet();

            IEnumerable<Token> nuevos;
            try { nuevos = Proyector(Texto, DirectorioTrabajo); }
            catch { nuevos = Array.Empty<Token>(); }

            Tokens.Clear();
            foreach (var t in nuevos)
                Tokens.Add(t);

            // Detectar comandos nuevos.
            foreach (var t in Tokens)
            {
                if (t.Tipo == TipoToken.Comando && t.Origen is Comando cmd)
                {
                    if (!comandosAntiguos.Contains(cmd.Nombre))
                    {
                        // Comando nuevo. Disparar evento.
                        ComandoReconocido?.Invoke(cmd);
                    }
                }
            }

            RecalcularComandoActivo();
            RecalcularDummies();
        }

        private void RecalcularComandoActivo()
        {
            Token? nuevo = null;
            for (int i = Tokens.Count - 1; i >= 0; i--)
            {
                var t = Tokens[i];
                if (t.Tipo == TipoToken.Operador) break;
                if (t.Tipo == TipoToken.Comando) { nuevo = t; break; }
            }

            // Si el comando activo representa el MISMO comando del catálogo,
            // no reconstruimos los modificadores: solo sincronizamos los checkboxes.
            if (ComandoActivo != null &&
                nuevo != null &&
                ReferenceEquals(ComandoActivo.Origen, nuevo.Origen))
            {
                _comandoActivo = nuevo;  // actualizamos la referencia al token
                OnPropertyChanged();
                OnPropertyChanged(nameof(TituloModificadores));
                SincronizarCheckboxes();
                return;
            }

            // En cualquier otro caso, asignamos (lo que dispara reconstrucción).
            ComandoActivo = nuevo;
        }

        /// <summary>
        /// Sincroniza los checkboxes con el estado real de los tokens,
        /// sin reconstruir la colección.
        /// </summary>
        private void SincronizarCheckboxes()
        {
            var parametrosPresentes = new HashSet<Parametro>(
                Tokens
                    .Where(t => t.Tipo == TipoToken.Parametro && t.Origen is Parametro)
                    .Select(t => (Parametro)t.Origen!)
            );

            foreach (var pv in Modificadores)
            {
                var deberia = parametrosPresentes.Contains(pv.Parametro);
                if (pv.EstaSeleccionado != deberia)
                {
                    // Desuscribimos para no disparar AgregarTokenParametro/QuitarTokenParametro.
                    pv.PropertyChanged -= ParametroVista_PropertyChanged;
                    pv.EstaSeleccionado = deberia;
                    pv.PropertyChanged += ParametroVista_PropertyChanged;
                }
            }
        }




        private void ReconstruirModificadores()
        {
            foreach (var old in Modificadores)
                old.PropertyChanged -= ParametroVista_PropertyChanged;

            Modificadores.Clear();

            if (ComandoActivo?.Origen is not Comando cmd) return;

            // ¿Qué parámetros están presentes en la línea ahora mismo?
            var parametrosPresentes = new HashSet<Parametro>(
                Tokens
                    .Where(t => t.Tipo == TipoToken.Parametro && t.Origen is Parametro)
                    .Select(t => (Parametro)t.Origen!)
            );

            foreach (var p in cmd.Parametros)
            {
                var pv = new ParametroVista(p)
                {
                    // Marcamos según el estado real de la línea.
                    EstaSeleccionado = parametrosPresentes.Contains(p)
                };
                pv.PropertyChanged += ParametroVista_PropertyChanged;
                Modificadores.Add(pv);
            }
        }

        private void ParametroVista_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ParametroVista.EstaSeleccionado)) return;
            if (sender is not ParametroVista pv) return;

            if (pv.EstaSeleccionado)
                AgregarTokenParametro(pv);
            else
                QuitarTokenParametro(pv);
        }

        private void AgregarTokenParametro(ParametroVista pv)
        {
            ConfirmarEstado();  // guarda el estado ANTES de añadir el parámetro
            var nuevoTexto = string.IsNullOrWhiteSpace(Texto)
                ? pv.Nombre
                : Texto + " " + pv.Nombre;
            ActualizarDesdeTexto(nuevoTexto);
            ConfirmarEstado();
        }

        private void QuitarTokenParametro(ParametroVista pv)
        {
            ConfirmarEstado();  // guarda el estado ANTES de quitar el parámetro
            var partes = Tokens
                .Where(t => !(t.Tipo == TipoToken.Parametro && ReferenceEquals(t.Origen, pv.Parametro)))
                .Select(t => t.Texto);
            ActualizarDesdeTexto(string.Join(" ", partes));
            ConfirmarEstado();
        }

        // ==== INotifyPropertyChanged ====

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? nombre = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
       

        /// <summary>
        /// Confirma el texto actual y lo apila en el historial.
        /// Se llama tras operaciones confirmadas (Enter, cambio de línea, blur...).
        /// </summary>



        // ==== Historial de estados (undo/redo) ====

        private const int MaxHistorial = 100;

        /// <summary>Historial de estados confirmados. El último es el actual.</summary>
        private readonly List<string> _historial = new();

        /// <summary>Índice del estado actual dentro del historial. -1 = sin historial.</summary>
        private int _indiceHistorial = -1;

        /// <summary>¿Se puede deshacer?</summary>
        public bool PuedeDeshacer => _indiceHistorial > 0;

        /// <summary>¿Se puede rehacer?</summary>
        public bool PuedeRehacer => _indiceHistorial >= 0 && _indiceHistorial < _historial.Count - 1;

        /// <summary>
        /// Confirma el texto actual como un nuevo punto de restauración.
        /// Se llama tras operaciones confirmadas por el usuario (Enter, blur,
        /// cambio de línea, drag & drop, etc.).
        /// </summary>
        public void ConfirmarEstado()
        {
            // Si estamos en medio de un undo (hay rehacer disponible),
            // cortamos la cola de rehacer.
            if (_indiceHistorial < _historial.Count - 1)
                _historial.RemoveRange(_indiceHistorial + 1, _historial.Count - _indiceHistorial - 1);

            // No apilamos duplicados.
            if (_historial.Count > 0 && _historial[^1] == Texto)
                return;

            _historial.Add(Texto);
            _indiceHistorial = _historial.Count - 1;

            // Acotamos el historial.
            if (_historial.Count > MaxHistorial)
            {
                _historial.RemoveAt(0);
                _indiceHistorial--;
            }

            OnPropertyChanged(nameof(PuedeDeshacer));
            OnPropertyChanged(nameof(PuedeRehacer));
        }

        /// <summary>Flag para indicar que el cambio viene del modelo, no del TextBox.</summary>
        private bool _cambioDesdeModelo;
        public bool CambioDesdeModelo
        {
            get => _cambioDesdeModelo;
            set => _cambioDesdeModelo = value;
        }

        /// <summary>Deshace el último cambio confirmado.</summary>
        public bool Deshacer()
        {
            if (!PuedeDeshacer) return false;

            _indiceHistorial--;
            CambioDesdeModelo = true;   // ← marcar
            ActualizarDesdeTexto(_historial[_indiceHistorial]);
            return true;
        }

        public bool Rehacer()
        {
            if (!PuedeRehacer) return false;

            _indiceHistorial++;
            CambioDesdeModelo = true;   // ← marcar
            ActualizarDesdeTexto(_historial[_indiceHistorial]);
            return true;
        }
        /// <summary>
        /// Prompt acortado para mostrar en la UI cuando la ruta es muy larga.
        /// Ejemplo: "C:\...\VisualCMD>".
        /// </summary>
        public string PromptCmdCorto
        {
            get
            {
                var p = DirectorioTrabajo;
                if (string.IsNullOrEmpty(p)) return ">";

                if (p.Length <= 40) return p + ">";

                try
                {
                    var partes = p.Split('\\', System.StringSplitOptions.RemoveEmptyEntries);
                    if (partes.Length <= 3) return p + ">";

                    var inicio = partes[0];           // "C:"
                    var ultima = partes[^1];          // última carpeta
                    return $"{inicio}\\...\\{ultima}>";
                }
                catch
                {
                    return p + ">";
                }
            }
        }
        /// <summary>
        /// Recalcula los dummies visuales en función del comando activo,
        /// los argumentos declarados en el catálogo, y los tokens reales
        /// que ya están presentes en la línea.
        /// </summary>
        private void RecalcularDummies()
        {
            Dummies.Clear();

           


            try
            {
                // Solo si hay un comando activo con argumentos declarados.
                if (ComandoActivo?.Origen is not Comando cmd) return;
                if (cmd.Argumentos == null || cmd.Argumentos.Count == 0) return;

                // Contamos los tokens reales que cuentan como argumentos
                // (tipo Texto o Ruta), EXCLUYENDO los que están antes del comando activo.
                var indiceComando = Tokens.IndexOf(ComandoActivo);
                if (indiceComando < 0) return;

                int argumentosCubiertos = 0;
                for (int i = indiceComando + 1; i < Tokens.Count; i++)
                {
                    var t = Tokens[i];
                    if (t.Tipo == TipoToken.Texto || t.Tipo == TipoToken.Ruta)
                        argumentosCubiertos++;
                }

                // Generamos dummies para los argumentos que faltan.
                var argumentosOrdenados = cmd.Argumentos.OrderBy(a => a.Posicion).ToList();

                for (int i = argumentosCubiertos; i < argumentosOrdenados.Count; i++)
                {
                    var arg = argumentosOrdenados[i];

                    var dummy = new Token
                    {
                        Tipo = TipoToken.Texto,
                        Estado = EstadoToken.Dummy,
                        Texto = "",
                        TextoDummy = arg.Dummy,
                        Descripcion = arg.Descripcion,
                        Color = "E8E8E8",
                        Icono = "◇",
                        EsRequerido = arg.Requerido   // ← NUEVO
                    };

                    Dummies.Add(dummy);
                }
            }
            finally
            {
                // SIEMPRE reconstruir BloquesVisibles, pase lo que pase.
                ReconstruirBloquesVisibles();
            }

            // Notificar a la UI.
            OnPropertyChanged(nameof(HayDummies));
        }

        /// <summary>¿Hay dummies visibles ahora mismo?</summary>
        public bool HayDummies => Dummies.Count > 0;

        /// <summary>
        /// Reconstruye BloquesVisibles = Tokens + Dummies (al final).
        /// Se llama cada vez que cambian los tokens o los dummies.
        /// </summary>
        private void ReconstruirBloquesVisibles()
        {

            BloquesVisibles.Clear();
            foreach (var t in Tokens) BloquesVisibles.Add(t);
            foreach (var d in Dummies) BloquesVisibles.Add(d);

            OnPropertyChanged(nameof(HayDummies));
        }
        private string? _snapshotTexto;

        public void GuardarSnapshotTexto()
        {
            _snapshotTexto = Texto;
        }

        public void RestaurarSnapshotTexto()
        {
            if (_snapshotTexto != null)
            {
                ActualizarDesdeTexto(_snapshotTexto);
                _snapshotTexto = null;
            }
        }
        public void OlvidarSnapshotTexto()
        {
            _snapshotTexto = null;
        }
        /// <summary>
        /// Mueve un token de su posición actual a una nueva posición dentro de la línea.
        /// Reconstruye el texto y confirma el estado.
        /// </summary>
        /// <summary>
        /// Mueve un token a una nueva posición. Si se indica un token destino,
        /// se elimina antes de mover (modo sustituir).
        /// </summary>
        public void MoverToken(Token tokenOrigen, int indiceDestino, Token? tokenAEliminar = null)
        {
            var tokens = Tokens.ToList();
            var indiceOrigen = tokens.IndexOf(tokenOrigen);
            if (indiceOrigen < 0) return;

            // Si hay un token a eliminar (Ctrl+Drop), quitarlo primero.
            if (tokenAEliminar != null)
            {
                var indiceEliminar = tokens.IndexOf(tokenAEliminar);
                if (indiceEliminar >= 0 && indiceEliminar != indiceOrigen)
                {
                    tokens.RemoveAt(indiceEliminar);

                    // Reajustar índices si el eliminado estaba antes del origen.
                    if (indiceEliminar < indiceOrigen)
                        indiceOrigen--;
                }
            }

            // Quitar el token origen de su posición actual.
            tokens.RemoveAt(indiceOrigen);

            // Ajustar el índice destino si el origen estaba antes.
            // (Ahora el origen ya no está en la lista, así que hay que ver
            // si el índice calculado sigue siendo válido.)
            if (indiceDestino > indiceOrigen)
                indiceDestino--;

            // Asegurar que el índice está en rango.
            if (indiceDestino < 0) indiceDestino = 0;
            if (indiceDestino > tokens.Count) indiceDestino = tokens.Count;

            // Insertar el origen en la posición final.
            tokens.Insert(indiceDestino, tokenOrigen);

            // Reconstruir la línea.
            var nuevoTexto = string.Join(" ", tokens.Select(t => t.Texto));
            ActualizarDesdeTexto(nuevoTexto);
            ConfirmarEstado();
        }
        /// <summary>
        /// Sustituye un token por otro dentro de la línea.
        /// El token destino se elimina; el token origen ocupa su posición.
        /// </summary>
        public void SustituirTokenPorToken(Token tokenOrigen, Token tokenDestino)
        {
            var tokens = Tokens.ToList();
            var indiceOrigen = tokens.IndexOf(tokenOrigen);
            var indiceDestino = tokens.IndexOf(tokenDestino);

            if (indiceOrigen < 0 || indiceDestino < 0) return;
            if (indiceOrigen == indiceDestino) return;

            // Quitar el token destino.
            tokens.RemoveAt(indiceDestino);

            // Reajustar el índice del origen si el destino estaba antes.
            if (indiceDestino < indiceOrigen)
                indiceOrigen--;

            // Quitar el token origen.
            tokens.RemoveAt(indiceOrigen);

            // Insertar el token origen en la posición del destino.
            int indiceFinal = indiceDestino <= tokens.Count ? indiceDestino : tokens.Count;
            tokens.Insert(indiceFinal, tokenOrigen);

            // Reconstruir la línea.
            var nuevoTexto = string.Join(" ", tokens.Select(t => t.Texto));
            ActualizarDesdeTexto(nuevoTexto);
            ConfirmarEstado();
        }
    }
}