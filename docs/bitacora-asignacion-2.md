# Bitácora — Asignación 2

## Tarea delegada al agente

Construir la pieza Gestión de permisos completa (RF-GP-01 a RF-GP-07) junto con su suite de pruebas unitarias y la sección del README que explica cómo provocar cada criterio. Issue #40, PR #42.

## Qué le pedí

Le pasé el enunciado de la asignación, el issue #40 con la tabla de transiciones y un diseño cerrado:

- el permiso solicitable es "ver el listado de usuarios";
- dos tablas nuevas (`SolicitudPermiso` y `PermisoOtorgado`);
- la máquina de estados en un solo archivo, igual que la de la orden de compra del inventario;
- los cinco endpoints bajo `/api/permisos/solicitudes`.

También le pedí:

- **Pruebas:** que cada una tuviera las fases preparar, actuar y verificar, un solo comportamiento y un nombre que diga cuál. Los casos obligatorios eran aprobar dos veces, pasar de Rechazada a Aplicada, rechazar sin motivo y resolver la propia solicitud.
- **README:** que probara cada comando contra la aplicación antes de escribirlo.
- **Errores:** que al final me listara con honestidad cada error o suposición equivocada que tuviera durante el trabajo.

## Qué me devolvió

El PR #42 con 13 commits atómicos, cada uno con el ID del requisito que cubre:

| Commit | Qué hace |
|---|---|
| `87a1d07` | Declara los estados y las transiciones en un solo lugar (RD-04) |
| `6fec9b6` | Crea, aprueba y rechaza solicitudes; al aprobar aplica el permiso en la misma operación (RF-GP-05) |
| `17e86a5` | El `Autorizador` deja pasar a quien tiene el permiso otorgado |
| `08476c0`, `311edfa` | Pruebas de las transiciones y del servicio, sin levantar la web |
| `4937b13` | Sección 8 del README y sección "Pruebas" |

La suite pasó de 254 a 298 pruebas, todas en verde, y la CI también pasó.

## Errores detectados y corrección

### 1. El README asumía ids fijos

En el primer borrador de la sección 8 del README, el agente escribió los comandos con ids fijos: la usuaria Sofía con id 2, las solicitudes 1, 2 y 3, y un administrador llamado "Admin". Eso solo es cierto en una base de datos vacía. Si antes se siguieron las secciones 1 a 7 del README, como va a hacer quien califica, los ids son otros, y los comandos `aprobar` y `rechazar` apuntarían a solicitudes que no existen o que son de otra persona.

- **Cómo se detectó:** el agente lo reportó en su lista de errores al revisar el borrador. Lo verifiqué leyendo la sección 8 del README antes del merge.
- **Corrección:** los comandos guardan en una variable el `id` que devuelve cada respuesta (`SOLICITUD=$(curl ...)`) en vez de usar números fijos, y se aclaró que los ids de las respuestas de ejemplo son los de una base recién creada. Está en el commit `4937b13`.

### 2. Las tablas del README se rompían

El agente puso la salida de `sqlite3`, que separa columnas con `|`, dentro de celdas de tablas Markdown, y eso partía las tablas.

- **Cómo se detectó:** revisando el Markdown antes del commit.
- **Corrección:** se escapó como `\|`.

### 3. Nadie había comprobado que las pruebas detecten errores reales

El agente reportó que todas las pruebas pasaron al primer intento y reconoció que no había verificado que fallaran si el código estaba mal. Una suite que siempre pasa no demuestra nada por sí sola, así que lo verifiqué con una prueba de mutación: rompí el código a propósito, corrí las pruebas de Gestión de permisos y después restauré el archivo.

| Cambio a propósito en `MaquinaEstadosSolicitudPermiso.cs` | Resultado |
|---|---|
| Agregar la transición prohibida Aplicada → Aprobada | **4 pruebas fallaron**, entre ellas `Aprobar_DosVecesLaMismaSolicitud_LaSegundaSeRechazaYNadaCambia` y `Transiciones_SoloPermitenLasTresDeLaTabla` |
| Aprobar sin registrar quién aprobó (`AprobadorId`) | **3 pruebas fallaron**, entre ellas `Aprobar_SolicitudPendiente_QuedaAplicadaYRegistraQuienAproboYCuando` |

Con el código original, las 298 pruebas vuelven a pasar. Esto confirma que las pruebas de transición válida y prohibida verifican lo que dicen verificar: el estado y el dato registrado.

### 4. Un comando quedó colgado

Durante el trabajo, el agente dejó un comando esperando entrada (`cat > /dev/null`) al final de un script que editaba varios archivos. El comando no terminó en 120 segundos y se movió a segundo plano.

- **Cómo se detectó:** por el aviso de tiempo agotado.
- **Corrección:** el agente detuvo la tarea y verificó con `git status` y `dotnet build` que las ediciones estaban completas y compilaban.
