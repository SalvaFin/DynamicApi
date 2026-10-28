# Motivos de suma de puntos

Cada negocio dispone de «Paso por caja» por defecto. El propietario con permiso de gestionar el negocio (o un administrador) puede crear motivos, renombrarlos, marcar «Único por cliente» o desactivarlos. El motivo por defecto permanece activo y conserva su nombre. Desactivar y reactivar un motivo conserva su identidad y los usos anteriores.

La configuración aparece en **Motivos de puntos** del panel de propietarios y de la edición administrativa del negocio. Los trabajadores reciben solo motivos activos. El formulario usa automáticamente el único motivo disponible; con varios muestra un selector visible, sin texto libre ni desplegar parámetros. Esto se aplica a QR individual, reparto a grupos, código, búsqueda por contacto y la pantalla alternativa de suma de puntos compartida con móvil.

## Contrato

Base: `/api/fidelity/negocios/{negocioId}/points-accrual-reasons`.

- `GET`: motivos activos, accesible al personal autorizado del negocio.
- `GET ?includeInactive=true`: incluye inactivos; requiere permisos de configuración.
- `POST`: crea `{ name, active, uniquePerCustomer }`.
- `PUT /{reasonId}`: modifica esos mismos campos sin cambiar la identidad del motivo.

Las peticiones de acreditación incluyen `reasonId`. El servidor obtiene el nombre y la limitación desde su configuración; el antiguo campo `reason` no permite introducir texto libre. Para compatibilidad, omitir `reasonId` selecciona «Paso por caja». Los endpoints QR aceptan además `negocioId`, que debe pertenecer a los negocios autorizados del trabajador; los clientes antiguos siguen usando su negocio único o principal.

El historial devuelve `reasonId` y conserva una copia del nombre usado en cada transacción. Los reintentos con la misma clave de operación devuelven el resultado original antes de comprobar el límite; cambiar de motivo con esa misma clave devuelve conflicto.

## Unicidad y grupos

La elegibilidad se comprueba por `(NegocioId, UserId, AccrualReasonId)`, dentro de los bloqueos MariaDB de negocio/cliente existentes. Un índice único sobre `(NegocioId, UserId, UniqueAccrualReasonId)` protege adicionalmente las acreditaciones limitadas; el último campo es nulo para motivos sin límite. El mismo cliente puede utilizar motivos de otros negocios, aunque tengan el mismo nombre.

Si se marca único después de usos anteriores de ese identificador, esos usos también cuentan. Cambiar su nombre no reinicia la elegibilidad. En grupos se comprueban todos los clientes antes de vincular o acreditar: si alguno ya lo usó, se rechaza todo el reparto. La previsualización también avisa y la confirmación vuelve a comprobar la elegibilidad.

## Migración

`20261025095558_AddPointsAccrualReasons` crea los motivos, añade sus identificadores al historial y grupos, y crea los índices. Inserta «Paso por caja» sin límite para cada negocio existente. Los negocios nuevos reciben automáticamente ese mismo motivo mediante un identificador estable igual al del negocio, aunque todavía no hayan guardado configuración.

La migración asocia al motivo por defecto los textos históricos conocidos de compra. Los otros textos libres históricos conservan su contenido y no se asignan automáticamente a futuros motivos personalizados. No se cambian saldos ni importes. Los motivos únicos personalizados se controlan desde sus usos con identificador.

El host aplica las migraciones al arrancar (Negocios antes de Fidelity). La nueva API necesita esta migración y debe publicarse junto con el frontend. La generación de migración y las pruebas en memoria no prueban su aplicación a una base real ni la concurrencia de MariaDB.

## Publicación del 28 de octubre de 2026

La migración se aplicó y verificó en MariaDB con la versión `20261028T173529Z-a10617e2`, que actualiza API, proxy y web. Se confirmó la existencia del índice único por motivo. La validación de concurrencia real con clientes simultáneos sigue pendiente.
