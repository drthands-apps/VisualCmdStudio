# Visual CMD Studio — Notas de diseño y planificación

Documento vivo. Se resume periódicamente. El histórico completo está
en los commits de Git; aquí solo se mantiene lo vigente.

Última actualización: 2026-10-28

---

## Filosofía del proyecto

Visual CMD Studio es un **editor** de comandos de Windows, no una consola.
La ejecución es posterior al ciclo de composición. Los comandos como `cd`
o `set` modifican el PLAN, no el sistema.

Analogía: Excel es un editor de cálculos, no un intérprete.

### Decisiones fundacionales

1. **MVVM estricto**: el modelo manda, la vista reacciona.
2. **El TEXTO es la fuente de verdad**. Los Tokens son una proyección.
3. **JSON como base de datos declarativa**: comandos y utilidades se
   declaran en JSON, no en código.
4. **Multi-línea con contexto por línea**: cada `LineaComando` tiene su
   propio `DirectorioTrabajo`.
5. **Permisos elevados comando-a-comando** (no app-wide).
6. **Modo simulación para comandos destructivos**.
7. **Apariencia CMD con marca de simulación**.
8. **Validación permisiva**: solo validar lo que es seguro. En caso de
   duda, aceptar. CMD no tiene un parser único.
9. **Pensar en la maldad ajena**: los archivos pueden ser modificados.
   Las firmas deben ser criptográficamente verificables.

---

## Estado actual

### Completado — Editor visual

- Catálogo JSON con ~55 comandos, agrupados por familia.
- Multi-línea con contexto de directorio independiente.
- Editor in-place de tokens (Texto, Ruta) con forma achaflanada + lápiz.
- Regla de "línea viva única".
- Copiar/Cortar/Pegar/Duplicar/Mover líneas.
- Undo/Redo por línea con historial de estados.
- Parser reforzado: comillas, espacios colapsados.
- Dummies guía (Fase 3 completa): visuales, requeridos/opcionales,
  bloqueo al cerrar con requeridos, aviso pre-ejecución combinado.
- Modo silencioso persistente (afecta solo a edición, no a ejecución).
- Organizador de comandos (Por familia / A-Z / Frecuencia con
  persistencia en `uso.json` y detección de comandos por texto).
- Hover sobre tokens del constructor → info en panel derecho.
- Ocultar paneles libera espacio (GridLength 0, MinWidth 0).
- Drag & drop de variables/operadores/comodines desde panel derecho
  al constructor, con posición exacta.
- Clic simple para insertar al final (además de drag).
- Flag `_dragEnCurso` para evitar el doble `DoDragDrop`.
- Menú Ver reorganizado ("Constructor visual" y "Línea de comandos").
- Auto-scroll al editor al añadir líneas / cambiar activa.
- Botón ✕ por línea cerrada (visible al hover).
- Modo aprendiz vs experto (Nivel 1 + Nivel 2 básico).
- Undo/Redo global (Fases 9.A y 9.B): líneas y edición de texto.
- Edición in-place con ComboBox para Constante / Variable / Comodín.
- Ctrl+Drop para sustituir tokens; drag interno para reordenar.
- Icono del programa (pavo real cyberpunk, `visualcmdstudio.ico`).
- Indicador "🔇 Silencioso" en barra de estado.

### Completado — Apariencia CMD

- Prompt dinámico coloreado (verde/amarillo/rojo).
- Prompt acortado con tooltip.
- Wrapping estilo CMD real.

### Completado — Ejecución

- Ejecución de línea individual y del plan completo (F5).
- Estados por línea (○ ▶ ✓ ✗ ⊘) + toggle "Detener en error".
- Buffer de salida (RangeObservableCollection).
- Escape/Detener durante la ejecución.
- Elevación UAC puntual para comandos `requiereAdmin`.
- Indicador 🛡 en bloque y en barra de estado.
- Conteo final: OK / errores.

### Completado — Seguridad

- Detección de comandos peligrosos (`peligroso: true`).
- Simuladores específicos: del, rd/rmdir, taskkill.
- Diálogo de confirmación con directorio y afectados.
- Opción "No volver a preguntar para esta línea en esta sesión".

### Completado — Sesiones y exportación

- Guardar/Cargar sesión `.vcmd` (JSON legible, campo `firma` nullable
  preparado para Tier 3).
- Metadatos: creador, verificado (aún no firma criptográfica).
- Exportar a `.bat`, `.cmd`, `.ps1` (completo), `.sh` (para revisión).
- Importar .vcmd/.bat/.cmd/.ps1/.sh. Marcando las limitaciones por el momento de los formatos no compatibles. Se aceptan como solo texto.
- Diálogos con opciones y vista previa.
- Atajos Ctrl+N/O/S/Shift+S.

### Completado — Interfaz

- Menú completo y funcional (Archivo, Editar, Ver, Herramientas, Ayuda).
- Placeholders informativos para funciones no implementadas.
- Panel izquierdo: modo, buscador, catálogo, modificadores, árbol.
- Panel derecho: comando activo, detalle del modificador, detalle del
  token, insertar tokens (texto/operadores/variables/constantes/comodines).
- Panel inferior: comentarios, errores, salida, historial.
- Botón "Insertar texto" con campo previo y Enter para confirmar.
- Explicaciones ampliadas para valores de `/A`, `/O` y otros.
- Árbol de archivos con unidades reales (carga perezosa).
- Drag & drop desde catálogo, panel de modificadores y árbol.

### Completado — Catálogo de tokens desde JSON

- `tokens.json` con constantes (9), variables (17), comodines (4) y
  operadores (12). Metadatos: nombre, descripción, ejemplo, categoría.
- `TokenInfo` y `CatalogoTokens` como modelos.
- `CatalogoTokensService` carga desde JSON.
- `MainViewModel.CatalogoTokens` disponible para la UI.
- Panel derecho bindeado al catálogo.
- Hover sobre cada elemento → descripción completa en panel derecho.
- Doble clic → insertar al final; drag → insertar en posición.

| Categoría | Cantidad | Archivo |
|---|---|---|
| Constantes | 9 | `tokens.json` |
| Variables | 17 | `tokens.json` |
| Comodines | 4 | `tokens.json` |
| Operadores | 12 | `tokens.json` |

**Total**: 42 tokens editables desde JSON sin recompilar.

### Completado — Lenguaje de gestos del editor

| Gesture | Resultado |
|---------|-----------|
| Arrastrar comando del catálogo | Insertar en posición |
| Ctrl+Arrastrar comando del catálogo | Sustituir token bajo cursor |
| Arrastrar token del constructor | Reordenar |
| Ctrl+Arrastrar token del constructor | Eliminar destino, mover origen a su sitio |
| Doble clic en token editable | Edición in-place (TextBox o ComboBox) |
| Doble clic en variable/constante/comodín (panel derecho) | Insertar al final |
| Drag desde panel derecho | Insertar en posición |

### Completado — Undo/Redo

**Dos sistemas conviven**:

- **Global** (`UndoRedoManager`): operaciones sobre líneas (crear,
  mover, borrar, duplicar, pegar) y cambios de texto vía
  `CambiarTextoLineaAction` (añadir/eliminar comandos, rutas, textos,
  mover/sustituir tokens, marcar/desmarcar parámetros con checkbox).
- **Por línea** (`LineaComando`): edición de texto dentro de una línea
  (usado cuando el foco está en el TextBox de la línea).

**Comportamiento**:

- `Ctrl+Z`: intenta global, luego línea.
- `Ctrl+Y`: intenta global, luego línea.
- Helper `EjecutarCambioTexto` envuelve cambios de texto en UndoRedo.

**Limitación conocida (Opción C aceptada)**:

- El orden no es cronológico global. Escenario: mover → escribir →
  Ctrl+Z (deshace escribir) → Ctrl+Y (rehace mover, no escribir).
  El usuario debe pulsar Ctrl+Y dos veces.
- **Fase 9.C completada (2026-10-28)**: parámetros con checkbox ya se
  apilan en UndoRedo global. Punto único
  `MainViewModel.CambiarSeleccionParametro(pv, seleccionado)`.
  CheckBox de la UI pasa de `IsChecked TwoWay` a `OneWay` + Click
  handler (`ModificadorCheck_Click`). El Drop de parámetros y
  `EliminarToken` de parámetro marcado también pasan por ahí.

**Solución futura (Opción A, sesión dedicada)**:

- Unificar en `UndoRedoManager` global.
- Migrar undo por línea a `EditarTextoAction`.
- Eliminar undo por línea.
- Duración estimada: 2-3 horas.
- Prioridad: alta.

---

## Pendiente

### Tier 1 postpuesto

- [X] Import desde `.bat`, `.ps1`, `.sh`.
- [X] @ como TipoToken.Prefijo: parser, LineaComando, ImportadorBat
- [X] Plantillas de comandos fase 1, incluido tar y control de flujo, iconos nuevos para organigrama
- [ ] Plantillas de comandos (A3).
- [ ] Comandos modulares (JSON por familias).

### Tier 2

- [ ] Menú contextual completo (revisar cobertura en todos los paneles).
- [ ] Análisis contextual del parser (REM, ECHO, comillas sin cerrar).
- [ ] Edición visual de parámetros con valor (`/A:D`, `/O:N`, `/FS:NTFS`)
      mediante ComboBox homogéneo con constantes.
- [ ] Drag & drop de tokens dentro del constructor con feedback visual
      (línea de inserción, cursor, tooltip "Ctrl para sustituir").
- [ ] Avisos "¿Sabías que...?" en modo aprendiz.
- [ ] Tutorial de primer arranque.
- [ ] Ampliar `MostrarInfoTokenInfo` con Categoria y Ejemplo (ya
      implementado parcialmente; revisar).
- [ ] Mejora de "Insertar comando" desde el constructor (opciones A/D
      discutidas: popup contextual o Command Palette estilo VS Code).

### Tier 3

- [ ] Firma criptográfica del `.vcmd` (RSA-SHA256, lista de confianza).
- [ ] Inserciones inteligentes de dummies (por tipo, no solo por orden).
- [ ] Edición de comodines con auto-corchetes.
- [ ] Edición de constantes con ComboBox de valores.
- [ ] Pestaña de Utilidades con ConPTY (diskpart, vi, etc.).
- [ ] Sistema de plugins (JSON + carpeta `Plugins/`).
- [ ] Simulación con estado (predice efectos entre líneas).
- [ ] Módulo traductor de comandos para `.sh`.
- [ ] Port a Linux.
- [ ] Colaboración / compartir.

### Refactorización pendiente

- [ ] Dividir `comandos.json` (2000+ líneas) en archivos por familia.
- [ ] Dividir `MainViewModel.cs` en partials por área funcional
      (sugerencia: `Ejecucion`, `Lineas`, `Tokens`, `Sesiones`,
      `Catalogo`, `UndoRedo`, `Exportacion`).
- [ ] Dividir `MainWindow.xaml` en `UserControl`s por panel.
- [ ] Dividir `MainWindow.xaml.cs` en partials por área.
- [ ] Extraer estilos a `ResourceDictionary`.

Razón de esperar: no dividir código que aún va a cambiar mucho.
**Momento sugerido**: al cerrar Tier 2.

---

## Decisiones de diseño consolidadas

### Editor

- **Enfoque E3 — grading por espacios**: escribir libre, parseo al
  espacio/Enter, tokens de texto pueden crecer.
- **Solo un campo activo a la vez** (visual o textual).
- **Solo la línea activa es editable** (modelo Excel).
- **Edición in-place**: doble clic sobre Texto o Ruta. Texto actual
  seleccionado. Enter confirma, Escape descarta, blur confirma.
- Vacío (0 chars) → se elimina. `""` (dos comillas) → se conserva.
- **Portapapeles**: doble (interno con metadatos + sistema texto plano).
  Pegar del sistema parsea como si el usuario hubiera escrito.

### Edición con ComboBox (constantes, variables, comodines)

- Un solo editor (`ComboBox IsEditable="True"`) para todos.
- **Sin binding `Text`**: el texto se gestiona manualmente desde los
  handlers (`IsVisibleChanged`, `SelectionChanged`, `PreviewKeyDown`,
  `DropDownClosed`).
- Razón: el binding `TwoWay` con `PropertyChanged` interfería con la
  edición y provocaba que Escape no restaurara.
- `[abc]` y `[a-z]` editables como texto; `*` y `?` seleccionables.
- Snapshot de la línea para restaurar en Escape. `OlvidarSnapshotTexto()`
  al confirmar.
- **Filosofía**: el parser clasifica, no corrige. Si el texto editado
  cambia de formato, el parser lo reclasifica.

### Undo/Redo

- Cada línea tiene su propio historial (`List<string>` + índice, límite 100).
- Se confirma después de cada acción: Enter, blur, cambio de línea,
  `＋ Nueva`, `✓ Cerrar`, ejecutar, drag & drop, checkbox, eliminar token.
- NO se confirma al pulsar espacio solo.
- `Ctrl+Z` deshace, `Ctrl+Y` / `Ctrl+Shift+Z` rehace.
- Escape descarta cambios NO confirmados.

### Contexto de ejecución

- El prompt verde muestra siempre `DirectorioTrabajo`.
- Se colorea: normal verde, sistema amarillo, raíz rojo.
- Al exportar `.bat`/`.cmd`: cabecera con `cd /D "ruta"`.
- Al exportar `.ps1`: `Set-Location -Path "ruta"`.
- Al exportar `.sh`: `cd` comentado (rutas de Windows no funcionan).
- El diálogo de confirmación muestra el directorio en grande.

### Dummies guía (Fase 3 completa)

- Se generan al insertar un comando con `argumentos` declarados en JSON.
- NO están en el texto: son solo visuales.
- Se recalculan automáticamente tras cada cambio.
- Requeridos: `[origen]` (corchetes), opacidad 0.75.
- Opcionales: `(mensaje)` (paréntesis), opacidad 0.5.
- Al cerrar con requeridos pendientes: diálogo de confirmación.
- Al cerrar con opcionales pendientes: se descartan.
- Modo silencioso: no pregunta al cerrar.

### Aviso pre-ejecución

- Antes de ejecutar (plan o línea), si hay dummies requeridos pendientes
  o comandos peligrosos, se muestra un diálogo único con dos secciones.
- **Nunca se silencia**, ni en modo silencioso.

### Modo silencioso

- Menú Herramientas → Modo silencioso.
- Persiste en `%APPDATA%\VisualCmdStudio\config.json`.
- Silencia avisos de construcción/edición.
- NO silencia avisos antes de ejecutar.
- Indicador "🔇 Silencioso" en barra de estado.
- Deshabilitado en modo aprendiz.

### Modo aprendiz vs experto

- Enum `ModoTrabajo` (`Aprendiz` / `Experto`) en `Configuracion.cs`.
- **Aprendiz** (por defecto): modo silencioso no disponible, avisos
  siempre, mensajes descriptivos con instrucciones.
- **Experto**: modo silencioso disponible, mensajes concisos.
- Sincronización completa: panel izquierdo ↔ `DialogoOpciones` ↔ menú
  Herramientas.
- Persistente entre sesiones.

### Elevación UAC

- Comandos con `requiereAdmin: true` piden UAC al ejecutar.
- Salida capturada a archivos temporales en
  `%LOCALAPPDATA%\VisualCmdStudio\temp\` (GUID único).
- Limitación: la salida no es en vivo. Futuro: ConPTY.

### Sistema de firma (Tier 3, preparado)

- Bloque `firma` nullable en `.vcmd` ya en el modelo.
- RSA-SHA256 del contenido completo.
- Lista de verificadores de confianza en
  `%APPDATA%\VisualCmdStudio\verificadores.json`.
- Estados: Sin verificar / Verificado por X / Verificador desconocido /
  Firma inválida.

### Ficheros y formatos

- **`.bat` y `.cmd` son funcionalmente idénticos** en Windows moderno.
  Reutilizamos `ExportadorBat` con un parámetro de extensión.
- **`.ps1`**: advertencia sobre política de ejecución.
- **`.sh`**: "para revisión", comandos de Windows sin traducir, LF y
  UTF-8 sin BOM.
- **`.vcmd`**: JSON legible, texto por línea + directorio + metadatos.

### Reparto de responsabilidades (Vista)

- **Panel izquierdo**: modo, buscador, catálogo, modificadores, árbol.
- **Panel derecho**: comando activo, detalle del modificador/token,
  insertar tokens, ayuda contextual.
- **Panel inferior**: comentarios, errores, salida, historial.
- **Panel central**: constructor visual, línea CMD.

### Parser

- Divide por espacios respetando comillas dobles.
- Comilla sin cerrar → todo el resto es un único token.
- Comillas se preservan en el token (CMD las necesita).
- Espacios colapsados, extremos recortados.
- NO reordena argumentos ni corrige sintaxis.

---

## Visión futura

### Constructor con todas las líneas (Tier 3)

Que el constructor visual muestre **todas las líneas** del plan,
apiladas verticalmente, igual que el cuerpo CMD. Cada línea con sus
tokens. Colores alternos entre líneas lógicas; línea activa resaltada.
Dummies visibles en todas las líneas. Menús contextuales unificados
entre constructor y CMD.

**Cuándo**: al refactorizar la interfaz en `UserControl`s (Tier 3).

### Pestaña de Utilidades (ConPTY)

- Plugin = archivo JSON en carpeta `Plugins/`.
- Motor ConPTY para utilidades interactivas (editores, REPLs).
- Ejemplos: análisis de redes, comparación de ficheros, integridad,
  gestión de licencias, diskpart script, bcdboot, vi/vim.

### Port a Linux (futuro lejano)

- El motor de tokens/texto es agnóstico de plataforma.
- Catálogo distinto (bash/zsh), rutas (/, ~), variables ($VAR).
- Muchas features reutilizables (simulación, undo/redo, multi-línea).

### Módulo traductor de comandos (Tier 3/4)

-Renombrar comandos.json a comandoscmd.json, crear comandosps.json y comandossh.json.
- `.bat` → `.sh` con traducción automática de comandos.
- Evaluar viabilidad técnica y coste.
- No todos los comandos tienen equivalente directo.

### Visión v2.0: Editor multi-entorno

**Estado actual (v1.0)**: solo CMD.

**Visión (v2.0)**: CMD + PowerShell + Bash como "perfiles de entorno".

- Cada `.vcmd` declara su entorno (campo `entorno`).
- El entorno es inmutable dentro de un plan.
- La importación infiere el entorno del formato (.bat→CMD, .ps1→PS, .sh→Bash).
- Sin traducción automática entre entornos.
- Catálogos separados por entorno.
- Apariencia diferenciada (color, icono).
- Selector visible en la UI.

**Fases para v2.0** (28-42h):

- R.1-R.5: infraestructura.
- R.6: catálogo PowerShell.
- R.7: catálogo Bash.
- R.8: parser PowerShell.
- R.9: parser Bash.
- R.10: import .ps1/.sh.
- R.11-R.12: ejecución.

**Preparación en v1.0**:

- Campo `entorno` en `.vcmd` (ya implementado).
- Avisos claros al exportar/importar `.ps1`/`.sh`.
- Documentación de limitaciones.

### Web del proyecto (preparada, no publicada)

**Ubicación prevista**: `drthands.com` (temporalmente; futuro dominio
propio).

**Estado**: primera versión preparada, pendiente de:

- Eliminar telemetría de Google (auto-hospedar fuentes, Tailwind, imágenes).
- Actualizar datos ficticios por placeholders honestos.
- Añadir `<link rel="icon">` para favicon.
- Añadir meta tags SEO (`description`, `og:image`, `twitter:card`).
- Añadir página de privacidad / términos.
- Compilar Tailwind localmente (requiere Node.js).
- Reemplazar imágenes de `lh3.googleusercontent.com` por locales.

**Nota**: la web declara "Zero Telemetry" pero carga CDNs de Google.
Incoherencia a resolver antes de publicar.

### Compromiso mutuo

Como asistentes, no ayudamos a usuarios a realizar actividades
maliciosas. Del mismo modo, diseñamos herramientas que empoderan
al usuario sin exponerlo a riesgos innecesarios.






Nuevo ticket abierto: "Bug: prompts no reevaluados tras cambios estructurales del plan". Con el diagnóstico y la lista de puntos donde llamar a RecalcularDirectorioTrabajo().

Nuevo pendiente: "Jueves de comandos" con la lista de temas (operadores 2>/@/2>&1, tar, control de flujo, comandos personalizados, refactor de operadores hardcodeados en ComandoParser).

Refactor menor pendiente: unificar CalcularDirectorioSiguiente entre LineaComando e ImportadorBat.

Nota cosmética: @ con espacio intermedio en exportación (no bloqueante).

Nuevo pendiente: "Revisión de JSON: operadores, @, 2>, 2>&1, :: como alias de rem, refactor del bloque hardcodeado de operadores en ComandoParser, reordenación ergonómica del catálogo".

Corregir el bug del CargarSesion en el histórico de notas (por si vuelve a aparecer).

Nuevo ticket abierto: "Problema ergonomico: en fondos de token oscuros, el texto oscuro es difícil de leer, se aconseja establecer un control de luminancia para cambiar el texto automaticamente y priorizar tokens de color brillante".
