# Condiciones de validación de tickets

Los formularios de tickets del administrador y del propietario, y la creación de tickets desde promociones, permiten configurar una cesta mínima y franjas semanales. Las restricciones son opcionales.

- `gastoMinimoRequerido`: importe en euros antes de aplicar el beneficio. Nulo o cero significa sin mínimo; se admiten dos decimales.
- `usageWindows`: lista de `{ dayOfWeek, startTime, endTime }`. Los días siguen ISO: lunes 1 a domingo 7. Una lista nula o vacía permite cualquier día y hora.
- Ambas horas nulas permiten todo el día seleccionado. Las horas usan `HH:mm`. Inicio incluido y fin excluido. Si fin es anterior al inicio, la franja termina al día siguiente. Se pueden combinar varios días e intervalos; basta con cumplir uno.
- Las horas se comprueban con `Negocio.ZonaHoraria`, incluido el cambio de hora. Si no está configurada, se usa `Europe/Madrid`. Una zona no válida bloquea un ticket con franjas y pide revisar la configuración.

Ejemplo: cesta de 20 €, lunes y miércoles de 16:00 a 19:00:

```json
{
  "gastoMinimoRequerido": 20,
  "usageWindows": [
    { "dayOfWeek": 1, "startTime": "16:00", "endTime": "19:00" },
    { "dayOfWeek": 3, "startTime": "16:00", "endTime": "19:00" }
  ]
}
```

`GET /api/backoffice/negocios/{negocioId}/tickets/validation-settings` devuelve `requiresPurchaseAmount`. Aplica la misma autorización y pertenencia al negocio que la validación. Comprueba tanto plantillas como tickets asignados activos y no caducados con mínimo.

Al abrir una acción de tickets, caja consulta esa configuración. Si hace falta importe, lo muestra directamente y lo exige antes de abrir la cámara o validar por código. Envía `purchaseAmount` al endpoint de validación existente. Tras un canje correcto limpia el importe para la siguiente cuenta; tras un rechazo conserva importe y código. Una lectura de cámara solo dispara una petición.

La API verifica las condiciones del ticket asignado antes de cambiar sus usos o guardar un canje. No se puede omitir el importe para saltarse su mínimo. Un rechazo devuelve el motivo al trabajador y publica `fidelity.ticket.rejected` al propietario del ticket. Web y móvil muestran ese motivo al cliente conectado mediante SignalR; el ticket sigue disponible. Un canje posterior correcto del mismo ticket cierra el aviso de rechazo.

Las condiciones aparecen en las recompensas y tickets del cliente. Se copian al asignar tickets mediante puntos, bienvenida, invitación y promociones. Editar una plantilla afecta a futuras asignaciones; las copias ya emitidas conservan sus condiciones.

La migración `20261025155713_AddTicketUsageWindows` añade únicamente una columna nullable `UsageWindows` en `fidelity_tickets`. Los tickets existentes siguen sin restricción horaria. `GastoMinimoRequerido` ya existía. El arranque habitual de la API aplica las migraciones pendientes; la publicación debe coordinar API, web y, por separado, la aplicación móvil.

Validación local: compilación de API, web y móvil, TypeScript, pruebas de Fidelity y frontend, comprobación de ausencia de cambios de modelo pendientes y generación/revisión del SQL. Las pruebas de persistencia usan EF InMemory. Publicación del 28 de octubre de 2026: migración aplicada y verificada en MariaDB, junto con API, proxy y web, en la versión `20261028T173529Z-a10617e2`. Las comprobaciones de servicio y archivos publicados no sustituyen un escaneo físico entre dos sesiones, que sigue sin realizarse. La aplicación móvil está compilada, sin distribuir una nueva versión nativa.
