# Propuesta: Agente Residente (Fase 2)

## Intención

El MVP actual es efímero: aplica el área de trabajo y termina. Windows recalcula el área ante cambios de resolución, reinicio de Explorer o movimiento de barra de tareas, perdiendo la reserva. Un agente residente monitorea estos eventos en segundo plano y reaplica automáticamente.

## Alcance

### Incluido
- Modo CLI extendido: comandos `install` / `uninstall` / `health`
- Modo Daemon: flag `--daemon`, bombeo de mensajes Win32
- Evaluación de estado antes de reaplicar (SPI_GETWORKAREA vs deseado)
- Debounce configurable (`eventDebounceMs`, default 400ms)
- Registro Run key en inicio de Windows (`HKCU\...\Run`)
- Logging de eventos, cambios, reaplicaciones y errores con rotación y límite
- Logs en `%LOCALAPPDATA%\ScreenSafe\Logs\`
- Mitigación contra bucles de reaplicación
- Original WorkArea capturado una sola vez e inmutable hasta restore manual
- Circuit breaker: máx. 10 reaplicaciones en 60s → suspender 5 min y loguear error

### Excluido
- Interfaz gráfica o bandeja de sistema
- Soporte multi-monitor
- Servicio de Windows (aislamiento Session 0)
- Notificaciones al usuario

## Capacidades

### Nuevas
- `resident-agent`: modo daemon, monitoreo Win32, auto-reaplicación
- `auto-start`: registro Run key (install/uninstall/update)

### Modificadas
- `cli-interface`: nuevos comandos `install`/`uninstall`/`health` + flag `--daemon`
- `config-persistence`: nuevo campo `eventDebounceMs` (default 400ms)
- `work-area-management`: evaluación de estado antes de reaplicar
- `logging`: rotación por tamaño (1 MB), retención de 3 archivos

## Enfoque

- **Mismo .exe**, sin nuevos proyectos. Reusa DI, config y estrategias MVP.
- **CLI mode**: comportamiento actual + `install`/`uninstall`. Efímero, sin message pump.
- **Daemon mode**: flag `--daemon` → hilo dedicado con ventana oculta (`CreateWindowExW`) y bombeo manual `GetMessage`/`DispatchMessage`. Message pump SOLO existe aquí.
- **WorkAreaWatcher**: filtra `WM_SETTINGCHANGE(SPI_SETWORKAREA)`, `WM_DISPLAYCHANGE`, `TaskbarCreated`.
- **EventDebouncer**: Timer con intervalo `eventDebounceMs`. Al disparar: lee `SPI_GETWORKAREA`, compara con deseado, reaplica **solo si diferente**.
- **StartupManager**: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` con `"C:\...\ScreenSafe.Console.exe --daemon"`. `install` sobreescribe (idempotente). `uninstall` elimina. En actualización, re-ejecutar `install`.
- **Health command** (`screensafe health`): salida estructurada con:
  ```text
  ScreenSafe Health

  Current Resolution: 1920x1080
  Desired WorkArea:   0,0,1920,1000
  Current WorkArea:   0,0,1920,1080
  Strategy:           SPI_SETWORKAREA
  Daemon:             Running
  AutoStart:          Enabled
  Last Reapply:       2026-06-02 15:21:10
  Status:             Mismatch Detected
  ```
- **Daemon startup sequence**: (1) Al iniciar Windows → carga `appsettings.json`; (2) aplica WorkArea deseado automáticamente — **no sobrescribe OriginalWorkArea**; (3) inicia monitoreo (message pump + watcher). Si el área ya es correcta, el paso 2 es no-op.
- **OriginalWorkArea inmutable**: Se captura una sola vez durante el primer `apply` manual (existente). El daemon **nunca lo reescribe**. Solo `restore` lo restaura y lo limpia. En cada reinicio del daemon, el OriginalWorkArea preservado en `appsettings.json` permanece intacto.
- **Logging**: extiende `ILogger` MVP. Nuevos entry types para eventos Win32, cambios detectados, reaplicaciones, errores y ciclo de vida.
- **Log rotation**: Máximo 1 MB por archivo. Retención: últimos 3 archivos rotados. Formato: `screensafe-{yyyy-MM-dd}-{n}.log`. Rotación por tamaño al alcanzar el límite, no por fecha.
- **Log path**: `%LOCALAPPDATA%\ScreenSafe\Logs\` — ruta fija, no relativa al ejecutable. Creada automáticamente si no existe.
- **Circuit breaker**: Contador de reaplicaciones en ventana deslizante de 60 segundos. Si excede 10, se suspende la reaplicación automática durante 5 minutos y se registra un error crítico en el log. Tras la pausa, se reanuda el monitoreo normalmente.

## Áreas Afectadas

| Área | Impacto | Descripción |
|------|---------|-------------|
| `Console/Program.cs` | Modificado | Routing dual CLI/daemon |
| `Console/CliDispatcher.cs` | Modificado | +install, +uninstall, +health |
| `Infrastructure/WorkAreaWatcher.cs` | Nuevo | Ventana oculta + msg pump |
| `Infrastructure/EventDebouncer.cs` | Nuevo | Timer debounce configurable |
| `Infrastructure/AutoApplyService.cs` | Nuevo | Receive → debounce → eval → apply |
| `Infrastructure/WindowsStartupManager.cs` | Nuevo | Run key CRUD |
| `Infrastructure/LogRotator.cs` | Nuevo | Rotación por tamaño, retención N archivos |
| `Application/HealthUseCase.cs` | Nuevo | Diagnóstico: estado deseado vs actual |
| `Domain/IWorkAreaWatcher.cs` | Nuevo | Interface watcher |
| `Domain/IWindowsStartupManager.cs` | Nuevo | Interface startup |
| `Infrastructure/NativeMethods/User32.cs` | Modificado | +CreateWindowExW, GetMessage, etc. |
| `Application/` | Modificado | Nuevo use case / service |
| `Tests/` | Modificado | Tests watcher, debouncer, auto-apply |

## Riesgos y Mitigaciones

| Riesgo | Prob. | Mitigación |
|--------|-------|------------|
| Win10+ Explorer sobreescribe SPI_SETWORKAREA → loop reaplicación | Alta | (a) 400ms debounce colapsa ráfagas; (b) evalúa SPI_GETWORKAREA antes de reaplicar (no-op si ya correcto); (c) backoff exponencial si N reapplies en T segundos (circuit breaker); (d) logging diagnóstico de cada reaplicación |
| GC recolecta delegate WndProc | Media | Delegate estático rooteado en campo `readonly` |
| Console window flash al iniciar `--daemon` | Baja | `FreeConsole()` post-creación |
| Thread message pump bloqueante | Baja | Hilo dedicado con CancellationToken |

## Plan de Reversión

1. `screensafe uninstall` — elimina Run key
2. Matar proceso desde Task Manager si está corriendo
3. Eliminar o renombrar binario (`ScreenSafe.Console.exe`)
4. El daemon no se inicia sin el binario
5. Si hay loop de reaplicación: paso 2 + desinstalar

## Dependencias

- .NET Framework 4.8 (existente)
- Sin nuevas dependencias NuGet/externas
- P/Invoke directo a `User32.dll`

## Criterios de Éxito

- [ ] Daemon inicia sin ventana visible, carga configuración, aplica WorkArea y arranca monitoreo
- [ ] Daemon recibe `WM_SETTINGCHANGE` y reacciona dentro de `eventDebounceMs`
- [ ] Debounce colapsa ≥5 eventos rápidos en 1 reaplicación
- [ ] No hay reaplicación si el área ya coincide con lo deseado
- [ ] `install`/`uninstall` gestionan Run key correctamente
- [ ] `screensafe health` muestra resolución, Desired WorkArea, Current WorkArea, Strategy, Daemon status, AutoStart, Last Reapply y Status con diagnóstico
- [ ] Logging captura todos los eventos, cambios y errores
- [ ] Logs almacenados en `%LOCALAPPDATA%\ScreenSafe\Logs\` (ruta fija, no relativa)
- [ ] Rotación de logs: archivo >1 MB → nuevo archivo, máximo 3 rotaciones retenidas
- [ ] Circuit breaker: >10 reaplicaciones en 60s → suspender 5 min + log error
- [ ] Sin bucles de reaplicación en Win10+ (backoff efectivo)
- [ ] **Mover la barra de tareas no elimina permanentemente la reserva**
- [ ] **Reiniciar Explorer.exe no elimina permanentemente la reserva**
- [ ] OriginalWorkArea se captura una sola vez y permanece inmutable hasta un restore manual; el daemon no lo reescribe
