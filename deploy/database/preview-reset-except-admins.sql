-- MariaDB 11.x
-- Solo lectura: inventario previo al borrado. No modifica datos.
-- Ejecutar seleccionando expresamente la base:
--   mariadb --host=HOST --user=USER --password --database=dynamic_api \
--     < deploy/database/preview-reset-except-admins.sql

SELECT DATABASE() AS selected_database;

SELECT
    COUNT(*) AS total_users,
    SUM(CASE WHEN `Role` = 'Admin' THEN 1 ELSE 0 END) AS admins_to_keep,
    SUM(CASE WHEN `Role` <> 'Admin' OR `Role` IS NULL THEN 1 ELSE 0 END) AS users_to_delete
FROM `users`;

-- TABLE_ROWS es una estimacion para InnoDB. El script de borrado devuelve
-- los recuentos exactos de filas afectadas antes de COMMIT o ROLLBACK.
SELECT
    TABLE_NAME AS table_name,
    ENGINE AS engine,
    TABLE_ROWS AS estimated_rows,
    CASE
        WHEN TABLE_NAME = 'users' THEN 'KEEP Role=Admin; DELETE the rest'
        WHEN TABLE_NAME = '__EFMigrationsHistory' THEN 'KEEP migration metadata'
        ELSE 'DELETE all rows'
    END AS planned_action
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_TYPE = 'BASE TABLE'
ORDER BY TABLE_NAME;

-- Debe devolver cero filas. El borrado confirmado se negara si encuentra
-- una tabla objetivo que no sea transaccional.
SELECT TABLE_NAME, ENGINE
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_TYPE = 'BASE TABLE'
  AND TABLE_NAME <> '__EFMigrationsHistory'
  AND ENGINE <> 'InnoDB'
ORDER BY TABLE_NAME;
