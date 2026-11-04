# LeagueOfStats

Prototipo Windows en **.NET 10 + WPF + SQLite**, sin servidor ni cuenta externa.

## Ejecutar

```powershell
dotnet build LeagueOfStats.sln -c Release
dotnet run --project src/LeagueOfStats
dotnet run --project tests/Smoke
```

Requiere SDK .NET 10 y Windows Desktop Runtime 10. La aplicación usa el destino `net10.0-windows` y las pruebas `net10.0`.

## Implementado

- Consulta durante la partida cada 2 segundos y espera 15 segundos si la API no está disponible; utiliza `activeplayer`, `playerlist`, `gamestats` y `eventdata` en `https://127.0.0.1:2999/liveclientdata/`.
- Monitor principal: jugador activo, campeón, vida, tiempo, nivel y oro; tabla de aliados con K/D/A, CS, posición, estado y objetos. Sin lectura de memoria, inyección, automatización de entradas ni captura de pantalla.
- Ventana overlay independiente, movible y ocultable, con resumen del jugador activo y su equipo aliado. No genera consejos a partir de la partida. Diseñada para ventana sin bordes; no se garantiza pantalla completa exclusiva. Por defecto deja pasar los clics al juego; permite activar interacción para moverlo.
- SQLite en `%LOCALAPPDATA%\LeagueOfStats\leagueofstats.db`: muestras del jugador con identificador local de sesión y catálogo regional. No guarda Riot ID. Retiene capturas cerradas 90 días y conserva al menos las diez comparables más recientes por jugador/modo/mapa. No cifra la base.
- Data Dragon: comprueba el realm de **LAN** en la primera sincronización y los miércoles/jueves de semanas alternas; botón manual independiente. Descarga campeones e ítems en `es_MX`, valida versión y guarda ambos de manera atómica. Conserva el último catálogo si falla la descarga. Región fija en `LeagueOfStats.Bootstrapper/ServiceCollectionExtensions.cs`, parámetros de `ExecuteAsync`.
- Sesiones persistidas y recuperables; solo `GameEnd` confirma una partida completada. Una desconexión conserva o interrumpe la captura sin inventar el resultado. Los IDs locales no equivalen a un match ID oficial.

## Alcance de Riot y coaching

**No es un producto aprobado por Riot y no se garantiza su aceptación.** Revisar y registrar el producto en el Developer Portal, describiendo endpoints y funciones; solicitar revisión del alcance antes de incorporar recomendaciones contextuales o distribuirlo.

Riot prohíbe productos que dicten decisiones o aporten información de sesión previamente desconocida. La API expone datos cuya disponibilidad técnica no implica permiso para todos sus usos. Este prototipo muestra datos del jugador activo y su equipo, sin generar recomendaciones estratégicas. No calcula cooldowns enemigos, rutas, posiciones ocultas, predicciones ni órdenes como «gankea ahora» o «compra este objeto».

Fuentes oficiales consultadas el 3 de noviembre de 2026:
- Políticas, Live Client Data y Data Dragon: https://developer.riotgames.com/docs/lol
- Aplicaciones externas: https://support.riotgames.com/en-us/riot/events/third-party-applications

Data Dragon puede retrasarse respecto al cliente regional. La versión del realm es la referencia de catálogo, **no una comprobación del parche real de la partida**. No se activa estrategia dependiente del parche. Descargar estadísticas no actualiza estrategias automáticamente: un motor futuro necesita paquetes de reglas revisados, versión de parche explícita y desactivación cuando la compatibilidad no pueda confirmarse.

## Próximas fases propuestas

1. Validar con Riot los casos concretos de recomendaciones educativas contextuales y sus límites.
2. Configuración de región y selección de guías; bandeja y atajo Ctrl + Shift + Z ya disponibles.
3. Integración opcional con un proveedor de IA: actualmente se exporta un paquete local para adjuntarlo manualmente.
4. Reglas educativas versionadas y revisadas por parche; solo habilitar funciones en vivo cuyo alcance se haya validado con Riot.

No se requiere API key para estos endpoints locales ni para Data Dragon. La excepción al certificado autofirmado está aislada al cliente loopback con host/puerto fijos, sin proxy ni redirecciones; para producción preferir validación con la raíz de Riot documentada.

## Validación

`tests/Smoke` comprueba parseo de datos propios, rechazo de campos ausentes, inicialización repetida del esquema, escritura/lectura SQLite y reemplazo de catálogo. No sustituye prueba manual de WPF ni prueba dentro de una partida real. No se ha validado interacción con Vanguard.

## Estructura del proyecto

Abre `LeagueOfStats.sln`. Capas: Domain, Application, Infrastructure, Presentation y vistas WPF.

- [Arquitectura DDD](docs/architecture.md)
- [Diagramas de flujo y ciclo de vida](docs/diagrams.md)
- [Base de datos y migraciones](database/README.md)
- [Modelos y endpoints oficiales de Riot](docs/riot-data-dragon.md)


La revisión técnica incorporó MVVM, cierre coordinado, colecciones inmutables, validación de respuestas, dependencias centralizadas y actualización de SQLitePCLRaw a 2.1.14. Las pruebas incluyen respuestas malformadas, cancelación HTTP, límites de sesión, aislamiento de idioma y rollback SQLite.


Las notas oficiales se revisan en un flujo independiente del catálogo. El calendario usa la semana de la primera sincronización exitosa como semana 0, con revisiones en semanas 0, 2, 4, etc. Zona horaria: `America/Mexico_City`. El estado sobrevive reinicios; se recupera la última revisión pendiente al abrir la aplicación. El seguimiento de notas guarda enlaces, no el texto completo ni interpretaciones automáticas. Ver [flujos actualizados](docs/diagrams.md).


## Captura del jugador y aliados

`LiveTeamParser` identifica al jugador por Riot ID (o nombre heredado si es único), obtiene su equipo y descarta las entradas rivales antes de construir `ActiveTeamSnapshot`. El endpoint `playerlist` devuelve ambos equipos; solo la proyección aliada llega a la interfaz y al overlay. Si no se identifica un jugador único con equipo válido, se descarta la captura y se limpian los datos visibles.

Los nombres, inventarios y métricas del equipo se mantienen en memoria para mostrar la partida. SQLite persiste métricas e inventario propios, modo/mapa y eventos normalizados para revisión posterior. No almacena respuestas JSON crudas ni nombres de otros participantes. Ver [revisión posterior](docs/postgame-review.md).

Para probarlo, inicia una partida de práctica y ejecuta `dotnet run --project src/LeagueOfStats`; la detección puede tardar hasta 15 segundos más el tiempo de las solicitudes. El mensaje «Esperando partida» indica que el puerto local no responde o que la API todavía no está lista. La validación automatizada usa respuestas simuladas; la última comprobación local no pudo conectar al puerto 2999.

La presentación ahora vive en `LeagueOfStats.Presentation` y depende de contratos de Application. Los repositorios de catálogo y muestras están separados; consulta la [arquitectura SOLID](docs/architecture.md) para responsabilidades y dependencias.


La inyección de dependencias usa `Microsoft.Extensions.DependencyInjection`. Los registros están en `LeagueOfStats.Bootstrapper`; `App` valida el contenedor y gestiona su liberación asíncrona.


El overlay ahora se muestra automáticamente al detectar jugador y equipo, se posiciona sobre la ventana del juego y reafirma su prioridad sin activar el teclado. Consulta [funcionamiento y límites del overlay](docs/overlay.md).


## Revisión posterior y últimas 10 partidas

En la ventana principal: **Actualizar historial → seleccionar captura cerrada → Generar resumen y comparación → Exportar para IA…**. El archivo contiene instrucciones y datos para adjuntar a una IA; no hay transmisión automática.

La referencia utiliza hasta diez partidas previas completas del mismo jugador, modo y mapa. La cola exacta normal/clasificatoria permanece desconocida. Las capturas incompletas se pueden revisar con advertencias, pero no entran en las medias. Consulta [tablas, estados, retención, registros y límites](docs/postgame-review.md).
