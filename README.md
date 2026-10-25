# SaveStock

SaveStock es un inventario para negocios pequeños. La **Práctica 1** entrega la primera pieza del Core
funcionando completa, el **control de acceso** (registro con activación por correo, sesión con bloqueo
por intentos, roles y administración de usuarios, recuperación y cambio de contraseña, y la cola de
correos con su enviador aparte), y deja declarada la **estructura de la máquina de estados** de la orden
de compra, la entidad central del inventario.

La **Asignación 2** agrega la segunda pieza del Core, la **gestión de permisos**: un usuario Estándar
pide un permiso que su rol no le da, un Administrador lo aprueba o lo rechaza con un motivo, y al
aprobarlo el sistema lo aplica solo. Se verifica en la [sección 8](#8-gestión-de-permisos-rf-gp-01-a-rf-gp-07-rd-04-rd-06-rd-07-rd-09)
y sus pruebas se ejecutan como dice [Pruebas](#pruebas).

Todos los comandos de este README se ejecutan **desde la raíz del repositorio**.

## Requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0) (`dotnet --version` debe mostrar `10.x`).
- `curl`, para las verificaciones de abajo.
- Opcionales: `sqlite3`, para leer la base de datos (viene instalado en macOS y en casi todas las
  distribuciones de Linux), y `jq`, para sacar el token de las respuestas JSON (cada comando que lo usa
  trae también la versión sin `jq`).

Los comandos están escritos para bash o zsh (macOS, Linux, o Git Bash / WSL en Windows).

## Instalación

```bash
git clone https://github.com/TomasitoD/SaveStock.git
cd SaveStock
git checkout asignacion-2
dotnet restore
```

## Variables de entorno

La configuración se lee **solo** de variables de entorno (RD-10); en el repositorio no hay ninguna
credencial. Hay dos formas de darlas:

- **Archivo `.env` (recomendado):** copia la plantilla y completa los valores. El `.env` está en
  `.gitignore`, así que nunca se sube. La web y el enviador lo cargan solos al arrancar.

  ```bash
  cp .env.example .env
  ```

- **Variables exportadas** en la terminal (`export SMTP_HOST=...`). Si una variable está definida en el
  entorno y también en el `.env`, gana la del entorno. Eso sirve para cambiar una sola variable en un
  comando, por ejemplo `SMTP_HOST=smtp.invalido dotnet run --project src/SaveStock.Enviador`.

| Variable | Para qué sirve | Obligatoria |
|---|---|---|
| `SAVESTOCK_DB_RUTA` | Archivo SQLite del Core (usuarios, sesiones, tokens, cola de correos). La web y el enviador usan el mismo. Si está vacía: `datos/savestock.db`. | No |
| `SAVESTOCK_INVENTARIO_DB_RUTA` | Archivo SQLite del inventario (órdenes de compra). Si está vacía: `datos/inventario.db`. | No |
| `SAVESTOCK_URL_BASE` | URL pública de la web, con la que se arman los enlaces de los correos. Si está vacía: `http://localhost:5080`. | No |
| `SAVESTOCK_ADMIN_CORREO` | Correo del administrador inicial. Se crea activado al iniciar la web si todavía no existe. | Sí, para tener un Administrador |
| `SAVESTOCK_ADMIN_CONTRASENA` | Contraseña del administrador inicial (al menos 8 caracteres, con letras y números). | Sí, para tener un Administrador |
| `SAVESTOCK_ADMIN_NOMBRE` | Nombre del administrador inicial. | Sí, para tener un Administrador |
| `SMTP_HOST` | Servidor SMTP con el que el enviador manda los correos. | Sí, para el enviador |
| `SMTP_PUERTO` | Puerto del servidor SMTP. Si está vacía: `587`. | No |
| `SMTP_USUARIO` | Usuario con el que el enviador se autentica en el servidor SMTP. | Sí, para el enviador |
| `SMTP_CONTRASENA` | Contraseña del usuario SMTP. | Sí, para el enviador |
| `SMTP_REMITENTE` | Dirección que aparece como remitente de los correos. | Sí, para el enviador |

Si falta alguna de las tres `SAVESTOCK_ADMIN_*`, no se crea el administrador inicial. Las variables
`SMTP_*` solo las lee el enviador: la web nunca se conecta al servidor de correo.

**Correo real con Gmail.** Sirve cualquier servidor SMTP. Con una cuenta de Gmail:

1. Activa la verificación en dos pasos de la cuenta de Google.
2. Crea una contraseña de aplicación en <https://myaccount.google.com/apppasswords>.
3. En el `.env`: `SMTP_HOST=smtp.gmail.com`, `SMTP_PUERTO=587`, tu dirección de Gmail en
   `SMTP_USUARIO` y en `SMTP_REMITENTE`, y la contraseña de aplicación (16 letras, sin espacios) en
   `SMTP_CONTRASENA`. Nunca tu contraseña normal.

## Ejecutar

Son dos programas, cada uno en su terminal.

**1. La web** (API, páginas y documentación):

```bash
dotnet run --project src/SaveStock.Web
```

Queda en <http://localhost:5080>. La documentación interactiva de la API está en
<http://localhost:5080/scalar>. La primera vez crea `datos/savestock.db` y `datos/inventario.db`, y el
administrador inicial de las variables `SAVESTOCK_ADMIN_*`. Los datos se conservan entre ejecuciones.

**2. El enviador de correos:**

```bash
dotnet run --project src/SaveStock.Enviador
```

Toma los correos pendientes de la cola, los manda por SMTP, los marca como enviados y termina
imprimiendo `Enviados: N, fallidos: M, pendientes: K`. **Hay que ejecutarlo cada vez que quieras que
salgan los correos encolados** (activación, recuperación, restablecimiento forzado).

Funciona así a propósito (RF-NOT-08, RF-NOT-09): ninguna operación de negocio manda correos. El registro
o la recuperación solo guardan el correo en la tabla `CorreosEnCola` en estado `Pendiente` y responden
enseguida, aunque el servidor SMTP no esté disponible. El envío lo hace este proceso aparte, y
ejecutarlo dos veces no manda nada dos veces (RF-NOT-12). Si falta alguna variable `SMTP_*`, el enviador
lo dice y termina con código de salida 1.

### Interfaz web

Además de la API, la web tiene páginas para usar el sistema desde el navegador:

| Página | Para qué |
|---|---|
| `/` | Inicio |
| `/registro` | Crear una cuenta |
| `/reenviar-activacion` | Pedir otro enlace de activación |
| `/iniciar-sesion` | Iniciar sesión |
| `/perfil` | Ver mis datos, cerrar sesión y cambiar mi contraseña |
| `/contrasena/recuperar` | Pedir un código de recuperación |
| `/contrasena/restablecer` | Definir una contraseña nueva con el código |
| `/admin/usuarios` | Administración de usuarios (solo Administrador; la tabla también la ve quien tiene aprobado el permiso `VerListadoUsuarios`) |

Las verificaciones de abajo usan la API con `curl`: así se ve el código HTTP exacto y la petición se
construye a mano, sin pasar por la interfaz (RD-06). Las páginas usan los mismos servicios del Core, con
las mismas reglas.

## Pruebas

Un solo comando, desde la raíz de un clon limpio, compila y ejecuta toda la suite:

```bash
dotnet test
```

Las pruebas de **Gestión de permisos** están en `tests/SaveStock.Tests/Unitarias/GestionPermisos/`:
`MaquinaEstadosSolicitudPermisoTests.cs` (transiciones válidas con el dato que registran y transiciones
prohibidas que dejan el estado igual) y `ServicioGestionPermisosTests.cs` (crear, listar, aprobar,
rechazar, RF-GP-07, la aplicación automática del permiso y la persistencia). No levantan la web: usan
una base SQLite en memoria y un reloj falso (RD-12), y cada prueba crea su propia base, así que pasan
en cualquier orden y por separado. Cada una tiene sus fases `// Preparar`, `// Actuar` y `// Verificar`,
y cita su requisito con `[Trait("Requisito", "RF-GP-..")]`. Para correr solo esas:

```bash
dotnet test --filter "FullyQualifiedName~GestionPermisos"
```

Además hay pruebas unitarias de los servicios del Core que no levantan la web (RD-12) y pruebas de integración
que levantan la API en memoria con una base SQLite temporal. Cubren también lo que no se puede provocar
a mano en pocos minutos: el vencimiento del bloqueo (15 minutos), del código de recuperación
(15 minutos) y del enlace de activación (24 horas).

### Qué prueba identifica cada requisito RF-GP

Cada prueba de Gestión de permisos lleva `[Trait("Requisito", "RF-GP-..")]` con su requisito. Algunas:

| Requisito | Prueba (en `tests/SaveStock.Tests/Unitarias/GestionPermisos/`) |
|---|---|
| RF-GP-01 | `ServicioGestionPermisosTests.CrearSolicitud_PermisoValido_NaceEnPendiente` |
| RF-GP-02 | `ServicioGestionPermisosTests.ListarMias_ConSolicitudesDeVariosUsuarios_SoloDevuelveLasDelPropioUsuario` |
| RF-GP-03 | `ServicioGestionPermisosTests.Aprobar_SolicitudPendiente_QuedaAplicadaYRegistraQuienAproboYCuando`, `ServicioGestionPermisosTests.Aprobar_PorUnUsuarioEstandar_SeRechazaYSiguePendiente` |
| RF-GP-04 | `ServicioGestionPermisosTests.Rechazar_ConMotivoVacioOAusente_NoSeCompletaYSiguePendiente`, `MaquinaEstadosSolicitudPermisoTests.Rechazar_SinMotivoEscrito_SeRechazaYLaSolicitudSigueEnPendiente` |
| RF-GP-05 | `ServicioGestionPermisosTests.Aprobar_SolicitudPendiente_OtorgaElPermisoAlSolicitanteSinOtroPaso`, `ServicioGestionPermisosTests.Autorizar_ListarUsuarios_EstandarConSolicitudAprobada_EstaPermitido` |
| RF-GP-06 | `ServicioGestionPermisosTests.Aprobar_DosVecesLaMismaSolicitud_LaSegundaSeRechazaYNadaCambia`, `MaquinaEstadosSolicitudPermisoTests.Aplicar_DeRechazadaAAplicada_SeRechazaYElEstadoSigueRechazada`, `MaquinaEstadosSolicitudPermisoTests.Rechazar_SolicitudAprobada_SeRechazaYSigueAprobada` |
| RF-GP-07 | `ServicioGestionPermisosTests.Aprobar_SolicitudCreadaPorElMismoAdministrador_SeRechazaYSiguePendiente`, `ServicioGestionPermisosTests.Rechazar_SolicitudCreadaPorElMismoAdministrador_SeRechazaYSiguePendiente` |

## Defectos

Los seis defectos de la **variante B** están plantados, uno por etiqueta, en `practica-2-defecto-1` a
`practica-2-defecto-6`. Cada commit de defecto tiene como padre el commit de `practica-2`, solo cambia
código de Gestión de permisos (`src/SaveStock.Core/GestionPermisos/`) y no toca ninguna prueba. Su
mensaje dice qué archivo y qué línea cambió.

| Defecto | Requisito | Qué se plantó | Prueba que lo detecta | Comportamiento que comprueba |
|---|---|---|---|---|
| 1 | RF-GP-04 | Al rechazar, solo se comprueba que el motivo fue enviado, no que tenga contenido | `MaquinaEstadosSolicitudPermisoTests.Rechazar_SinMotivoEscrito_SeRechazaYLaSolicitudSigueEnPendiente` (casos `""` y `"   "`) | Un rechazo con motivo vacío no se completa y la solicitud sigue Pendiente |
| 2 | RF-GP-06 | Se agrega la transición Aprobada → Rechazada | `MaquinaEstadosSolicitudPermisoTests.Rechazar_SolicitudAprobada_SeRechazaYSigueAprobada` | Una solicitud aprobada no puede rechazarse y su estado sigue Aprobada |
| 3 | RF-GP-03 | Se elimina la verificación de que quien aprueba es Administrador | `ServicioGestionPermisosTests.Aprobar_PorUnUsuarioEstandar_SeRechazaYSiguePendiente` | Un Estándar que intenta aprobar recibe un rechazo y la solicitud sigue Pendiente, sin aprobador |
| 4 | RF-GP-07 | Se invierte la condición de "nadie resuelve su propia solicitud" | `ServicioGestionPermisosTests.Aprobar_SolicitudCreadaPorElMismoAdministrador_SeRechazaYSiguePendiente` | Un Administrador no puede aprobar una solicitud creada por él mismo |
| 5 | RF-GP-06 | Se agrega la transición Aplicada → Rechazada | `ServicioGestionPermisosTests.Rechazar_SolicitudYaAplicada_SeRechazaYSigueAplicada` | Una solicitud ya aplicada no puede rechazarse y su estado sigue Aplicada |
| 6 | RF-GP-03 | Al aprobar, se deja de registrar quién aprobó y cuándo | `ServicioGestionPermisosTests.Aprobar_SolicitudPendiente_QuedaAplicadaYRegistraQuienAproboYCuando` | Al aprobar queda registrado el Administrador que aprobó y la fecha |

Para comprobar un defecto, por ejemplo el 3:

```bash
git checkout practica-2-defecto-3
git log -1 --format=%B
git diff practica-2 practica-2-defecto-3 --stat
dotnet test
```

`dotnet test` reporta al menos una prueba fallida, entre ellas la de la tabla. Para volver al estado
entregado, donde pasan todas: `git checkout practica-2`.

## Cómo verificar cada criterio

Las secciones siguen el orden de la revisión de la Práctica 1 y se hacen una detrás de otra, con la web
corriendo en una terminal y los comandos en otra. Cada `curl` imprime el cuerpo de la respuesta y, al
final, `-> <código HTTP>`. Debajo de cada bloque está lo que tiene que responder.

Las mismas peticiones, en el mismo orden, están en [`pruebas/practica-1.http`](pruebas/practica-1.http)
para usarlas desde VS Code (extensión REST Client), Rider o Visual Studio.

### Preparación

1. Completa el `.env` con el administrador inicial (`SAVESTOCK_ADMIN_*`) y un servidor SMTP real
   (`SMTP_*`), y arranca la web: `dotnet run --project src/SaveStock.Web`.
2. En la terminal donde vas a probar, define estas variables. `CORREO` es **tu correo real, en
   minúsculas**: ahí llegan el enlace de activación y los códigos. Los `ADMIN_*` son los mismos valores
   que pusiste en `SAVESTOCK_ADMIN_CORREO` y `SAVESTOCK_ADMIN_CONTRASENA`.

   ```bash
   API=http://localhost:5080
   CORREO=tu-correo@gmail.com
   CONTRASENA=clave1234
   ADMIN_CORREO=admin@tu-negocio.com
   ADMIN_CONTRASENA=la-del-env-1
   ```

Los demás usuarios de las pruebas usan el dominio reservado `example.com`: sus correos no llegan a
nadie, así que sus enlaces y códigos se leen de la cola con `sqlite3`.

**Si un correo tarda en llegar**, puedes leerlo directamente de la cola (la tabla `CorreosEnCola`
guarda el cuerpo del correo):

```bash
sqlite3 datos/savestock.db "select Id, Destinatario, Asunto, Estado from CorreosEnCola;"
sqlite3 datos/savestock.db "select Cuerpo from CorreosEnCola where Destinatario='$CORREO' order by Id desc limit 1;"
```

**Sin `jq`:** donde diga `jq -r .token` usa `sed -E 's/.*"token":"([^"]+)".*/\1/'`, y donde diga
`jq .id` usa `sed -E 's/.*"id":([0-9]+).*/\1/'`.

### 1. Registro y activación (RF-CA-01, RF-CA-02, RF-CA-14, RF-CA-15, RF-CA-16, RF-CA-17)

**1.1 Registrarte con tu propio correo (RF-CA-15).**

```bash
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/cuentas/registro" -H 'Content-Type: application/json' \
  -d "{\"nombre\":\"Tu nombre\",\"correo\":\"$CORREO\",\"contrasena\":\"$CONTRASENA\"}"
```

`{"mensaje":"Te enviamos un correo para activar tu cuenta."} -> 201`. El usuario nace inactivo y el
correo queda `Pendiente` en la cola; todavía no salió.

**1.2 Iniciar sesión antes de activar (RF-CA-15).**

```bash
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"contrasena\":\"$CONTRASENA\"}"
```

`{"mensaje":"La cuenta no está activa. Revisa tu correo para activarla."} -> 403`.

**1.3 Mandar el correo y abrir el enlace (RF-CA-16, RF-NOT-08).** En otra terminal, desde la raíz:

```bash
dotnet run --project src/SaveStock.Enviador
```

`Enviados: 1, fallidos: 0, pendientes: 0`. Llega a tu bandeja el correo «Activa tu cuenta de
SaveStock». Abre el enlace en el navegador: la página dice **«Tu cuenta fue activada. Ya puedes iniciar
sesión.»** (HTTP 200).

Para hacerlo desde la terminal, guarda el enlace en una variable (el mismo del correo, leído de la
cola) y ábrelo con `curl`:

```bash
ENLACE=$(sqlite3 datos/savestock.db "select Cuerpo from CorreosEnCola where Destinatario='$CORREO' order by Id desc limit 1;" | grep -o 'http[^ ]*activar?token=[A-Za-z0-9_-]*')
echo "$ENLACE"
curl -s -w ' -> %{http_code}\n' "$ENLACE"
```

**1.4 Abrir el enlace por segunda vez (RF-CA-16).** Recarga la página o:

```bash
curl -s -w ' -> %{http_code}\n' "$ENLACE"
```

La página dice **«El enlace no es válido, ya fue usado o venció.»** (HTTP 400) y la cuenta sigue
activada. Ahora el inicio de sesión de 1.2 responde `200` con `{"token":"...","expiraEn":"..."}`.

**1.5 Registrar el mismo correo otra vez (RF-CA-01).** También con otras mayúsculas: el correo se
guarda normalizado.

```bash
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/cuentas/registro" -H 'Content-Type: application/json' \
  -d "{\"nombre\":\"Otra vez\",\"correo\":\"$CORREO\",\"contrasena\":\"$CONTRASENA\"}"
```

`{"mensaje":"Ya existe una cuenta con ese correo."} -> 409`.

**1.6 Reenviar el enlace (RF-CA-17).** Se prueba con un segundo usuario, Luis, que todavía no activó su
cuenta. Luis usa **la misma contraseña que tú** (sirve para 1.9).

```bash
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/cuentas/registro" -H 'Content-Type: application/json' \
  -d "{\"nombre\":\"Luis\",\"correo\":\"luis@example.com\",\"contrasena\":\"$CONTRASENA\"}"
ENLACE_VIEJO=$(sqlite3 datos/savestock.db "select Cuerpo from CorreosEnCola where Destinatario='luis@example.com' order by Id desc limit 1;" | grep -o 'http[^ ]*activar?token=[A-Za-z0-9_-]*')

curl -s -w ' -> %{http_code}\n' -X POST "$API/api/cuentas/reenviar-activacion" -H 'Content-Type: application/json' \
  -d '{"correo":"luis@example.com"}'
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/cuentas/reenviar-activacion" -H 'Content-Type: application/json' \
  -d '{"correo":"nadie@example.com"}'
```

Las dos respuestas son idénticas, exista o no el correo:
`{"mensaje":"Si el correo está registrado y la cuenta no está activa, te enviamos un nuevo enlace."} -> 200`.
Solo el de Luis encola un correo nuevo. El reenvío invalida el enlace anterior:

```bash
ENLACE_NUEVO=$(sqlite3 datos/savestock.db "select Cuerpo from CorreosEnCola where Destinatario='luis@example.com' order by Id desc limit 1;" | grep -o 'http[^ ]*activar?token=[A-Za-z0-9_-]*')
curl -s -w ' -> %{http_code}\n' "$ENLACE_VIEJO"
curl -s -w ' -> %{http_code}\n' "$ENLACE_NUEVO"
```

El viejo: «El enlace no es válido, ya fue usado o venció.» `-> 400`. El nuevo: «Tu cuenta fue
activada. Ya puedes iniciar sesión.» `-> 200`.

**1.7 Enlace vencido (RF-CA-16), opcional.** El enlace vence a las 24 horas. Para no esperar, se vence a
mano en la base:

```bash
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/cuentas/registro" -H 'Content-Type: application/json' \
  -d "{\"nombre\":\"Marta\",\"correo\":\"marta@example.com\",\"contrasena\":\"$CONTRASENA\"}"
ENLACE_MARTA=$(sqlite3 datos/savestock.db "select Cuerpo from CorreosEnCola where Destinatario='marta@example.com' order by Id desc limit 1;" | grep -o 'http[^ ]*activar?token=[A-Za-z0-9_-]*')
sqlite3 datos/savestock.db "update TokensActivacion set FechaVencimiento='2020-01-01 00:00:00' where UsuarioId=(select Id from Usuarios where Correo='marta@example.com');"
curl -s -w ' -> %{http_code}\n' "$ENLACE_MARTA"
sqlite3 datos/savestock.db "select Correo, Activo, FechaActivacion from Usuarios where Correo='marta@example.com';"
```

«El enlace no es válido, ya fue usado o venció.» `-> 400`, y Marta sigue con `Activo` en `0` y sin
`FechaActivacion`.

**1.8 Contraseña de 5 caracteres y correo mal formado (RF-CA-14, RD-07).**

```bash
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/cuentas/registro" -H 'Content-Type: application/json' \
  -d '{"nombre":"Pedro","correo":"pedro@example.com","contrasena":"ab123"}'
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/cuentas/registro" -H 'Content-Type: application/json' \
  -d '{"nombre":"Pedro","correo":"pedro@","contrasena":"clave1234"}'
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/cuentas/registro" -H 'Content-Type: application/json' \
  -d '{"nombre":"Pedro","correo":"","contrasena":"clave1234"}'
```

| Caso | Respuesta |
|---|---|
| Contraseña de 5 caracteres | `{"mensaje":"La contraseña debe tener al menos 8 caracteres e incluir letras y números."} -> 400` |
| Correo mal formado | `{"mensaje":"El correo no tiene un formato válido."} -> 400` |
| Correo vacío | `{"mensaje":"El correo es obligatorio."} -> 400` |

Una contraseña de 8 letras sin números (`"solamenteletras"`) también da el mensaje de la política, y un
JSON roto da `{"mensaje":"La solicitud no es válida."} -> 400`. Nunca se ve una traza (RD-08).

**1.9 Leer el almacenamiento (RF-CA-02, RD-05).**

```bash
sqlite3 datos/savestock.db "select Correo, HashContrasena from Usuarios;"
```

Ninguna fila contiene la contraseña (`$CONTRASENA`). El valor guardado tiene la forma
`pbkdf2-sha256$100000$<sal>$<hash>`: PBKDF2-SHA256 con 100.000 iteraciones y una sal aleatoria de
16 bytes por usuario, que no se puede revertir a la contraseña. **Tu fila y la de Luis tienen la misma
contraseña y valores distintos**, porque cada una tiene su propia sal.

### 2. Sesión (RF-CA-03, RF-CA-07, RF-CA-18, RF-CA-19)

**2.1 Contraseña incorrecta y correo inexistente (RF-CA-03).**

```bash
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"contrasena\":\"incorrecta1\"}"
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"nadie@example.com\",\"contrasena\":\"$CONTRASENA\"}"
```

Los dos rechazos son idénticos y no dicen cuál dato falló:
`{"mensaje":"Correo o contraseña incorrectos."} -> 401`.

**2.2 Iniciar sesión y consultar quién soy (RF-CA-03, RF-CA-07).** La credencial de sesión es un token
que viaja en el encabezado `Authorization: Bearer <token>`.

```bash
TOKEN=$(curl -s -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"contrasena\":\"$CONTRASENA\"}" | jq -r .token)
```

Sin `jq`:

```bash
TOKEN=$(curl -s -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"contrasena\":\"$CONTRASENA\"}" | sed -E 's/.*"token":"([^"]+)".*/\1/')
```

```bash
echo "$TOKEN"
curl -s -w ' -> %{http_code}\n' "$API/api/sesion/yo" -H "Authorization: Bearer $TOKEN"
curl -s -w ' -> %{http_code}\n' "$API/api/sesion/yo"
curl -s -w ' -> %{http_code}\n' "$API/api/sesion/yo" -H "Authorization: Bearer inventado"
```

| Caso | Respuesta |
|---|---|
| Con el token | `{"id":2,"nombre":"Tu nombre","correo":"...","rol":"Estandar"} -> 200` |
| Sin token | `{"mensaje":"Necesitas iniciar sesión."} -> 401` |
| Con un token inventado | `{"mensaje":"Necesitas iniciar sesión."} -> 401` |

**2.3 Cerrar sesión y volver a usar la credencial (RF-CA-18).**

```bash
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/sesion/cerrar" -H "Authorization: Bearer $TOKEN"
curl -s -w ' -> %{http_code}\n' "$API/api/sesion/yo" -H "Authorization: Bearer $TOKEN"
```

`{"mensaje":"Sesión cerrada."} -> 200`, y después `{"mensaje":"Necesitas iniciar sesión."} -> 401`.

**2.4 Cinco fallos seguidos y luego la contraseña correcta (RF-CA-19).** Se prueba con Luis, para que
tu cuenta no quede bloqueada durante el resto de la revisión.

```bash
for i in 1 2 3 4 5; do
  curl -s -o /dev/null -w '%{http_code} ' -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
    -d '{"correo":"luis@example.com","contrasena":"incorrecta1"}'
done; echo
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"luis@example.com\",\"contrasena\":\"$CONTRASENA\"}"
```

`401 401 401 401 401`, y el sexto intento, aun con la contraseña correcta:
`{"mensaje":"La cuenta está bloqueada temporalmente por intentos fallidos. Intenta de nuevo más tarde."} -> 423`.
El bloqueo dura 15 minutos y se ve en la base:

```bash
sqlite3 datos/savestock.db "select Correo, IntentosFallidos, BloqueadoHasta from Usuarios where Correo='luis@example.com';"
```

**2.5 Un inicio de sesión correcto pone el contador en cero (RF-CA-19).** Con tu cuenta: cuatro fallos,
uno correcto y otro fallo. Si el contador no se reiniciara, ese sería el quinto fallo y bloquearía la
cuenta.

```bash
for i in 1 2 3 4; do
  curl -s -o /dev/null -w '%{http_code} ' -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
    -d "{\"correo\":\"$CORREO\",\"contrasena\":\"incorrecta1\"}"
done; echo
curl -s -o /dev/null -w '%{http_code}\n' -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"contrasena\":\"$CONTRASENA\"}"
curl -s -o /dev/null -w '%{http_code}\n' -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"contrasena\":\"incorrecta1\"}"
curl -s -o /dev/null -w '%{http_code}\n' -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"contrasena\":\"$CONTRASENA\"}"
```

`401 401 401 401`, `200`, `401`, `200`: la cuenta no se bloqueó.

### 3. Roles y administración (RF-CA-04, RF-CA-05, RF-CA-06, RF-CA-08, RF-CA-20, RF-CA-21, RD-06)

Todo usuario tiene exactamente un rol, `Estandar` o `Administrador` (RF-CA-04): es una columna
obligatoria de `Usuarios` y todo registro nace `Estandar`. La exigencia de rol de cada operación se lee
en un solo lugar, `src/SaveStock.Core/ControlAcceso/Autorizacion/Permisos.cs` (RF-CA-05), y la aplica
del lado del servidor el filtro `src/SaveStock.Web/Filtros/FiltroOperacion.cs` antes de ejecutar cada
endpoint.

**3.1 Como Estándar, invocar operaciones de Administrador construyendo la petición a mano (RF-CA-06,
RF-CA-08, RF-CA-21).**

```bash
TOKEN=$(curl -s -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"contrasena\":\"$CONTRASENA\"}" | jq -r .token)
MI_ID=$(curl -s "$API/api/sesion/yo" -H "Authorization: Bearer $TOKEN" | jq .id)
echo "$MI_ID"

curl -s -w ' -> %{http_code}\n' "$API/api/admin/usuarios" -H "Authorization: Bearer $TOKEN"
curl -s -w ' -> %{http_code}\n' -X PUT "$API/api/admin/usuarios/$MI_ID/rol" -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' -d '{"rol":"Administrador"}'
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/admin/usuarios/1/desactivar" -H "Authorization: Bearer $TOKEN"
```

Las tres: `{"mensaje":"No tienes permiso para realizar esta operación."} -> 403`. Incluido el cambio
de tu propio rol.

**3.2 Como Administrador, listar usuarios (RF-CA-21).** El administrador inicial lo crea la web con
las variables `SAVESTOCK_ADMIN_*`.

```bash
ADMIN=$(curl -s -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$ADMIN_CORREO\",\"contrasena\":\"$ADMIN_CONTRASENA\"}" | jq -r .token)
ADMIN_ID=$(curl -s "$API/api/sesion/yo" -H "Authorization: Bearer $ADMIN" | jq .id)
curl -s -w ' -> %{http_code}\n' "$API/api/admin/usuarios" -H "Authorization: Bearer $ADMIN"
```

`-> 200` con una lista como
`[{"id":1,"nombre":"Admin","correo":"...","rol":"Administrador","activo":true,"cuentaActivada":true}, ...]`:
id, nombre, correo, rol y estado. Nunca hashes ni tokens.

**3.3 Cambiar un rol (RF-CA-08).**

```bash
curl -s -w ' -> %{http_code}\n' -X PUT "$API/api/admin/usuarios/$MI_ID/rol" -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"rol":"Administrador"}'
curl -s -w ' -> %{http_code}\n' "$API/api/sesion/yo" -H "Authorization: Bearer $TOKEN"
curl -s -w ' -> %{http_code}\n' -X PUT "$API/api/admin/usuarios/$MI_ID/rol" -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"rol":"Estandar"}'
curl -s -w ' -> %{http_code}\n' -X PUT "$API/api/admin/usuarios/$ADMIN_ID/rol" -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"rol":"Estandar"}'
```

| Caso | Respuesta |
|---|---|
| Cambiar tu rol a Administrador | `{"mensaje":"El rol del usuario se actualizó."} -> 200` |
| Consultar tu usuario | `{..."rol":"Administrador"} -> 200` |
| Devolverlo a Estándar | `{"mensaje":"El rol del usuario se actualizó."} -> 200` |
| El Administrador cambia su propio rol | `{"mensaje":"No puedes cambiar tu propio rol."} -> 400` |

Un rol que no existe (`{"rol":"Jefe"}`) da `400` y un id inexistente, `404`.

**3.4 Desactivar un usuario con sesión abierta y probar esa sesión (RF-CA-20).** `$TOKEN` es tu
sesión, que sigue abierta:

```bash
curl -s -w ' -> %{http_code}\n' "$API/api/sesion/yo" -H "Authorization: Bearer $TOKEN"
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/admin/usuarios/$MI_ID/desactivar" -H "Authorization: Bearer $ADMIN"
curl -s -w ' -> %{http_code}\n' "$API/api/sesion/yo" -H "Authorization: Bearer $TOKEN"
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"contrasena\":\"$CONTRASENA\"}"
```

| Caso | Respuesta |
|---|---|
| Tu sesión antes | `{"id":...,"rol":"Estandar"} -> 200` |
| Desactivar | `{"mensaje":"El usuario fue desactivado y sus sesiones se cerraron."} -> 200` |
| Tu sesión después | `{"mensaje":"Necesitas iniciar sesión."} -> 401` |
| Iniciar sesión desactivado | `{"mensaje":"La cuenta está desactivada. Contacta a un administrador."} -> 403` |

**3.5 Intentar desactivarte a ti mismo y reactivar (RF-CA-20).**

```bash
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/admin/usuarios/$ADMIN_ID/desactivar" -H "Authorization: Bearer $ADMIN"
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/admin/usuarios/$MI_ID/reactivar" -H "Authorization: Bearer $ADMIN"
curl -s -o /dev/null -w '%{http_code}\n' -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"contrasena\":\"$CONTRASENA\"}"
```

`{"mensaje":"No puedes desactivar tu propia cuenta."} -> 400`, después
`{"mensaje":"El usuario fue reactivado."} -> 200`, y tu inicio de sesión vuelve a dar `200`.

### 4. Contraseñas (RF-CA-09, RF-CA-10, RF-CA-11, RF-CA-12, RF-CA-13, RF-CA-22)

**4.1 Pedir recuperación con un correo inexistente y con el tuyo (RF-CA-09).** Antes, abre una sesión
para comprobar en 4.4 que deja de servir.

```bash
TOKEN_VIEJO=$(curl -s -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"contrasena\":\"$CONTRASENA\"}" | jq -r .token)

curl -s -w ' -> %{http_code}\n' -X POST "$API/api/contrasena/recuperar" -H 'Content-Type: application/json' \
  -d '{"correo":"nadie@example.com"}'
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/contrasena/recuperar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\"}"
```

Las dos respuestas son idénticas:
`{"mensaje":"Si el correo está registrado, te enviamos un código para restablecer tu contraseña."} -> 200`.
Solo la segunda encola un correo.

**4.2 Recibir el código (RF-CA-10, RF-NOT-08).**

```bash
dotnet run --project src/SaveStock.Enviador
```

Llega el correo «Código para restablecer tu contraseña de SaveStock» con un código de 8 caracteres que
sirve una sola vez y vence en 15 minutos. Guárdalo en una variable, copiándolo del correo
(`CODIGO=ABCD2345`) o leyéndolo de la cola:

```bash
CODIGO=$(sqlite3 datos/savestock.db "select Cuerpo from CorreosEnCola where Destinatario='$CORREO' order by Id desc limit 1;" | sed -n 's/^Código: //p')
echo "$CODIGO"
```

**4.3 Usar el código y volver a usarlo (RF-CA-10, RF-CA-11).**

```bash
NUEVA=nueva5678
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/contrasena/restablecer" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"codigo\":\"$CODIGO\",\"contrasenaNueva\":\"$NUEVA\"}"
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/contrasena/restablecer" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"codigo\":\"$CODIGO\",\"contrasenaNueva\":\"otra9999\"}"
```

El primero: `{"mensaje":"Contraseña restablecida. Inicia sesión con tu contraseña nueva."} -> 200`. El
segundo: `{"mensaje":"El código no es válido o ya venció."} -> 400`, y la contraseña no cambia. Un
código vencido da el mismo `400`.

**4.4 Contraseña vieja, contraseña nueva y una credencial emitida antes del cambio (RF-CA-11,
RF-CA-12).**

```bash
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"contrasena\":\"$CONTRASENA\"}"
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"contrasena\":\"$NUEVA\"}"
curl -s -w ' -> %{http_code}\n' "$API/api/sesion/yo" -H "Authorization: Bearer $TOKEN_VIEJO"
```

| Caso | Respuesta |
|---|---|
| Contraseña vieja | `{"mensaje":"Correo o contraseña incorrectos."} -> 401` |
| Contraseña nueva | `{"token":"...","expiraEn":"..."} -> 200` |
| Sesión abierta antes del restablecimiento | `{"mensaje":"Necesitas iniciar sesión."} -> 401` |

**4.5 Forzar el restablecimiento como Administrador (RF-CA-13).** Se fuerza el tuyo, para que recibas
el correo. Antes abre una sesión con tu contraseña actual:

```bash
TOKEN=$(curl -s -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"contrasena\":\"$NUEVA\"}" | jq -r .token)

curl -s -w ' -> %{http_code}\n' -X POST "$API/api/admin/usuarios/$MI_ID/forzar-restablecimiento" -H "Authorization: Bearer $ADMIN"
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"contrasena\":\"$NUEVA\"}"
curl -s -w ' -> %{http_code}\n' "$API/api/sesion/yo" -H "Authorization: Bearer $TOKEN"
```

| Caso | Respuesta |
|---|---|
| Forzar | `{"mensaje":"Se restableció la contraseña y se le envió al usuario un código para definir una nueva."} -> 200` |
| Tu contraseña anterior | `{"mensaje":"Correo o contraseña incorrectos."} -> 401` |
| Tu sesión abierta | `{"mensaje":"Necesitas iniciar sesión."} -> 401` |

Un Estándar recibe `403` en esta operación y un id inexistente da `404`. El código llega por la cola:

```bash
dotnet run --project src/SaveStock.Enviador
```

Llega «Un administrador restableció tu contraseña de SaveStock». Con ese código defines la nueva:

```bash
CODIGO=$(sqlite3 datos/savestock.db "select Cuerpo from CorreosEnCola where Destinatario='$CORREO' order by Id desc limit 1;" | sed -n 's/^Código: //p')
FINAL=final2468
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/contrasena/restablecer" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"codigo\":\"$CODIGO\",\"contrasenaNueva\":\"$FINAL\"}"
```

`{"mensaje":"Contraseña restablecida. Inicia sesión con tu contraseña nueva."} -> 200`.

**4.6 Cambiar tu contraseña con sesión (RF-CA-22, RF-CA-14, RF-CA-12).**

```bash
TOKEN=$(curl -s -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"contrasena\":\"$FINAL\"}" | jq -r .token)

curl -s -w ' -> %{http_code}\n' -X PUT "$API/api/contrasena/cambiar" -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' -d '{"contrasenaActual":"equivocada1","contrasenaNueva":"otra9999"}'
curl -s -w ' -> %{http_code}\n' -X PUT "$API/api/contrasena/cambiar" -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' -d "{\"contrasenaActual\":\"$FINAL\",\"contrasenaNueva\":\"12345678\"}"
curl -s -w ' -> %{http_code}\n' -X PUT "$API/api/contrasena/cambiar" -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' -d "{\"contrasenaActual\":\"$FINAL\",\"contrasenaNueva\":\"otra9999\"}"
curl -s -w ' -> %{http_code}\n' "$API/api/sesion/yo" -H "Authorization: Bearer $TOKEN"
```

| Caso | Respuesta |
|---|---|
| Contraseña actual incorrecta | `{"mensaje":"La contraseña actual no es correcta."} -> 400` |
| Nueva sin letras | `{"mensaje":"La contraseña debe tener al menos 8 caracteres e incluir letras y números."} -> 400` |
| Cambio correcto | `{"mensaje":"Contraseña actualizada. Inicia sesión de nuevo."} -> 200` |
| La sesión con la que cambiaste | `{"mensaje":"Necesitas iniciar sesión."} -> 401` |

Desde aquí tu contraseña es `otra9999`.

### 5. Correo por cola con el servidor SMTP apagado (RF-NOT-08, RF-NOT-09, RF-NOT-12, RF-NOT-13)

La web nunca se conecta al servidor SMTP: solo encola. Para simular que el servidor no responde, el
enviador se ejecuta con `SMTP_HOST` apuntando a un servidor que no existe. La variable del comando gana
sobre la del `.env`, así que tu configuración real no cambia.

**5.1 Registrar un usuario con el SMTP apagado.** También se pide una recuperación de tu correo, para
ver después que te llega una sola vez.

```bash
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/cuentas/registro" -H 'Content-Type: application/json' \
  -d "{\"nombre\":\"Sin SMTP\",\"correo\":\"sinsmtp@example.com\",\"contrasena\":\"$CONTRASENA\"}"
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/contrasena/recuperar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\"}"
sqlite3 datos/savestock.db "select Id, Destinatario, Estado, Intentos, FechaEnvio, UltimoError from CorreosEnCola where Estado <> 'Enviado';"
```

El registro termina bien (`-> 201`) y los dos correos quedan en la cola como `Pendiente`, con
`Intentos` en `0`.

**5.2 Ejecutar el enviador dos veces sin servidor.**

```bash
SMTP_HOST=smtp.invalido dotnet run --project src/SaveStock.Enviador
SMTP_HOST=smtp.invalido dotnet run --project src/SaveStock.Enviador
sqlite3 datos/savestock.db "select Id, Destinatario, Estado, Intentos, FechaEnvio, UltimoError from CorreosEnCola where Estado <> 'Enviado';"
```

Cada ejecución imprime, por cada correo,
`No se pudo enviar el correo N a sinsmtp@example.com: No se pudo conectar con el servidor SMTP smtp.invalido:587. Queda Pendiente.`
y termina con `Enviados: 0, fallidos: 2, pendientes: 2` (más, si quedaban otros pendientes de antes).
En la base siguen `Pendiente`, con `Intentos` en `2` y el error en `UltimoError`. Nada se perdió y no
hubo traza.

**5.3 Volver a encender el servidor y ejecutar el enviador dos veces.**

```bash
dotnet run --project src/SaveStock.Enviador
dotnet run --project src/SaveStock.Enviador
sqlite3 datos/savestock.db "select Id, Destinatario, Estado, Intentos, FechaEnvio from CorreosEnCola;"
```

La primera ejecución: `Enviados: 2, fallidos: 0, pendientes: 0`. La segunda:
`Enviados: 0, fallidos: 0, pendientes: 0`, porque solo toma los `Pendiente` (RF-NOT-12). Todos los
correos quedan `Enviado` con su `FechaEnvio`, y el código de recuperación te llega **una sola vez**.

**5.4 Credenciales fuera del repositorio (RF-NOT-13, RD-10).**

```bash
SMTP_HOST= dotnet run --project src/SaveStock.Enviador; echo "código de salida: $?"
git ls-files | grep -i env
git log --all --oneline -- .env
```

Con `SMTP_HOST` vacía, el enviador responde
`Falta configurar SMTP_HOST. Agrégalas al archivo .env (mira .env.example) o como variables de entorno.`
y `código de salida: 1`: las credenciales solo salen del entorno o del `.env`. En el repositorio solo está `.env.example`, sin valores, y el `.env` nunca se
subió (el `git log` sale vacío).

### 6. Reiniciar la aplicación (RD-09)

En la terminal de la web, detenla con `Ctrl+C` y vuelve a arrancarla:

```bash
dotnet run --project src/SaveStock.Web
```

```bash
sqlite3 datos/savestock.db "select Id, Correo, Rol, Activo from Usuarios;"
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$CORREO\",\"contrasena\":\"otra9999\"}"
```

Siguen todos los usuarios (el administrador inicial no se duplica) y tu inicio de sesión responde
`200`: los datos viven en `datos/savestock.db`, fuera del proceso.

### 7. Máquina de estados de la orden de compra (RF-NEG-03, RF-NEG-04, RF-NEG-05, RD-04)

La tabla de transiciones (desde, hacia, quién la ejecuta, condición) está en
[`docs/maquina-de-estados.md`](docs/maquina-de-estados.md). En el código:

| Qué | Dónde |
|---|---|
| Entidad central `OrdenDeCompra`, con su atributo `Estado` | `src/SaveStock.Inventario/OrdenesDeCompra/OrdenDeCompra.cs` |
| Los 4 estados (Borrador, Enviada, Recibida, Cancelada), en un solo lugar (RF-NEG-03) | `src/SaveStock.Inventario/OrdenesDeCompra/EstadoOrdenCompra.cs` |
| Transiciones permitidas, la prohibida explícita Recibida → Borrador (RF-NEG-04) y los estados terminales Recibida y Cancelada (RF-NEG-05), en un solo punto (RD-04) | `src/SaveStock.Inventario/OrdenesDeCompra/MaquinaEstadosOrdenCompra.cs` |
| Tabla `OrdenesDeCompra` en `datos/inventario.db` | `src/SaveStock.Inventario/Datos/InventarioDbContext.cs` |

```bash
sqlite3 datos/inventario.db ".schema OrdenesDeCompra"
```

### 8. Gestión de permisos (RF-GP-01 a RF-GP-07, RD-04, RD-06, RD-07, RD-09)

Un usuario Estándar pide el permiso `VerListadoUsuarios` (ver `GET /api/admin/usuarios`, que por rol
es solo de Administrador) y un Administrador lo aprueba o lo rechaza. La tabla de estados y
transiciones, los endpoints y todos los mensajes de error están en
[`docs/gestion-de-permisos.md`](docs/gestion-de-permisos.md):

| Desde | Hacia | Quién la ejecuta | Condición |
|---|---|---|---|
| Pendiente | Aprobada | Administrador | La solicitud está en Pendiente. |
| Aprobada | Aplicada | El sistema | Automática, inmediatamente después de aprobar. |
| Pendiente | Rechazada | Administrador | Exige un motivo escrito. |
| Cualquier otra combinación | — | — | Prohibida. El sistema la rechaza y el estado no cambia. |

Las transiciones se resuelven en un solo punto, `src/SaveStock.Core/GestionPermisos/MaquinaEstadosSolicitudPermiso.cs`
(RD-04): es el único código que cambia el estado de una solicitud.

> **Si ya habías ejecutado una versión anterior**, detén la web y borra la carpeta `datos/` antes de
> arrancarla (`rm -rf datos`). La base se crea con `EnsureCreated`, que no agrega tablas nuevas
> (`SolicitudesPermiso`, `PermisosOtorgados`) a un archivo que ya existe. En un clon limpio no hace falta.

Esta sección es independiente de las anteriores: solo necesita la web corriendo y las variables
`API`, `ADMIN_CORREO`, `ADMIN_CONTRASENA` y `CONTRASENA` de la [Preparación](#preparación).

**8.1 Preparar dos usuarios Estándar y el Administrador.** Sofía y Tito usan `example.com`, así que su
enlace de activación se lee de la cola:

```bash
for NOMBRE in sofia tito; do
  curl -s -o /dev/null -X POST "$API/api/cuentas/registro" -H 'Content-Type: application/json' \
    -d "{\"nombre\":\"$NOMBRE\",\"correo\":\"$NOMBRE@example.com\",\"contrasena\":\"$CONTRASENA\"}"
  ENLACE=$(sqlite3 datos/savestock.db "select Cuerpo from CorreosEnCola where Destinatario='$NOMBRE@example.com' order by Id desc limit 1;" | grep -o 'http[^ ]*activar?token=[A-Za-z0-9_-]*')
  curl -s -o /dev/null "$ENLACE"
done
SOFIA=$(curl -s -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"sofia@example.com\",\"contrasena\":\"$CONTRASENA\"}" | jq -r .token)
TITO=$(curl -s -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"tito@example.com\",\"contrasena\":\"$CONTRASENA\"}" | jq -r .token)
ADMIN=$(curl -s -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"$ADMIN_CORREO\",\"contrasena\":\"$ADMIN_CONTRASENA\"}" | jq -r .token)
echo "$SOFIA $TITO $ADMIN"
```

Se imprimen tres tokens. Los ids y nombres de las respuestas de abajo son los de una base recién
creada con el administrador `Admin` (id 1); en la tuya pueden ser otros.

**8.2 Crear una solicitud: nace en Pendiente (RF-GP-01, RD-07).** Antes, Sofía no puede listar usuarios:

```bash
curl -s -w ' -> %{http_code}\n' "$API/api/admin/usuarios" -H "Authorization: Bearer $SOFIA"
SOLICITUD=$(curl -s -X POST "$API/api/permisos/solicitudes" -H "Authorization: Bearer $SOFIA" \
  -H 'Content-Type: application/json' -d '{"permiso":"VerListadoUsuarios"}' | tee /dev/stderr | jq .id)
echo; echo "$SOLICITUD"
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/permisos/solicitudes" -H "Authorization: Bearer $SOFIA" \
  -H 'Content-Type: application/json' -d '{"permiso":"VerListadoUsuarios"}'
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/permisos/solicitudes" -H "Authorization: Bearer $SOFIA" \
  -H 'Content-Type: application/json' -d '{"permiso":"SerAdministrador"}'
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/permisos/solicitudes" -H "Authorization: Bearer $SOFIA" \
  -H 'Content-Type: application/json' -d '{"permiso":'
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/permisos/solicitudes" \
  -H 'Content-Type: application/json' -d '{"permiso":"VerListadoUsuarios"}'
```

| Caso | Respuesta |
|---|---|
| Sofía lista usuarios antes de pedir el permiso | `{"mensaje":"No tienes permiso para realizar esta operación."} -> 403` |
| Crear la solicitud | `{"id":1,"solicitante":{"id":2,"nombre":"sofia"},"permiso":"VerListadoUsuarios","estado":"Pendiente","aprobador":null,...}` y el id |
| Pedir lo mismo otra vez | `{"mensaje":"Ya tienes una solicitud pendiente para ese permiso."} -> 409` |
| Un permiso que no existe | `{"mensaje":"El permiso solicitado no existe. Permisos válidos: VerListadoUsuarios."} -> 400` |
| JSON mal formado | `{"mensaje":"La solicitud no es válida."} -> 400` |
| Sin sesión | `{"mensaje":"Necesitas iniciar sesión."} -> 401` |

**8.3 Un Estándar no ve las solicitudes de otros (RF-GP-02, RD-06).**

```bash
curl -s -w ' -> %{http_code}\n' "$API/api/permisos/solicitudes/mias" -H "Authorization: Bearer $TITO"
curl -s -w ' -> %{http_code}\n' "$API/api/permisos/solicitudes/pendientes" -H "Authorization: Bearer $TITO"
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/permisos/solicitudes/$SOLICITUD/aprobar" -H "Authorization: Bearer $TITO"
curl -s -w ' -> %{http_code}\n' "$API/api/permisos/solicitudes/mias" -H "Authorization: Bearer $SOFIA"
```

| Caso | Respuesta |
|---|---|
| Las solicitudes de Tito | `[] -> 200`: la de Sofía no aparece |
| Tito pide las pendientes de todos | `{"mensaje":"No tienes permiso para realizar esta operación."} -> 403` |
| Tito intenta aprobar | `{"mensaje":"No tienes permiso para realizar esta operación."} -> 403` |
| Las solicitudes de Sofía | `[{"id":1,...,"estado":"Pendiente",...}] -> 200`: solo la suya |

**8.4 El Administrador consulta las pendientes (RF-GP-02).**

```bash
curl -s -w ' -> %{http_code}\n' "$API/api/permisos/solicitudes/pendientes" -H "Authorization: Bearer $ADMIN"
```

`[{"id":1,"solicitante":{"id":2,"nombre":"sofia"},"permiso":"VerListadoUsuarios","estado":"Pendiente",...}] -> 200`.

**8.5 Aprobar: queda registrado quién y cuándo (RF-GP-03), y el permiso se aplica solo (RF-GP-05).**

```bash
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/permisos/solicitudes/$SOLICITUD/aprobar" -H "Authorization: Bearer $ADMIN"
curl -s -w ' -> %{http_code}\n' "$API/api/admin/usuarios" -H "Authorization: Bearer $SOFIA"
curl -s -w ' -> %{http_code}\n' "$API/api/admin/usuarios" -H "Authorization: Bearer $TITO"
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/admin/usuarios/1/desactivar" -H "Authorization: Bearer $SOFIA"
sqlite3 datos/savestock.db "select Id, SolicitanteId, Permiso, Estado, AprobadorId, FechaResolucion, FechaAplicacion from SolicitudesPermiso;"
sqlite3 datos/savestock.db "select UsuarioId, Permiso, SolicitudId, FechaOtorgamiento from PermisosOtorgados;"
```

| Caso | Respuesta |
|---|---|
| Aprobar | `{"id":1,...,"estado":"Aplicada","aprobador":{"id":1,"nombre":"Admin"},...,"fechaResolucion":"2026-...Z",...,"fechaAplicacion":"2026-...Z"} -> 200` |
| Sofía lista usuarios **sin volver a iniciar sesión** | `[{"id":1,"nombre":"Admin",...}, ...] -> 200` (antes era `403`) |
| Tito, que no pidió nada, lista usuarios | `-> 403` |
| Sofía intenta otra operación de Administrador | `-> 403`: el permiso habilita solo el listado |
| La base | `1\|2\|VerListadoUsuarios\|Aplicada\|1\|<fecha>\|<fecha>` y `2\|VerListadoUsuarios\|1\|<fecha>` |

**8.6 Aprobar dos veces se rechaza y el estado no cambia (RF-GP-06).**

```bash
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/permisos/solicitudes/$SOLICITUD/aprobar" -H "Authorization: Bearer $ADMIN"
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/permisos/solicitudes/$SOLICITUD/rechazar" -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"motivo":"Ya no lo necesita."}'
sqlite3 datos/savestock.db "select Id, Estado, AprobadorId, FechaResolucion, MotivoRechazo from SolicitudesPermiso where Id=$SOLICITUD;"
```

`{"mensaje":"La solicitud está en estado Aplicada y no se puede aprobar."} -> 409`, después
`{"mensaje":"La solicitud está en estado Aplicada y no se puede rechazar."} -> 409`, y la base sigue
igual que en 8.5: `Aplicada`, el mismo aprobador, la misma fecha y sin motivo.

**8.7 Rechazar sin motivo no se completa; con motivo, sí (RF-GP-04).** Tito crea su solicitud:

```bash
SOLICITUD_TITO=$(curl -s -X POST "$API/api/permisos/solicitudes" -H "Authorization: Bearer $TITO" \
  -H 'Content-Type: application/json' -d '{"permiso":"VerListadoUsuarios"}' | jq .id)
echo "$SOLICITUD_TITO"
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/permisos/solicitudes/$SOLICITUD_TITO/rechazar" -H "Authorization: Bearer $ADMIN"
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/permisos/solicitudes/$SOLICITUD_TITO/rechazar" -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"motivo":"   "}'
sqlite3 datos/savestock.db "select Id, Estado, AprobadorId, MotivoRechazo from SolicitudesPermiso where Id=$SOLICITUD_TITO;"
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/permisos/solicitudes/$SOLICITUD_TITO/rechazar" -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"motivo":"Su puesto no necesita ver a los usuarios."}'
```

| Caso | Respuesta |
|---|---|
| Rechazar sin cuerpo | `{"mensaje":"El rechazo necesita un motivo escrito."} -> 400` |
| Rechazar con un motivo en blanco | `{"mensaje":"El rechazo necesita un motivo escrito."} -> 400` |
| La base | `2\|Pendiente\|\|`: sigue Pendiente, sin aprobador ni motivo |
| Rechazar con motivo | `{"id":2,...,"estado":"Rechazada","aprobador":{"id":1,"nombre":"Admin"},...,"motivoRechazo":"Su puesto no necesita ver a los usuarios.",...} -> 200` |

**8.8 De Rechazada no se puede aprobar ni aplicar (RF-GP-06).**

```bash
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/permisos/solicitudes/$SOLICITUD_TITO/aprobar" -H "Authorization: Bearer $ADMIN"
sqlite3 datos/savestock.db "select Id, Estado, FechaAplicacion from SolicitudesPermiso where Id=$SOLICITUD_TITO;"
curl -s -w ' -> %{http_code}\n' "$API/api/admin/usuarios" -H "Authorization: Bearer $TITO"
```

`{"mensaje":"La solicitud está en estado Rechazada y no se puede aprobar."} -> 409`, la base sigue en
`2|Rechazada|` (nunca se aplicó) y Tito sigue recibiendo `403` en el listado.

**8.9 Nadie resuelve su propia solicitud (RF-GP-07).** El Administrador crea una solicitud propia e
intenta resolverla:

```bash
SOLICITUD_ADMIN=$(curl -s -X POST "$API/api/permisos/solicitudes" -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"permiso":"VerListadoUsuarios"}' | jq .id)
echo "$SOLICITUD_ADMIN"
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/permisos/solicitudes/$SOLICITUD_ADMIN/aprobar" -H "Authorization: Bearer $ADMIN"
curl -s -w ' -> %{http_code}\n' -X POST "$API/api/permisos/solicitudes/$SOLICITUD_ADMIN/rechazar" -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{"motivo":"Me la rechazo yo."}'
sqlite3 datos/savestock.db "select Id, Estado, AprobadorId from SolicitudesPermiso where Id=$SOLICITUD_ADMIN;"
```

Las dos: `{"mensaje":"No puedes resolver tu propia solicitud."} -> 403`, y la solicitud sigue
`Pendiente` y sin aprobador (`3|Pendiente|`). Un id que no existe da
`{"mensaje":"No existe una solicitud de permiso con ese id."} -> 404`.

**8.10 Las solicitudes sobreviven a reiniciar (RD-09).** Detén la web con `Ctrl+C`, vuelve a arrancarla
con `dotnet run --project src/SaveStock.Web` y repite:

```bash
SOFIA=$(curl -s -X POST "$API/api/sesion/iniciar" -H 'Content-Type: application/json' \
  -d "{\"correo\":\"sofia@example.com\",\"contrasena\":\"$CONTRASENA\"}" | jq -r .token)
curl -s -w ' -> %{http_code}\n' "$API/api/permisos/solicitudes/mias" -H "Authorization: Bearer $SOFIA"
curl -s -o /dev/null -w '%{http_code}\n' "$API/api/admin/usuarios" -H "Authorization: Bearer $SOFIA"
sqlite3 datos/savestock.db "select Id, SolicitanteId, Permiso, Estado, AprobadorId, MotivoRechazo from SolicitudesPermiso;"
```

La solicitud de Sofía sigue `Aplicada` con su aprobador y sus fechas, Sofía sigue pudiendo listar
usuarios (`200`) y la base muestra las tres solicitudes: `Aplicada`, `Rechazada` con su motivo y
`Pendiente`. Viven en `datos/savestock.db`, fuera del proceso.

## Dónde está cada cosa

| Requisito | Archivo |
|---|---|
| RF-CA-01, RF-CA-15, RF-CA-16, RF-CA-17 (registro, activación, reenvío) | `src/SaveStock.Core/ControlAcceso/Registro/ServicioRegistro.cs` |
| RF-CA-02, RD-05 (hash con sal) | `src/SaveStock.Core/ControlAcceso/Seguridad/HasherContrasenas.cs` |
| RF-CA-14 (política de contraseña) | `src/SaveStock.Core/ControlAcceso/Seguridad/PoliticaContrasena.cs` |
| RD-07 (validación de entradas) | `src/SaveStock.Core/Comun/ValidadorEntrada.cs` |
| RF-CA-03, RF-CA-18, RF-CA-19 (inicio y cierre de sesión, bloqueo) | `src/SaveStock.Core/ControlAcceso/InicioSesion/ServicioInicioSesion.cs` |
| RF-CA-07, RF-CA-12 (validar y revocar sesiones) | `src/SaveStock.Core/ControlAcceso/Sesiones/GestorSesiones.cs` |
| RF-CA-04 (dos roles, uno por usuario) | `src/SaveStock.Core/ControlAcceso/Entidades/Rol.cs` y `Usuario.cs` |
| **RF-CA-05 (exigencia de rol de cada operación, en un solo lugar)** | **`src/SaveStock.Core/ControlAcceso/Autorizacion/Permisos.cs`** |
| RF-CA-06, RD-06 (rechazo del lado del servidor) | `src/SaveStock.Web/Filtros/FiltroOperacion.cs` y `src/SaveStock.Core/ControlAcceso/Autorizacion/Autorizador.cs` |
| RF-CA-08, RF-CA-20, RF-CA-21 (cambio de rol, desactivación, listado) | `src/SaveStock.Core/ControlAcceso/Administracion/ServicioAdministracionUsuarios.cs` |
| RF-CA-09 a RF-CA-13, RF-CA-22 (recuperación, restablecimiento, cambio) | `src/SaveStock.Core/ControlAcceso/Contrasenas/ServicioContrasenas.cs` |
| RF-NOT-08 (encolar el correo) | `src/SaveStock.Core/Correo/CorreoCola.cs` y `CorreoEnCola.cs` |
| RF-NOT-09, RF-NOT-12 (enviador independiente, sin duplicados) | `src/SaveStock.Core/Correo/ProcesadorColaCorreos.cs` y `src/SaveStock.Enviador/` |
| RF-NOT-13, RD-10 (configuración por variables de entorno) | `src/SaveStock.Core/Comun/ConfiguracionSaveStock.cs` y `.env.example` |
| RD-08 (errores sin detalles internos) | `src/SaveStock.Web/Errores/ManejoErrores.cs` |
| RD-09 (persistencia en SQLite) | `src/SaveStock.Core/Datos/CoreDbContext.cs` |
| RD-11 (fechas en UTC con un solo reloj) | `src/SaveStock.Core/Comun/IReloj.cs` |
| RF-GP-01 a RF-GP-04, RF-GP-07 (crear, consultar, aprobar, rechazar, nadie resuelve la suya) | `src/SaveStock.Core/GestionPermisos/ServicioGestionPermisos.cs` |
| **RF-GP-06, RD-04 (transiciones de la solicitud, en un solo punto)** | **`src/SaveStock.Core/GestionPermisos/MaquinaEstadosSolicitudPermiso.cs`** |
| RF-GP-05 (el permiso aprobado habilita la operación) | `Permisos.OperacionesPorPermiso` en `Permisos.cs` y `Autorizador.cs` |
| Endpoints de gestión de permisos | `src/SaveStock.Web/Endpoints/GestionPermisosEndpoints.cs` |
| RD-12 (pruebas sin levantar la aplicación) | `tests/SaveStock.Tests/Unitarias/` (Gestión de permisos en `Unitarias/GestionPermisos/`) |
| Endpoints (solo traducen HTTP, RD-02) | `src/SaveStock.Web/Endpoints/` |

## Estructura del proyecto

Una sola solución (`SaveStock.slnx`) con cuatro proyectos y uno de pruebas. Cada uno tiene una
responsabilidad (RD-01) y la dependencia va en un solo sentido: el Core no referencia al inventario
(RD-03), así que se construye y funciona sin él.

| Proyecto | Qué hace | Depende de |
|---|---|---|
| `src/SaveStock.Core` | Biblioteca del Core: control de acceso (registro, sesión, roles, contraseñas), cola de correos, seguridad y datos (`datos/savestock.db`). Toda la lógica de negocio vive aquí (RD-02). | — |
| `src/SaveStock.Inventario` | Módulo de negocio: la orden de compra y su máquina de estados, con su propia base (`datos/inventario.db`). | — |
| `src/SaveStock.Web` | Aplicación ASP.NET Core: la API en `/api`, las páginas y la documentación en `/scalar`. Solo traduce HTTP a llamadas a los servicios y aplica los permisos. | Core, Inventario |
| `src/SaveStock.Enviador` | Consola aparte que manda por SMTP los correos pendientes de la cola y termina (RF-NOT-09). | Core |
| `tests/SaveStock.Tests` | Pruebas xUnit: unitarias de los servicios del Core e integración de la API. | Core, Web |

```text
SaveStock.slnx
Directory.Build.props      configuración común (net10.0, versión)
.env.example               nombres de las variables de entorno, sin valores
docs/                      maquina-de-estados.md, gestion-de-permisos.md
src/SaveStock.Core/        Comun/, ControlAcceso/, Correo/, Datos/, GestionPermisos/
src/SaveStock.Inventario/  OrdenesDeCompra/, Datos/
src/SaveStock.Web/         Endpoints/, Filtros/, Errores/, Pages/
src/SaveStock.Enviador/
tests/SaveStock.Tests/     Unitarias/, Integracion/, Apoyo/
```

## Entrega

La Asignación 2 (Gestión de permisos) se entrega en la etiqueta `asignacion-2` (versión `1.1.0`), y
su bitácora está en `docs/bitacora-asignacion-2.md`:

```bash
git checkout asignacion-2
```

La Práctica 1 sigue en la etiqueta `practica-1` (versión `1.0.0`):

```bash
git checkout practica-1
```
