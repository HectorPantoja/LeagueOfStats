# Flujos y ciclo de vida

Diagramas Mermaid del comportamiento implementado. Hay tres procesos independientes: catálogo Data Dragon, seguimiento de notas oficiales y monitoreo de la API local. La disponibilidad de una partida no controla las sincronizaciones externas.

## 1. Inicio y procesos independientes

```mermaid
flowchart TD
    A[Iniciar aplicación] --> B[Inicializar SQLite y migraciones]
    B --> C[Leer calendario persistido]
    C --> D[Ejecutar tres bucles concurrentes]
    D --> E[Monitoreo de partida]
    D --> F[Sincronización del catálogo]
    D --> G[Seguimiento de notas oficiales]
    E --> H[Estado y estadísticas WPF]
    F --> I[Estado del catálogo WPF]
    G --> J[Estado de notas WPF]
    B -->|Error| K[Mostrar fallo de inicio]
```

## 2. Calendario de revisiones

- Primera sincronización: inmediata, independientemente del día. El primer éxito de cualquiera de los flujos fija un ancla común en SQLite.
- La semana del ancla es la **semana 0**, de lunes a domingo, en `America/Mexico_City`.
- Revisiones: miércoles y jueves en semanas **0, 2, 4, 6...**, una revisión exitosa por día y por flujo. La primera sincronización ya cuenta como revisión de ese día.
- Ejemplo con primera sincronización el martes 3 de noviembre de 2026: revisiones el 4 y 5; se omiten el 11 y 12; se revisa el 18 y 19.
- Cada flujo conserva su último intento, último éxito y resumen. Reiniciar no cambia el ancla ni repite una revisión completada.
- Si la aplicación estuvo cerrada, al abrir recupera la revisión pendiente más reciente, incluso fuera de miércoles/jueves. No reproduce cada fecha perdida.
- Si falla, reintenta como máximo una vez por hora, incluida la primera sincronización. No marca éxito ni borra los datos anteriores.
- Los botones manuales fuerzan únicamente el flujo seleccionado y no cambian el ancla.
- Cada minuto se evalúa el calendario **localmente**; esto no implica una petición a Riot cada minuto. La aplicación debe estar abierta para ejecutar tareas; no se instaló un servicio ni una tarea de Windows.

```mermaid
flowchart TD
    A[Inicio o comprobación local cada minuto] --> B[Leer ancla y estado del flujo]
    B --> C{Intento hace menos de una hora?}
    C -->|Sí| Z[Esperar]
    C -->|No| D{Sin primera sincronización exitosa?}
    D -->|Sí| H[Registrar intento y ejecutar flujo]
    D -->|No| E[Calcular semanas desde el lunes del ancla]
    E --> F{Miércoles o jueves pendiente en semana par?}
    F -->|No| Z
    F -->|Sí, hoy o fecha perdida| H
    M[Botón manual del flujo] --> H
    H --> I{Resultado}
    I -->|Correcto| J[Guardar éxito y resumen]
    J --> K[Crear ancla solo si todavía no existe]
    I -->|Error| L[Conservar último éxito y datos; esperar reintento]
```

## 3. Catálogo de objetos y campeones

```mermaid
flowchart TD
    A[Revisión de catálogo autorizada por calendario o botón] --> B[Consultar realm de LAN]
    B --> C[Consultar catálogo SQLite de LAN y es_MX]
    C --> D{Misma versión?}
    D -->|Sí| E[Registrar revisión sin descargar datos]
    D -->|No| F[Descargar champion.json e item.json]
    F --> G[Validar versión y construir GameCatalog]
    G --> H[Transacción: sustituir cabecera, campeones y objetos]
    H --> I[Commit]
    I --> E
    F -->|Error| J[Conservar catálogo previo]
    G -->|Error| J
    H -->|Error| K[Rollback]
    K --> J
```

`RefreshCatalog` usa Data Dragon y actualiza objetos y campeones juntos para conservar una versión coherente. Un éxito sin cambio de versión cuenta como revisión. Si Riot publica el catálogo después de la ventana prevista, se detectará en la siguiente revisión programada o manual. Las notas publicadas no fuerzan ni sustituyen una versión del catálogo.

## 4. Notas del parche

```mermaid
flowchart TD
    A[Revisión de notas autorizada por calendario o botón] --> B[Consultar índice oficial de notas en español]
    B --> C[Extraer enlaces de publicaciones del parche]
    C --> D{Hay enlaces reconocidos?}
    D -->|No| E[Reportar cambio de formato y conservar datos]
    D -->|Sí| F[Transacción: insertar enlaces nuevos en PatchNotes]
    F --> G[Registrar éxito y resumen del flujo de notas]
    B -->|Error HTTP| H[Reintento independiente del catálogo]
    F -->|Error| I[Rollback]
```

Fuente: https://www.leagueoflegends.com/es-mx/news/tags/patch-notes/ . Es un adaptador del sitio oficial, no una API JSON documentada de Riot. Persiste URL y fecha de descubrimiento. No descarga el cuerpo de cada artículo, no detecta ediciones dentro del mismo artículo y no genera estrategias automáticamente. El adaptador falla explícitamente si el índice deja de contener enlaces reconocibles.

## 5. API local: espera, partida y desconexión

```mermaid
stateDiagram-v2
    [*] --> BuscandoPartida
    BuscandoPartida --> EsperandoPartida: conexión rechazada, HTTP 404 o timeout
    EsperandoPartida --> BuscandoPartida: esperar 15 segundos
    BuscandoPartida --> PartidaActiva: jugador y equipo aliado identificados
    PartidaActiva --> PartidaActiva: persistir muestra y esperar 2 segundos
    PartidaActiva --> EsperandoPartida: API desaparece o deja de responder
    BuscandoPartida --> ErrorDeDatos: respuesta malformada u otro error
    PartidaActiva --> ErrorDeDatos: datos o persistencia inválidos
    ErrorDeDatos --> BuscandoPartida: reintentar
    BuscandoPartida --> Cerrando: cancelación
    EsperandoPartida --> Cerrando: cancelación
    PartidaActiva --> Cerrando: cancelación
    ErrorDeDatos --> Cerrando: cancelación
    Cerrando --> [*]
```

No se requiere que el cliente local esté abierto para iniciar la aplicación. Mientras no haya datos válidos, no se guardan muestras ni se muestran estadísticas antiguas. Una desconexión no demuestra que la partida terminó: puede ser transitoria. Las sesiones pasan por Capturing, Disconnected e Interrupted; solo GameEnd las marca Completed. La recuperación consulta el estado persistido y la compatibilidad temporal. Ver postgame-review.md para las condiciones exactas. Los intervalos se suman al tiempo empleado por las solicitudes y la escritura. El overlay comparte la proyección filtrada del jugador y aliados con la ventana principal; se limpia al perder la conexión.

## 6. Base de datos

```mermaid
flowchart TD
    A[Script opcional 000_create_database.ps1] --> B[Crear archivo SQLite o conservar existente]
    B --> C[Iniciar aplicación]
    C --> D[Abrir conexión y activar WAL]
    D --> E[Crear SchemaMigrations si falta]
    E --> F[Aplicar scripts pendientes en orden]
    F --> G[001: muestras y caché anterior]
    G --> H[002: catálogo tipado]
    H --> I[003: SyncAnchor, SyncState y PatchNotes]
    I --> J[Eliminar muestras con más de 30 días]
    J --> K[Base lista para los tres procesos]
```

Cada migración tiene su propia transacción y registro; las migraciones ya aplicadas se omiten. Las sincronizaciones y sus estados son reintentables: si se guardan datos pero falla el registro de éxito, el siguiente intento vuelve a comprobar la fuente sin duplicar notas.

## 7. Cierre coordinado

```mermaid
sequenceDiagram
    actor Usuario
    participant W as MainWindow
    participant VM as MonitorViewModel
    participant M as Monitoreo
    participant C as Catálogo
    participant N as Notas
    participant App
    Usuario->>W: Cerrar
    W->>W: Deshabilitar UI y aplazar cierre
    W->>VM: DisposeAsync
    VM->>VM: stopping y Cancel token
    VM-->>M: Cancelar espera o HTTP local
    VM-->>C: Cancelar espera o sincronización
    VM-->>N: Cancelar espera o revisión de notas
    VM->>VM: Esperar run y tareas Pending de catálogo y notas
    M-->>VM: Finalizado
    C-->>VM: Finalizado
    N-->>VM: Finalizado
    VM-->>W: Dispose completado
    W->>W: Cerrar overlay y ventana
    W->>App: Closed
    App->>App: Esperar ServiceProvider.DisposeAsync
    App->>App: Shutdown después de liberar servicios
```

Un fallo de catálogo no detiene notas ni monitoreo. Un fallo de notas tampoco detiene los demás procesos. Los trabajos manuales de cada flujo comparten su tarea en curso para evitar descargas simultáneas del mismo flujo dentro de la aplicación.

## 8. Proyección del jugador y equipo aliado

```mermaid
flowchart TD
    A[GET activeplayer] --> B[GET playerlist]
    B --> C[GET gamestats]
    C --> D[Identificar jugador por Riot ID o nombre heredado único]
    D --> E{Identidad única y equipo válido?}
    E -->|No| F[Descartar captura y limpiar interfaz]
    E -->|Sí| G[Filtrar playerlist por equipo del jugador]
    G --> H[Construir ActiveTeamSnapshot sin rivales]
    H --> I[Persistir tiempo, nivel y oro propios]
    I --> J[Actualizar jugador y tabla aliada en WPF]
    J --> K[Overlay enlazado al mismo ViewModel]
```

## Componentes responsables

`MonitorViewModel` coordina el ciclo de vida. `LiveMonitorViewModel` ejecuta el monitoreo y dos instancias de `SynchronizationViewModel` ejecutan catálogo y notas. Todos están en el proyecto Presentation, sin dependencia de WPF o SQLite. Véase [arquitectura SOLID](architecture.md).




La revisión posterior, exportación y estados recuperables se documentan en [flujo de revisión](postgame-review.md).

