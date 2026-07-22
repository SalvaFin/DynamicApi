# Prompts de implementación frontend: reportes

La API serializa propiedades en `camelCase` y enums como strings exactamente con los valores documentados. Todos los endpoints requieren `Authorization: Bearer <token>`. Los errores funcionales usan `{ "message": "..." }`; contempla también `401`, `403`, `404` y `409`.

## Prompt 1 — Pantalla de reportes en el perfil de usuario

```text
Implementa en el frontend actual una sección “Mis reportes” dentro de la pantalla de perfil del usuario. Antes de programar, inspecciona la arquitectura, router, cliente HTTP, gestión de sesión, componentes, tokens de diseño, sistema de formularios y librería de queries ya existentes; reutilízalos y no introduzcas otro stack paralelo.

Objetivo UX:
- Añadir una entrada/tarjeta “Ayuda y reportes” en el perfil.
- Mostrar una lista paginada de los reportes del usuario con asunto, categoría, estado, última actualización y referencia a ticket/negocio/promoción cuando exista.
- Permitir filtrar por estado y categoría.
- Incluir una acción clara “Reportar un problema” que abra una pantalla o modal accesible.
- El formulario debe obtener categorías desde la API; no hardcodear sus textos. Mostrar campos contextuales de ticket, negocio o promoción solo cuando la categoría devuelva supportsTicket/supportsBusiness/supportsPromotion. Las referencias son opcionales: ofrece selectores con los datos que ya tenga el perfil y permite continuar sin referencia cuando tenga sentido (por ejemplo un ticket que ya no aparece).
- Campos: category, subject, description, ticketId?, businessId?, promotionCampaignId?, occurredAtUtc?, pageUrl? y appVersion?. Rellena pageUrl y appVersion automáticamente si el frontend dispone de esos datos.
- Validar asunto 5–160 caracteres y descripción 10–5000. Fechas en ISO-8601 UTC.
- Al crear, enseñar confirmación y abrir el detalle del reporte.
- El detalle debe mostrar una línea temporal, respuestas del soporte y cambios de estado. Permitir añadir mensajes mientras el estado no sea Resolved ni Rejected.
- Estados con etiquetas en español: Open=Abierto, InReview=En revisión, WaitingForUser=Esperando tu respuesta, Resolved=Resuelto, Rejected=Descartado.
- Diseñar estados de carga, vacío, error, reintento, paginación responsive y feedback de envío. Evitar optimistic updates en creación/mensajes; usar la respuesta canónica de la API e invalidar la lista.
- Mantener accesibilidad: labels reales, foco tras abrir/cerrar modal, errores asociados a campos, navegación por teclado y contraste.

Endpoints disponibles:
1. GET /api/users/me/reports/options
   Respuesta:
   {
     "categories": [{
       "value": "TicketLost",
       "label": "He perdido un ticket",
       "description": "...",
       "supportsTicket": true,
       "supportsBusiness": true,
       "supportsPromotion": false
     }],
     "statuses": ["Open", "InReview", "WaitingForUser", "Resolved", "Rejected"],
     "priorities": ["Low", "Normal", "High", "Critical"]
   }

2. GET /api/users/me/reports?page=1&pageSize=20&status=Open&category=TicketLost
   status y category son opcionales. Respuesta paginada:
   { "page": 1, "pageSize": 20, "totalItems": 1, "totalPages": 1, "items": [ReportSummary] }

3. POST /api/users/me/reports
   Body de ejemplo:
   {
     "category": "TicketIncorrectlyRedeemed",
     "subject": "Mi ticket figura como usado",
     "description": "No he realizado este canje y necesito que lo reviséis.",
     "ticketId": "uuid-opcional",
     "businessId": "uuid-opcional",
     "promotionCampaignId": null,
     "occurredAtUtc": "2026-07-21T15:30:00Z",
     "pageUrl": "/perfil/tickets",
     "appVersion": "1.2.3"
   }
   Devuelve 201 y ReportDetail. Si ticketId no pertenece al usuario o la promoción no le fue enviada, devuelve 404. Si las referencias no corresponden al mismo negocio, devuelve 400.

4. GET /api/users/me/reports/{reportId}
   Devuelve ReportDetail solo si pertenece al usuario. Nunca contiene notas internas.

5. POST /api/users/me/reports/{reportId}/messages
   Body: { "message": "Añado esta información..." }
   Devuelve el ReportDetail actualizado. Devuelve 409 si ya está Resolved o Rejected. Si estaba WaitingForUser pasa automáticamente a InReview.

Tipos relevantes:
ReportSummary = {
  id, reporterUserId, category, status, priority, subject,
  ticketId?, businessId?, promotionCampaignId?, assignedAdminUserId?,
  createdAtUtc, updatedAtUtc, resolvedAtUtc?
}

ReportDetail extiende ReportSummary con:
{
  description, occurredAtUtc?, pageUrl?, appVersion?, resolvedByAdminUserId?,
  ticket?: { id, label }, business?: { id, label }, promotion?: { id, label },
  timeline: [{
    id, actorUserId, kind, isInternal, message?, previousStatus?, newStatus?,
    previousPriority?, newPriority?, previousAssignedAdminUserId?,
    newAssignedAdminUserId?, createdAtUtc
  }]
}

Valores de category:
TicketLost, TicketNotReceived, TicketIncorrectlyRedeemed, TicketOther,
PointsBalance, Promotion, QrScan, AccountAccess, AccountData,
BusinessInformation, BusinessExperience, Other.

Valores de kind visibles al usuario: Created, UserMessage, AdminReply, StatusChanged.

Entrega:
- Componentes/páginas integrados en el perfil.
- Tipos de API y funciones del cliente HTTP.
- Hooks/queries y manejo de caché siguiendo el patrón del repo.
- Tests del formulario, lista/detalle y errores 400/404/409 con el framework existente.
- Un resumen corto de archivos cambiados y decisiones tomadas.
```

## Prompt 2 — Pestaña de reportes en el superbackoffice

```text
Añade al superbackoffice una pestaña de navegación “Reportes” visible exclusivamente para superadmins. En este backend la autorización de superadmin corresponde a la policy AdminAuth / role Admin; todos los endpoints administrativos devuelven 401 sin sesión y 403 sin ese rol. Antes de programar, inspecciona y reutiliza router, layout, guardas, cliente HTTP, tablas, filtros, modales/drawers, diseño y librería de queries existentes.

Objetivo UX:
- Cabecera con indicadores de Abiertos, En revisión, Esperando usuario, Sin asignar y Críticos.
- Tabla paginada ordenada por updatedAtUtc descendente con: asunto, usuario, categoría, estado, prioridad, referencias, asignado y última actualización.
- Filtros combinables: status, priority, category, assignedAdminUserId, unassigned y search. Sincronizarlos con query params de la URL y aplicar debounce a search.
- Al seleccionar una fila, abrir detalle en ruta o drawer enlazable. Mostrar descripción, contexto técnico, usuario reportante, ticket/negocio/promoción relacionados y timeline completa.
- Acciones: asignarme, desasignar, cambiar estado, cambiar prioridad, responder al usuario y añadir nota interna. Distinguir visualmente respuesta pública y nota interna con una advertencia clara; nunca presentar una nota interna como respuesta pública.
- Una sola llamada PATCH puede combinar acciones. Tras éxito, reemplazar el detalle por la respuesta canónica e invalidar tabla/dashboard.
- Pedir confirmación antes de Resolved o Rejected. Permitir reabrir pasando a Open o InReview.
- Etiquetas: Open=Abierto, InReview=En revisión, WaitingForUser=Esperando usuario, Resolved=Resuelto, Rejected=Descartado; Low=Baja, Normal=Normal, High=Alta, Critical=Crítica.
- Estados robustos de carga, vacío, error y reintento; tabla responsive y accesible.

Endpoints disponibles:
1. GET /api/admin/reports/options
   Devuelve categories/statuses/priorities con el mismo contrato de la pantalla de usuario. Usa categories para etiquetas y capacidades; no dupliques textos.

2. GET /api/admin/reports/dashboard
   Respuesta:
   {
     "open": 4,
     "inReview": 2,
     "waitingForUser": 1,
     "resolved": 20,
     "rejected": 3,
     "unassigned": 5,
     "critical": 1
   }

3. GET /api/admin/reports?page=1&pageSize=20&status=Open&priority=High&category=TicketLost&assignedAdminUserId={uuid}&unassigned=true&search=texto
   Todos los filtros son opcionales. No envíes assignedAdminUserId y unassigned=true simultáneamente.
   Respuesta: { page, pageSize, totalItems, totalPages, items: ReportSummary[] }.

4. GET /api/admin/reports/{reportId}
   Devuelve ReportDetail administrativo. Además de las referencias y timeline, incluye:
   reporter?: { id, userName, displayName?, email? }
   assignedAdmin?: { id, userName, displayName?, email? }
   La timeline administrativa incluye eventos internos: InternalNote, PriorityChanged y AssignmentChanged, además de Created, UserMessage, AdminReply y StatusChanged.

5. PATCH /api/admin/reports/{reportId}
   Todos los campos son opcionales, pero debe existir al menos una acción:
   {
     "status": "InReview",
     "priority": "High",
     "assignToMe": true,
     "unassign": false,
     "publicReply": "Estamos revisando tu incidencia.",
     "internalNote": "Comprobar el canje con el negocio."
   }
   assignToMe y unassign no pueden ser true a la vez (400). Devuelve ReportDetail actualizado.

Contratos:
ReportSummary = {
  id, reporterUserId, category, status, priority, subject,
  ticketId?, businessId?, promotionCampaignId?, assignedAdminUserId?,
  createdAtUtc, updatedAtUtc, resolvedAtUtc?
}

ReportDetail extiende ReportSummary con:
{
  description, occurredAtUtc?, pageUrl?, appVersion?, resolvedByAdminUserId?,
  reporter?, assignedAdmin?, ticket?: { id, label }, business?: { id, label },
  promotion?: { id, label }, timeline: ReportEvent[]
}

ReportEvent = {
  id, actorUserId, kind, isInternal, message?, previousStatus?, newStatus?,
  previousPriority?, newPriority?, previousAssignedAdminUserId?,
  newAssignedAdminUserId?, createdAtUtc
}

Categorías: TicketLost, TicketNotReceived, TicketIncorrectlyRedeemed, TicketOther,
PointsBalance, Promotion, QrScan, AccountAccess, AccountData,
BusinessInformation, BusinessExperience, Other.

Entrega:
- Nueva pestaña, ruta protegida y navegación.
- Cliente API tipado, queries/mutations e invalidaciones.
- Dashboard, tabla/filtros, detalle/timeline y panel de acciones.
- Tests de permisos, filtros, diferenciación público/interno y payload PATCH.
- Resumen de archivos cambiados y decisiones.
```
