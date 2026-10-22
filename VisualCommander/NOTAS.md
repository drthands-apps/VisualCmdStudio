# Visual CMD Studio — Notas de diseño y planificación

Documento vivo. Se resume periódicamente. El histórico completo está
en los commits de Git; aquí solo se mantiene lo vigente.

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

## Estado actual (2026-10-21)

### Completado

**Editor visual:**
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

**Apariencia CMD:**
- Prompt dinámico coloreado (verde/amarillo/rojo).
- Prompt acortado con tooltip.
- Wrapping estilo CMD real.

**Ejecución:**
- Ejecución de línea individual y del plan completo (F5).
- Estados por línea (○ ▶ ✓ ✗ ⊘) + toggle "Detener en error".
- Buffer de salida (RangeObservableCollection).
- Escape/Detener durante la ejecución.
- Elevación UAC puntual para comandos `requiereAdmin`.
- Indicador 🛡 en bloque y en barra de estado.

**Seguridad:**
- Detección de comandos peligrosos (`peligroso: true`).
- Simuladores específicos: del, rd/rmdir, taskkill.
- Diálogo de confirmación con directorio y afectados.
- Opción "No volver a preguntar para esta línea en esta sesión".

**Sesiones y exportación:**
- Guardar/Cargar sesión `.vcmd` (JSON legible, campo `firma` nullable
  preparado para Tier 3).
- Metadatos: creador, verificado (aún no firma criptográfica).
- Exportar a `.bat`, `.cmd`, `.ps1` (completo), `.sh` (para revisión).
- Diálogos con opciones y vista previa.
- Atajos Ctrl+N/O/S/Shift+S.

**Interfaz:**
- Menú completo y funcional (Archivo, Editar, Ver, Herramientas, Ayuda).
- Placeholders informativos para funciones no implementadas.
- Panel izquierdo: modo (radio buttons placeholder), buscador, catálogo,
  modificadores, árbol de archivos.
- Panel derecho: comando activo, detalle del modificador (hover),
  detalle del token (hover), insertar tokens (texto/operadores/variables/
  constantes/comodines).
- Panel inferior: comentarios, errores, salida, historial.
- Panel derecho responde al comando activo y al hover de modificadores.
- Botón "Insertar texto" con campo previo y Enter para confirmar.
- Explicaciones ampliadas para valores de /A, /O y otros.
- Árbol de archivos con unidades reales (carga perezosa).
- Drag & drop desde catálogo, panel de modificadores y árbol.


## Tier 2 — Avances 2026-10-21

### Completado
- [x] Organizador de comandos (Por familia / A-Z / Frecuencia con
      persistencia en uso.json y detección de comandos por texto).
- [x] Hover sobre tokens del constructor → info en panel derecho.
- [x] Ocultar paneles libera espacio (GridLength 0, MinWidth 0).
- [x] Menú Ver reorganizado: se elimina "Panel central" (no útil),
      se añaden "Constructor visual" y "Línea de comandos".
- [x] Drag & drop de variables/operadores/comodines desde panel derecho
      al constructor, con posición exacta.
- [x] Clic simple para insertar al final (además de drag).
- [x] Flag _dragEnCurso para evitar el doble DoDragDrop.

### Pendiente Tier 2
- [ ] Menú contextual (clic derecho sobre línea).
- [ ] Undo/Redo global (operaciones sobre líneas: crear, mover, borrar).
- [ ] Auto-scroll al editor al añadir líneas.
- [ ] Botón ✕ por línea cerrada.
- [ ] Modo aprendiz vs experto.
- [ ] Historial con contexto.
- [ ] Análisis contextual del parser (REM, ECHO, comillas sin cerrar).
- [ ] Indicador visual permanente del modo silencioso.
- [ ] Edición visual de parámetros con valor (`/A:D`, `/O:N`, `/FS:NTFS`)
      mediante ComboBox homogéneo con constantes.
- [ ] Modos de trabajo: conectar los radio buttons del panel izquierdo.
- [ ] Drag & drop de tokens dentro del constructor (reordenar).
      mediante ComboBox homogéneo con la edición de constantes.


### Pendiente (Tier 1 postpuesto)

- [ ] Import desde `.bat`, `.ps1`, `.sh`.
- [ ] Plantillas de comandos (A3).
- [ ] Comandos modulares (JSON por familias).

### Pendiente (Tier 3)

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
- [ ] Dividir `MainViewModel.cs` en partials por área funcional.
- [ ] Dividir `MainWindow.xaml` en `UserControl`s por panel.
- [ ] Extraer estilos a `ResourceDictionary`.

Razón de esperar: no dividir código que aún va a cambiar mucho.

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

### Undo/Redo

- Cada línea tiene su propio historial (List<string> + índice, límite 100).
- Se confirma después de cada acción: Enter, blur, cambio de línea,
  `＋ Nueva`, `✓ Cerrar`, ejecutar, drag & drop, checkbox, eliminar token.
- NO se confirma al pulsar espacio solo.
- Ctrl+Z deshace, Ctrl+Y / Ctrl+Shift+Z rehace.
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
- `.bat` → `.sh` con traducción automática de comandos.
- Evaluar viabilidad técnica y coste.
- No todos los comandos tienen equivalente directo.

### Compromiso mutuo
Como asistentes, no ayudamos a usuarios a realizar actividades
maliciosas. Del mismo modo, diseñamos herramientas que empoderan
al usuario sin exponerlo a riesgos innecesarios.

### Tokens desde catálogo JSON (2026-10-22)

**Completado**:
- `tokens.json` con constantes (9), variables (17), comodines (4) y
  operadores (12). Metadatos: nombre, descripción, ejemplo, categoría.
- `TokenInfo` y `CatalogoTokens` como modelos.
- `CatalogoTokensService` carga desde JSON.
- `MainViewModel.CatalogoTokens` disponible para la UI.
- Panel derecho bindeado al catálogo: variables, constantes, comodines.
- Hover sobre cada elemento → descripción completa en panel derecho.
- Doble clic → insertar al final (coherente con catálogo de comandos).
- Clic simple → seleccionar (visual).
- Drag → insertar en posición.

**Patrón de interacción (consistente)**:
- Clic simple: selecciona.
- Doble clic: inserta al final.
- Drag: inserta en posición.
- Hover: info en panel derecho.

**Pendiente**:
- [ ] Refactorizar `ComandoParser` para reconocer constantes/variables
      desde el JSON en lugar de listas hardcodeadas.
- [ ] Edición de constantes con ComboBox de valores (A1).
- [ ] Edición de comodines con auto-corchetes (A2).
- [ ] Edición de parámetros con valor `/A:D` (A3).

## Migración completa al catálogo JSON (2026-10-22)

### Estado
Todos los tokens del panel derecho están bindeados al catálogo JSON:

| Categoría | Cantidad | Archivo |
|---|---|---|
| Constantes | 9 | `tokens.json` |
| Variables | 17 | `tokens.json` |
| Comodines | 4 | `tokens.json` |
| Operadores | 12 | `tokens.json` |

**Total**: 42 tokens editables desde JSON sin recompilar.

### Infraestructura
- `Models/TokenInfo.cs` y `CatalogoTokens`.
- `Services/CatalogoTokensService.cs` carga desde `Data/tokens.json`.
- `MainViewModel.CatalogoTokens` disponible para la UI.

### Patrón de interacción (unificado)
- **Clic simple**: selecciona (visual).
- **Doble clic**: inserta al final de la línea.
- **Drag**: inserta en la posición de drop.
- **Hover**: muestra la descripción en el panel derecho.

### Presentación
- Operadores: `UniformGrid` con 4 columnas (aspecto de botones).
- Variables/Constantes: lista vertical.
- Comodines: lista con nombre + descripción corta.

### Ventajas del catálogo
- Añadir un token: editar JSON.
- Cambiar una descripción: editar JSON.
- Traducir a otros idiomas: editar JSON (futuro).
- Sin recompilar.
