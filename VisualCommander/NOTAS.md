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
6. **Modo simulación para comandos destructivos**: diferencial frente a CMD.
7. **Apariencia CMD con marca de simulación**: familiaridad + seguridad.
8. **Validación permisiva**: solo validar lo que es seguro. En caso de
   duda, aceptar. CMD no tiene un parser único.

---

## Estado actual (2026-10-14)

### Completado

**Editor visual:**
- Catálogo JSON con ~55 comandos, agrupados por familia.
- Multi-línea con contexto de directorio independiente.
- Editor in-place con foco y cursor correctos.
- Token de texto editable (doble clic, forma achaflanada + lápiz).
- Regla de "línea viva única".
- Copiar / Cortar / Pegar / Duplicar / Mover líneas.
- Atajos: Ctrl+C/X/V/D, Ctrl+Shift+↑/↓, Ctrl+Enter, Ctrl+Z/Y/Shift+Z.
- Parser reforzado: comillas abren "modo sin dividir", espacios colapsados.
- Undo/Redo por línea con historial de estados.

**Apariencia CMD:**
- Prompt dinámico coloreado (verde/amarillo/rojo según peligrosidad).
- Prompt acortado con tooltip cuando es muy largo.
- Wrapping estilo CMD real (continuación alineada con el comando).

**Ejecución:**
- Ejecución de línea individual y del plan completo (F5).
- Estados por línea (○ ▶ ✓ ✗ ⊘) + toggle "Detener en error".
- Buffer de salida (RangeObservableCollection) para no saturar la UI.
- Escape detiene la ejecución en curso.

**Seguridad:**
- Detección de comandos peligrosos (`peligroso: true` en JSON).
- Simuladores específicos: del, rd/rmdir, taskkill.
- Simulador genérico para el resto.
- Diálogo de confirmación con directorio destacado y lista de afectados.
- Opción "No volver a preguntar para esta línea en esta sesión".

**Exportación:**
- Guardar como `.bat` con cabecera, codificación configurable, `pause`.
- Diálogo con vista previa y opciones.

**Otros:**
- Árbol de archivos con unidades reales (carga perezosa).
- Drag & drop desde catálogo, panel de modificadores y árbol.

### En curso

**Fase 3 — Sistema de dummies guía:**
- [x] 1. Extender Token con estado (Normal, Dummy, Invalido) y TextoDummy.
- [x] 2. Extender Comando con lista de ArgumentoComando.
- [x] 3. Añadir campo "argumentos" al JSON de 8 comandos iniciales.
- [ ] 4. Generación de dummies al insertar un comando.
- [ ] 5. Renderizado visual de dummies (borde punteado, parpadeo, tooltip).
- [ ] 6. Algoritmo de relleno (escribir en orden, arrastrar a posición).
- [ ] 7. Reglas al cerrar línea (obligatorios vs opcionales).
- [ ] 8. Modo silencioso global.

---

## Roadmap

### Tier 1 — Funcionalidad núcleo
- [ ] `requiereAdmin` + elevación puntual con UAC + indicador visual.
- [ ] Guardar/cargar sesión completa (.vcmd en JSON).
- [ ] Plantillas de comandos frecuentes.
- [ ] Exportar a .ps1 y .sh.
- [ ] Dummies guía (Fase 3, en curso).

### Tier 2 — Ergonomía
- [ ] Undo/Redo global (operaciones sobre líneas: crear, mover, borrar).
- [ ] Menú contextual (clic derecho sobre línea).
- [ ] Auto-scroll al editor al añadir líneas.
- [ ] Botón ✕ por línea cerrada.
- [ ] Modo aprendiz vs experto.
- [ ] Historial con contexto.
- [ ] Análisis contextual del parser (REM, ECHO, comillas sin cerrar...).

### Tier 3 — Visión futura
- [ ] Pestaña de Utilidades con ConPTY (diskpart interactivo, vi, etc.).
- [ ] Sistema de plugins (JSON + carpeta `Plugins/`).
- [ ] Simulación con estado (predice efectos entre líneas).
- [ ] Port a Linux.
- [ ] Colaboración / compartir.

### Refactorización pendiente
- [ ] Dividir `comandos.json` (2000+ líneas) en múltiples archivos por
      familia (Archivos.json, Sistema.json, Red.json...).
- [ ] Dividir `MainViewModel.cs` en partials por área funcional.
- [ ] Dividir `MainWindow.xaml` en `UserControl`s por panel.
- [ ] Extraer estilos a `ResourceDictionary`.

Razón de esperar: no dividir código que aún va a cambiar mucho.

---

## Decisiones de diseño específicas

### Editor

**Enfoque E3 — grading por espacios:**
- El usuario escribe libremente en el campo CMD.
- Cada espacio (o Enter) dispara el parseo.
- Un token de texto puede "crecer" mientras se escribe.
- Espacios consecutivos se colapsan a uno.
- Solo un campo activo a la vez (visual o textual).
- Solo la línea activa es editable (modelo Excel).

**Edición in-place de tokens:**
- Doble clic sobre token de tipo Texto o Ruta.
- El texto actual se SELECCIONA (reemplazar es escribir directamente).
- Enter confirma, Escape descarta, blur confirma.
- Vacío (0 chars) → se elimina. `""` (dos comillas) → se conserva.
- Forma visual diferenciada: esquina achaflanada + icono lápiz.

**Portapapeles:**
- Doble portapapeles: interno (con metadatos) + sistema (texto plano).
- Pegar del sistema parsea el texto como si lo hubiera escrito el usuario.
- Cortar la única línea la vacía en lugar de eliminarla.

**Undo/Redo:**
- Cada línea tiene su propio historial de estados confirmados.
- List<string> + índice, límite de 100 estados por línea.
- Se confirma después de cada acción: Enter, blur, cambiar línea,
  `＋ Nueva`, `✓ Cerrar`, ejecutar, drag & drop, checkbox, eliminar token.
- NO se confirma al pulsar espacio solo (excepto cuando ya hay texto
  pendiente y se pulsa espacio tras escribir una palabra).
- `Ctrl+Z` deshace, `Ctrl+Y`/`Ctrl+Shift+Z` rehace.
- `Escape` descarta cambios NO confirmados.

**Señales visuales de la línea activa:**
- Borde izquierdo azul + fondo claro → marca "activa".
- Símbolo de estado (○ ▶ ✓ ✗ ⊘) → marca el estado de ejecución.

### Contexto de ejecución

Tres contextos distintos:
1. **Composición**: dónde navega el usuario mientras construye.
2. **Ejecución inmediata**: `WorkingDirectory = DirectorioTrabajo`.
3. **Ejecución diferida**: el `.bat` exportado antepone `C:` + `cd /D`.

Reglas:
- El prompt verde muestra siempre `DirectorioTrabajo`.
- Se colorea: normal verde, sistema amarillo, raíz rojo.
- Al exportar `.bat`: cabecera con `cd /D "ruta"` siempre.
- El diálogo de confirmación muestra el directorio en grande.

### Confirmación de comandos peligrosos

- Detección: campo `peligroso: true` en el JSON.
- Simuladores específicos: del, rd/rmdir, taskkill.
- Simulador genérico para el resto.
- Diálogo modal con: directorio destacado, comando, efectos, lista de
  afectados, botones "Ejecutar de todas formas" / "Cancelar".
- Opción "No volver a preguntar para esta línea en esta sesión".
- **NO se avisa durante la ejecución del plan**: todos los avisos se
  hacen antes de empezar. Interrumpir a mitad rompe el flujo.

### Sistema de tipos de argumentos (base para Fase 3)

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

Validación permisiva:
- NO validar el orden de los argumentos.
- SOLO validar existencia del comando, pertenencia de modificadores,
  compatibilidad básica de tipos.
- En caso de duda, ACEPTAR.

### Parser

- Divide por espacios respetando comillas dobles.
- Mientras haya una comilla abierta, los espacios NO dividen.
- Comilla sin cerrar → todo el resto es un único token.
- Las comillas se preservan en el token (CMD las necesita).
- Espacios múltiples colapsados, extremos recortados.
- NO reordena argumentos ni corrige sintaxis.

---

## Visión: Pestaña de Utilidades (ConPTY)

Espacio para utilidades avanzadas que aprovechan la misma infraestructura.

- Plugin = archivo JSON en carpeta `Plugins/`.
- Motor ConPTY para utilidades interactivas (editores, REPLs).
- Ejemplos: análisis de redes, comparación de ficheros, integridad del
  sistema, gestión de licencias, diskpart script, bcdboot, vi/vim.

Se hará después de Tier 1. Es **aditiva**, no invasiva.

---

## Port a Linux (futuro lejano)

Si el proyecto llega a ser funcional y estable en Windows, plantear un
port a Linux es un reto interesante. Consideraciones:
- El motor de tokens/texto es agnóstico de plataforma.
- El catálogo sería otro JSON con comandos de bash/zsh.
- Muchas features (simulación, undo/redo, multi-línea) son reutilizables.
- Cambios necesarios: ejecución de procesos, rutas (/, ~), variables ($VAR).
- No urgente. Requiere que Windows esté consolidado primero.

### Pieza 4 completada (2026-10-15)

- `LineaComando.Dummies` como ObservableCollection paralela a Tokens.
- `RecalcularDummies()` se ejecuta tras cada `ReprojectarTokens`.
- Regla: se cuentan los tokens reales (Texto o Ruta) DESPUÉS del
  comando activo. Los que cubren argumentos no generan dummy.
- Los dummies NO están en `Tokens`, son solo UI.
- Solo se generan si el comando activo tiene `argumentos` declarados.

### Detalle observado
`echo` con argumento `mensaje` marcado como NO requerido aún genera
dummie. Se distinguirá en la Pieza 7.
### Patrón: try/finally para garantizar limpieza

Cuando un método modifica colecciones derivadas (`BloquesVisibles` a partir
de `Tokens` + `Dummies`) y tiene varios `return` tempranos:

- Poner TODO el cuerpo dentro del `try`.
- Poner la limpieza/reconstrucción en el `finally`.
- NO dejar validaciones con `return` fuera del `try`.

Error cometido: los `return` de las validaciones estaban fuera del `try`,
así que el `finally` no se ejecutaba cuando había un `return` temprano.
### Pieza 8-bis — Aviso pre-ejecución de líneas incompletas (2026-10-15)

**Concepto**: antes de ejecutar (plan o línea), comprobar si hay líneas
con dummies requeridos sin rellenar. Si las hay, mostrar aviso combinado
con los comandos peligrosos.

**Un solo diálogo**: ConfirmarPlanDialog incluye dos secciones:
- LÍNEAS PELIGROSAS (comandos destructivos).
- LÍNEAS INCOMPLETAS (dummies requeridos sin rellenar).

**El aviso NUNCA se silencia**, ni en modo silencioso. Es información
crítica antes de ejecutar.

**Las líneas incompletas NO se añaden a "no preguntar"** porque pueden
completarse en el futuro; se vuelven a comprobar cada vez.

**Aplica a**:
- Ejecutar plan completo (F5).
- Ejecutar línea individual.