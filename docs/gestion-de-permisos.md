# Gestión de permisos

Un usuario Estándar puede pedir un permiso que su rol no le da. En esta versión el único permiso que
se puede pedir es **`VerListadoUsuarios`**, que habilita la operación `ListarUsuarios`
(`GET /api/admin/usuarios`), que por rol es solo de Administrador. Un Administrador aprueba o rechaza
la solicitud, y al aprobarla el sistema aplica el permiso sin ningún paso manual.

## Estados

| Estado | Qué significa |
|---|---|
| **Pendiente** | Estado inicial. Espera que un Administrador la resuelva (RF-GP-01). |
| **Aprobada** | Un Administrador la aprobó. Queda registrado quién y cuándo (RF-GP-03). |
| **Aplicada** | El sistema otorgó el permiso al solicitante (RF-GP-05). Estado terminal. |
| **Rechazada** | Un Administrador la rechazó con un motivo escrito (RF-GP-04). Estado terminal. |

## Transiciones permitidas (RF-GP-06, RD-04)

| Desde | Hacia | Quién la ejecuta | Condición | Qué deja registrado |
|---|---|---|---|---|
| Pendiente | Aprobada | Administrador | La solicitud está en Pendiente. | `AprobadorId` y `FechaResolucion` |
| Aprobada | Aplicada | El sistema | Automática, inmediatamente después de aprobar. | `FechaAplicacion` y una fila en `PermisosOtorgados` |
| Pendiente | Rechazada | Administrador | Exige un motivo escrito. | `AprobadorId`, `FechaResolucion` y `MotivoRechazo` |
| Cualquier otra combinación | — | — | Prohibida. El sistema la rechaza y el estado no cambia. | Nada |

Además, nadie resuelve su propia solicitud (RF-GP-07): un Administrador que intenta aprobar o
rechazar una solicitud creada por él mismo recibe `403` y la solicitud no cambia.

Aprobar hace las dos primeras transiciones juntas y las guarda con un solo `SaveChanges` (una
transacción): la respuesta de aprobar ya trae el estado `Aplicada`.

## Dónde está en el código

| Qué | Dónde |
|---|---|
| Estados, en un solo lugar | `src/SaveStock.Core/GestionPermisos/EstadoSolicitudPermiso.cs` |
| **Tabla de transiciones y único código que cambia el estado (RD-04)** | **`src/SaveStock.Core/GestionPermisos/MaquinaEstadosSolicitudPermiso.cs`** |
| Entidades `SolicitudPermiso` y `PermisoOtorgado` | `src/SaveStock.Core/GestionPermisos/` |
| Crear, listar, aprobar y rechazar, y la regla RF-GP-07 | `src/SaveStock.Core/GestionPermisos/ServicioGestionPermisos.cs` |
| Qué operación habilita cada permiso | `Permisos.OperacionesPorPermiso` en `src/SaveStock.Core/ControlAcceso/Autorizacion/Permisos.cs` |
| La decisión de autorización (rol o permiso otorgado) | `src/SaveStock.Core/ControlAcceso/Autorizacion/Autorizador.cs` |
| Endpoints | `src/SaveStock.Web/Endpoints/GestionPermisosEndpoints.cs` |

Esta máquina es independiente de la de la orden de compra (`docs/maquina-de-estados.md`): no
comparte estados, tabla ni código con ella.

## Endpoints

| Método y ruta | Quién | Respuesta |
|---|---|---|
| `POST /api/permisos/solicitudes` `{"permiso":"VerListadoUsuarios"}` | Cualquier usuario con sesión | `201` con la solicitud en `Pendiente` |
| `GET /api/permisos/solicitudes/mias` | Cualquier usuario con sesión | `200` con solo sus propias solicitudes |
| `GET /api/permisos/solicitudes/pendientes` | Administrador | `200` con las pendientes de todos |
| `POST /api/permisos/solicitudes/{id}/aprobar` | Administrador | `200` con la solicitud en `Aplicada` |
| `POST /api/permisos/solicitudes/{id}/rechazar` `{"motivo":"..."}` | Administrador | `200` con la solicitud en `Rechazada` |

| Error | Código | Mensaje |
|---|---|---|
| Permiso que no está en el catálogo | `400` | `El permiso solicitado no existe. Permisos válidos: VerListadoUsuarios.` |
| JSON mal formado | `400` | `La solicitud no es válida.` |
| Rechazo sin motivo | `400` | `El rechazo necesita un motivo escrito.` |
| Resolver la propia solicitud | `403` | `No puedes resolver tu propia solicitud.` |
| Id que no existe | `404` | `No existe una solicitud de permiso con ese id.` |
| Transición fuera de la tabla | `409` | `La solicitud está en estado <Estado> y no se puede aprobar.` (o `rechazar`) |
| Ya hay una solicitud pendiente del mismo permiso | `409` | `Ya tienes una solicitud pendiente para ese permiso.` |
| El permiso ya está otorgado | `409` | `Ya tienes ese permiso.` |
