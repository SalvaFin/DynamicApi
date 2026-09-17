-- MariaDB 11.x
-- Conserva exclusivamente:
--   * filas de `users` cuyo `Role` sea exactamente 'Admin'
--   * `__EFMigrationsHistory` (metadatos del esquema de EF Core)
-- El resto de las filas de todas las tablas de la base seleccionada se borra.
--
-- SEGURO POR DEFECTO: la configuracion incluida ejecuta todo y hace ROLLBACK.
-- Para aplicar de verdad hay que cambiar AMBAS variables:
--   SET @commit_changes = 1;
--   SET @confirmation = 'BORRAR:dynamic_api';

SET @expected_database = 'dynamic_api';
SET @commit_changes = 0;
SET @confirmation = 'SIMULAR';

DELIMITER //

DROP PROCEDURE IF EXISTS `__dynamicapi_reset_except_admins_20260831`//
CREATE PROCEDURE `__dynamicapi_reset_except_admins_20260831`(
    IN p_expected_database VARCHAR(64),
    IN p_commit_changes BOOLEAN,
    IN p_confirmation VARCHAR(255)
)
main: BEGIN
    DECLARE v_done BOOLEAN DEFAULT FALSE;
    DECLARE v_table_name VARCHAR(64);
    DECLARE v_sql LONGTEXT;
    DECLARE v_deleted BIGINT DEFAULT 0;
    DECLARE v_admins_before BIGINT DEFAULT 0;
    DECLARE v_admins_after BIGINT DEFAULT 0;
    DECLARE v_remaining_rows BIGINT DEFAULT 0;
    DECLARE v_bad_engines BIGINT DEFAULT 0;
    DECLARE v_old_foreign_key_checks INT DEFAULT 1;

    DECLARE table_cursor CURSOR FOR
        SELECT TABLE_NAME
        FROM information_schema.TABLES
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_TYPE = 'BASE TABLE'
          AND TABLE_NAME <> '__EFMigrationsHistory'
        ORDER BY CASE WHEN TABLE_NAME = 'users' THEN 1 ELSE 0 END, TABLE_NAME;

    DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_done = TRUE;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        SET FOREIGN_KEY_CHECKS = v_old_foreign_key_checks;
        RESIGNAL;
    END;

    SET v_old_foreign_key_checks = @@FOREIGN_KEY_CHECKS;

    IF DATABASE() IS NULL OR BINARY DATABASE() <> BINARY p_expected_database THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'ABORTADO: la base seleccionada no coincide con @expected_database';
    END IF;

    IF p_commit_changes = TRUE
       AND BINARY p_confirmation <> BINARY CONCAT('BORRAR:', p_expected_database) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'ABORTADO: confirmacion incorrecta para el borrado definitivo';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.COLUMNS
        WHERE TABLE_SCHEMA = DATABASE()
          AND TABLE_NAME = 'users'
          AND COLUMN_NAME = 'Role'
    ) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'ABORTADO: no existe users.Role en la base seleccionada';
    END IF;

    SELECT COUNT(*) INTO v_admins_before
    FROM `users`
    WHERE `Role` = 'Admin';

    IF v_admins_before = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'ABORTADO: no existe ningun usuario con Role=Admin';
    END IF;

    SELECT COUNT(*) INTO v_bad_engines
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_TYPE = 'BASE TABLE'
      AND TABLE_NAME <> '__EFMigrationsHistory'
      AND ENGINE <> 'InnoDB';

    IF v_bad_engines > 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'ABORTADO: hay tablas objetivo no InnoDB; no se garantiza ROLLBACK';
    END IF;

    DROP TEMPORARY TABLE IF EXISTS `reset_except_admins_audit`;
    CREATE TEMPORARY TABLE `reset_except_admins_audit` (
        `table_name` VARCHAR(64) NOT NULL,
        `deleted_rows` BIGINT NOT NULL
    ) ENGINE=MEMORY;

    START TRANSACTION;
    SET FOREIGN_KEY_CHECKS = 0;

    OPEN table_cursor;
    delete_loop: LOOP
        FETCH table_cursor INTO v_table_name;
        IF v_done THEN
            LEAVE delete_loop;
        END IF;

        IF v_table_name = 'users' THEN
            SET v_sql = 'DELETE FROM `users` WHERE `Role` <> ''Admin'' OR `Role` IS NULL';
        ELSE
            SET v_sql = CONCAT(
                'DELETE FROM `',
                REPLACE(v_table_name, '`', '``'),
                '`'
            );
        END IF;

        PREPARE delete_statement FROM v_sql;
        EXECUTE delete_statement;
        SET v_deleted = ROW_COUNT();
        DEALLOCATE PREPARE delete_statement;

        INSERT INTO `reset_except_admins_audit` (`table_name`, `deleted_rows`)
        VALUES (v_table_name, v_deleted);
    END LOOP;
    CLOSE table_cursor;

    -- Segunda pasada independiente: detecta tambien datos que un trigger pudiera
    -- haber insertado de nuevo en una tabla procesada anteriormente.
    SET v_done = FALSE;
    OPEN table_cursor;
    validation_loop: LOOP
        FETCH table_cursor INTO v_table_name;
        IF v_done THEN
            LEAVE validation_loop;
        END IF;

        IF v_table_name = 'users' THEN
            SELECT COUNT(*) INTO v_remaining_rows
            FROM `users`
            WHERE `Role` <> 'Admin' OR `Role` IS NULL;
        ELSE
            SET @reset_except_admins_remaining_rows = NULL;
            SET v_sql = CONCAT(
                'SELECT COUNT(*) INTO @reset_except_admins_remaining_rows FROM `',
                REPLACE(v_table_name, '`', '``'),
                '`'
            );
            PREPARE validation_statement FROM v_sql;
            EXECUTE validation_statement;
            DEALLOCATE PREPARE validation_statement;
            SET v_remaining_rows = @reset_except_admins_remaining_rows;
        END IF;

        IF v_remaining_rows <> 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'ABORTADO: una tabla objetivo conserva filas tras el borrado';
        END IF;
    END LOOP;
    CLOSE table_cursor;

    SELECT COUNT(*) INTO v_admins_after
    FROM `users`
    WHERE `Role` = 'Admin';

    IF v_admins_after <> v_admins_before
       OR EXISTS (SELECT 1 FROM `users` WHERE `Role` <> 'Admin' OR `Role` IS NULL) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'ABORTADO: fallo la validacion posterior al borrado';
    END IF;

    IF p_commit_changes = TRUE THEN
        COMMIT;
    ELSE
        ROLLBACK;
    END IF;

    SET FOREIGN_KEY_CHECKS = v_old_foreign_key_checks;

    SELECT
        CASE WHEN p_commit_changes = TRUE THEN 'COMMIT' ELSE 'ROLLBACK (simulation)' END AS result,
        p_expected_database AS database_name,
        v_admins_before AS admins_preserved,
        SUM(`deleted_rows`) AS total_rows_affected
    FROM `reset_except_admins_audit`;

    SELECT `table_name`, `deleted_rows`
    FROM `reset_except_admins_audit`
    ORDER BY `table_name`;
END//

DELIMITER ;

CALL `__dynamicapi_reset_except_admins_20260831`(
    @expected_database,
    @commit_changes,
    @confirmation
);
DROP PROCEDURE `__dynamicapi_reset_except_admins_20260831`;
