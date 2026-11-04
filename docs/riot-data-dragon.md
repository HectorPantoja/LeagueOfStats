# Contrato oficial de Riot

Fuente revisada: https://developer.riotgames.com/docs/lol#data-dragon (3 de noviembre de 2026).

Para información estática se utiliza Data Dragon, que no requiere API key. Base: `https://ddragon.leagueoflegends.com/`.

| Ruta | Modelo / uso |
| --- | --- |
| `api/versions.json` | Lista de `PatchVersion`; versiones disponibles |
| `realms/{region}.json` | `RealmDto`; `v` versión regional, `n` versiones de componentes, `l` idioma predeterminado |
| `cdn/{version}/data/{locale}/champion.json` | `DataDragonEnvelope<ChampionDto>`; resumen de campeones |
| `cdn/{version}/data/{locale}/champion/{id}.json` | Detalle de campeón, pasiva y habilidades |
| `cdn/{version}/data/{locale}/item.json` | `DataDragonEnvelope<ItemDto>`; objetos |

El `id` de campeón es texto (Aatrox) y `key` contiene su identificador numérico como cadena. El ID de objeto proviene de la clave del diccionario `data`, no de un campo del objeto. Se convierten explícitamente durante el mapeo. Los DTOs cubren los campos consumidos y los principales campos de detalle; no pretenden ser un espejo exhaustivo de todos los campos opcionales de Riot.

`stats` conserva las claves oficiales y sus valores numéricos; `maps` usa claves textuales con booleanos; recetas `from`/`into` contienen IDs como cadenas. Las descripciones pueden contener etiquetas y las habilidades contienen variables sin resolver: no se interpretan como HTML ni se calculan daños o cooldowns enemigos.

La sincronización usa `realm.v` para descargar ambos catálogos y rechaza respuestas cuya versión no coincida. Data Dragon puede publicarse después del parche del cliente; el catálogo no demuestra qué parche está ejecutando una partida. Las estrategias no se actualizan mediante estos endpoints. No se descarga contenido de modos especiales por defecto.

Las notas tienen una fuente separada: https://www.leagueoflegends.com/es-mx/news/tags/patch-notes/ . El adaptador `RiotPatchNotesSource` extrae enlaces del sitio oficial y detecta ausencia de resultados como error de contrato. No es un endpoint JSON de Data Dragon ni se asume equivalencia entre la numeración editorial de notas y `realm.v`. Actualmente se almacenan los enlaces, no el cuerpo de los artículos.
