"""Exercise the actual migration SQL in an isolated MariaDB schema.

Run with Python: welcome_ticket_guard.py [--ssh user@host]
Requires mariadb socket access locally or on the SSH host. Uses synthetic data
only; creates and drops its own codex_welcome_guard_<uuid> schema.
"""
import argparse
import concurrent.futures
import pathlib
import re
import subprocess
import uuid

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--ssh')
args = parser.parse_args()
schema = 'codex_welcome_guard_' + uuid.uuid4().hex
command = (['ssh', '-o', 'BatchMode=yes', '-o', 'ConnectTimeout=10', args.ssh]
           if args.ssh else []) + ['mariadb', '--batch', '--skip-column-names']


def sql(statement, expected_error=None, database=True):
    result = subprocess.run(command + ([schema] if database else []),
                            input=statement, text=True, capture_output=True)
    if expected_error:
        assert result.returncode != 0 and expected_error in result.stderr, result.stderr
    else:
        assert result.returncode == 0, result.stderr
    return result.stdout.strip()


def ticket(ticket_id, user='customer', business='business', category='PrimerRegistro', ignore=False):
    return (f"INSERT {'IGNORE ' if ignore else ''}INTO fidelity_tickets "
            f"VALUES ('{ticket_id}','{user}','{business}',0,'{category}',NOW(),0,0);")


root = pathlib.Path(__file__).resolve().parents[2]
migration = root / 'DynamicApi/DynamicApi/Migrations/Fidelity/20261001120000_EnforceWelcomeTicketLifetimeGuard.cs'
up = migration.read_text(encoding='utf-8').split('protected override void Down')[0]
statements = re.findall(r'migrationBuilder.Sql\("""\s*(.*?)\s*"""\);', up, re.S)
assert len(statements) == 3
try:
    sql(f'CREATE DATABASE {schema};', database=False)
    sql('''CREATE TABLE fidelity_tickets (
        Id CHAR(36) PRIMARY KEY, UserId CHAR(36), NegocioId CHAR(36) NOT NULL,
        EsPlantilla BOOLEAN NOT NULL, CategoriaEnvioEspecial VARCHAR(32) NOT NULL,
        CreatedAtUtc DATETIME NOT NULL, Usado BOOLEAN NOT NULL, Activo BOOLEAN NOT NULL
    ) ENGINE=InnoDB;
    CREATE TABLE fidelity_welcome_ticket_claims (
        UserId CHAR(36), NegocioId CHAR(36), TicketId CHAR(36) NOT NULL,
        CreatedAtUtc DATETIME NOT NULL, PRIMARY KEY(UserId,NegocioId)
    ) ENGINE=InnoDB;''')
    sql(ticket('historical-a', 'historical') + ticket('historical-b', 'historical'))
    for statement in statements:
        sql('DELIMITER //\n' + statement + '//\nDELIMITER ;\n')
    assert sql('SELECT COUNT(*) FROM fidelity_tickets;') == '2'
    assert sql('SELECT COUNT(*) FROM fidelity_welcome_ticket_claims;') == '1'
    sql(ticket('historical-c', 'historical'), 'welcome_already_claimed')
    sql("UPDATE fidelity_tickets SET Usado=1 WHERE Id='historical-b';")
    print('PASS: historical duplicates preserved, guarded and still redeemable')

    sql(ticket('first'))
    sql(ticket('second'), 'welcome_already_claimed')
    sql(ticket('ignored-second', ignore=True), 'welcome_already_claimed')
    sql(ticket('other-business', business='other'))
    sql(ticket('other-user', user='other'))
    print('PASS: direct and INSERT IGNORE duplicates blocked; other customers/businesses allowed')

    sql("UPDATE fidelity_tickets SET Usado=1,Activo=0 WHERE Id='first';")
    sql(ticket('after-use'), 'welcome_already_claimed')
    sql("DELETE FROM fidelity_tickets WHERE Id='first';")
    sql(ticket('after-delete'), 'welcome_already_claimed')
    print('PASS: redemption, deactivation and deletion never reset eligibility')

    sql("INSERT INTO fidelity_welcome_ticket_claims VALUES ('reserved','business','reserved-ticket',NOW());")
    sql(ticket('reserved-ticket', 'reserved'))
    sql(ticket('reserved-second', 'reserved'), 'welcome_already_claimed')
    print('PASS: existing service reservation remains compatible')

    sql(ticket('general', category='General'))
    sql("UPDATE fidelity_tickets SET CategoriaEnvioEspecial='PrimerRegistro' WHERE Id='general';", 'welcome_already_claimed')
    sql(ticket('conversion', user='conversion', category='General'))
    sql("UPDATE fidelity_tickets SET CategoriaEnvioEspecial='PrimerRegistro' WHERE Id='conversion';")
    sql(ticket('converted-second', user='conversion'), 'welcome_already_claimed')
    sql("UPDATE fidelity_tickets SET UserId='changed' WHERE Id='conversion';", 'welcome_identity_immutable')
    sql("UPDATE fidelity_tickets SET CategoriaEnvioEspecial='General' WHERE Id='conversion';", 'welcome_identity_immutable')
    print('PASS: conversions reserve eligibility; issued welcome identity cannot be changed')

    def race(index):
        return subprocess.run(command + [schema],
            input='START TRANSACTION;' + ticket(f'race-{index}', 'race') + 'DO SLEEP(0.2); COMMIT;',
            text=True, capture_output=True)

    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        results = list(pool.map(race, [1, 2]))
    assert sum(result.returncode == 0 for result in results) == 1, results
    assert all(result.returncode == 0 or 'welcome_already_claimed' in result.stderr for result in results)
    assert sql("SELECT COUNT(*) FROM fidelity_tickets WHERE UserId='race';") == '1'
    print('PASS: concurrent direct writers produce exactly one welcome')

    sql('DROP TRIGGER fidelity_welcome_ticket_before_insert; DROP TRIGGER fidelity_welcome_ticket_before_update;')
    sql(ticket('after-down', 'race'))
    print('PASS: migration rollback removes triggers')
finally:
    # schema is generated here, never supplied by a caller or read from config.
    sql(f'DROP DATABASE IF EXISTS {schema};', database=False)
