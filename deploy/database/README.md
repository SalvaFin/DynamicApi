# Reinicio de datos conservando administradores

Estos scripts estan dirigidos a MariaDB y a la base `dynamic_api`. Conservan
solamente los usuarios cuyo campo `Role` sea exactamente `Admin` y el historial
de migraciones de Entity Framework. Eliminan sesiones y cualquier otro dato,
incluidos los usuarios con roles `User`, `PropietarioNegocio` y
`TrabajadorNegocio`.

## Uso controlado

1. Deten la API y cualquier worker que escriba en la base.
2. Haz y verifica un backup completo.
3. Ejecuta `preview-reset-except-admins.sql` contra la base correcta.
4. Ejecuta `reset-except-admins.sql` sin modificarlo. El resultado debe indicar
   `ROLLBACK (simulation)` y muestra los recuentos exactos que se borrarian.
5. Solo si los recuentos son correctos, edita las dos variables iniciales del
   segundo script:

   ```sql
   SET @commit_changes = 1;
   SET @confirmation = 'BORRAR:dynamic_api';
   ```

6. Vuelve a ejecutarlo y comprueba que el resultado sea `COMMIT`.

Ejemplo con el cliente de MariaDB, que solicita la clave sin guardarla en el
historial del terminal:

```sh
mariadb --host=HOST --port=3306 --user=USER --password --database=dynamic_api < deploy/database/preview-reset-except-admins.sql
mariadb --host=HOST --port=3306 --user=USER --password --database=dynamic_api < deploy/database/reset-except-admins.sql
```

El script definitivo se niega a continuar si la base seleccionada no se llama
`dynamic_api`, no existe `users.Role`, no hay al menos un administrador o alguna
tabla objetivo no usa InnoDB. Desactiva las comprobaciones de claves foraneas
solo dentro de su propia sesion y las restaura al terminar o ante un error.
