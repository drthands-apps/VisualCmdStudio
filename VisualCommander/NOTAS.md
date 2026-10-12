# Visual Commander — Notas de diseño y planificación

Documento vivo. Se resume periódicamente. El histórico completo está
en los commits de Git; aquí solo se mantiene lo vigente.

---

## Filosofía del proyecto

Visual Commander es un **editor** de comandos de Windows, no una consola.
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
6. **Modo simulación para comandos destructivos**: diferencial frente a CMD.
7. **Apariencia CMD con marca de simulación**: familiaridad + seguridad.

---

## Estado actual

### Completado
- Editor visual con catálogo de 25 comandos en JSON, agrupado por familia.
- Multi-línea con contexto de directorio independiente por línea.
- Editor in-place con foco y cursor correctos.
- Regla de "línea viva única" (sin vacías intermedias).
- Copiar / Cortar / Pegar / Duplicar / Mover líneas.
- Atajos: Ctrl+C/X/V/D, Ctrl+Shift+↑/↓, Ctrl+Enter.
- Apariencia CMD simulada con prompt dinámico coloreado (verde/amarillo/rojo).
- Ejecución real con captura de stdout/stderr en vivo.
- Ciclo de seguridad: detección + simulación + confirmación modal.
- Árbol de archivos con unidades reales (carga perezosa).
- Drag & drop desde catálogo, panel de modificadores y árbol.
- **R.1 completado**: Texto como fuente de verdad, Tokens como proyección.

### En curso
- **R.2.B**: Edición del campo CMD en vivo, con grading por espacios.

---

## Roadmap

### Refactor R — Editor bidireccional
- [x] R.1: Texto como fuente de verdad
- [ ] **R.2.B**: Edición del campo CMD con estado propio y push al modelo
- [ ] R.3: Drag & drop inserta texto en la posición del cursor
- [ ] R.4: Edición in-place de un token (doble clic)
- [ ] R.5: Insertar entre tokens con hueco visual

### Tier 1 — Funcionalidad núcleo
- [ ] Ejecución del plan completo (todas las líneas en secuencia)
- [ ] `requiereAdmin` + elevación puntual con UAC + indicador visual
- [ ] Guardar/cargar sesión completa (.vcmd en JSON)
- [ ] Plantillas de comandos frecuentes
- [ ] Exportar a .bat / .ps1 / .sh (con cabecera `cd /D`)

### Tier 2 — Ergonomía
- [ ] Undo/Redo a nivel de modelo (Stack<IUndoableAction>)
- [ ] Menú contextual (clic derecho sobre línea)
- [ ] Auto-scroll al editor al añadir líneas
- [ ] Botón ✕ por línea cerrada
- [ ] Indicador de estado por línea (pendiente/ejecutada/error)
- [ ] Modo aprendiz vs experto
- [ ] Historial con contexto

### Tier 3 — Visión futura
- [ ] Pestaña de Utilidades con ConPTY (diskpart interactivo, vi, etc.)
- [ ] Sistema de plugins (JSON + carpeta `Plugins/`)
- [ ] Colaboración / compartir

---

## Decisiones de diseño específicas

### Editor (reglas vigentes)

**Enfoque E3 — grading por espacios:**
- El usuario escribe libremente en el campo CMD.
- Cada espacio (o Enter) dispara el parseo.
- Un token de texto puede "crecer" mientras se escribe (C → Co → Copy).
- **Tokens de texto consecutivos se funden en uno solo**, con un espacio
  entre sus textos. Evita multiplicación de tokens vacíos.
- Espacios consecutivos se colapsan a uno.
- Solo un campo activo a la vez (visual o textual).
- Solo la línea activa es editable (modelo Excel).

**Portapapeles:**
- Doble portapapeles: interno (con metadatos) + sistema (texto plano).
- Pegar del sistema parsea el texto como si el usuario lo hubiera escrito.
- Cortar la única línea la vacía en lugar de eliminarla.
- `Ctrl+C/X/V/D` funcionan si el foco NO está en un TextBox.

**Ctrl+Z:**
- Actualmente solo revierte texto escrito a mano.
- Los cambios por binding (drag, doble clic, checkbox) no son reversibles.
- Se resolverá con Undo/Redo a nivel de modelo (Tier 2).

### Contexto de ejecución

Tres contextos distintos:
1. **Composición**: dónde navega el usuario mientras construye.
2. **Ejecución inmediata**: `WorkingDirectory = DirectorioTrabajo`.
3. **Ejecución diferida**: el `.bat` exportado antepone `C:` + `cd /D`.

Reglas:
- El prompt verde del CMD simulado muestra siempre `DirectorioTrabajo`.
- Se colorea según peligrosidad: normal verde, sistema amarillo, raíz rojo.
- Al exportar .bat: cabecera con `cd /D "ruta"` siempre.
- El diálogo de confirmación muestra el directorio en grande.

### Confirmación de comandos peligrosos

- Detección: campo `peligroso: true` en el JSON.
- Simuladores específicos: `del`, `rd`/`rmdir`, `taskkill`.
- Simulador genérico para el resto.
- Diálogo modal con: directorio destacado, comando, efectos, lista de
  afectados, botones "Ejecutar de todas formas" / "Cancelar".
- Opción "No volver a preguntar para esta línea en esta sesión".

### Organización del código

**Minimalismo y especialización.** Cada archivo = una responsabilidad.
Preferible 8 archivos de 100 líneas que 2 de 400.

Pendiente de aplicar (tras R.2–R.5):
- División de `MainViewModel.cs` en partials por área funcional.
- División de `MainWindow.xaml` en `UserControl`s por panel.
- Estilos a `ResourceDictionary`.

Razón de esperar: no dividir código que aún va a cambiar mucho.

---

## Visión: Pestaña de Utilidades (ConPTY)

Espacio para utilidades avanzadas que aprovechan la misma infraestructura.

- Plugin = archivo JSON en carpeta `Plugins/`.
- Motor ConPTY para utilidades interactivas (editores, REPLs).
- Ejemplos: análisis de redes, comparación de ficheros, integridad del
  sistema, gestión de licencias, diskpart script, bcdboot, vi/vim.

Se hará después de Tier 1. Es **aditiva**, no invasiva.

### Análisis contextual (pendiente)

El parser divide por palabras. NO interpreta contexto.
Un validador posterior (Tier 2 o 3) analizará:
- `dir` tras un `Texto` → probablemente un error del usuario.
- `dir` tras `rem` → es texto, no comando.
- `dir` dentro de `"` sin cerrar → es texto.
- Cadenas con comillas no balanceadas → todo lo que sigue es texto.

Separación de responsabilidades:
1. Parser (actual): divide por espacios, respeta comillas, clasifica
   heurísticamente.
2. Analizador contextual (futuro): verifica que los tipos encajan
   según la gramática de CMD.
3. Validador semántico (futuro): comprueba que los valores son
   válidos (por ejemplo, `/S` solo existe en ciertos comandos).
   ## Undo / Redo (implementado 2026-10-08)

### Modelo
- Cada LineaComando tiene su propio historial de estados confirmados.
- Se usa List<string> + índice (para rehacer correctamente).
- Límite de 100 estados por línea.
- Al deshacer y luego hacer una acción nueva, se corta la cola de rehacer.

### Cuándo se confirma un estado
- Enter en el editor
- Perder foco
- Cambiar de línea activa
- `＋ Nueva`, `✓ Cerrar`
- Ejecutar
- Drag & drop
- Marcar/desmarcar checkbox
- Eliminar token ✕

### Cuándo NO se confirma
- Pulsar espacio (solo previsualiza el parseo)

### Atajos
- Ctrl+Z: deshacer
- Ctrl+Y o Ctrl+Shift+Z: rehacer
- Escape: descarta cambios NO confirmados (no toca el historial)
- Los atajos se interceptan incluso dentro del TextBox (no dejamos
  que el undo nativo del TextBox actúe).

  ### Refactor R — Editor bidireccional
- [x] R.1: Texto como fuente de verdad
- [x] R.2.B: Edición del campo CMD con estado propio y push al modelo
- [x] **R.3**: Drag & drop inserta texto en la posición del cursor
- [ ] R.4: Edición in-place de un token (doble clic)
- [ ] R.5: Insertar entre tokens con hueco visual (animado)

### Detalles de R.3
- Comandos y rutas se insertan según la posición del cursor.
- Parámetros siguen marcando checkbox y se añaden al final.
- Feedback visual: tinte azul del panel durante el drag.
- Pendiente R.5: hueco animado tipo Notion en lugar del tinte.

### Pendiente
- [ ] Undo/Redo global (operaciones sobre líneas: crear, mover, borrar)
- [ ] Mostrar "N cambios disponibles" en la barra de estado
- [ ] Botones de deshacer/rehacer en la toolbar


## Visión: port a Linux (futuro lejano)

Si Visual Commander llega a ser funcional y estable en Windows, plantear
un port a Linux es un reto interesante. Consideraciones:
- El motor de tokens/texto es agnóstico de plataforma.
- El catálogo sería otro JSON con comandos de bash/zsh.
- La apariencia CMD se adaptaría a terminal (colores, prompt).
- Muchas features (simulación, undo/redo, multi-línea) son reutilizables.
- Cambios necesarios: ejecución de procesos, rutas (/, ~), variables ($VAR).
- No urgente. Requiere que Windows esté consolidado primero.

## Programas auxiliares históricos a incluir en el catálogo

Recopilación de comandos clásicos de Windows que conviene añadir:
- `format` (formatear unidades) — comando clásico y peligroso
- `chkdsk` (verificar disco)
- `sfc` (verificador de archivos de sistema)
- `diskpart` (particionado avanzado)
- `bcdboot`, `bcdedit` (arranque)
- `reg` (registro de Windows)
- `wmic` (instrumentación)
- `takeown`, `icacls` (permisos)
- `attrib` (atributos de archivo)
- `cipher` (cifrado)
- `compact` (compresión)
- `label`, `vol`, `chkntfs`
- `net user`, `net share`, `net use` (red)
- `sc` (servicios)
- `shutdown`, `logoff`
- `driverquery`, `wmic`
- `schtasks` (tareas programadas)
- `assoc`, `ftype` (asociaciones de archivos)
- `mklink` (enlaces simbólicos)
- `robocopy` (ya lo tenemos)
- `xcopy` (ya lo tenemos)
- Y los clásicos: `tree`, `dir`, `copy`, `move`, `del` (ya los tenemos)

Todos se pueden declarar en `comandos.json` sin recompilar.
Los peligrosos llevan `peligroso: true` y simulador si es posible.

## Simulación con estado (idea futura)

El simulador actual evalúa cada línea peligrosa contra el estado ACTUAL
del sistema. Problema: si una línea crea algo y otra lo borra, la
simulación de la segunda no ve lo que va a existir tras ejecutar la
primera.

**Visión**: análisis previo más inteligente que tenga en cuenta los
cambios que la ejecución previa introduciría:
- Creación de archivos/carpetas → la simulación posterior los ve.
- Renombrado → los ve con el nuevo nombre.
- Eliminación → los ve ausentes.
- Cambios de directorio (cd) → ya lo hacemos por línea.

**Implementación**:
- Mantener un "sistema de archivos virtual" durante la simulación del plan.
- Cada línea, al simularse, modifica ese estado virtual.
- Las simulaciones consultan el virtual, no el real.
- Requiere modelar: crear, borrar, renombrar, mover, copiar, cd.

**Prioridad**: Tier 3. Antes hay otras cosas más urgentes.
**Filosofía**: mejor avisar antes de empezar que interrumpir durante.

## Decisión: NO avisar durante la ejecución

Una vez iniciada la ejecución del plan, NO se avisa de peligros.
Todos los avisos se hacen ANTES de empezar. Motivo: el usuario debe
poder evaluar el plan completo y decidir. Interrumpir a mitad es
molesto y rompe el flujo de "ejecutar un plan".

## Señales visuales de la línea activa (2026-10-09)

Finalmente:
- Borde izquierdo azul (visible)
- Fondo más claro (visible)
- Símbolo de estado de ejecución (○ ▶ ✓ ✗ ⊘) — indica el estado, no la actividad
- Círculo verde — descartado (redundante con el borde)
- Contador N/M — descartado (la posición es evidente por el orden)

## Estado del proyecto (2026-10-09)

### Completado esta sesión
- [x] Ejecución del plan completo (F5)
- [x] Estado por línea + indicadores visuales
- [x] Toggle "Detener en error"
- [x] Diálogo único de peligros para el plan
- [x] Buffer de salida con AddRange (RangeObservableCollection)
- [x] Autoscroll coalescido
- [x] Escape detiene la ejecución
- [x] Fix sincronización TextBox↔modelo tras clic en campo CMD
- [x] Prompt acortado con tooltip
- [x] Grid de dos columnas + TextWrapping para comandos largos
- [x] Renombrado a Visual CMD Studio
- [x] Catálogo ampliado (format, diskpart, sfc, dism, reg, sc,
      schtasks, shutdown, takeown, icacls, mklink, cipher, net...)
- [x] Limpieza: CirculoActivo y ContadorLinea descartados

### Pendiente próxima sesión
- [ ] Reducir llamadas a EnfocarEditorActivo a las estrictamente necesarias
- [ ] Menú contextual sobre las líneas (D.3.G)
- [ ] Exportación a .bat con cabecera `cd /D`
- [ ] Plantillas de comandos frecuentes
- [ ] Ampliar catálogo si quedan comandos por añadir

### Ideas anotadas para el futuro
- Simulación con estado (predice efectos entre líneas)
- Pestaña de Utilidades con ConPTY
- Port a Linux (lejano)
- Indicador visual de modo admin
- Atajo Escape como detener universal

### Señales visuales de la línea activa (decisión final)
- Borde izquierdo azul + fondo claro → marca "activa"
- Símbolo de estado (○ ▶ ✓ ✗ ⊘) → marca el estado de ejecución
- Sin círculo verde ni contador N/M (redundantes)

### ConfirmarEstado también en Space (2026-10-12)

Motivo: si el usuario escribe `dir` + espacio, luego hace clic en otra
línea sin pulsar Enter, el estado no se confirmaba. Ahora cada palabra
es un punto de undo. Coherente con editores de código.

La guarda interna de ConfirmarEstado evita duplicados cuando se pulsan
varios espacios seguidos.