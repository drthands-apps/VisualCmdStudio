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

## Hito: ciclo completo (2026-10-12)

Visual CMD Studio ya cierra el ciclo completo:

1. **Componer**: editor visual con bloques, drag & drop, tokens.
2. **Guardar**: exportación a .bat autocontenido con opciones.
3. **Ejecutar**: dentro de la app o haciendo doble clic en el .bat.

### Capacidades actuales
- Editor visual con catálogo JSON extensible.
- Multi-línea con contexto de directorio independiente.
- Undo/Redo por línea, copiar/cortar/pegar/duplicar/mover.
- Drag & drop de comandos, parámetros y rutas.
- Apariencia CMD simulada con prompt dinámico coloreado.
- Ejecución con captura de salida en vivo.
- Ciclo de seguridad completo: detección + simulación + confirmación.
- Ejecución del plan completo con estados por línea.
- **Exportación a .bat**: cabecera, codificación configurable, pause.
- Árbol de archivos con unidades reales.

### Pendiente (Tier 1)
- Menú contextual en las líneas.
- Plantillas de comandos frecuentes.
- Guardar/cargar sesión (.vcmd).
- Exportar a .ps1 y .sh.

### Pendiente (Tier 2)
- Undo/Redo global (operaciones sobre líneas).
- Simulación con estado (predice efectos entre líneas).
- Modo aprendiz vs experto.
- Historial con contexto.

### Pendiente (Tier 3)
- Pestaña de Utilidades con ConPTY.
- Port a Linux.
- Sistema de plugins.

## Pendiente prioritario (2026-10-13)

### 1. Token de texto editable in-situ

**Problema**: al hacer un `rename` (o cualquier comando que necesite un texto libre
como argumento), no hay forma de insertar un texto desde el editor visual. El usuario
tiene que escribirlo por línea de comandos, lo cual rompe el flujo.

**Solución**: un token de tipo **Texto Editable** que:
- Se inserta como un bloque más (arrastrable o por botón).
- Aparece con un placeholder tipo `<texto>` o `[escribe aquí]`.
- Doble clic sobre el token → se convierte en editable in-place.
- Al confirmar (Enter o blur), el texto se fija y pasa al modelo.
- El token se pinta con un color distintivo (por ejemplo, `EDEDED` gris claro
  para textos libres, o `FFF2CC` para el actual patrón).

**Casos de uso**:
- `ren archivo_viejo.txt archivo_nuevo.txt` → dos tokens de texto editable.
- `find "cadena a buscar" archivo.txt` → un texto editable (la cadena).
- `mkdir nombre_carpeta` → un texto editable (el nombre).
- `echo Hola mundo` → texto editable.

**Dónde insertarlo**:
- Botón nuevo en el panel derecho: "📝 Texto" o similar.
- Arrastrable desde el panel de operadores/variables a la línea.
- También desde un menú contextual "Insertar → Texto".

**Detalles de implementación**:
- El token de texto puede empezar con placeholder y, al perder el foco sin
  texto escrito, eliminarse automáticamente.
- Comillas automáticas si el texto contiene espacios (`"Hola mundo"`).
- Validación básica: no permitir caracteres que rompan la línea
  (`"`, `|`, `&`, `<`, `>` sin escapar) o advertir al usuario.

### 2. Tokens guía para requisitos del comando

**Idea**: al insertar un comando, el editor debe:
- Añadir automáticamente tokens **placeholder** para los argumentos obligatorios.
- Estos tokens aparecen **parpadeando** o con un borde punteado.
- El usuario los rellena con drag & drop (por ejemplo, arrastrando una ruta)
  o con texto libre.
- Cuando el requisito se satisface, el token deja de parpadear y se fija.

**Casos**:
- `dir` → no tiene argumentos obligatorios, pero podría sugerir un token de
  ruta opcional.
- `copy` → dos tokens obligatorios: origen y destino.
- `move` → dos tokens obligatorios: origen y destino.
- `ren` → dos tokens obligatorios: nombre actual y nombre nuevo.
- `xcopy` → dos tokens: origen y destino.
- `robocopy` → dos tokens: origen y destino.
- `find` → un token obligatorio: la cadena a buscar.

**Diseño visual del placeholder**:
- Borde punteado en color de acento (`#4A90D9`).
- Animación: parpadeo suave (opacidad 0.6 ↔ 1.0).
- Texto tipo "[origen]", "[destino]", "[cadena]", "[texto]".
- Al pasar el ratón, tooltip explicando qué espera.

**Diseño del flujo**:
1. Usuario arrastra `copy` al constructor.
2. Aparecen automáticamente: `copy` + `[origen]` + `[destino]`.
3. El usuario arrastra una carpeta del árbol a `[origen]` → se sustituye.
4. Hace lo mismo con `[destino]` → se sustituye.
5. La línea queda completa y ejecutable.

**Dónde obtener los requisitos**:
- Añadir al JSON de comandos un campo `"argumentosObligatorios"`:
  ```json
  {
    "nombre": "copy",
    "argumentosObligatorios": [
      { "nombre": "origen", "tipo": "Ruta", "descripcion": "Archivo a copiar" },
      { "nombre": "destino", "tipo": "Ruta", "descripcion": "Destino de la copia" }
    ]
  }
  ## Sistema de Tipos de Argumentos (2026-10-13)

### Objetivo
Discriminar el significado de cada texto según el comando activo.
Validar contra el estado real del sistema cuando sea posible.
Marcar visualmente los argumentos inválidos.

### Tipos de argumento

Declarados en el JSON de cada comando (`campo "argumentos"`):

| Tipo | Validación |
|---|---|
| `Ruta` | `Path.GetFullPath` no falla |
| `ArchivoExistente` | `File.Exists` |
| `DirectorioExistente` | `Directory.Exists` |
| `NombreNuevo` | `!File.Exists && !Directory.Exists` (opcional) |
| `CadenaLibre` | Sin restricción |
| `Modificador` | Está en la lista de parámetros del comando |
| `Variable` | Patrón `%...%` |

### Validación contextual
1. Mira qué tipo espera el comando en esa posición.
2. Aplica la regla del tipo.
3. Marca el token: verde / amarillo / rojo.

### Texto sin contexto válido
- Después de `REM` o `::` → comentario (gris).
- Después de `ECHO` → mensaje (verde claro).
- Comando no espera más argumentos → error rojo.
- Otro caso → advertencia amarilla.

### Comillas
- El texto se guarda SIN comillas.
- El generador de línea entrecomilla automáticamente si el texto contiene espacios.
- Excepción: comandos como `echo` que muestran las comillas literalmente.

### Fases de implementación
1. Añadir `argumentos` al JSON de los 10 comandos más usados.
2. Extender `ComandoParser` para consultar el tipo esperado.
3. Extender `Token` con `EstadoValidacion` (Ok / Advertencia / Error).
4. Aplicar estilos visuales según `EstadoValidacion`.
5. Manejar casos especiales: REM, ECHO, texto tras comando completo.
1. ## Sistema de Dummies Guía (2026-10-13)

### Concepto
Al insertar un comando, se añaden automáticamente **tokens dummy** que
representan los argumentos esperados, en orden, según la sintaxis del comando.
El usuario los "rellena" escribiendo en la línea CMD o arrastrando tokens.

### Características
- Los dummies son tokens REALES en el modelo, con estado `Dummy`.
- Tienen un texto descriptivo ("origen", "destino", "nombre nuevo").
- Se renderizan visualmente como placeholders: corchetes, borde punteado,
  parpadeo suave, color apagado.
- Al rellenarse, se transforman en tokens reales del tipo apropiado
  (Ruta, ArchivoExistente, CadenaLibre...).

### Relleno
- **Escribiendo**: al procesar la línea, los tokens reales reemplazan a los
  dummies EN ORDEN.
- **Arrastrando**: al soltar un token sobre un dummy, se rellena específico.

### Al cerrar la línea
- Los dummies rellenados desaparecen (ya son tokens reales).
- Los dummies vacíos **opcionales** se eliminan.
- Los dummies vacíos **requeridos** se mantienen marcando el hueco.
- Se avisa al usuario de los requeridos sin rellenar (excepto en modo silencioso).

### Modos
- **Normal**: avisa de errores y dummies pendientes.
- **Silencioso**: no avisa, el usuario asume la responsabilidad.

### Ampliación del catálogo
Añadir comandos de presentación y control:
- Presentación: `cls`, `color`, `title`, `pause`, `prompt`, `mode`.
- Control: `rem`, `::`, `echo`, `if`, `for`, `goto`, `call`, `exit`, `set`.
- Sistema: `cmd`, `start`.

Algunos (if, for) son complejos y se posponen a una fase posterior.

### Fases
1. Extender Token con estado `Dummy` y texto descriptivo.
2. Añadir campo `argumentos` al JSON de los comandos más usados.
3. Generar dummies al insertar un comando (solo si no está ya presente).
4. Algoritmo de relleno por orden (escribir) o por posición (arrastrar).
5. Reglas al cerrar línea (opcionales vs requeridos).
6. Modo silencioso global.
7. Ampliar catálogo con comandos de presentación y control.

## Validación permisiva (2026-10-13)

### Regla
CMD no tiene un parser único. Cada aplicación interpreta la línea a su manera.
La documentación da el orden canónico, pero CMD acepta múltiples órdenes.

### Consecuencia para el validador
- NO validar el orden de los argumentos.
- SOLO validar:
  - Existencia del comando en el catálogo.
  - Pertenencia de los modificadores a la lista del comando.
  - Compatibilidad básica de tipos.
- En caso de duda, ACEPTAR (mejor falso negativo que falso positivo).

### Dummies guía
- Son SUGERENCIAS, no imposiciones.
- Se generan al insertar el comando, en orden documentado.
- Si el usuario escribe en otro orden, no se corrige.
- Solo bloquean si falta un argumento realmente obligatorio.
- ### Comportamiento de edición in-place (decisión final)

Al entrar en edición in-place de un token:
- El texto actual se SELECCIONA (SelectAll).
- El caret NO es visible mientras hay selección activa.
- Es el comportamiento estándar de Windows (Notepad, Word, VSCode, etc.).

Motivo: al editar un token de texto, el caso más común es REEMPLAZARLO
completo. Con el texto seleccionado, escribir directamente lo reemplaza.
Para editar una parte, el usuario hace clic en la posición deseada y el
caret aparece.

Referencias: Excel, VS Code (F2 para renombrar), Notepad.