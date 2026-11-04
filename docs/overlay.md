# Overlay de Windows

El overlay sigue siendo una ventana externa; no se inyecta en el proceso de League.

## Mostrar y ocultar con el teclado

Con LeagueOfStats abierto, pulsa **Ctrl + Shift + Z** para alternar la visibilidad, incluso con la ventana principal minimizada o el juego enfocado. El botón y el atajo comparten la misma acción: ocultarlo manualmente impide que la detección de partida lo vuelva a mostrar. Pulsa de nuevo para restaurarlo.

`OverlayHotkey`, registrado por DI, utiliza `RegisterHotKey` con `MOD_NOREPEAT`, sin capturar todo el teclado. Su receptor es la ventana principal, por lo que ocultar el overlay no desactiva el atajo. Se libera al comenzar el cierre. Si otra aplicación ocupa la combinación, la interfaz muestra el error y el botón sigue disponible.

El atajo no cambia el modo de video ni elimina la limitación de pantalla completa exclusiva descrita abajo. Para probar: inicia una partida de práctica, pulsa la combinación para ocultar y restaurar, comprueba que el juego conserva el foco y repite con LeagueOfStats minimizado.

Referencia: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey

## Icono de bandeja

Minimizar la ventana principal la oculta y la mantiene disponible en la bandeja del sistema. Haz doble clic en el icono o elige «Abrir LeagueOfStats» para restaurarla; el menú «Salir» cierra la app y libera el icono.

## Implementación

- `WindowsOverlayHost` aplica `HWND_TOPMOST` mediante `SetWindowPos` con `SWP_NOACTIVATE`, además de los estilos `WS_EX_NOACTIVATE` y `WS_EX_TOOLWINDOW`.
- Por defecto utiliza `WS_EX_TRANSPARENT` para permitir que los clics atraviesen la ventana. La opción «Permitir mover e interactuar con el overlay» desactiva ese paso de clics y permite arrastrarlo.
- Al recibir la primera proyección válida del equipo, la ventana principal muestra el overlay automáticamente. Ocultarlo manualmente suspende esa presentación automática hasta volver a mostrarlo con el botón.
- Al mostrarlo, busca las ventanas visibles del proceso `League of Legends` mediante `EnumWindows`, obtiene el área cliente y lo coloca en esa pantalla. No depende de la posición fija del monitor principal ni de `Process.MainWindowHandle`.
- Mientras esté visible, comprueba la posición cada 500 ms y reafirma el orden superior cuando el juego tiene el foco. Si cambia el área del juego, recoloca el overlay. Las coordenadas se manejan con las APIs nativas en el mismo contexto DPI.
- El temporizador se detiene al ocultar o cerrar. `WM_MOUSEACTIVATE` devuelve `MA_NOACTIVATE` para evitar quitarle el teclado al juego.

## Modos de pantalla

Ventana y ventana sin bordes permiten composición de ventanas de escritorio. El modo «pantalla completa» puede funcionar si Windows lo presenta mediante composición/optimizaciones de pantalla completa. **No se garantiza pantalla completa exclusiva real**: elevar una ventana WPF no obliga a un motor gráfico exclusivo a componerla. La aplicación no cambia automáticamente la configuración de video del usuario. Ese requisito necesitaría otra integración de renderizado y validación específica con Riot.

Referencia Win32: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos

## Validación

```powershell
dotnet run --project tests/OverlaySmoke -c Release
```

La prueba abre temporalmente dos ventanas, comprueba prioridad superior, ausencia de activación, estilos de paso de clics, posicionamiento y alternancia de interacción. No valida el renderizado de una partida real ni garantiza pantalla completa exclusiva. Las pruebas generales siguen en `tests/Smoke`.

También registra el atajo real y envía mensajes `WM_HOTKEY` para comprobar alternancia con el overlay oculto, receptor minimizado, mensajes ajenos y liberación/reutilización del registro. La pulsación física en League requiere validación manual.
