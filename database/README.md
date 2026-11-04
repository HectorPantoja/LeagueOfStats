# Base de datos SQLite

## Paso previo: crear el archivo de base de datos

Desde la raíz del repositorio:

```powershell
.\database\000_create_database.ps1
# Opcional: otra ubicación
.\database\000_create_database.ps1 -DatabasePath 'C:\datos\leagueofstats.db'
```

Requiere SDK .NET 10; la primera ejecución restaura Microsoft.Data.Sqlite desde NuGet. No requiere instalar `sqlite3.exe`. Crea el directorio y un archivo SQLite válido, sin tablas de aplicación. Si la base existe, la abre en modo de solo lectura para comprobar su esquema y la conserva. Los errores se notifican sin sobrescribir archivos existentes.

La ruta predeterminada es `%LOCALAPPDATA%\LeagueOfStats\leagueofstats.db`. Una ruta personalizada solo prepara ese archivo; no cambia la configuración de la aplicación. El siguiente paso es iniciar LeagueOfStats para aplicar las migraciones. Este script es opcional porque la aplicación también crea el archivo automáticamente. SQLite no utiliza una sentencia `CREATE DATABASE`.

Los scripts de `migrations/` son la fuente de verdad del esquema. Se incluyen como recursos en Infrastructure; `DatabaseInitializer` los ejecuta en orden y registra cada aplicación en `SchemaMigrations` dentro de una transacción. No modificar scripts aplicados: añadir `003_...sql`, etc.

Al iniciar la aplicación se crea `%LOCALAPPDATA%\LeagueOfStats\leagueofstats.db` y se aplican las migraciones pendientes. No necesitas instalar un servidor ni ejecutar SQL manualmente. Para comprobarlas en una base temporal: `dotnet run --project tests/Smoke` desde la raíz.

- `001_initial.sql`: muestras, índices y caché anterior compatible con el prototipo.
- `002_typed_catalog.sql`: `GameCatalogs`, `Champions`, `Items`. Clave del agregado: región + idioma. La versión vive en su cabecera; los hijos se reemplazan juntos en una transacción con claves foráneas habilitadas.
- `SchemaMigrations`: historial creado por el inicializador.

Los campos de consulta e identidad son columnas SQL; los atributos variables de campeones y objetos se guardan como JSON tipado en `Payload`. Se conserva un catálogo actual por región e idioma, no un historial de todos los parches. El catálogo antiguo se conserva y la siguiente sincronización lo sustituye funcionalmente por el catálogo tipado. Las muestras existentes no se borran al migrar; la retención normal elimina las de más de 30 días.

Implementación: `src/LeagueOfStats.Infrastructure/Persistence`. Antes de modificar un esquema en producción, respaldar la base con la aplicación cerrada (incluidos archivos WAL si existen). Las pruebas verifican inicialización repetida y rollback ante un fallo de escritura.

- `003_independent_sync.sql`: ancla común de calendario, estado independiente de cada flujo y enlaces de notas oficiales. No se altera el contenido de migraciones previas. Los intentos se registran antes de consultar la red; los éxitos solo después de persistir resultados.

Los accesos están separados por responsabilidad: `SqliteDatabase` configura la ruta/conexión, `DatabaseInitializer` aplica migraciones, `SqliteSampleRepository` escribe muestras y `SqliteCatalogRepository` administra el catálogo. El refactor SOLID no cambia el esquema ni requiere una nueva migración.

## Captura y revisión posterior

- `004_match_review.sql`: crea `MatchSessions`, `MatchSamples` y `MatchEvents`.
- `005_reliable_reviews.sql`: agrega modo/mapa, estado y calidad de captura, clave seudónima, métricas adicionales e índices; sanea payloads antiguos.
- `MatchSamples` y `MatchEvents` se escriben durante el juego y se conservan para revisión. `MatchSessions.ReviewJson` guarda el paquete al generarlo. `Samples` queda como historial del prototipo y ya no recibe escrituras.
- Retención de capturas cerradas: 90 días, preservando las diez comparables más recientes de cada jugador/modo/mapa; se aplica al iniciar y al observar GameEnd. El borrado de sesiones elimina sus hijos por FK.

El esquema completo, criterios de comparación, logs y estados se describen en [revisión posterior](../docs/postgame-review.md).

- `006_champion_identity.sql`: agrega el identificador canónico del campeón a `MatchSessions` y recupera el nombre desde la última muestra para sesiones antiguas que no lo tenían en la cabecera. Lo utiliza la tarjeta de última partida para enlazar el retrato del catálogo.
