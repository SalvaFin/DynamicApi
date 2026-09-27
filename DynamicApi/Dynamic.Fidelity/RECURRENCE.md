# Reglas de visitas y puntos

## Auditoría del flujo anterior

El factor base es `Negocio.RatioConversionEurosAPuntos`. Una acumulación individual calculaba `ceil(importe * factor)`; la operación cliente/PIN congelaba ese factor al iniciarse y acreditaba al validarse. Backoffice por código, por cliente y por QR de trabajador calculaban y acreditaban inmediatamente. La acumulación de grupo distribuía los puntos base entre los destinatarios. Las transacciones `Earn` y `BackofficeEarn` guardan negocio, cliente, fecha UTC, puntos e identificador propio; la operación PIN tiene además `OperationId`. Los movimientos de regalo, traspaso, reparto, canje y emisión de tickets no representan visitas.

Una **visita** es una transacción persistida con puntos positivos, tipo `Earn` o `BackofficeEarn`, el mismo `NegocioId` y `UserId`. Para cada destinatario de una compra de grupo cuenta su transacción positiva; el importe total de grupo solo figura en la primera transacción. El QR identifica al cliente, pero su lectura sola no cuenta. La operación pendiente o cancelada tampoco cuenta. Los datos históricos conservan su importe, puntos y fecha; no se reescriben ni se recalculan.

Antes de esta ampliación, repetir un POST directo podía acreditar dos veces: esas rutas no tenían clave de idempotencia. La validación PIN sí tenía un identificador de operación, aunque dos validadores podían leer el estado pendiente simultáneamente. Grupo ya tenía `IdempotencyKey`, índice único y transacción serializable. Una referencia libre, un QR o un importe igual no demuestran que dos compras sean la misma: deduplicar por ellos eliminaría compras legítimas.

## Resolución

El motor recibe la historia válida previa del cliente y la nueva operación. La nueva visita es el ordinal `historia + 1`. Ordena por fecha UTC e ID. Una ventana de `N` días incluye visitas desde `ahora - N días` hasta ahora; la vigencia de regla empieza inclusivamente y acaba antes de `EndsAtUtc`. Usa UTC para evitar cambios de horario local.

* **Hito**: la visita quinta puede recibir `×2` solo en esa visita, en las dos siguientes (sexta y séptima), hasta siete días después de la quinta, o en todas las posteriores. En duración se incluye la visita que activó el hito y se excluye el instante exacto de vencimiento.
* **Frecuencia**: la tercera visita en 30 días puede recibir el beneficio una vez por ventana móvil, volver a activarlo tras otras tres visitas desde la última activación, o mantenerlo siete días. La modalidad de duración conserva la fecha de activación original en las instantáneas sucesivas y exige un nuevo umbral tras el vencimiento.
* **Conflictos**: mayor prioridad primero y, en empate, ID ascendente. Una regla exclusiva impide aplicar otras; las compatibles multiplican sus factores en ese orden hasta un máximo conjunto de `×10`.

`MaxActivationsPerCustomer` limita opcionalmente cuántas veces puede comenzar el beneficio para un cliente. Las visitas que siguen dentro de una activación de duración o permanente mantienen esa activación y no consumen otra. La ausencia de límite permite las renovaciones definidas por la modalidad.

La instantánea de cada transacción nueva contiene el factor base, puntos base, ordinal de visita, multiplicador final, resultado en `PointsAmount` y JSON de cada regla realmente aplicada: nombre, familia, umbral, ventana, modalidad, duración, prioridad, compatibilidad, multiplicador y fecha de activación. Editar o desactivar una regla cambia futuras resoluciones; no modifica instantáneas ni puntos previos. Las visitas anteriores a la creación de una regla cuentan para los hitos y ventanas, pero nunca se bonifican retroactivamente.

En compras individuales se redondea una sola vez: `ceil(importe × factor base × multiplicador)`. En compras de grupo se distribuyen primero los puntos base entre clientes y luego cada parte recibe `ceil(puntos base asignados × multiplicador propio)`; la suma final puede superar el total base de la compra.

## Escritura y aislamiento

Todas las rutas de acumulación toman un bloqueo MariaDB por negocio/cliente antes de contar visitas y escribir puntos. Grupo bloquea los clientes en orden estable. El bloqueo dura hasta que se guarda la transacción y se libera incluso si falla el método. Las solicitudes directas nuevas envían `IdempotencyKey`; el índice único `(NegocioId, ClientOperationId)` y una lectura bajo bloqueo devuelven el resultado persistido en un reintento. Si hay reglas activas, una solicitud directa sin clave se rechaza. La ruta cliente/PIN reutiliza `OperationId`; grupo conserva su propia clave. Esas claves deben reutilizarse al reintentar **la misma compra** y renovarse para una compra nueva.

El QR del cliente es reutilizable y no identifica una compra. Leerlo dos veces dentro de la misma operación, o reintentar el POST con la misma clave, acredita una sola visita. Dos compras válidas distintas del mismo cliente pueden usar el mismo QR y deben tener claves de operación distintas.

La API de reglas exige propietario activo con permiso de gestión de negocio (o administrador) y filtra siempre por `NegocioId`; la consulta del cliente usa exclusivamente su ID autenticado. La previsualización de propietario muestra el cálculo de la próxima visita sin escribir puntos. El portal solo expone al cliente sus conteos y reglas públicas de ese negocio.

La previsualización acepta un borrador de regla y lo evalúa como activo sin guardarlo. El cliente de backoffice conserva el mismo ID para ese borrador al crearlo, de modo que los desempates por prioridad coinciden entre la simulación y la regla persistida.

## Verificación y despliegue

La migración añade columnas anulables a las transacciones existentes y una tabla de reglas; no modifica importes anteriores. Antes de activar reglas en una base real conviene consultar duplicados históricos de `OperationId`, revisar que las migraciones de Fidelity previas están aplicadas y validar los caminos PIN, código, QR y grupo contra MariaDB con reintentos simultáneos. Las pruebas unitarias del motor cubren hitos, ventanas, prioridad, vencimiento y repetición del ID. Un build local y esas pruebas no prueban el estado de una base de datos desplegada ni las carreras reales del proveedor MariaDB.

Consulta de auditoría previa a la activación, solo lectura:

```sql
SELECT NegocioId, OperationId, COUNT(*) AS Transacciones
FROM fidelity_points_transactions
WHERE OperationId IS NOT NULL
GROUP BY NegocioId, OperationId
HAVING COUNT(*) > 1;

SELECT NegocioId, UserId, Reference, COUNT(*) AS Coincidencias
FROM fidelity_points_transactions
WHERE TransactionType IN ('Earn', 'BackofficeEarn')
  AND PointsAmount > 0 AND Reference IS NOT NULL
GROUP BY NegocioId, UserId, Reference
HAVING COUNT(*) > 1;
```

La segunda consulta da **candidatos** a revisión, no duplicados confirmados: una referencia manual puede repetirse entre compras distintas. No se eliminan transacciones históricas automáticamente.
