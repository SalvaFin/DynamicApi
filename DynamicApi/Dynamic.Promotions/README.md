# Dynamic Promotions

La bandeja de promociones es la fuente de verdad. El push de Firebase es un canal adicional: si un usuario no tiene notificaciones activas, la promocion sigue apareciendo en `GET /api/users/me/promotions`.

## Flujo del propietario

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

### Filtros disponibles

Todos son opcionales y se combinan con AND. Dentro de un campo de lista (`genders`, `cities`, etc.), los valores se combinan con OR. Una lista omitida o vacia no filtra:

| Campo | Semantica |
|---|---|
| `genders` | `Hombre`, `Mujer`, `OtroPrefieroNoEspecificar`. Omitido o vacio significa todos |
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
| `cities`, `regions`, `countryCodes`, `languages` | Segmentacion geografica y de idioma; hasta 100 valores por campo |
| `hasAnyPoints` / `hasAnyTickets` | Tiene relacion con el negocio por puntos o por tickets |
| `hasActiveTickets` | Tiene al menos un ticket activo, no usado y no caducado |
| `hasEverUsedTicket` | Ha utilizado al menos un ticket del negocio |
| `minimumTicketCount` / `maximumTicketCount` | Cantidad total de tickets recibidos |
| `minimumUsedTicketCount` / `maximumUsedTicketCount` | Cantidad de tickets utilizados |
| `hasActivePushNotifications` | Tiene al menos un dispositivo con push habilitado |
| `hasConfirmedEmail` | Email confirmado |

Aunque no se envien filtros, solo entran usuarios relacionados con ese negocio mediante puntos o tickets. Siempre se excluyen usuarios sin `MarketingAccepted`, cuentas inactivas y personal del propio negocio.

Dynamic aplica tambien limites no controlables por el negocio: por defecto, una promocion del mismo negocio cada 7 dias y un maximo global de 3 promociones por usuario cada 7 dias.

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

## Registro y genero

Los endpoints de finalizacion de registro y edicion de perfil aceptan `gender`: `Hombre`, `Mujer` u `OtroPrefieroNoEspecificar`. Los valores anteriores se normalizan mediante migracion.
