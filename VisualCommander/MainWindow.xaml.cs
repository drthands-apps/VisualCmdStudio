using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using VisualCommander.Models;
using VisualCommander.ViewModels;


namespace VisualCommander
{
    public partial class MainWindow : Window
    {

        private const string FormatoComando = "VisualCommander.Comando";
        private const string FormatoParametro = "VisualCommander.Parametro";
        private const string FormatoRuta = "VisualCommander.Ruta";
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();



            ViewModel = new MainViewModel();
            DataContext = ViewModel;



            // Cada vez que cambia la línea activa, forzamos el foco al editor.
            ViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.LineaActiva))
                    EnfocarEditorActivo();
            };

            // Auto-scroll del panel de salida cada vez que se añade una línea.
            ViewModel.Salida.CollectionChanged += (_, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Add
                    || e.Action == NotifyCollectionChangedAction.Reset)
                {
                    // Coalescer: si ya hay un scroll programado, no programamos otro.
                    if (_scrollPendiente) return;
                    _scrollPendiente = true;

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _scrollPendiente = false;
                        if (ListaSalida.Items.Count > 0)
                            ListaSalida.ScrollIntoView(ListaSalida.Items[^1]);
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            };
        }
        private TextBox? _editorActivo;

        private bool _scrollPendiente;
        
        private Point _posInicialDrag;

        private void ListaComandos_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Guardamos dónde empezó el clic. Lo usaremos para medir el umbral.
            _posInicialDrag = e.GetPosition(null);
        }

        private void ListaComandos_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            // Solo arrastramos si el botón izquierdo sigue pulsado.
            if (e.LeftButton != MouseButtonState.Pressed) return;

            // Solo si hay un comando seleccionado.
            if (ViewModel.ComandoSeleccionado is null) return;

            // ¿Ha superado el umbral mínimo de movimiento?
            var posActual = e.GetPosition(null);
            var delta = posActual - _posInicialDrag;

            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            // Empaquetamos el comando y arrancamos el drag.
            var datos = new DataObject(FormatoComando, ViewModel.ComandoSeleccionado);
            DragDrop.DoDragDrop((DependencyObject)sender, datos, DragDropEffects.Copy);
        }

        private void ListaComandos_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ViewModel.AgregarComandoSeleccionado();
        }

        private void ListaComandos_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ViewModel.AgregarComandoSeleccionado();
                e.Handled = true; // evita que se propague
            }
        }
        private void EliminarToken_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { DataContext: Token token })
            {
                ViewModel.EliminarToken(token);
                ViewModel.LineaActiva.ConfirmarEstado();
                e.Handled = true;  // evita que el clic se propague al Border padre
            }
        }
        private void PanelBloques_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DeterminarEfecto(e);
            e.Handled = true;
        }

        private void PanelBloques_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DeterminarEfecto(e);

            // Feedback visual: marcamos el panel como "destino activo".
            PanelBloques.Background = new SolidColorBrush(
                Color.FromArgb(20, 74, 144, 217)); // azul muy transparente

            e.Handled = true;
        }

        private void PanelBloques_DragLeave(object sender, DragEventArgs e)
        {
            PanelBloques.Background = Brushes.Transparent;
        }

        private void PanelBloques_Drop(object sender, DragEventArgs e)
        {

            PanelBloques.Background = Brushes.Transparent;
            // Calculamos la posición del cursor y el índice donde va a insertarse.
            var posicionCursor = e.GetPosition(PanelBloques);
            int indice = CalcularIndiceInsercion(posicionCursor);
                 
            // ¿Qué tipo de dato trae?
            // --- Comando ---
            if (e.Data.GetDataPresent(FormatoComando) &&
                e.Data.GetData(FormatoComando) is Comando cmd)
            {
                ViewModel.InsertarTextoEnPosicion(cmd.Nombre, indice);
                e.Handled = true;
                return;
            }

            // --- Ruta ---
            if (e.Data.GetDataPresent(FormatoRuta) &&
                e.Data.GetData(FormatoRuta) is string ruta)
            {
                if (ViewModel.LineaActiva.ComandoActivo is null)
                {
                    ViewModel.SetStatus("Primero añade un comando para poder añadirle una ruta");
                    e.Handled = true;
                    return;
                }

                var textoRuta = ruta.Contains(' ') ? $"\"{ruta}\"" : ruta;
                ViewModel.InsertarTextoEnPosicion(textoRuta, indice);
                e.Handled = true;
                return;
            }

            // --- Parámetro ---
            if (e.Data.GetDataPresent(FormatoParametro) &&
                e.Data.GetData(FormatoParametro) is ParametroVista pv)
            {
                if (ViewModel.LineaActiva.ComandoActivo is null)
                {
                    ViewModel.SetStatus("Primero añade un comando para poder añadirle modificadores");
                    e.Handled = true;
                    return;
                }

                // Los parámetros siguen su lógica específica (marcar checkbox
                // y añadirse al texto). NO los insertamos en la posición del cursor,
                // porque los parámetros no suelen ir entre dos rutas.
                pv.EstaSeleccionado = true;
                e.Handled = true;
                return;
            }
        }

        /// <summary>
        /// Decide el efecto del cursor según el formato arrastrado y el estado actual.
        /// </summary>
        private DragDropEffects DeterminarEfecto(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(FormatoComando))
                return DragDropEffects.Copy;

            if (e.Data.GetDataPresent(FormatoParametro))
            {
                return ViewModel.LineaActiva.ComandoActivo is null
                    ? DragDropEffects.None
                    : DragDropEffects.Copy;
            }

            if (e.Data.GetDataPresent(FormatoRuta))
            {
                // Aceptamos rutas si hay un comando activo (tiene sentido: la ruta
                // es un argumento de un comando).
                return ViewModel.LineaActiva.ComandoActivo is null
                    ? DragDropEffects.None
                    : DragDropEffects.Copy;
            }

            return DragDropEffects.None;
        }
        private Point _posInicialDragMod;

        private void Modificador_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _posInicialDragMod = e.GetPosition(null);
        }

        private void Modificador_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            // Necesitamos que el Border tenga un ParametroVista en su DataContext.
            if (sender is not FrameworkElement { DataContext: ParametroVista pv }) return;

            // Umbral mínimo de movimiento.
            var posActual = e.GetPosition(null);
            var delta = posActual - _posInicialDragMod;

            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            var datos = new DataObject(FormatoParametro, pv);
            DragDrop.DoDragDrop((DependencyObject)sender, datos, DragDropEffects.Copy);
        }
        private void NuevaLinea_Click(object sender, RoutedEventArgs e)
        {
            EmpujarEditorActivoAlModelo();   // ← empujar primero
            ViewModel.LineaActiva.ConfirmarEstado();
            ViewModel.CerrarYCrearSiguiente();
            EnfocarEditorActivo();
        }

        private void CerrarLinea_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.LineaActiva.ConfirmarEstado();
            ViewModel.CerrarLineaActiva();
          
        }
        private void EliminarLinea_Click(object sender, RoutedEventArgs e)
            => ViewModel.EliminarLineaActiva();

        private async void Ejecutar_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.LineaActiva.ConfirmarEstado();
            await ViewModel.EjecutarAsync();
            EnfocarEditorActivo();

        }
        private void Detener_Click(object sender, RoutedEventArgs e)
            => ViewModel.DetenerEjecucion();
        private void Simular_Click(object sender, RoutedEventArgs e)
    => ViewModel.Simular();
        private void ArbolArchivos_Loaded(object sender, RoutedEventArgs e)
        {
            if (ArbolArchivos.Items.Count > 0) return; // solo poblar la primera vez

            var raiz = new TreeViewItem { Header = "💻 Este equipo", IsExpanded = true };

            foreach (var drive in System.IO.DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;

                string etiqueta = string.IsNullOrEmpty(drive.VolumeLabel)
                    ? drive.Name
                    : $"{drive.Name}  ({drive.VolumeLabel})";

                var nodoDrive = new TreeViewItem
                {
                    Header = "💽 " + etiqueta,
                    Tag = drive.RootDirectory.FullName
                };
                nodoDrive.Items.Add(new TreeViewItem { Header = "Cargando..." });
                nodoDrive.Expanded += NodoDrive_Expanded;
                raiz.Items.Add(nodoDrive);
            }

            ArbolArchivos.Items.Add(raiz);
        }

        private void NodoDrive_Expanded(object sender, RoutedEventArgs e)
        {
            if (sender is not TreeViewItem nodo) return;
            if (nodo.Items.Count != 1) return; // ya expandido antes
            if (nodo.Items[0] is not TreeViewItem placeholder) return;
            if (placeholder.Header as string != "Cargando...") return;

            nodo.Items.Clear();
            if (nodo.Tag is not string ruta) return;

            try
            {
                foreach (var dir in System.IO.Directory.EnumerateDirectories(ruta))
                {
                    var hijo = new TreeViewItem
                    {
                        Header = "📁 " + System.IO.Path.GetFileName(dir),
                        Tag = dir
                    };
                    hijo.Items.Add(new TreeViewItem { Header = "Cargando..." });
                    hijo.Expanded += NodoDrive_Expanded;
                    nodo.Items.Add(hijo);
                }
            }
            catch
            {
                nodo.Items.Add(new TreeViewItem { Header = "⛔ Acceso denegado" });
            }
        }
        private Point _posInicialDragArbol;

        private void ArbolArchivos_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _posInicialDragArbol = e.GetPosition(null);
        }

        private void ArbolArchivos_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            // Umbral mínimo de movimiento.
            var posActual = e.GetPosition(null);
            var delta = posActual - _posInicialDragArbol;

            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            // ¿Qué nodo está bajo el cursor?
            var nodoOrigen = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);
            if (nodoOrigen?.Tag is not string ruta || string.IsNullOrWhiteSpace(ruta))
                return;

            // Empaquetamos la ruta y arrancamos el drag.
            var datos = new DataObject(FormatoRuta, ruta);
            DragDrop.DoDragDrop(nodoOrigen, datos, DragDropEffects.Copy);
        }

        /// <summary>Sube por el árbol visual buscando un ancestro del tipo T.</summary>
        private static T? FindAncestor<T>(DependencyObject? actual) where T : DependencyObject
        {
            while (actual != null)
            {
                if (actual is T encontrado) return encontrado;
                actual = System.Windows.Media.VisualTreeHelper.GetParent(actual);
            }
            return null;
        }
        private void CambiarDirectorio_Click(object sender, RoutedEventArgs e)
        {
            var dialogo = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Seleccionar directorio de trabajo",
                InitialDirectory = ViewModel.DirectorioTrabajo
            };

            if (dialogo.ShowDialog() == true)
            {
                ViewModel.CambiarDirectorioTrabajo(dialogo.FolderName);
            }
        }
        private void Prompt_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(FormatoRuta)
                ? DragDropEffects.Move
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void Prompt_DragOver(object sender, DragEventArgs e)
        {
            // Igual que DragEnter; en WPF hay que gestionar ambos.
            e.Effects = e.Data.GetDataPresent(FormatoRuta)
                ? DragDropEffects.Move
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void Prompt_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(FormatoRuta)) return;
            if (e.Data.GetData(FormatoRuta) is not string ruta) return;

            // Solo aceptamos carpetas, no archivos.
            if (!System.IO.Directory.Exists(ruta))
            {
                ViewModel.SetStatus("Solo se puede cambiar el directorio a una carpeta");
                e.Handled = true;
                return;
            }

            ViewModel.CambiarDirectorioTrabajo(ruta);
            e.Handled = true;
        }
       

       

        private static bool TerminaEnOperador(LineaComando linea)
        {
            if (linea.Tokens.Count == 0) return false;
            var ultimo = linea.Tokens[^1].Texto;
            return ultimo is "|" or "&&" or "||" or "&";
        }

        private static bool EsComentario(LineaComando linea)
        {
            if (linea.Tokens.Count == 0) return false;
            var primero = linea.Tokens[0].Texto.ToLowerInvariant();
            return primero == "rem" || primero.StartsWith("::");
        }



        private void ParsearYReemplazar(TextBox tb, LineaComando linea)
        {
            var texto = tb.Text;
            if (string.IsNullOrWhiteSpace(texto)) return;

            var tokens = ComandoParser.Parsear(
                texto,
                ViewModel.Comandos,
                linea.DirectorioTrabajo);

            linea.ReemplazarCon(tokens);

            tb.CaretIndex = tb.Text.Length;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                tb.CaretIndex = tb.Text.Length;
            }), System.Windows.Threading.DispatcherPriority.Background);

            ViewModel.SetStatus($"Línea {linea.NumeroLinea} parseada: {tokens.Count} token(s)");
        }
        private void LineaClic_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: LineaComando linea }) return;

            if (ReferenceEquals(ViewModel.LineaActiva, linea)) return;

            // Confirmamos el estado de la línea que dejamos atrás.
            ViewModel.LineaActiva.ConfirmarEstado();

            ViewModel.LineaActiva = linea;
            e.Handled = true;
        }

        /// <summary>Busca un hijo visual del tipo T recursivamente.</summary>
        private static T? FindVisualChild<T>(DependencyObject padre) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(padre); i++)
            {
                var hijo = System.Windows.Media.VisualTreeHelper.GetChild(padre, i);
                if (hijo is T encontrado) return encontrado;
                var subHijo = FindVisualChild<T>(hijo);
                if (subHijo != null) return subHijo;
            }
            return null;
        }
        private void EditorComando_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            if (tb.DataContext is not LineaComando linea) return;

            if (e.NewValue is true)
            {
                _editorActivo = tb;
                tb.Text = linea.Texto;
                tb.CaretIndex = tb.Text.Length;

                // Nos suscribimos al cambio de Texto de la línea (una sola vez).
                if (tb.Tag as string != "suscrito")
                {
                    tb.Tag = "suscrito";
                    linea.PropertyChanged += (_, args) =>
                    {
                        if (args.PropertyName != nameof(LineaComando.Texto)) return;

                        // Solo sincronizamos si el TextBox NO tiene el foco
                        // o si el cambio vino del modelo (por ejemplo Deshacer).
                        if (!tb.IsFocused || linea.CambioDesdeModelo)
                        {
                            tb.Text = linea.Texto;
                            tb.CaretIndex = tb.Text.Length;
                            linea.CambioDesdeModelo = false;
                        }
                    };
                }

                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    await System.Threading.Tasks.Task.Delay(10);
                    tb.Focus();
                    Keyboard.Focus(tb);
                    tb.SelectAll();
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }
        private void EnfocarEditorActivo()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var itemsControl = PanelLineas;
                if (itemsControl == null) return;

                var generador = itemsControl.ItemContainerGenerator;
                foreach (var item in itemsControl.Items)
                {
                    if (item is not LineaComando linea || !linea.EsActiva) continue;

                    var contenedor = generador.ContainerFromItem(item) as DependencyObject;
                    if (contenedor == null) continue;

                    var tb = FindVisualChild<TextBox>(contenedor);
                    if (tb != null)
                    {
                        Keyboard.Focus(tb);
                        tb.CaretIndex = tb.Text.Length;
                    }
                    break;
                }
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
        private void CopiarLinea_Click(object sender, RoutedEventArgs e)
    => ViewModel.CopiarLineaActiva();

        private void CortarLinea_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.CortarLineaActiva();
            
        }

        private void PegarLinea_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.PegarLinea();
            
        }

        private void DuplicarLinea_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.DuplicarLineaActiva();
            
        }
        private void MenuAcciones_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            
            // ===== PRIORIDAD 1: Esc durante ejecución → detener =====
            if (e.Key == Key.Escape && ViewModel.EstaEjecutando)
            {
                ViewModel.DetenerEjecucion();
                e.Handled = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    EnfocarEditorActivo();
                }), System.Windows.Threading.DispatcherPriority.Background);
                return;
            }



            // Si el foco está en un TextBox, primero gestionamos los atajos
            // que queremos interceptar aunque el foco esté dentro.
            var focoEnTextBox = Keyboard.FocusedElement is TextBox;

            // Ctrl+Z → deshacer
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z)
            {
                if (ViewModel.LineaActiva.Deshacer())
                    ViewModel.SetStatus("Deshecho");
                else
                    ViewModel.SetStatus("Nada que deshacer");
                e.Handled = true;
                return;
            }

            // Ctrl+Y (o Ctrl+Shift+Z) → rehacer
            if ((Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y) ||
                (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Z))
            {
                if (ViewModel.LineaActiva.Rehacer())
                    ViewModel.SetStatus("Rehecho");

                else
                    ViewModel.SetStatus("Nada que rehacer");
                e.Handled = true;
                return;
            }
            // Ctrl+D → duplicar línea
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D)
            {
                ViewModel.DuplicarLineaActiva();
                e.Handled = true;
                return;
            }

            // Ctrl+Shift+↑/↓ → mover línea activa
            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                if (e.Key == Key.Up)
                {
                    ViewModel.MoverActivaArriba();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Down)
                {
                    ViewModel.MoverActivaAbajo();
                    e.Handled = true;
                    return;
                }
            }

            // Ctrl+Enter → cerrar y crear nueva línea
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Enter)
            {
                ViewModel.CerrarYCrearSiguiente();
                e.Handled = true;
                return;
            }
            // Ctrl+N → nueva sesión
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
            {
                NuevaSesion_Click(sender, e);
                e.Handled = true;
                return;
            }

            // Ctrl+O → abrir sesión
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
            {
                AbrirSesion_Click(sender, e);
                e.Handled = true;
                return;
            }

            // Ctrl+S → guardar sesión
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
            {
                GuardarSesion_Click(sender, e);
                e.Handled = true;
                return;
            }

            // ===== Atajos solo cuando el foco NO está en un TextBox =====

            if (focoEnTextBox) return;

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.C: ViewModel.CopiarLineaActiva(); e.Handled = true; break;
                    case Key.X: ViewModel.CortarLineaActiva(); e.Handled = true; break;
                    case Key.V: ViewModel.PegarLinea(); e.Handled = true; break;
                }
            }
            // F5 → ejecutar plan completo
            if (e.Key == Key.F5 && Keyboard.Modifiers == ModifierKeys.None)
            {
                _ = ViewModel.EjecutarPlanAsync();
                EnfocarEditorActivo();
                e.Handled = true;
                return;
            }
        }
        private void MoverArriba_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.MoverActivaArriba();
            
        }

        private void MoverAbajo_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.MoverActivaAbajo();
           
        }
        private void EditorComando_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox tb) return;
            if (tb.DataContext is not LineaComando linea) return;

            if (e.Key == Key.F5 && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (!ViewModel.EstaEjecutando)
                    _ = ViewModel.EjecutarPlanAsync();
                e.Handled = true;
                return;
            }
            if (tb != null)
            {
                // Forzar el foco lógico de teclado.
                Keyboard.Focus(tb);
                tb.CaretIndex = tb.Text.Length;
            }

            if (e.Key == Key.Space)
            {
                // Empujamos el texto al modelo para que se re-proyecten los tokens.
                // Escribimos el espacio primero, luego empujamos.
                EmpujarTextoAlModelo(tb, linea);
                linea.ConfirmarEstado();
                return; // dejamos que el espacio se escriba normalmente
            }

            if (e.Key == Key.Enter)
            {
                EmpujarTextoAlModelo(tb, linea);
                linea.ConfirmarEstado();
                e.Handled = true;
                return;
            }

            else if (e.Key == Key.Escape)
            {
             // Descartamos los cambios no confirmados: volvemos al texto del modelo.
                tb.Text = linea.Texto;
                tb.CaretIndex = tb.Text.Length;
                e.Handled = true;

                return;
            }
        }

      

        private void EditorComando_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            if (tb.DataContext is not LineaComando linea) return;

            // Solo empujamos si el texto del TextBox difiere del modelo.
            if (tb.Text != linea.Texto)
            {
                EmpujarTextoAlModelo(tb, linea);
                linea.ConfirmarEstado();
            }
        }

        /// <summary>
        /// Empuja el texto del TextBox al modelo. Solo si hay diferencia.
        /// </summary>
        private void EmpujarTextoAlModelo(TextBox tb, LineaComando linea)
        {
            if (tb.Text == linea.Texto) return;
            linea.ActualizarDesdeTexto(tb.Text);
            ViewModel.SetStatus($"Línea {linea.NumeroLinea} actualizada");
        }
        /// <summary>
        /// Calcula el índice de inserción según la posición del cursor
        /// sobre el panel de bloques.
        /// Devuelve el índice del token ANTES del cual debe insertarse el nuevo.
        /// </summary>
        private int CalcularIndiceInsercion(Point posicionCursor)
        {
            var tokens = ViewModel.LineaActiva.Tokens;
            if (tokens.Count == 0) return 0;

            // Recorremos los contenedores visuales de cada token.
            for (int i = 0; i < tokens.Count; i++)
            {
                var contenedor = PanelBloques.ItemContainerGenerator.ContainerFromIndex(i)
                                 as FrameworkElement;
                if (contenedor == null) continue;

                // Coordenadas del contenedor en el espacio del PanelBloques.
                Point origen;
                try
                {
                    origen = contenedor.TransformToAncestor(PanelBloques)
                                       .Transform(new Point(0, 0));
                }
                catch
                {
                    // Si el contenedor no es descendiente visual (no debería pasar),
                    // saltamos este token.
                    continue;
                }

                var centroX = origen.X + contenedor.ActualWidth / 2;

                // Si el cursor está a la IZQUIERDA del centro del token actual,
                // el nuevo se inserta ANTES de este token.
                if (posicionCursor.X < centroX)
                    return i;
            }

            // Si el cursor está a la derecha de todos los tokens, va al final.
            return tokens.Count;
        }
        private async void EjecutarPlan_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.EjecutarPlanAsync();
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Al arrancar, damos el foco al editor de la línea activa.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                EnfocarEditorActivo();
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
        private void EmpujarEditorActivoAlModelo()
        {
            var tb = EncontrarEditorActivo();
            if (tb == null)
            {
                
                return;
            }
            if (tb.DataContext is not LineaComando linea)
            {
                
                return;
            }

            

            EmpujarTextoAlModelo(tb, linea);
        }

        private TextBox? EncontrarEditorActivo()
        {
            // Primero intentamos con el campo guardado.
            if (_editorActivo != null && _editorActivo.IsVisible)
                return _editorActivo;

            // Fallback: buscar en el árbol visual.
            var itemsControl = PanelLineas;
            if (itemsControl == null) return null;

            var generador = itemsControl.ItemContainerGenerator;
            foreach (var item in itemsControl.Items)
            {
                if (item is not LineaComando linea || !linea.EsActiva) continue;

                var contenedor = generador.ContainerFromItem(item) as DependencyObject;
                if (contenedor == null) continue;

                return FindVisualChild<TextBox>(contenedor);
            }
            return null;
        }
        private void GuardarBat_Click(object sender, RoutedEventArgs e)
        {
            // Verificamos que hay algo que exportar.
            var lineasConTexto = ViewModel.Lineas
                .Where(l => !string.IsNullOrWhiteSpace(l.Texto))
                .ToList();

            if (lineasConTexto.Count == 0)
            {
                ViewModel.SetStatus("No hay nada que exportar");
                MessageBox.Show("El plan está vacío. Añade al menos una línea con comandos.",
                    "Nada que exportar", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Ruta sugerida: carpeta del escritorio, o la última usada.
            var escritorio = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var rutaSugerida = System.IO.Path.Combine(escritorio, ViewModel.NombreSugeridoBat);

            var dialogo = new DialogoExportarBat(ViewModel.Lineas, rutaSugerida)
            {
                Owner = this
            };

            if (dialogo.ShowDialog() == true)
            {
                ViewModel.ExportarABat(dialogo.RutaSeleccionada, dialogo.Opciones);
            }
        }
        // ==== Edición in-place de tokens ====

        private void Token_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Solo doble clic.
            if (e.ClickCount != 2) return;

            if (sender is not FrameworkElement fe) return;
            if (fe.DataContext is not Token token) return;

            // Solo tokens editables.
            if (!token.EsEditable) return;

            // Guardamos el texto original para poder restaurar con Escape.
            token.TextoAntesDeEditar = token.Texto;

            // Activamos el modo edición.
            token.EstoyEditando = true;

            e.Handled = true;

            EnfocarEditorActivo();
        }

        private void EditorInPlace_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            if (e.NewValue is not true) return;

            // Damos el foco en el siguiente ciclo del dispatcher, cuando
            // el layout ya está asentado. ApplicationIdle es más seguro que
            // Background porque se ejecuta cuando no hay nada más pendiente.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                tb.Focus();
                Keyboard.Focus(tb);
                tb.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
        private void EditorInPlace_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
                _editorActivo = tb;
        }
        private void EditorInPlace_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not TextBox tb) return;
            if (tb.DataContext is not Token token) return;

            if (e.Key == Key.Enter)
            {
                ConfirmarEdicionToken(token, tb.Text);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelarEdicionToken(token);
                e.Handled = true;
            }
        }

        private void EditorInPlace_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox tb) return;
            if (tb.DataContext is not Token token) return;

            // Si todavía estamos en modo edición (no se ha confirmado/cancelado),
            // confirmamos por defecto (como hace cualquier editor).
            if (token.EstoyEditando)
                ConfirmarEdicionToken(token, tb.Text);
        }
        /// <summary>
        /// Da el foco al TextBox del token que esté en modo edición.
        /// Útil cuando el token acaba de entrar en edición y el IsVisibleChanged
        /// no se dispara porque ya nace visible.
        /// </summary>
        private void EnfocarEditorInPlace()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var itemsControl = PanelBloques;
                if (itemsControl == null) return;

                var generador = itemsControl.ItemContainerGenerator;

                foreach (var item in itemsControl.Items)
                {
                    if (item is not Token token || !token.EstoyEditando) continue;

                    var contenedor = generador.ContainerFromItem(item) as DependencyObject;
                    if (contenedor == null) continue;

                    var tb = FindVisualChild<TextBox>(contenedor);
                    if (tb != null)
                    {
                        tb.Focus();
                        Keyboard.Focus(tb);
                        tb.SelectAll();
                    }
                    break;
                }
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void ConfirmarEdicionToken(Token token, string nuevoTexto)
        {
            // Regla: un token de texto se elimina si queda VACÍO (0 caracteres).
            // Pero `""` (dos comillas) es un texto válido.
            if (token.Tipo == TipoToken.Texto && string.IsNullOrEmpty(nuevoTexto))
            {
                token.EstoyEditando = false;
                ViewModel.EliminarToken(token);
                return;
            }

            // Actualizamos el texto del token.
            token.Texto = nuevoTexto;

            // Salimos del modo edición.
            token.EstoyEditando = false;
            token.TextoAntesDeEditar = null;

            // Reconstruimos la línea: es la fuente de verdad.
            var linea = ViewModel.LineaActiva;
            var textos = linea.Tokens.Select(t => t.Texto).ToList();
            linea.ActualizarDesdeTexto(string.Join(" ", textos));
            linea.ConfirmarEstado();

            ViewModel.SetStatus($"Token editado: {nuevoTexto}");
        }

        private void CancelarEdicionToken(Token token)
        {
            // Restauramos el texto original.
            if (token.TextoAntesDeEditar != null)
                token.Texto = token.TextoAntesDeEditar;

            token.EstoyEditando = false;
            token.TextoAntesDeEditar = null;
        }
        private void AgregarTexto_Click(object sender, RoutedEventArgs e)
        {
            // Empujar primero el texto del campo CMD al modelo, para no perder
            // lo que el usuario haya escrito y aún no se haya confirmado.
            EmpujarEditorActivoAlModelo();

            ViewModel.AgregarTextoVacio();
            EnfocarEditorInPlace();
        }
        // ==== Sesiones .vcmd ====

        private string? _rutaSesionActual;

        private void NuevaSesion_Click(object sender, RoutedEventArgs e)
        {
            // TODO futuro: preguntar si guardar cambios pendientes.

            ViewModel.NuevaSesionVacia();
            _rutaSesionActual = null;
            ActualizarTituloVentana();
        }

        private void AbrirSesion_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Abrir sesión",
                Filter = Services.SesionService.Filtro,
                DefaultExt = Services.SesionService.Extension
            };

            if (dlg.ShowDialog() == true)
            {
                if (ViewModel.CargarSesion(dlg.FileName))
                {
                    _rutaSesionActual = dlg.FileName;
                    ActualizarTituloVentana();
                }
            }
        }

        private void GuardarSesion_Click(object sender, RoutedEventArgs e)
        {
            // Empujar el texto del TextBox activo al modelo antes de guardar.
            EmpujarEditorActivoAlModelo();

            if (!string.IsNullOrEmpty(_rutaSesionActual))
            {
                ViewModel.GuardarSesion(_rutaSesionActual);
                return;
            }

            GuardarSesionComo_Click(sender, e);
        }

        private void GuardarSesionComo_Click(object sender, RoutedEventArgs e)
        {
            // Empujar el texto del TextBox activo al modelo antes de guardar.
            EmpujarEditorActivoAlModelo();

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Guardar sesión como",
                Filter = Services.SesionService.Filtro,
                DefaultExt = Services.SesionService.Extension,
                FileName = string.IsNullOrEmpty(_rutaSesionActual)
                    ? "sesion" + Services.SesionService.Extension
                    : System.IO.Path.GetFileName(_rutaSesionActual)
            };

            if (dlg.ShowDialog() == true)
            {
                if (ViewModel.GuardarSesion(dlg.FileName))
                {
                    _rutaSesionActual = dlg.FileName;
                    ActualizarTituloVentana();
                }
            }
        }

                   
               private void ActualizarTituloVentana()
        {
            var nombreBase = "Visual CMD Studio — Constructor visual de línea de comandos";

            if (!string.IsNullOrEmpty(_rutaSesionActual))
            {
                var nombreArchivo = System.IO.Path.GetFileName(_rutaSesionActual);
                Title = $"{nombreArchivo} — {nombreBase}";
            }
            else
            {
                Title = nombreBase;
            }
        }
        private void Salir_Click(object sender, RoutedEventArgs e)
        {
            // TODO futuro: preguntar si guardar cambios pendientes.
            Close();
        }
    }
}