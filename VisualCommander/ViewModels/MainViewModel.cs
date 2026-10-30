using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using VisualCommander.Collections;
using VisualCommander.Models;
using VisualCommander.Services;
using VisualCommander.Services.UndoRedo;
using VisualCommander.Services.UndoRedo.Actions;

// Registrar el provider de codificaciones antiguas (ANSI 1252, etc.)
System.Text.Encoding.RegisterProvider(
    System.Text.CodePagesEncodingProvider.Instance);


namespace VisualCommander.ViewModels
{
    /// <summary>
    /// ViewModel principal. Contiene el catálogo de comandos, la colección
    /// de líneas de comandos y coordina la línea activa.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged

    {

        // ==== Configuración persistente ====

        private readonly Models.Configuracion _configuracion;
        private bool _ignorarSalida;

        /// <summary>Gestor global de undo/redo.</summary>
        public UndoRedoManager UndoRedo { get; } = new();

        public CatalogoTokens CatalogoTokens { get; }
        // ==== Catálogo ====

        public ObservableCollection<Comando> Comandos { get; } = new();
        public ICollectionView ComandosView { get; }

        // ==== Líneas de comando ====

        /// <summary>Todas las líneas que el usuario ha creado.</summary>
        public ObservableCollection<LineaComando> Lineas { get; } = new();

        private LineaComando _lineaActiva = null!;
        public LineaComando LineaActiva
        {
            get => _lineaActiva;
            set
            {
                // Protección contra nulos: nunca dejamos el ViewModel sin línea activa.
                if (value is null) return;
                if (ReferenceEquals(_lineaActiva, value)) return;

                if (_lineaActiva != null)
                {
                    _lineaActiva.PropertyChanged -= LineaActiva_PropertyChanged;
                    _lineaActiva.EsActiva = false;
                }

                _lineaActiva = value;

                if (_lineaActiva != null)
                {
                    _lineaActiva.PropertyChanged += LineaActiva_PropertyChanged;
                    _lineaActiva.EsActiva = true;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(DirectorioTrabajo));
                OnPropertyChanged(nameof(PromptCmd));
                OnPropertyChanged(nameof(NivelPeligroDirectorio));
                OnPropertyChanged(nameof(PuedeSubirActiva));
                OnPropertyChanged(nameof(PuedeBajarActiva));
            }
        }



        /// <summary>
        /// Cuando la línea activa cambia su directorio, replicamos el cambio
        /// en las propiedades delegadas del ViewModel para que la UI se actualice.
        /// </summary>
        private void LineaActiva_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Si por alguna razón la línea activa ha quedado en null, no notificamos.
            if (_lineaActiva is null) return;

            if (e.PropertyName == nameof(LineaComando.DirectorioTrabajo) ||
                e.PropertyName == nameof(LineaComando.PromptCmd) ||
                e.PropertyName == nameof(LineaComando.NivelPeligroDirectorio))
            {
                OnPropertyChanged(nameof(DirectorioTrabajo));
                OnPropertyChanged(nameof(PromptCmd));
                OnPropertyChanged(nameof(NivelPeligroDirectorio));
            }
        }



        // ==== Contexto de ejecución (delegado a la línea activa) ====

        /// <summary>Directorio de trabajo de la línea activa.</summary>
        public string DirectorioTrabajo => LineaActiva.DirectorioTrabajo;

        /// <summary>Prompt de la línea activa.</summary>
        public string PromptCmd => LineaActiva.PromptCmd;

        /// <summary>Nivel de peligro del directorio de la línea activa.</summary>
        public int NivelPeligroDirectorio => LineaActiva.NivelPeligroDirectorio;

        /// <summary>
        /// Cambia el directorio de trabajo de la línea activa.
        /// Valida que exista.
        /// </summary>
        public void CambiarDirectorioTrabajo(string nuevaRuta)
        {
            if (string.IsNullOrWhiteSpace(nuevaRuta))
            {
                SetStatus("Ruta vacía");
                return;
            }

            try
            {
                var full = System.IO.Path.GetFullPath(nuevaRuta);

                if (!System.IO.Directory.Exists(full))
                {
                    SetStatus($"El directorio no existe: {full}");
                    return;
                }

                LineaActiva.DirectorioTrabajo = full;
                SetStatus($"Directorio de trabajo: {full}");
            }
            catch (Exception ex)
            {
                SetStatus($"Ruta inválida: {ex.Message}");
            }
        }



        // ==== Estado de ejecución ====

        /// <summary>Líneas de salida del último comando ejecutado.</summary>


        public RangeObservableCollection<string> Salida { get; } = new();

        private bool _estaEjecutando;
        public bool EstaEjecutando
        {
            get => _estaEjecutando;
            private set { _estaEjecutando = value; OnPropertyChanged(); }
        }

        private string _mensajeCmd = "SIMULACIÓN — el comando aún no se ha ejecutado";
        public string MensajeCmd
        {
            get => _mensajeCmd;
            private set { _mensajeCmd = value; OnPropertyChanged(); }
        }

        private Process? _procesoActual;

        // ==== Buffer de salida ====

        private readonly System.Text.StringBuilder _bufferSalida = new();
        private readonly object _bufferLock = new();
        private System.Windows.Threading.DispatcherTimer? _timerVolcado;

        private void IniciarVolcadoSalida()
        {
            _timerVolcado = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _timerVolcado.Tick += (_, _) => VolcarBuffer();
            _timerVolcado.Start();
        }

        private void DetenerVolcadoSalida()
        {
            _timerVolcado?.Stop();
            _timerVolcado = null;
            VolcarBuffer();
        }

        private void VolcarBuffer()
        {
            string texto;
            lock (_bufferLock)
            {
                if (_bufferSalida.Length == 0) return;
                texto = _bufferSalida.ToString();
                _bufferSalida.Clear();
            }

            var lineas = texto.Split('\n')
                .Select(l => l.TrimEnd('\r'))
                .Where(l => l.Length > 0)
                .ToList();

            if (lineas.Count == 0) return;

            // Un solo evento para todas las líneas del tick.
            Salida.AddRange(lineas);
        }

        private void EncolarSalida(string linea)
        {
            lock (_bufferLock)
            {
                _bufferSalida.AppendLine(linea);
            }
        }


        /// <summary>¿Se debe detener la ejecución del plan?</summary>
        private bool _detenerSolicitado;

        /// <summary>¿Detener el plan completo si una línea falla?</summary>
        private bool _detenerEnError = true;
        public bool DetenerEnError
        {
            get => _detenerEnError;
            set { _detenerEnError = value; OnPropertyChanged(); }
        }

        /// <summary>¿Está ejecutándose el plan completo (no solo una línea)?</summary>
        private bool _ejecutandoPlan;
        public bool EjecutandoPlan
        {
            get => _ejecutandoPlan;
            private set
            {
                _ejecutandoPlan = value; OnPropertyChanged();
            }
        }



        // ==== Estado de selección de catálogo ====

        private Comando? _comandoSeleccionado;
        public Comando? ComandoSeleccionado
        {
            get => _comandoSeleccionado;
            set
            {
                if (ReferenceEquals(_comandoSeleccionado, value)) return;
                _comandoSeleccionado = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TextoDescripcion));
                OnPropertyChanged(nameof(TextoAyuda));
            }
        }

        private string _filtro = "";
        public string Filtro
        {
            get => _filtro;
            set
            {
                if (_filtro == value) return;
                _filtro = value;
                OnPropertyChanged();
                ComandosView.Refresh();
            }
        }

        private string _status = "Listo";
        public string Status
        {
            get => _status;
            private set { _status = value; OnPropertyChanged(); }
        }


        // ==== Propiedades calculadas ====

        public string TextoDescripcion =>
            ComandoSeleccionado is null
                ? "Selecciona un comando de la lista para ver aquí su descripción, sintaxis, parámetros y ejemplos de uso."
                : $"📌 {ComandoSeleccionado.Descripcion}\n\n" +
                  $"Sintaxis:\n{ComandoSeleccionado.Sintaxis}\n\n" +
                  $"Ejemplos:\n  " + string.Join("\n  ", ComandoSeleccionado.Ejemplos);

        public string TextoAyuda =>
            ComandoSeleccionado is null
                ? "Aquí aparecerá información sensible al contexto: parámetros válidos para el comando seleccionado, combinaciones permitidas, advertencias y ejemplos."
                : (ComandoSeleccionado.Peligroso
                    ? "⚠ ATENCIÓN: este comando puede ser destructivo. Revisa bien los parámetros antes de ejecutarlo.\n\n"
                    : "") +
                  $"Parámetros válidos ({ComandoSeleccionado.Parametros.Count}):";

        // ==== Constructor ====

        public MainViewModel()
        {

            // Cargar configuración PRIMERO, antes que cualquier otra cosa.
            _configuracion = Services.ConfiguracionService.Cargar();
            _registroUso = UsoService.Cargar();
            /// <summary>Catálogo de tokens pequeños (constantes, variables, comodines, operadores).</summary>


            CargarComandos();
            CatalogoTokens = Services.CatalogoTokensService.Cargar();


            ComandoParser.CatalogoTokens = CatalogoTokens;

            // Conectamos el proyector de tokens al parser.
            LineaComando.Proyector = (texto, dirTrabajo) =>
                ComandoParser.Parsear(texto, Comandos, dirTrabajo);
            ComandosView = CollectionViewSource.GetDefaultView(Comandos);
            AplicarOrdenacion();   // ← en lugar de solo GroupDescriptions.Filter
            ComandosView.Filter = FiltroComando;

            Lineas.CollectionChanged += (_, _) =>
            {
                RenumerarLineas();
                OnPropertyChanged(nameof(PuedeSubirActiva));
                OnPropertyChanged(nameof(PuedeBajarActiva));
            };

            // Creamos la primera línea.
            _lineaActiva = new LineaComando();
            _lineaActiva.EsActiva = true;
            Lineas.Add(_lineaActiva);
            RenumerarLineas();
        }

        private void RenumerarLineas()
        {
            for (int i = 0; i < Lineas.Count; i++)
            {
                Lineas[i].NumeroLinea = i + 1;
                Lineas[i].TotalLineas = Lineas.Count;
            }
        }

        // ==== Gestión de líneas ====

        /// <summary>
        /// Cierra la línea activa y crea una nueva justo después.
        /// Si la activa estaba vacía, simplemente se reutiliza.
        /// </summary>
        public void CerrarYCrearSiguiente()
        {
            var actual = LineaActiva;

            // Si la línea actual está vacía, no creamos una nueva: la reutilizamos.
            if (string.IsNullOrWhiteSpace(actual.Texto))
            {
                SetStatus("La línea actual ya está vacía. Escribe algo o usa ✓ Cerrar para saltar a la siguiente.");
                return;
            }

            // ¿Tiene dummies requeridos?
            if (!ConfirmarCierreSiEsNecesario())
            {
                SetStatus("Cierre cancelado por el usuario");
                return;
            }


            // ¿Cambio de directorio declarado? (cd / pushd)
            var nuevoDir = actual.CalcularDirectorioSiguiente();

            // Creamos la nueva línea.
            var nueva = new LineaComando
            {
                DirectorioTrabajo = nuevoDir ?? actual.DirectorioTrabajo
            };

            // Insertamos justo después de la activa.
            var idx = Lineas.IndexOf(actual);
            if (idx < 0) idx = Lineas.Count - 1;
            var action = new AgregarLineaAction(Lineas, nueva, idx + 1);
            UndoRedo.Ejecutar(action);

            LineaActiva = nueva;

            if (nuevoDir != null)
                SetStatus($"Nueva línea en {nuevoDir}");
            else
                SetStatusSegunModo(
    $"Nueva línea creada en {nueva.DirectorioTrabajo}. Puedes empezar a escribir o arrastrar comandos del catálogo.",
    $"Nueva línea en {nueva.DirectorioTrabajo}");
            ConfigurarNuevaLinea(nueva);
        }

        /// <summary>
        /// Cierra la línea activa sin crear una nueva. El foco pasa a la
        /// siguiente línea existente, o a la anterior si no hay siguiente.
        /// Si la línea estaba vacía, se elimina.
        /// </summary>
        public void CerrarLineaActiva()
        {
            var actual = LineaActiva;

            // Si está vacía, la eliminamos.
            if (string.IsNullOrWhiteSpace(actual.Texto))
            {
                if (Lineas.Count <= 1)
                {
                    SetStatus("No hay nada que cerrar");
                    return;
                }
                EliminarLineaYLimpiarHuerfanas(actual);
                return;
            }

            // ¿Tiene dummies requeridos?
            if (!ConfirmarCierreSiEsNecesario())
            {
                SetStatus("Cierre cancelado por el usuario");
                return;
            }

            // Buscamos la siguiente línea (después de la activa).
            var idx = Lineas.IndexOf(actual);
            LineaComando? siguiente = idx >= 0 && idx + 1 < Lineas.Count
                ? Lineas[idx + 1]
                : (idx > 0 ? Lineas[idx - 1] : null);

            if (siguiente is null)
            {
                // No hay otra línea: creamos una vacía al final.
                var nueva = new LineaComando
                {
                    DirectorioTrabajo = actual.DirectorioTrabajo
                };
                Lineas.Add(nueva);
                LineaActiva = nueva;
            }
            else
            {
                LineaActiva = siguiente;
            }

            SetStatus("Línea cerrada");
        }

        /// <summary>
        /// Elimina la línea activa y limpia cualquier línea vacía huérfana
        /// que quede entre líneas cerradas.
        /// </summary>
        public void EliminarLineaActiva()
        {
            if (Lineas.Count <= 1)
            {
                SetStatus("Debe quedar al menos una línea");
                return;
            }

            EliminarLineaYLimpiarHuerfanas(LineaActiva);
        }

        /// <summary>
        /// Elimina la línea indicada y, tras ello, limpia las líneas vacías
        /// que queden entre líneas cerradas no vacías. Garantiza que al menos
        /// haya una línea activa al final.
        /// </summary>
        private void EliminarLineaYLimpiarHuerfanas(LineaComando aBorrar)
        {
            var idx = Lineas.IndexOf(aBorrar);
            if (idx < 0) return;

            // Elegimos nueva activa antes de borrar.
            LineaComando nuevaActiva;
            if (idx + 1 < Lineas.Count)
                nuevaActiva = Lineas[idx + 1];
            else if (idx > 0)
                nuevaActiva = Lineas[idx - 1];
            else
                nuevaActiva = aBorrar; // no debería pasar

            // Envolver en UndoRedoManager.
            var action = new EliminarLineaAction(Lineas, aBorrar, idx);
            UndoRedo.Ejecutar(action);

            // Limpiamos vacías huérfanas (no la activa final).
            LimpiarVaciasIntermedias();

            // Asignamos activa si la que elegimos sigue existiendo.
            if (Lineas.Contains(nuevaActiva))
                LineaActiva = nuevaActiva;
            else
                LineaActiva = Lineas[^1];

            SetStatusSegunModo(
                $"Línea eliminada ({Lineas.Count} restantes). Puedes deshacer con Ctrl+Z.",
                $"Línea eliminada ({Lineas.Count} restantes)");
        }

        /// <summary>
        /// Elimina líneas vacías que estén rodeadas por líneas no vacías.
        /// Nunca elimina la última línea (que es la "línea viva" de trabajo).
        /// </summary>
        private void LimpiarVaciasIntermedias()
        {
            for (int i = Lineas.Count - 2; i >= 0; i--)
            {
                var l = Lineas[i];
                if (string.IsNullOrWhiteSpace(l.Texto) && !l.EsActiva)
                {
                    // No la borramos si es la única línea vacía justo antes de la activa,
                    // porque podría ser la "siguiente a editar" tras cerrar la activa.
                    // Pero si hay otra vacía después, esta es redundante.
                    bool hayOtraVacia = false;
                    for (int j = i + 1; j < Lineas.Count; j++)
                    {
                        if (string.IsNullOrWhiteSpace(Lineas[j].Texto)) { hayOtraVacia = true; break; }
                    }
                    if (hayOtraVacia)
                        Lineas.RemoveAt(i);
                }
            }
        }



        // ==== API pública para la línea activa ====

        public void AgregarComando(Comando cmd)
        {
            var linea = LineaActiva;

            EjecutarCambioTexto(
                linea,
                () => linea.AgregarToken(new Token
                {
                    Tipo = TipoToken.Comando,
                    Texto = cmd.Nombre,
                    Descripcion = $"{cmd.Resumen}\n{cmd.Descripcion}",
                    Color = cmd.ColorFamilia,
                    Icono = cmd.Icono,
                    Origen = cmd,
                    RequiereAdmin = cmd.RequiereAdmin
                }),
                $"Añadir comando: {cmd.Nombre}");

            SetStatusSegunModo(
                $"Añadido: {cmd.Nombre}. Arrástralo si quieres insertarlo en otra posición.",
                $"Añadido: {cmd.Nombre}");
        }
        /// <summary>
        /// Inserta un texto (palabra, ruta, comando) en la posición indicada
        /// dentro de los tokens de la línea activa.
        /// </summary>
        public void InsertarTextoEnPosicion(string texto, int indice)
        {
            var linea = LineaActiva;

            EjecutarCambioTexto(
                linea,
                () =>
                {
                    var tokens = linea.Tokens.ToList();
                    if (indice < 0) indice = 0;
                    if (indice > tokens.Count) indice = tokens.Count;

                    var textos = tokens.Select(t => t.Texto).ToList();
                    textos.Insert(indice, texto);
                    linea.ActualizarDesdeTexto(string.Join(" ", textos));
                },
                $"Insertar texto: {texto}");

            SetStatusSegunModo(
                $"Insertado en posición {indice}: {texto}. Puedes deshacer con Ctrl+Z.",
                $"Insertado en posición {indice}: {texto}");
        }

        public void AgregarComandoSeleccionado()
        {
            if (ComandoSeleccionado is null)
            {
                SetStatus("Ningún comando seleccionado");
                return;
            }
            AgregarComando(ComandoSeleccionado);
        }

        public void EliminarToken(Token token)
        {
            var linea = LineaActiva;
            // Caso especial: parámetro con checkbox. El checkbox se encarga.
            if (token.Tipo == TipoToken.Parametro && token.Origen is Parametro p)
            {
                var pv = linea.Modificadores.FirstOrDefault(m => m.Parametro == p);
                if (pv is { EstaSeleccionado: true })
                {
                    CambiarSeleccionParametro(pv, false);
                    return;
                }
                SetStatusSegunModo(
                        $"Eliminado: {token.Texto}. Puedes deshacer con Ctrl+Z.",
                        $"Eliminado: {token.Texto}");
                return;
            }

            // Caso normal.
            EjecutarCambioTexto(
                linea,
                () => linea.QuitarToken(token),
                $"Eliminar token: {token.Texto}");

            SetStatusSegunModo(
                $"Eliminado: {token.Texto}. Puedes deshacer con Ctrl+Z.",
                $"Eliminado: {token.Texto}");
        }

        /// <summary>
        /// Marca o desmarca un parámetro con checkbox, dentro de UndoRedo global.
        /// Sustituye el binding directo del CheckBox en la UI.
        /// </summary>
        public void CambiarSeleccionParametro(ParametroVista pv, bool seleccionado)
        {
            if (pv.EstaSeleccionado == seleccionado) return;

            var linea = LineaActiva;

            EjecutarCambioTexto(
                linea,
                () => pv.EstaSeleccionado = seleccionado,
                seleccionado
                    ? $"Marcar parámetro: {pv.Nombre}"
                    : $"Desmarcar parámetro: {pv.Nombre}");

            SetStatusSegunModo(
                seleccionado
                    ? $"Parámetro añadido: {pv.Nombre}. Puedes deshacer con Ctrl+Z."
                    : $"Parámetro eliminado: {pv.Nombre}. Puedes deshacer con Ctrl+Z.",
                seleccionado
                    ? $"Parámetro añadido: {pv.Nombre}"
                    : $"Parámetro eliminado: {pv.Nombre}");
        }

        /// <summary>
        /// Ejecuta una acción que modifica el texto de una línea,
        /// envolviéndola en UndoRedo para que sea deshacible.
        /// </summary>
        private void EjecutarCambioTexto(
            LineaComando linea,
            Action accion,
            string descripcion)
        {
            var textoAnterior = linea.Texto;

            // Ejecutar la acción (modifica linea.Texto).
            accion();

            var textoNuevo = linea.Texto;

            // Si no hubo cambio real, no apilar.
            if (textoAnterior == textoNuevo)
                return;

            // Crear y ejecutar la acción deshacible.
            var undoAction = new CambiarTextoLineaAction(
                linea,
                textoAnterior,
                textoNuevo,
                descripcion);

            UndoRedo.Ejecutar(undoAction);
        }


        public void LimpiarTokens()
        {
            LineaActiva.Limpiar();
            SetStatusSegunModo(
    "Línea limpiada. Puedes empezar de nuevo arrastrando comandos del catálogo.",
    "Línea limpiada");
        }

        // ==== Helpers ====

        public void SetStatus(string mensaje) => Status = mensaje;

        private bool FiltroComando(object obj)
        {
            if (string.IsNullOrWhiteSpace(Filtro)) return true;
            if (obj is not Comando c) return false;
            var q = Filtro.Trim();
            return c.Nombre.Contains(q, System.StringComparison.OrdinalIgnoreCase)
                || c.Resumen.Contains(q, System.StringComparison.OrdinalIgnoreCase)
                || c.Familia.Contains(q, System.StringComparison.OrdinalIgnoreCase);
        }
        /// <summary>
        /// Establece el estado según el modo de trabajo.
        /// En modo aprendiz, muestra el mensaje más descriptivo.
        /// En modo experto, muestra el mensaje más conciso.
        /// </summary>
        private void SetStatusSegunModo(string mensajeAprendiz, string mensajeExperto)
        {
            SetStatus(EsModoAprendiz ? mensajeAprendiz : mensajeExperto);
        }

        private void CargarComandos()
        {
            var ruta = Path.Combine(System.AppContext.BaseDirectory, "Data", "comandos.json");
            if (!File.Exists(ruta))
                ruta = Path.Combine(System.AppContext.BaseDirectory, "comandos.json");
            if (!File.Exists(ruta))
            {
                SetStatus($"No se encontró comandos.json en {ruta}");
                return;
            }

            System.Collections.Generic.List<Comando>? lista = null;

            // ---- NIVEL 1: deserialización ----
            try
            {
                var json = File.ReadAllText(ruta);
                var opciones = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                lista = JsonSerializer.Deserialize<System.Collections.Generic.List<Comando>>(json, opciones);
            }
            catch (JsonException jex)
            {
                System.Windows.MessageBox.Show(
                    $"Error de sintaxis en comandos.json:\n\n" +
                    $"{jex.Message}\n\n" +
                    $"Línea: {jex.LineNumber}, posición: {jex.BytePositionInLine}",
                    "Error de JSON",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                SetStatus("Error de sintaxis en comandos.json");
                return;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error inesperado al leer comandos.json:\n\n{ex.GetType().Name}: {ex.Message}",
                    "Error de lectura",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                SetStatus("Error al leer comandos.json");
                return;
            }

            // ---- NIVEL 2: lista nula o vacía ----
            if (lista == null)
            {
                System.Windows.MessageBox.Show(
                    "JsonSerializer devolvió null al deserializar comandos.json.\n" +
                    "Esto suele indicar que el JSON raíz no es un array o está mal formado.",
                    "Catálogo vacío",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                SetStatus("El catálogo de comandos está vacío");
                return;
            }

            if (lista.Count == 0)
            {
                SetStatus("comandos.json no contiene comandos");
                return;
            }

            // ---- NIVEL 3: iteración (aquí ponemos el debug) ----
            int añadidos = 0;
            int fallidos = 0;
            var errores = new System.Text.StringBuilder();

            foreach (var c in lista.OrderBy(c => c.Familia).ThenBy(c => c.Nombre))
            {
                try
                {
                    // Validación mínima defensiva
                    if (string.IsNullOrWhiteSpace(c.Nombre))
                    {
                        errores.AppendLine($"• Comando sin nombre (familia: '{c.Familia}')");
                        fallidos++;
                        continue;
                    }

                    if (_registroUso.Comandos.TryGetValue(c.Nombre, out var usos))
                        c.Usos = usos;

                    Comandos.Add(c);
                    añadidos++;
                }
                catch (Exception ex)
                {
                    errores.AppendLine($"• '{c.Nombre}': {ex.GetType().Name}: {ex.Message}");
                    fallidos++;
                }
            }

            // ---- NIVEL 4: reporte de errores ----
            if (fallidos > 0)
            {
                System.Windows.MessageBox.Show(
                    $"Cargados {añadidos} comando(s). Fallaron {fallidos}:\n\n{errores}",
                    "Errores al cargar comandos",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                SetStatus($"Catálogo: {añadidos} OK, {fallidos} con error");
            }
            else
            {
                SetStatus($"Catálogo cargado: {añadidos} comando(s)");
            }
        }

        /// <summary>
        /// Añade una ruta (archivo o carpeta) a la línea activa, entre comillas
        /// si contiene espacios (como haría CMD).
        /// </summary>
        public void AgregarRuta(string ruta)
        {
            var linea = LineaActiva;

            var textoRuta = ruta.Contains(' ') ? $"\"{ruta}\"" : ruta;
            var esCarpeta = System.IO.Directory.Exists(ruta);

            EjecutarCambioTexto(
                linea,
                () => linea.AgregarToken(new Token
                {
                    Tipo = TipoToken.Ruta,
                    Texto = textoRuta,
                    Descripcion = (esCarpeta ? "Carpeta: " : "Archivo: ") + ruta,
                    Color = "DEEBF7",
                    Icono = esCarpeta ? "📁" : "📄",
                    Origen = ruta
                }),
                $"Añadir ruta: {ruta}");

            SetStatusSegunModo(
                $"Añadida ruta: {ruta}. Puedes deshacer con Ctrl+Z.",
                $"Añadida ruta: {ruta}");
        }

        /// <summary>
        /// Añade un token de texto vacío al final de la línea activa.
        /// Se marcará automáticamente en modo edición in-place.
        /// </summary>
        public void AgregarTextoVacio()
        {
            var linea = LineaActiva;

            var token = new Token
            {
                Tipo = TipoToken.Texto,
                Texto = "Texto...",
                Descripcion = "Texto libre (doble clic para editar)",
                Color = "EDEDED",
                Icono = "📝"
            };

            EjecutarCambioTexto(
                linea,
                () => linea.AgregarToken(token),
                "Añadir texto");

            SetStatusSegunModo(
                "Token de texto añadido. Escribe el contenido y pulsa Enter.",
                "Texto añadido");
        }

        // ==== INotifyPropertyChanged ====

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? nombre = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
        // ==== Ejecución ====

        /// <summary>Simula la línea activa sin ejecutarla. Solo actúa con comandos peligrosos.</summary>
        public void Simular()
        {
            var r = SimuladorComando.Simular(LineaActiva);

            if (!r.Aplica)
            {
                SetStatus("Este comando no necesita simulación");
                Salida.Clear();
                Salida.Add("✔ Este comando no está marcado como peligroso.");
                Salida.Add("No hay nada que simular; puedes ejecutarlo directamente.");
                return;
            }

            Salida.Clear();
            Salida.Add($"🔍 SIMULACIÓN DE: {r.Comando}");
            Salida.Add(new string('─', 60));
            Salida.Add("");
            foreach (var linea in r.Resumen.Split('\n'))
                Salida.Add(linea);
            Salida.Add("");

            if (r.Afectados.Count > 0)
            {
                Salida.Add($"Elementos afectados ({r.Afectados.Count}):");
                Salida.Add(new string('─', 60));
                foreach (var item in r.Afectados)
                    Salida.Add(item);
                Salida.Add("");
            }

            Salida.Add(new string('─', 60));
            Salida.Add("Nada se ha ejecutado todavía.");
            SetStatus($"Simulación completada: {r.Afectados.Count} elemento(s) afectado(s)");
        }



        /// <summary>Líneas para las que el usuario marcó "no volver a preguntar".</summary>
        private readonly System.Collections.Generic.HashSet<LineaComando> _lineasNoPreguntar = new();

        /// <summary>
        /// Ejecuta la línea activa. Antes, si contiene comandos peligrosos y el
        /// usuario no ha marcado "no preguntar", muestra el diálogo de confirmación.
        /// </summary>
        public async Task EjecutarAsync()
        {
            if (EstaEjecutando) return;

            var comando = LineaActiva.Texto;
            if (string.IsNullOrWhiteSpace(comando))
            {
                SetStatusSegunModo(
    "Nada que ejecutar. La línea activa está vacía. Añade un comando del catálogo o escribe algo.",
    "Nada que ejecutar");
                return;
            }

            // Recoger peligros de la línea activa.
            var simulacion = SimuladorComando.Simular(LineaActiva);
            var esPeligrosa = simulacion.Aplica && !_lineasNoPreguntar.Contains(LineaActiva);

            // Recoger incompletas de la línea activa.
            var esIncompleta = LineaActiva.TieneDummiesRequeridos;

            // Si hay algo que avisar, mostrar diálogo.
            if (esPeligrosa || esIncompleta)
            {
                var peligrosas = esPeligrosa
                    ? new List<(LineaComando, ResultadoSimulacion)> { (LineaActiva, simulacion) }
                    : new List<(LineaComando, ResultadoSimulacion)>();

                var incompletas = esIncompleta
                    ? new List<LineaComando> { LineaActiva }
                    : new List<LineaComando>();

                var dialogo = new ConfirmarPlanDialog(peligrosas, incompletas)
                {
                    Owner = System.Windows.Application.Current?.MainWindow
                };

                if (dialogo.ShowDialog() != true)
                {
                    SetStatusSegunModo(
    "Ejecución cancelada. No se ha ejecutado nada.",
    "Ejecución cancelada");
                    return;
                }

                if (dialogo.NoPreguntarMas && esPeligrosa)
                {
                    _lineasNoPreguntar.Add(LineaActiva);

                    SetStatusSegunModo(
    "No se volverá a preguntar para esta línea en esta sesión. Si cierras y abres la app, volverá a preguntar.",
    "No volver a preguntar (esta línea, esta sesión)");

                }
            }

            // ----- A partir de aquí, ejecución normal -----
            Salida.Clear();
            AgregarSalida($"📂 {DirectorioTrabajo}>");
            AgregarSalida($"> {comando}");
            AgregarSalida("");

            EstaEjecutando = true;
            MensajeCmd = "EJECUTANDO...";
            SetStatus("Ejecutando...");
            IniciarVolcadoSalida();
            try
            {
                await EjecutarInternoAsync(comando);
            }
            catch (Exception ex)
            {

                AgregarSalida($"ERROR: {ex.Message}");
                MensajeCmd = "ERROR AL EJECUTAR";
                SetStatusSegunModo(
    $"Error al ejecutar. Revisa la pestaña 'Salida' para más detalles: {ex.Message}",
    "Error al ejecutar");
            }
            finally
            {
                DetenerVolcadoSalida();
                EstaEjecutando = false;
                _procesoActual = null;
            }
        }

        /// <summary>
        /// Ejecuta todas las líneas del plan en secuencia.
        /// Antes de empezar, comprueba si hay comandos peligrosos y pide confirmación.
        /// </summary>
        public async Task EjecutarPlanAsync()
        {

            if (EstaEjecutando) return;
            _detenerSolicitado = false;
            _ignorarSalida = false;

            if (Lineas.Count == 0)
            {
                SetStatusSegunModo(
    "No hay líneas que ejecutar. Inserte algunas lineas para ejecutar.",
    "No hay líneas que ejecutar");
                return;
          
            }

            // NUEVO: comprobar si hay al menos una línea con contenido.
            var lineasConContenido = Lineas
                .Where(l => !string.IsNullOrWhiteSpace(l.Texto))
                .ToList();

            if (lineasConContenido.Count == 0)
            {
                SetStatusSegunModo(
                    "El plan está vacío. Todas las líneas están en blanco. Añade comandos a alguna línea antes de ejecutar.",
                    "Plan vacío: no hay comandos que ejecutar");
                return;
            }

            // Resetear estados previos.
            foreach (var l in Lineas)
                l.ResetearEstado();

            // Recoger líneas incompletas (con dummies requeridos)
            // Recoger líneas peligrosas
            var lineasPeligrosas = Lineas
                .Where(l => !_lineasNoPreguntar.Contains(l))
                .Select(l => new { Linea = l, Simulacion = SimuladorComando.Simular(l) })
                .Where(x => x.Simulacion.Aplica)
                .ToList();

            // Recoger líneas incompletas (con dummies requeridos)
            var lineasIncompletas = Lineas
                .Where(l => l.TieneDummiesRequeridos)
                .ToList();

            // Si hay algo que avisar, mostrar el diálogo combinado.
            if (lineasPeligrosas.Count > 0 || lineasIncompletas.Count > 0)
            {
                var dialogo = new ConfirmarPlanDialog(
                    lineasPeligrosas.Select(x => (x.Linea, x.Simulacion)).ToList(),
                    lineasIncompletas)
                {
                    Owner = System.Windows.Application.Current?.MainWindow
                };

                if (dialogo.ShowDialog() != true)
                {
                    SetStatus("Ejecución del plan cancelada por el usuario");
                    return;
                }

                if (dialogo.NoPreguntarMas)
                {
                    foreach (var x in lineasPeligrosas)
                        _lineasNoPreguntar.Add(x.Linea);
                    // Las incompletas no se añaden a "no preguntar" porque pueden
                    // completarse en el futuro; se vuelven a comprobar cada vez.
                }
            }

            // ----- Comienza la ejecución -----
            _detenerSolicitado = false;
            EjecutandoPlan = true;
            EstaEjecutando = true;
            MensajeCmd = "EJECUTANDO PLAN...";
            SetStatus("Ejecutando plan...");

            Salida.Clear();
            AgregarSalida("╔══════════════════════════════════════════════════════╗");
            AgregarSalida($"║  EJECUCIÓN DEL PLAN — {Lineas.Count} línea(s)            ");
            AgregarSalida("╚══════════════════════════════════════════════════════╝");
            AgregarSalida("");

            int errores = 0;
            int exitos = 0;

            IniciarVolcadoSalida();
            try
            {
                for (int i = 0; i < Lineas.Count; i++)
                {
                    if (_detenerSolicitado) break;

                    var linea = Lineas[i];
                    var texto = linea.Texto;

                    // Si la línea está vacía, la saltamos.
                    if (string.IsNullOrWhiteSpace(texto))
                    {
                        linea.Estado = EstadoEjecucion.Cancelada;
                        continue;
                    }

                    // Cambiamos la activa a esta línea para que el editor la siga.
                    LineaActiva = linea;

                    AgregarSalida("═══════════════════════════════════════════════════════");
                    AgregarSalida($"📂 Línea {i + 1}/{Lineas.Count} · {linea.DirectorioTrabajo}");
                    AgregarSalida($"> {texto}");
                    AgregarSalida("───────────────────────────────────────────────────────");

                    var codigo = await EjecutarLineaAsync(linea);

                    if (codigo == 0)
                    {
                        linea.Estado = EstadoEjecucion.Exito;
                        exitos++;
                    }
                    else if (codigo == -1)
                    {
                        linea.Estado = EstadoEjecucion.Cancelada;
                        AgregarSalida("[Línea cancelada por el usuario]");
                        break;
                    }
                    else
                    {
                        linea.Estado = EstadoEjecucion.Error;
                        errores++;

                        if (_detenerEnError)
                        {
                            AgregarSalida("");
                            AgregarSalida("⛔ Ejecución detenida por error.");
                            SetStatusSegunModo(
        $"Error en línea {i + 1}. Ejecución detenida. Verifique la pestaña de 'salida'",
        $"Ejecución detenida por error en la linea {i + 1}");
                            break;
                        }
                    }

                    AgregarSalida("");
                }
            }
            catch (Exception ex)
            {
                AgregarSalida($"ERROR: {ex.Message}");
                SetStatusSegunModo(
   $"Error al ejecutar el plan. Revisa la pestaña 'Salida' para más detalles: {ex.Message}",
   "Error al ejecutar el plan.");
                
            }
            finally
            {
                DetenerVolcadoSalida();
                EstaEjecutando = false;
                EjecutandoPlan = false;
                _procesoActual = null;
                MensajeCmd = _detenerSolicitado ? "PLAN CANCELADO" : "PLAN COMPLETADO";

                AgregarSalida("╔══════════════════════════════════════════════════════╗");
                AgregarSalida($"║  RESULTADO: {exitos} OK · {errores} error(es)");
                AgregarSalida("╚══════════════════════════════════════════════════════╝");

                SetStatusSegunModo(
    $"Plan terminado: {exitos} línea(s) OK, {errores} con error. Revisa la pestaña 'Salida' para el detalle de cada línea.",
    $"Plan terminado: {exitos} OK, {errores} con error");
            }
        }

        private async Task EjecutarInternoAsync(string comando)
        {
            var linea = LineaActiva;
            var requiereAdmin = linea.RequiereAdmin;

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c chcp 65001 >nul & " + comando,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = DirectorioTrabajo,
            };

            // Si requiere admin, elevar con UAC.
            if (requiereAdmin)
            {
                // UseShellExecute debe ser false para redirigir salida.
                // Pero para elevar, necesitamos UseShellExecute = true.
                // No podemos tener ambos.

                // Solución: usar un proceso wrapper que eleve el comando
                // y capture su salida a un archivo temporal.

                AgregarSalida("⚠ Este comando requiere permisos de administrador.");
                AgregarSalida("Se solicitará elevación UAC al ejecutar.");
                AgregarSalida("");

                await EjecutarElevadoAsync(comando);
                return;
            }

            // Ejecución normal (sin admin).
            using var proceso = new Process { StartInfo = psi };
            _procesoActual = proceso;

            proceso.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null && !_ignorarSalida)
                    EncolarSalida(e.Data);
            };
            proceso.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null && !_ignorarSalida)
                    EncolarSalida("ERR: " + e.Data);
            };

            proceso.Start();
            proceso.BeginOutputReadLine();
            proceso.BeginErrorReadLine();

            await proceso.WaitForExitAsync();

            AgregarSalida("");
            AgregarSalida($"[Proceso terminado con código {proceso.ExitCode}]");

            MensajeCmd = proceso.ExitCode == 0
                ? "EJECUTADO (código 0)"
                : $"EJECUTADO (código {proceso.ExitCode})";


            SetStatusSegunModo(
    proceso.ExitCode == 0
        ? "Comando ejecutado correctamente. Revisa la pestaña 'Salida' para ver el resultado."
        : $"El comando devolvió código {proceso.ExitCode}. Revisa la pestaña 'Salida' para más detalles.",
    proceso.ExitCode == 0
        ? "Comando ejecutado correctamente"
        : $"El comando devolvió código {proceso.ExitCode}");
        }

        /// <summary>
        /// Ejecuta un comando con elevación UAC.
        /// Como no podemos redirigir stdout con UAC directamente,
        /// usamos un archivo temporal para capturar la salida.
        /// </summary>
        private async Task EjecutarElevadoAsync(string comando)
        {
            _detenerSolicitado = false;

            // Usar carpeta propia para temporales (más limpio que %TEMP%).
            var carpetaTemp = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VisualCmdStudio", "temp");

            if (!System.IO.Directory.Exists(carpetaTemp))
                System.IO.Directory.CreateDirectory(carpetaTemp);

            // Nombre único por ejecución: evita colisiones incluso si algo falla.
            var idUnico = Guid.NewGuid().ToString("N");
            var tempOut = System.IO.Path.Combine(carpetaTemp, $"salida_{idUnico}.tmp");
            var tempErr = System.IO.Path.Combine(carpetaTemp, $"error_{idUnico}.tmp");

            try
            {
                var comandoConRedireccion =
                    $"/c chcp 65001 >nul & ({comando}) > \"{tempOut}\" 2> \"{tempErr}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = comandoConRedireccion,
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = DirectorioTrabajo,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var proceso = new Process { StartInfo = psi };
                _procesoActual = proceso;

                try
                {
                    proceso.Start();
                }
                catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
                {
                    AgregarSalida("⚠ Elevación cancelada por el usuario.");
                    MensajeCmd = "CANCELADO";
                    SetStatusSegunModo(
        "Elevación cancelada por el usuario.",
        "Elevación cancelada");
                    return;
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    AgregarSalida($"⚠ Error al elevar: código={ex.NativeErrorCode}, {ex.Message}");
                    MensajeCmd = "ERROR";
                    SetStatusSegunModo(
        "Error al elevar.Por favor verificar la pestaña 'Salida' para mas información.",
        "Error al elevar");
                    return;
                }

                // Esperar a que termine (con posibilidad de detener).
                while (!proceso.HasExited)
                {
                    if (_detenerSolicitado)
                    {
                        try { proceso.Kill(entireProcessTree: true); } catch { }

                        // Esperar a que el proceso termine DE VERDAD antes de leer/borrar.
                        try
                        {
                            using var cts = new System.Threading.CancellationTokenSource(3000);
                            await proceso.WaitForExitAsync(cts.Token);
                        }
                        catch { }

                        break;
                    }
                    await Task.Delay(100);
                }

                // Leer los archivos de salida (con reintentos por si Windows aún no
                // ha liberado los handles).
                await LeerSalidaConReintentos(tempOut, tempErr);

                AgregarSalida("");
                AgregarSalida($"[Proceso elevado terminado con código {proceso.ExitCode}]");

                MensajeCmd = proceso.ExitCode == 0
                    ? "EJECUTADO CON ADMIN (código 0)"
                    : $"EJECUTADO CON ADMIN (código {proceso.ExitCode})";

                SetStatusSegunModo(
    proceso.ExitCode == 0
        ? "Comando ejecutado correctamente. Revisa la pestaña 'Salida' para ver el resultado."
        : $"El comando devolvió código {proceso.ExitCode}. Revisa la pestaña 'Salida' para más detalles.",
    proceso.ExitCode == 0
        ? "Comando ejecutado correctamente"
        : $"El comando devolvió código {proceso.ExitCode}");
            }
            finally
            {
                // Borrar con reintentos (el proceso puede tardar en liberar los handles).
                await BorrarConReintentos(tempOut);
                await BorrarConReintentos(tempErr);
            }
        }

        /// <summary>
        /// Lee los archivos de salida con reintentos. Si un archivo está bloqueado,
        /// espera un poco y reintenta.
        /// </summary>
        private async Task LeerSalidaConReintentos(string tempOut, string tempErr)
        {
            // Leer stdout
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    if (System.IO.File.Exists(tempOut))
                    {
                        var lineas = System.IO.File.ReadAllLines(tempOut);
                        foreach (var linea in lineas)
                            AgregarSalida(linea);
                    }
                    break;
                }
                catch (System.IO.IOException)
                {
                    if (i == 4)
                    {
                        AgregarSalida($"[No se pudo leer el archivo de salida tras varios intentos]");
                    }
                    else
                    {
                        await Task.Delay(200);
                    }
                }
            }

            // Leer stderr
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    if (System.IO.File.Exists(tempErr))
                    {
                        var lineas = System.IO.File.ReadAllLines(tempErr);
                        foreach (var linea in lineas)
                            AgregarSalida("ERR: " + linea);
                    }
                    break;
                }
                catch (System.IO.IOException)
                {
                    if (i == 4)
                    {
                        AgregarSalida($"[No se pudo leer el archivo de error tras varios intentos]");
                    }
                    else
                    {
                        await Task.Delay(200);
                    }
                }
            }
        }

        /// <summary>
        /// Borra un archivo con reintentos. Si está bloqueado, espera y reintenta.
        /// </summary>
        private async Task BorrarConReintentos(string ruta)
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    if (System.IO.File.Exists(ruta))
                        System.IO.File.Delete(ruta);
                    return;   // Éxito
                }
                catch
                {
                    if (i < 4)
                        await Task.Delay(300);
                }
            }
            // Si tras 5 intentos no se ha podido borrar, lo dejamos. Se limpiará en
            // el próximo arranque.
        }

        /// <summary>
        /// Ejecuta una línea concreta. Devuelve:
        /// - El código de salida (0 = éxito, otro = error).
        /// - -1 si se ha cancelado por el usuario.
        /// </summary>
        private async Task<int> EjecutarLineaAsync(LineaComando linea)
        {
            linea.Estado = EstadoEjecucion.Ejecutando;

            if (linea.RequiereAdmin)
            {
                return await EjecutarLineaElevadaAsync(linea);
            }
            linea.Estado = EstadoEjecucion.Ejecutando;

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c chcp 65001 >nul & " + linea.Texto,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = linea.DirectorioTrabajo,
            };

            using var proceso = new Process { StartInfo = psi };
            _procesoActual = proceso;

            proceso.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null && !_ignorarSalida)
                    EncolarSalida(e.Data);
            };
            proceso.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    EncolarSalida("ERR: " + e.Data);
            };

            try
            {
                proceso.Start();
                proceso.BeginOutputReadLine();
                proceso.BeginErrorReadLine();

                // Esperamos con cancelación.
                while (!proceso.HasExited)
                {
                    if (_detenerSolicitado)
                    {
                        try { proceso.Kill(entireProcessTree: true); } catch { }
                        return -1;
                    }
                    await Task.Delay(100);
                }

                return proceso.ExitCode;
            }
            catch (Exception ex)
            {
                AgregarSalida($"ERROR al ejecutar: {ex.Message}");
                return -2;
            }
        }


        private async Task<int> EjecutarLineaElevadaAsync(LineaComando linea)
        {
            var tempOut = System.IO.Path.GetTempFileName();
            var tempErr = System.IO.Path.GetTempFileName();

            try
            {
                AgregarSalida("⚠ Comando con elevación UAC...");

                var comandoConRedireccion =
                    $"/c chcp 65001 >nul & ({linea.Texto}) > \"{tempOut}\" 2> \"{tempErr}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = comandoConRedireccion,
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = linea.DirectorioTrabajo,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var proceso = new Process { StartInfo = psi };
                _procesoActual = proceso;

                try
                {
                    proceso.Start();
                }
                catch (System.ComponentModel.Win32Exception ex)
                    when (ex.NativeErrorCode == 1223)
                {
                    AgregarSalida("⚠ Elevación cancelada por el usuario.");
                    return -1;
                }

                while (!proceso.HasExited)
                {
                    if (_detenerSolicitado)
                    {
                        try { proceso.Kill(entireProcessTree: true); } catch { }
                        return -1;
                    }
                    await Task.Delay(100);
                }

                try
                {
                    if (System.IO.File.Exists(tempOut))
                        foreach (var l in System.IO.File.ReadAllLines(tempOut)) AgregarSalida(l);
                    if (System.IO.File.Exists(tempErr))
                        foreach (var l in System.IO.File.ReadAllLines(tempErr)) AgregarSalida("ERR: " + l);
                }
                catch { }

                return proceso.ExitCode;
            }
            finally
            {
                try { if (System.IO.File.Exists(tempOut)) System.IO.File.Delete(tempOut); } catch { }
                try { if (System.IO.File.Exists(tempErr)) System.IO.File.Delete(tempErr); } catch { }
            }
        }

        /// <summary>Detiene el proceso en curso si lo hay.</summary>
        public void DetenerEjecucion()
        {
            _detenerSolicitado = true;
            _ignorarSalida = true;   // ← ignoramos toda salida nueva

            lock (_bufferLock)
            {
                _bufferSalida.Clear();
            }

            if (_procesoActual != null && !_procesoActual.HasExited)
            {
                try { _procesoActual.Kill(entireProcessTree: true); } catch { }
            }

            AgregarSalida("");
            AgregarSalida("[Detención solicitada por el usuario]");
            MensajeCmd = "CANCELANDO...";
            SetStatus("Deteniendo ejecución...");
        }

        // Handler vacío para reemplazar los que había.
        private void NullHandler(object sender, DataReceivedEventArgs e) { }

        // ==== Helpers ====

        private void AgregarSalida(string linea) => Salida.Add(linea);

        /// <summary>Ejecuta una acción en el hilo de la UI, venga de donde venga.</summary>
        private static void InvokeUI(Action accion)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                accion();
            else
                dispatcher.Invoke(accion);
        }
        // ==== Portapapeles de líneas ====

        /// <summary>
        /// Última línea copiada o cortada, pendiente de pegar.
        /// Guardamos una copia completa para conservar todos los metadatos.
        /// </summary>
        private LineaComando? _lineaCopiada;

        /// <summary>¿Hay algo que pegar?</summary>
        public bool PuedePegar => _lineaCopiada != null;

        /// <summary>Copia la línea activa al portapapeles interno y del sistema.</summary>
        public void CopiarLineaActiva()
        {
            _lineaCopiada = LineaActiva.Clonar();
            OnPropertyChanged(nameof(PuedePegar));

            // También al portapapeles del sistema, como texto plano.
            try
            {
                System.Windows.Clipboard.SetText(LineaActiva.Texto);
                SetStatusSegunModo(
    $"Línea {LineaActiva.NumeroLinea} copiada. Puedes pegarla con Ctrl+V o 'Editar → Pegar'.",
    $"Línea {LineaActiva.NumeroLinea} copiada");
            }
            catch
            {
                SetStatus($"Línea {LineaActiva.NumeroLinea} copiada (solo interno)");
            }
        }

        /// <summary>Corta la línea activa: copia + elimina.</summary>
        public void CortarLineaActiva()
        {
            _lineaCopiada = LineaActiva.Clonar();
            OnPropertyChanged(nameof(PuedePegar));

            try { System.Windows.Clipboard.SetText(LineaActiva.Texto); } catch { }

            // Si es la única línea, solo limpiamos su contenido en lugar de eliminarla.
            if (Lineas.Count == 1)
            {
                LineaActiva.Limpiar();
                SetStatusSegunModo(
    "Línea cortada. Puedes pegarla con Ctrl+V.",
    "Línea cortada");
                return;
            }

            EliminarLineaActiva();
            SetStatus("Línea cortada");
        }

        /// <summary>
        /// Pega la línea copiada justo después de la activa y la deja como activa.
        /// Si no hay nada en el portapapeles interno, intenta leer del sistema.
        /// </summary>
        /// <summary>
        /// Pega la línea copiada. Reglas:
        /// 1. Si la activa está vacía, se reutiliza (pegar "en" ella).
        /// 2. Si la activa NO está vacía y lo pegado también está vacío,
        ///    no se hace nada (evita crear vacías intermedias).
        /// 3. En caso contrario, se inserta justo después de la activa.
        /// </summary>
        public void PegarLinea()
        {
            LineaComando aPegar;

            if (_lineaCopiada != null)
            {
                aPegar = _lineaCopiada.Clonar();
                ConfigurarNuevaLinea(aPegar);
            }
            else if (System.Windows.Clipboard.ContainsText())
            {
                var texto = System.Windows.Clipboard.GetText().Trim();
                if (string.IsNullOrWhiteSpace(texto))
                {
                    SetStatus("Nada que pegar");
                    return;
                }

                aPegar = new LineaComando
                {
                    DirectorioTrabajo = LineaActiva.DirectorioTrabajo
                };
                var tokens = ComandoParser.Parsear(texto, Comandos, aPegar.DirectorioTrabajo);
                aPegar.ReemplazarCon(tokens);
            }
            else
            {
                SetStatus("Nada que pegar");
                return;
            }

            var activaVacia = string.IsNullOrWhiteSpace(LineaActiva.Texto);
            var pegadaVacia = string.IsNullOrWhiteSpace(aPegar.Texto);

            // Caso 1: activa vacía → se reutiliza.
            if (activaVacia)
            {
                LineaActiva.ReemplazarCon(aPegar.Tokens.Select(t => new Token
                {
                    Tipo = t.Tipo,
                    Texto = t.Texto,
                    Descripcion = t.Descripcion,
                    Color = t.Color,
                    Icono = t.Icono,
                    Origen = t.Origen
                }));

                if (pegadaVacia)
                    SetStatus("Nada que pegar (contenido vacío)");
                else
                    SetStatusSegunModo(
    $"Pegado como línea {aPegar.NumeroLinea}. Puedes editarla o moverla con las flechas ▲▼.",
    $"Pegado como línea {aPegar.NumeroLinea}");
                return;
            }

            // Caso 2: activa no vacía + pegada vacía → no hacemos nada.
            if (pegadaVacia)
            {
                SetStatus("El contenido copiado está vacío");
                return;
            }

            // Caso 3: inserción después de la activa.
            var idx = Lineas.IndexOf(LineaActiva);
            if (idx < 0) idx = Lineas.Count - 1;
            var action = new PegarLineaAction(Lineas, aPegar, idx + 1);
            UndoRedo.Ejecutar(action);

            LineaActiva = aPegar;

            SetStatus($"Pegado como línea {aPegar.NumeroLinea}");
        }

        /// <summary>Duplica la línea activa justo después.</summary>
        public void DuplicarLineaActiva()
        {
            var copia = LineaActiva.Clonar();
            var idx = Lineas.IndexOf(LineaActiva);
            if (idx < 0) idx = Lineas.Count - 1;

            var action = new DuplicarLineaAction(Lineas, copia, idx + 1);
            UndoRedo.Ejecutar(action);

            LineaActiva = copia;

            SetStatusSegunModo(
                $"Línea duplicada como {copia.NumeroLinea}. Puedes deshacer con Ctrl+Z.",
                $"Línea duplicada como {copia.NumeroLinea}");
        }
        // ==== Reordenación ====

        /// <summary>¿Puede la línea activa subir en la pila?</summary>
        public bool PuedeSubirActiva =>
            Lineas.IndexOf(LineaActiva) > 0;

        /// <summary>¿Puede la línea activa bajar en la pila?</summary>
        public bool PuedeBajarActiva =>
            Lineas.IndexOf(LineaActiva) < Lineas.Count - 1;

        /// <summary>Mueve la línea activa una posición arriba.</summary>
        public void MoverActivaArriba()
        {
            var idx = Lineas.IndexOf(LineaActiva);
            if (idx <= 0) return;

            var action = new MoverLineaAction(Lineas, LineaActiva, idx, idx - 1);
            UndoRedo.Ejecutar(action);

            SetStatusSegunModo(
                $"Línea movida a la posición {idx}. Puedes deshacer con Ctrl+Z.",
                $"Línea movida a la posición {idx}");

            OnPropertyChanged(nameof(PuedeSubirActiva));
            OnPropertyChanged(nameof(PuedeBajarActiva));
        }

        /// <summary>Mueve la línea activa una posición abajo.</summary>
        public void MoverActivaAbajo()
        {
            var idx = Lineas.IndexOf(LineaActiva);
            if (idx < 0 || idx >= Lineas.Count - 1) return;

            var action = new MoverLineaAction(Lineas, LineaActiva, idx, idx + 1);
            UndoRedo.Ejecutar(action);

            SetStatusSegunModo(
                $"Línea movida a la posición {idx + 2}. Puedes deshacer con Ctrl+Z.",
                $"Línea movida a la posición {idx + 2}");

            OnPropertyChanged(nameof(PuedeSubirActiva));
            OnPropertyChanged(nameof(PuedeBajarActiva));
        }

        /// <summary>
        /// Exporta el plan actual como archivo .bat en la ruta indicada.
        /// </summary>
        public void ExportarABat(string ruta, OpcionesExportacionBat opciones)
        {
            try
            {
                var contenido = ExportadorBat.GenerarContenido(Lineas, opciones);
                ExportadorBat.GuardarEnArchivo(ruta, contenido, opciones);

                SetStatusSegunModo(
    $"Exportado a: {ruta}. Puedes ejecutarlo con doble clic o añadirlo a una tarea programada.",
    $"Exportado a: {ruta}");
            }
            catch (Exception ex)
            {
                SetStatus($"Error al exportar: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"No se pudo exportar el archivo:\n{ex.Message}",
                    "Error de exportación",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Comprueba si la línea activa puede cerrarse.
        /// Si tiene dummies requeridos, muestra un diálogo de confirmación.
        /// Devuelve true si el usuario decide continuar con el cierre.
        /// </summary>
        private bool ConfirmarCierreSiEsNecesario()
        {
            var linea = LineaActiva;

            if (!linea.TieneDummiesRequeridos)
                return true;

            // En modo aprendiz, siempre avisa.
            // En modo experto, solo si no está el modo silencioso.
            if (EsModoExperto && ModoSilencioso)
                return true;

            // Mostrar diálogo.
            var dialogo = new ConfirmarCierreDialog(linea.DummiesRequeridos)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };

            var resultado = dialogo.ShowDialog();
            return resultado == true && dialogo.CerrarIgualmente;
        }
        
       

        /// <summary>
        /// Guarda la sesión actual en el archivo indicado.
        /// Devuelve true si se guardó correctamente.
        /// </summary>
        public bool GuardarSesion(string ruta, string? creador = null)
        {
            try
            {
                var sesion = new SesionVcmd
                {
                    Entorno = _configuracion.Entorno,
                    Creador = creador ?? Environment.UserName,
                    ModoSilencioso = ModoSilencioso,
                    LineaActivaIndice = Math.Max(0, Lineas.IndexOf(LineaActiva)),
                    Lineas = new List<LineaSesion>()
                };

                // Solo guardar líneas con contenido.
                foreach (var linea in Lineas)
                {
                    if (string.IsNullOrWhiteSpace(linea.Texto))
                        continue;

                    sesion.Lineas.Add(new LineaSesion
                    {
                        DirectorioTrabajo = linea.DirectorioTrabajo,
                        Comando = linea.Texto
                    });
                }

                // Ajustar el índice de la línea activa si algunas líneas se omitieron.
                if (sesion.Lineas.Count == 0)
                {
                    // Si todas estaban vacías, guardamos una sesión vacía válida.
                    sesion.LineaActivaIndice = 0;
                }
                else if (sesion.LineaActivaIndice >= sesion.Lineas.Count)
                {
                    // Si la activa estaba más allá de las líneas guardadas, ajustar.
                    sesion.LineaActivaIndice = sesion.Lineas.Count - 1;
                }

                SesionService.Guardar(ruta, sesion);


                SetStatusSegunModo(
     $"Sesión guardada en: {ruta}. Puedes recuperarla con 'Archivo → Abrir sesión' (Ctrl+O).",
     $"Sesión guardada: {ruta}");
                return true;
            }
            catch (Exception ex)
            {
                SetStatus($"Error al guardar la sesión: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Importa un plan desde distintos formatos:
        /// - .vcmd: carga nativa completa.
        /// - .bat/.cmd: parsea respetando el contenido original.
        /// - .ps1/.sh: importa como texto plano (previa confirmación del usuario).
        /// Devuelve true si se importó algo.
        /// </summary>
        public bool ImportarPlan(string ruta)
        {
            var ext = Path.GetExtension(ruta).ToLowerInvariant();

            // .vcmd: delegar en CargarSesion.
            if (ext == ".vcmd")
                return CargarSesion(ruta);

            List<LineaSesion>? lineas = null;

            if (ext == ".bat" || ext == ".cmd")
            {
                try
                {
                    lineas = ImportadorBat.Importar(ruta, Comandos);
                }
                catch (Exception ex)
                {
                    SetStatus($"Error al importar .bat/.cmd: {ex.Message}");
                    return false;
                }
            }
            else if (ext == ".ps1" || ext == ".sh")
            {
                // Diálogo de soporte limitado (persistente con "no volver a preguntar").
                if (!_configuracion.AvisoImportacionLimitadaSilenciado)
                {
                    var dialogo = new DialogoImportacionLimitada(ext)
                    {
                        Owner = System.Windows.Application.Current?.MainWindow
                    };
                    if (dialogo.ShowDialog() != true)
                    {
                        SetStatus("Importación cancelada");
                        return false;
                    }
                    if (dialogo.NoVolverAMostrar)
                    {
                        _configuracion.AvisoImportacionLimitadaSilenciado = true;
                        ConfiguracionService.Guardar(_configuracion);
                    }
                }

                try
                {
                    lineas = ImportadorTextoPlano.Importar(ruta);
                }
                catch (Exception ex)
                {
                    SetStatus($"Error al importar: {ex.Message}");
                    return false;
                }
            }
            else
            {
                SetStatus($"Formato no soportado: {ext}");
                return false;
            }

            if (lineas == null || lineas.Count == 0)
            {
                SetStatus("El archivo no contiene líneas importables");
                return false;
            }

            // Reemplazar plan actual.
            Lineas.Clear();
            UndoRedo.Limpiar();

            foreach (var ls in lineas)
            {
                var linea = new LineaComando
                {
                    DirectorioTrabajo = string.IsNullOrWhiteSpace(ls.DirectorioTrabajo)
                        ? Environment.CurrentDirectory
                        : ls.DirectorioTrabajo
                };

                linea.ActualizarDesdeTexto(ls.Comando ?? "");
                linea.LimpiarHistorial();

                Lineas.Add(linea);
                ConfigurarNuevaLinea(linea);
            }

            if (Lineas.Count == 0)
            {
                var vacia = new LineaComando();
                vacia.ConfirmarEstado();
                Lineas.Add(vacia);
            }

            LineaActiva = Lineas[0];
            LineaActiva.EsActiva = true;

            SetStatusSegunModo(
                $"Importado desde {Path.GetFileName(ruta)}: {Lineas.Count} línea(s). Revisa los directorios de trabajo si el script usaba rutas relativas.",
                $"Importado: {Lineas.Count} línea(s)");

            return true;
        }

        /// <summary>
        /// Carga una sesión desde el archivo indicado.
        /// Reemplaza el contenido actual del editor.
        /// Devuelve true si se cargó correctamente.
        /// </summary>
        public bool CargarSesion(string ruta)
        {
            try
            {
                var sesion = SesionService.Cargar(ruta);

                if (sesion == null)
                {
                    SetStatus($"No se pudo leer el archivo: {ruta}");
                    return false;
                }

                // Verificar entorno (en v1.0 solo "cmd").
                if (sesion.Entorno != "cmd")
                {
                    var resp = MessageBox.Show(
                        $"Este plan está creado para el entorno '{sesion.Entorno}'.\n\n" +
                        "La versión actual de Visual CMD Studio solo soporta CMD. " +
                        "El plan se cargará como texto, pero los comandos específicos del " +
                        $"entorno '{sesion.Entorno}' no serán reconocidos.\n\n" +
                        "¿Deseas cargarlo igualmente?",
                        "Entorno no soportado",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (resp != MessageBoxResult.Yes)
                    {
                        SetStatus("Carga cancelada por el usuario");
                        return false;
                    }
                }

                // Limpiar el estado actual.
                Lineas.Clear();
                UndoRedo.Limpiar();

                // Reconstruir las líneas.
                if (sesion.Lineas != null)
                {
                    foreach (var lineaSesion in sesion.Lineas)
                    {
                        var linea = new LineaComando
                        {
                            DirectorioTrabajo = string.IsNullOrWhiteSpace(lineaSesion.DirectorioTrabajo)
                                ? Environment.CurrentDirectory
                                : lineaSesion.DirectorioTrabajo
                        };

                        linea.ActualizarDesdeTexto(lineaSesion.Comando ?? "");
                        linea.LimpiarHistorial();

                        Lineas.Add(linea);
                        ConfigurarNuevaLinea(linea);
                    }
                }

                // Si no se cargó ninguna línea, crear una vacía.
                if (Lineas.Count == 0)
                {
                    var vacia = new LineaComando();
                    vacia.ConfirmarEstado();
                    Lineas.Add(vacia);
                }

                // Activar la línea que estaba activa al guardar.
                int indiceActiva = Math.Clamp(sesion.LineaActivaIndice, 0, Lineas.Count - 1);
                LineaActiva = Lineas[indiceActiva];
                LineaActiva.EsActiva = true;

                // Restaurar el modo silencioso.
                ModoSilencioso = sesion.ModoSilencioso;

                SetStatus($"Sesión cargada: {ruta}");
                return true;
            }
            catch (Exception ex)
            {
                SetStatus($"Error al cargar la sesión: {ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// Crea una nueva sesión vacía con una sola línea en blanco.
        /// </summary>
        public void NuevaSesionVacia()
        {
            Lineas.Clear();

            var primera = new LineaComando();
            primera.EsActiva = true;
            primera.ConfirmarEstado();

            Lineas.Add(primera);
            LineaActiva = primera;

            SetStatus("Nueva sesión");
            ConfigurarNuevaLinea(primera);
        }
        /// <summary>
        /// Exporta el plan actual como archivo .ps1 (PowerShell).
        /// </summary>
        public void ExportarAPs1(string ruta, OpcionesExportacionPs1 opciones)
        {
            try
            {
                var contenido = ExportadorPs1.GenerarContenido(Lineas, opciones);
                ExportadorPs1.GuardarEnArchivo(ruta, contenido);

                SetStatusSegunModo(
    $"Exportado a: {ruta}. Para ejecutarlo, abre PowerShell y usa: Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass; .\\{System.IO.Path.GetFileName(ruta)}",
    $"Exportado a: {ruta}");
            }
            catch (Exception ex)
            {
                SetStatus($"Error al exportar: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"No se pudo exportar el archivo:\n{ex.Message}",
                    "Error de exportación",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }


        /// <summary>
        /// Exporta el plan actual como archivo .sh (Bash) para revisión.
        /// </summary>
        public void ExportarASh(string ruta, OpcionesExportacionSh opciones)
        {
            try
            {
                var contenido = ExportadorSh.GenerarContenido(Lineas, opciones);
                ExportadorSh.GuardarEnArchivo(ruta, contenido);

                SetStatusSegunModo(
     $"Exportado (para revisión) a: {ruta}. No deberias ejecutarlo con doble clic ni añadirlo a una tarea programada.",
     $"Exportado (para revsión) a: {ruta}");
            }
            catch (Exception ex)
            {
                SetStatus($"Error al exportar: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"No se pudo exportar el archivo:\n{ex.Message}",
                    "Error de exportación",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }


        /// <summary>
        /// Nombre sugerido para el archivo de exportación.
        /// </summary>
        public string NombreSugeridoBat =>
            $"plan_{DateTime.Now:yyyyMMdd_HHmmss}.bat";

        /// <summary>Nombre sugerido para el archivo de exportación .ps1.</summary>
        public string NombreSugeridoPs1 =>
            $"plan_{DateTime.Now:yyyyMMdd_HHmmss}.ps1";

        /// <summary>Nombre sugerido para el archivo de exportación .cmd.</summary>
        public string NombreSugeridoCmd =>
            $"plan_{DateTime.Now:yyyyMMdd_HHmmss}.cmd";

        /// <summary>Nombre sugerido para el archivo de exportación .sh.</summary>
        public string NombreSugeridoSh =>
            $"plan_{DateTime.Now:yyyyMMdd_HHmmss}.sh";

        public void RecargarCatalogo()
        {
            Comandos.Clear();
            CargarComandos();
            ComandosView.Refresh();
        }
        // ==== Registro de uso y orden de comandos ====

        private readonly RegistroUso _registroUso;
        private readonly object _registroUsoLock = new();

        private int _ordenSeleccionado = 0;
        /// <summary>
        /// Modo de ordenación del catálogo:
        /// 0 = Por familia (por defecto)
        /// 1 = A-Z
        /// 2 = Frecuencia
        /// </summary>
        public int OrdenSeleccionado
        {
            get => _ordenSeleccionado;
            set
            {
                if (_ordenSeleccionado == value) return;
                _ordenSeleccionado = value;
                OnPropertyChanged();
                AplicarOrdenacion();
            }
        }
        /// <summary>
        /// Aplica la agrupación y ordenación al catálogo según OrdenSeleccionado.
        /// </summary>
        private void AplicarOrdenacion()
        {
            if (ComandosView == null) return;

            // Limpiar agrupación y ordenación previas.
            ComandosView.GroupDescriptions.Clear();
            ComandosView.SortDescriptions.Clear();

            switch (_ordenSeleccionado)
            {
                case 1:   // A-Z
                    ComandosView.SortDescriptions.Add(
                        new SortDescription(nameof(Comando.Nombre), ListSortDirection.Ascending));
                    break;

                case 2:   // Frecuencia
                          // Ordenar por uso descendente, luego alfabético.
                          // Como no podemos ordenar directamente por un diccionario,
                          // ordenamos por una propiedad calculada.
                    ComandosView.SortDescriptions.Add(
                        new SortDescription(nameof(Comando.Nombre), ListSortDirection.Ascending));
                    // Necesitamos una propiedad Usos en Comando que actualizaremos.
                    ComandosView.SortDescriptions.Clear();
                    ComandosView.SortDescriptions.Add(
                        new SortDescription(nameof(Comando.Usos), ListSortDirection.Descending));
                    ComandosView.SortDescriptions.Add(
                        new SortDescription(nameof(Comando.Nombre), ListSortDirection.Ascending));

                    break;

                default:  // 0 = Por familia
                    ComandosView.GroupDescriptions.Add(
                        new PropertyGroupDescription(nameof(Comando.Familia)));
                    ComandosView.SortDescriptions.Add(
                        new SortDescription(nameof(Comando.Familia), ListSortDirection.Ascending));
                    ComandosView.SortDescriptions.Add(
                        new SortDescription(nameof(Comando.Nombre), ListSortDirection.Ascending));
                    break;
            }
        }
        /// <summary>
        /// Registra el uso de un comando. Actualiza el contador y persiste.
        /// </summary>
        private void RegistrarUso(Comando cmd)
        {
            lock (_registroUsoLock)
            {
                if (_registroUso.Comandos.TryGetValue(cmd.Nombre, out var cuenta))
                    _registroUso.Comandos[cmd.Nombre] = cuenta + 1;
                else
                    _registroUso.Comandos[cmd.Nombre] = 1;

                UsoService.Guardar(_registroUso);
            }

            // Actualizar la propiedad Usos del comando para que la ordenación
            // por frecuencia funcione.
            cmd.Usos = _registroUso.Comandos.TryGetValue(cmd.Nombre, out var n) ? n : 0;

            // Si estamos en modo frecuencia, refrescar.
            if (_ordenSeleccionado == 2)
                ComandosView.Refresh();

        }
        private void ConfigurarNuevaLinea(LineaComando linea)
        {
            linea.ComandoReconocido += cmd => RegistrarUso(cmd);
        }
        /// <summary>
        /// Sustituye un token por otro dentro de la línea activa.
        /// El token destino se elimina; el token origen ocupa su posición.
        /// </summary>
        public void SustituirTokenPorToken(Token tokenOrigen, Token tokenDestino)
        {
            LineaActiva.SustituirTokenPorToken(tokenOrigen, tokenDestino);
            SetStatus($"Token sustituido: {tokenDestino.Texto} → {tokenOrigen.Texto}");
        }
                // ==== Portapapeles de tokens ====

        /// <summary>
        /// Token copiado o cortado, pendiente de pegar.
        /// Se guarda una copia con todos sus metadatos.
        /// </summary>
        private Token? _tokenCopiado;

        /// <summary>¿Hay un token en el portapapeles interno?</summary>
        public bool PuedePegarToken => _tokenCopiado != null;

        /// <summary>Copia un token al portapapeles interno.</summary>
        public void CopiarToken(Token token)
        {
            _tokenCopiado = ClonarToken(token);
            OnPropertyChanged(nameof(PuedePegarToken));
            SetStatus($"Token copiado: {token.Texto}");
        }

        /// <summary>Corta un token: copia + elimina de la línea.</summary>
        public void CortarToken(Token token)
        {
            _tokenCopiado = ClonarToken(token);
            OnPropertyChanged(nameof(PuedePegarToken));

            LineaActiva.QuitarToken(token);
            SetStatus($"Token cortado: {token.Texto}");
        }

        /// <summary>
        /// Pega el token copiado en la posición indicada (o al final si no se indica).
        /// </summary>
        public void PegarToken(int? indice = null)
        {
            if (_tokenCopiado == null)
            {
                SetStatus("No hay token en el portapapeles");
                return;
            }

            var nuevo = ClonarToken(_tokenCopiado);

            if (indice.HasValue)
            {
                // Insertar en la posición indicada.
                var tokens = LineaActiva.Tokens.ToList();
                if (indice.Value < 0) indice = 0;
                if (indice.Value > tokens.Count) indice = tokens.Count;
                tokens.Insert(indice.Value, nuevo);
                var nuevoTexto = string.Join(" ", tokens.Select(t => t.Texto));
                LineaActiva.ActualizarDesdeTexto(nuevoTexto);
                LineaActiva.ConfirmarEstado();
            }
            else
            {
                // Al final.
                LineaActiva.AgregarToken(nuevo);
            }

            SetStatus($"Token pegado: {nuevo.Texto}");
        }

        /// <summary>
        /// Clona un token (nueva instancia con los mismos valores).
        /// </summary>
        private static Token ClonarToken(Token original)
        {
            return new Token
            {
                Tipo = original.Tipo,
                Texto = original.Texto,
                Descripcion = original.Descripcion,
                Color = original.Color,
                Icono = original.Icono,
                Origen = original.Origen,
                RequiereAdmin = original.RequiereAdmin,
                ValoresDesplegable = original.ValoresDesplegable != null
                    ? new List<string>(original.ValoresDesplegable)
                    : null
            };
        }
        public ModoTrabajo ModoTrabajo
        {
            get => _configuracion.ModoTrabajo;
            set
            {
                if (_configuracion.ModoTrabajo == value) return;
                _configuracion.ModoTrabajo = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EsModoAprendiz));
                OnPropertyChanged(nameof(EsModoExperto));

                // Si cambiamos a aprendiz y el modo silencioso estaba activo, desactivarlo.
                if (value == ModoTrabajo.Aprendiz && ModoSilencioso)
                {
                    _configuracion.ModoSilencioso = false;
                    OnPropertyChanged(nameof(ModoSilencioso));
                }

                ConfiguracionService.Guardar(_configuracion);

                SetStatus(value == ModoTrabajo.Aprendiz
                    ? "Modo aprendiz activado"
                    : "Modo experto activado");
            }
        }

        public bool EsModoAprendiz => ModoTrabajo == ModoTrabajo.Aprendiz;
        public bool EsModoExperto => ModoTrabajo == ModoTrabajo.Experto;

        public bool ModoSilencioso
        {
            get => _configuracion.ModoSilencioso;
            set
            {
                if (_configuracion.ModoSilencioso == value) return;

                // En modo aprendiz, no permitir activar el modo silencioso.
                if (value && EsModoAprendiz)
                {
                    SetStatus("El modo silencioso no está disponible en modo aprendiz");
                    OnPropertyChanged();  // Para que el binding vuelva a false
                    return;
                }

                _configuracion.ModoSilencioso = value;
                OnPropertyChanged();

                ConfiguracionService.Guardar(_configuracion);

                SetStatus(value
                    ? "Modo silencioso activado"
                    : "Modo silencioso desactivado");
            }
        }
        /// <summary>
        /// Deshace la última acción global.
        /// Si no hay acción global, delega al undo por línea.
        /// </summary>
        public void DeshacerGlobal()
        {
            if (UndoRedo.Deshacer())
            {
                // Asegurar que la línea activa sigue siendo válida.
                if (!Lineas.Contains(LineaActiva) && Lineas.Count > 0)
                {
                    LineaActiva = Lineas[^1];
                }

                SetStatusSegunModo(
                    $"Deshecho: {UndoRedo.DescripcionRehacer ?? "acción"}",
                    "Deshecho");
                return;
            }

            // Fallback: undo por línea.
            if (LineaActiva.Deshacer())
            {
                SetStatus("Deshecho (edición de línea)");
                return;
            }

            SetStatus("Nada que deshacer");
        }

        /// <summary>
        /// Rehace la última acción global.
        /// Si no hay acción global, delega al redo por línea.
        /// </summary>
        public void RehacerGlobal()
        {
            if (UndoRedo.Rehacer())
            {
                if (!Lineas.Contains(LineaActiva) && Lineas.Count > 0)
                {
                    LineaActiva = Lineas[^1];
                }

                SetStatusSegunModo(
                    $"Rehecho: {UndoRedo.DescripcionDeshacer ?? "acción"}",
                    "Rehecho");
                return;
            }

            // Fallback: redo por línea.
            if (LineaActiva.Rehacer())
            {
                SetStatus("Rehecho (edición de línea)");
                return;
            }

            SetStatus("Nada que rehacer");
        }
        /// <summary>
        /// Mueve un token dentro de la línea activa (con undo global).
        /// </summary>
        /// <summary>
        /// Mueve un token dentro de la línea activa (con undo global).
        /// Si tokenAEliminar no es null, lo elimina primero (Ctrl+Drop).
        /// </summary>
        public void MoverTokenConUndo(Token token, int indiceDestino, Token? tokenAEliminar = null)
        {
            var linea = LineaActiva;

            var action = new MoverTokenAction(linea, token, indiceDestino, tokenAEliminar);
            UndoRedo.Ejecutar(action);

            SetStatusSegunModo(
                tokenAEliminar != null
                    ? "Token sustituido. Puedes deshacer con Ctrl+Z."
                    : "Token movido. Puedes deshacer con Ctrl+Z.",
                tokenAEliminar != null ? "Token sustituido" : "Token movido");
        }
    }
}