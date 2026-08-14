# Dynamic Promotions

La bandeja de promociones es la fuente de verdad. Push y correo son canales adicionales: si un canal no está disponible, la promoción sigue apareciendo en `GET /api/users/me/promotions`.

## Flujo del propietario

### Previsualizar audiencia antes de enviar

`POST /api/promotions/negocios/{negocioId}/campaigns/audience-preview`

Requiere JWT con rol `PropietarioNegocio` (o `Admin`) y que el usuario sea realmente propietario del negocio indicado. No crea campaña, ticket, destinatarios ni outbox: solo calcula la audiencia potencial usando los mismos filtros y límites que el envío real.

```json
{
  "filters": {
    "genders": ["Mujer"],
    "provinces": ["Madrid", "Barcelona"],
    "minimumAge": 18,
    "maximumAge": 45,
    "minimumDaysSinceLastPointsEarned": 30,
    "includeUsersWithoutPointEarnings": false
  }
}
```

Respuesta:

```json
{
  "negocioId": "business-guid",
  "audienceCount": 1240,
  "pushEligibleCount": 810,
  "businessPushEnabled": true,
  "firebasePushEnabled": true,
  "pushAvailable": true,
  "calculatedAtUtc": "2026-07-01T10:30:00Z",
  "filters": {
    "genders": ["Mujer"],
    "provinces": ["Madrid", "Barcelona"],
    "minimumAge": 18,
    "maximumAge": 45,
    "minimumDaysSinceLastPointsEarned": 30,
    "includeUsersWithoutPointEarnings": false
  }
}
```

El conteo puede variar ligeramente entre preview y envío si cambian usuarios, puntos, tickets o límites temporales entre ambas llamadas.

### Crear y enviar una campana

`POST /api/promotions/negocios/{negocioId}/campaigns`

Requiere JWT con rol `PropietarioNegocio` (o `Admin`) y que el usuario sea realmente propietario del negocio indicado.

```json
{
  "title": "Te echamos de menos",
  "message": "Vuelve este mes y descubre tu nueva recompensa.",
  "imageUrl": null,
  "actionLabel": "Ver promocion",
  "deepLink": "/promotions",
  "conditions": "Valida hasta la fecha indicada.",
  "startsAtUtc": null,
  "scheduledAtUtc": null,
  "expiresAtUtc": "2026-08-31T21:59:59Z",
  "idempotencyKey": "summer-return-2026",
  "filters": {
    "genders": ["Mujer"],
    "minimumAge": 18,
    "maximumAge": 45,
    "minimumDaysSinceLastPointsEarned": 30,
    "includeUsersWithoutPointEarnings": false
  }
}
```

Devuelve `202 Accepted`. La audiencia se construye de forma asincrona. El frontend debe mostrar el estado y consultar:

`GET /api/promotions/negocios/{negocioId}/campaigns/{campaignId}`

Estados posibles: `Queued`, `ProcessingAudience`, `Sent`, `Failed`, `Cancelled`.

`Sent` significa que la promocion ya esta en las bandejas. Los contadores push pueden seguir aumentando mientras los workers entregan notificaciones.

La respuesta de preview incluye también `emailEligibleCount`, `businessEmailEnabled`, `smtpEmailEnabled` y `emailAvailable`. Una dirección es elegible si el usuario mantiene el consentimiento de correo promocional para ese negocio concreto (`permiteCorreosPromocionales`), dispone de correo y el negocio permite notificaciones por email. El consentimiento global `MarketingAccepted` queda reservado para futuras campañas generales de Dynamic y no interviene en campañas de negocio. El envío promocional no exige que `EmailConfirmed` esté marcado.

### Filtros disponibles

Todos son opcionales y se combinan con AND. Dentro de un campo de lista (`genders`, `postalCodes`, etc.), los valores se combinan con OR. Una lista omitida o vacia no filtra:

| Campo | Semantica |
|---|---|
| `genders` | `Hombre`, `Mujer`, `OtroPrefieroNoEspecificar`. Omitido o vacio significa todos |
| `provinces` | Provincias de España segun el enum `SpanishProvince`. Omitido o vacio significa todas |
| `minimumAge` / `maximumAge` | Edad actual calculada desde la fecha de nacimiento |
| `minimumCurrentPoints` / `maximumCurrentPoints` | Saldo actual en el negocio |
| `minimumTotalPointsEarned` / `maximumTotalPointsEarned` | Total historico acumulado en el negocio |
| `minimumTotalPointsSpent` / `maximumTotalPointsSpent` | Total historico de puntos gastados |
| `lastPointsEarnedBeforeUtc` / `lastPointsEarnedAfterUtc` | Rango UTC de la ultima acumulacion |
| `lastPointsSpentBeforeUtc` / `lastPointsSpentAfterUtc` | Rango UTC del ultimo gasto de puntos |
| `minimumDaysSinceLastPointsEarned` | Ej. `30`: acumulo puntos hace 30 dias o mas |
| `maximumDaysSinceLastPointsEarned` | Acumulo puntos dentro de los ultimos N dias |
| `includeUsersWithoutPointEarnings` | Incluye clientes con ticket pero que nunca acumularon puntos en filtros de ultima acumulacion |
| `lastActivityBeforeUtc` / `lastActivityAfterUtc` | Ultima actividad conocida por puntos o tickets |
| `customerSinceBeforeUtc` / `customerSinceAfterUtc` | Primera actividad conocida en el negocio |
| `registeredBeforeUtc` / `registeredAfterUtc` | Fecha de registro en Dynamic |
| `lastAppSeenBeforeUtc` / `lastAppSeenAfterUtc` | Ultima actividad conocida en Dynamic |
| `minimumDaysSinceLastAppSeen` / `maximumDaysSinceLastAppSeen` | Dias desde la ultima actividad en Dynamic |
| `birthMonth` | Mes de nacimiento, de 1 a 12 |
| `postalCodes`, `regions`, `countryCodes`, `languages` | Segmentacion geografica y de idioma; hasta 100 valores por campo |
| `hasAnyPoints` / `hasAnyTickets` | Tiene relacion con el negocio por puntos o por tickets |
| `hasActiveTickets` | Tiene al menos un ticket activo, no usado y no caducado |
| `hasEverUsedTicket` | Ha utilizado al menos un ticket del negocio |
| `minimumTicketCount` / `maximumTicketCount` | Cantidad total de tickets recibidos |
| `minimumUsedTicketCount` / `maximumUsedTicketCount` | Cantidad de tickets utilizados |
| `hasActivePushNotifications` | Tiene al menos un dispositivo con push habilitado |
| `hasConfirmedEmail` | Email confirmado |

Aunque no se envien filtros, solo entran usuarios que forman parte de la audiencia activa del negocio. Los puntos y tickets se usan como datos de segmentacion cuando existen, pero ya no son la fuente primaria de audiencia. Las cuentas inactivas se excluyen. El consentimiento por negocio solo limita el canal email; no elimina la promoción de la bandeja ni del canal push.

## Flujo del cliente

### Bandeja

`GET /api/users/me/promotions?page=1&pageSize=20&includeRead=true`

```json
{
  "items": [
    {
      "id": "recipient-guid",
      "campaignId": "campaign-guid",
      "negocio": {
        "id": "business-guid",
        "name": "Cafe Dynamic",
        "slug": "cafe-dynamic",
        "logoUrl": "/logo.png"
      },
      "title": "Te echamos de menos",
      "message": "Vuelve este mes.",
      "imageUrl": null,
      "actionLabel": "Ver promocion",
      "deepLink": "/promotions",
      "conditions": "Valida hasta fin de mes.",
      "startsAtUtc": "2026-07-01T08:00:00Z",
      "expiresAtUtc": "2026-07-31T21:59:59Z",
      "receivedAtUtc": "2026-07-01T08:00:00Z",
      "isRead": false,
      "readAtUtc": null
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 1,
  "totalPages": 1,
  "unreadCount": 1
}
```

Usar `unreadCount` para el badge. Para mostrar solo nuevas: `includeRead=false`.

### Marcar como leida

`POST /api/users/me/promotions/{recipientId}/read`

El `recipientId`, no el `campaignId`, identifica la promocion concreta recibida por el usuario.

### Animacion al entrar en Dynamic

La visualizacion en la animacion se registra por separado de la lectura de la bandeja. Consultar las
promociones pendientes no cambia su estado, por lo que un cierre de pestana o un error del frontend no
las pierde.

Al iniciar una sesion autenticada o al volver a entrar en la aplicacion:

`GET /api/users/me/promotions/unseen?limit=10`

Devuelve como maximo 10 promociones vigentes que aun no se han presentado, ordenadas de la mas antigua
a la mas reciente, y `totalPending` con el total pendiente. El servidor limita `limit` al rango 1-20.

```json
{
  "items": [],
  "totalPending": 0
}
```

Despues de mostrar cada promocion (no antes), el cliente confirma una o varias mediante:

`POST /api/users/me/promotions/presented`

```json
{
  "recipientIds": ["recipient-guid"]
}
```

La operacion admite entre 1 y 100 identificadores, es idempotente y solo modifica promociones del
usuario autenticado. Devuelve `presentedCount` y `presentedAtUtc`. Si la animacion se interrumpe, el
cliente debe confirmar solo los elementos que llegaron a mostrarse; los demas reapareceran al entrar.

## Push Android / Firebase

El cliente registra su token mediante el flujo existente de dispositivos con `PushProvider=Firebase` y `NotificationsEnabled=true`.

Datos incluidos en el push:

```json
{
  "type": "promotion",
  "promotionRecipientId": "...",
  "campaignId": "...",
  "negocioId": "...",
  "deepLink": "/promotions"
}
```

Configurar los secretos por entorno, sin guardarlos en Git:

- `Promotions__Firebase__Enabled=true`
- `Promotions__Firebase__ProjectId=...`
- `Promotions__Firebase__ServiceAccountJson=...`

La app Android debe crear el canal de notificaciones `promotions`.

## Correo promocional

Cada destinatario elegible genera una entrega persistente e independiente del push. El worker comprueba de nuevo el consentimiento del negocio concreto justo antes de enviar, limita el ritmo, reintenta errores temporales y registra los contadores `emailEligibleCount`, `emailDeliveredCount` y `emailFailedCount` en la campaña.

La primera alta en la audiencia de un negocio activa automáticamente `permiteCorreosPromocionales`. Una baja no se revierte al reactivar una relación ya existente. El usuario autenticado puede desactivar únicamente el correo del negocio, sin dejar de formar parte de él, mediante:

`POST /api/negocios/{negocioId}/audiencia/email/unsubscribe`

La plantilla responsive usa la identidad visual oscura y violeta de Dynamic e incluye promoción, negocio, dirección, enlace a Google Maps (coordenadas si existen), acceso a `portal/tickets`, versión de texto y baja. La baja también se anuncia mediante las cabeceras estándar `List-Unsubscribe` y `List-Unsubscribe-Post`.

Configuración recomendada por entorno:

- `Notify__Smtp__Enabled=true` y credenciales SMTP de un proveedor transaccional/marketing.
- `Promotions__Email__AppBaseUrl=https://appdynamic.es`
- `Promotions__Email__PublicApiBaseUrl=https://appdynamic.es`
- `Promotions__Email__CompanyName=Dynamic`
- `Promotions__Email__CompanyAddress=...` (domicilio identificativo que aparecerá en el pie legal).
- `Promotions__Dispatch__EmailsPerMinute=60` y `EmailBatchSize=20`; ajústalos a los límites contratados con el proveedor.

Antes de activar producción hay que autenticar el dominio remitente con SPF, DKIM y DMARC. No se deben usar cuentas SMTP personales para campañas masivas.

### Monitor de cola para administradores

`GET /api/admin/promotions/email-queue`

Requiere JWT con rol `Admin`. El endpoint no consulta la base de datos: devuelve la última instantánea en memoria recopilada por el worker cada `Promotions:Dispatch:EmailTelemetryRefreshSeconds` segundos. Incluye salud (`Idle`, `Running`, `Degraded`, `Stalled`, `Disabled` o `Unavailable`), profundidad de cola, entregas bloqueadas, rendimiento desde el arranque, entrega actual, hasta 20 campañas activas y los últimos 20 errores saneados. No expone destinatarios ni tokens y responde con `Cache-Control: no-store`.

La telemetría tiene alcance `process-instance`: se reinicia con el proceso y, si existen varias réplicas, cada una muestra únicamente su propio worker. Para una visión agregada futura habrá que publicar estas métricas en un sistema compartido de observabilidad.

## Registro y genero

Los endpoints de finalizacion de registro y edicion de perfil aceptan `gender`: `Hombre`, `Mujer` u `OtroPrefieroNoEspecificar`. Tambien aceptan `postalCode` para identificar la zona del usuario. Los valores de genero anteriores se normalizan mediante migracion.
