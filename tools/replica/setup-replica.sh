#!/usr/bin/env bash
# =============================================================================
# Builds the read replica for MainDb from zero, and re-verifies it if it exists.
#
# WHY a script in a container (and not manual commands): a new device must reach
# the same state with ONE command. Every step checks "is this already done?"
# first, so running it twice is safe (idempotent).
#
# WHY we talk to both servers over the Docker network: this container is not
# inside either server, so it cannot copy certificate files between them. We
# transfer the certificate as hex text instead (CERTENCODED / CERTPRIVATEKEY).
# =============================================================================
set -Eeuo pipefail
trap 'echo ""; echo "!!! SETUP FAILED near line $LINENO. Read the error printed above this message."' ERR

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
PRIMARY="${PRIMARY_HOST:-sqlserver}"
REPLICA="${REPLICA_HOST:-sqlserver-replica}"
DB="MainDb"
AG="ag_main"

: "${SA_PASSWORD:?SA_PASSWORD is not set}"
: "${AG_CERT_PASSWORD:?AG_CERT_PASSWORD is not set}"

# ---------- helpers -----------------------------------------------------------
# run <host> <database> <sql> [extra sqlcmd args]  -> prints output, fails on SQL error
# -b makes sqlcmd return a non-zero exit code on a SQL error, so set -e stops us.
run() {
  local host="$1" db="$2" query="$3"; shift 3
  "$SQLCMD" -C -b -S "$host" -U sa -P "$SA_PASSWORD" -d "$db" -Q "$query" "$@"
}

# scalar <host> <database> <sql> [extra sqlcmd args]  -> prints ONE short value
# WHY -h -1 -W: no header line and no padding spaces, so the first output line
# IS the value. (-y cannot be combined with -h or -W, so long values use scalar_long.)
# awk skips the "Changed database context" line that sqlcmd may print for -d,
# and prints only the first remaining line.
scalar() {
  local host="$1" db="$2" query="$3"; shift 3
  "$SQLCMD" -C -b -h -1 -W -w 65535 -S "$host" -U sa -P "$SA_PASSWORD" -d "$db" \
    -Q "SET NOCOUNT ON; $query" "$@" \
  | tr -d '\r' \
  | awk '!done && NF && $0 !~ /^Changed database context/ { print; done = 1 }'
}

# scalar_long <host> <database> <varchar(max) expression>  -> prints ONE long value
# WHY chunks: sqlcmd truncates long values at 256 characters. We return the value
# as rows of 200 characters and glue them together here.
scalar_long() {
  local host="$1" db="$2" expr="$3"
  "$SQLCMD" -C -b -h -1 -W -w 65535 -S "$host" -U sa -P "$SA_PASSWORD" -d "$db" -Q "
    SET NOCOUNT ON;
    DECLARE @h varchar(max) = $expr;
    SELECT SUBSTRING(@h, n, 200)
    FROM (SELECT TOP (CAST(CEILING(LEN(@h) / 200.0) AS int))
                 (ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1) * 200 + 1 AS n
          FROM sys.all_objects a CROSS JOIN sys.all_columns b) x
    ORDER BY n;" \
  | awk '!/^Changed database context/' \
  | tr -d '\r\n'
}

step() { echo ""; echo "=== $* ==="; }

wait_for_sql() {
  local host="$1"
  for _ in $(seq 1 60); do
    if "$SQLCMD" -C -b -S "$host" -U sa -P "$SA_PASSWORD" -Q "SELECT 1" >/dev/null 2>&1; then
      echo "$host is answering."; return 0
    fi
    sleep 2
  done
  echo "ERROR: $host did not answer within 2 minutes."; exit 1
}

# ---------- 0. both servers are up, names and HADR are correct -----------------
step "0. Checking both servers"
wait_for_sql "$PRIMARY"
wait_for_sql "$REPLICA"

for host in "$PRIMARY" "$REPLICA"; do
  name="$(scalar "$host" master "SELECT CONVERT(varchar(128), @@SERVERNAME)")"
  hadr="$(scalar "$host" master "SELECT CONVERT(varchar(10), SERVERPROPERTY('IsHadrEnabled'))")"
  # The Availability Group finds servers by this name. An old volume can keep an
  # old random name; we cannot fix that from here (it needs a restart).
  if [ "${name,,}" != "${host,,}" ]; then
    echo "ERROR: @@SERVERNAME of $host is '$name', expected '$host'."
    echo "This happens when an OLD volume is reused. Fix on that server, then restart its container:"
    echo "  EXEC sp_dropserver '$name'; EXEC sp_addserver '$host', 'local';"
    exit 1
  fi
  if [ "$hadr" != "1" ]; then
    echo "ERROR: HADR is not enabled on $host (set MSSQL_ENABLE_HADR=1 and recreate the container)."
    exit 1
  fi
  echo "$host: name OK, HADR enabled."
done

# ---------- 1. master key + certificate (identity card of both servers) --------
step "1. Master key and certificate"
for host in "$PRIMARY" "$REPLICA"; do
  # The master key protects the certificate's private key inside 'master'.
  run "$host" master "
    IF NOT EXISTS (SELECT 1 FROM sys.symmetric_keys WHERE name = '##MS_DatabaseMasterKey##')
        CREATE MASTER KEY ENCRYPTION BY PASSWORD = '$AG_CERT_PASSWORD';"
done

run "$PRIMARY" master "
  IF NOT EXISTS (SELECT 1 FROM sys.certificates WHERE name = 'dbm_certificate')
      CREATE CERTIFICATE dbm_certificate WITH SUBJECT = 'dbm';"

REPLICA_HAS_CERT="$(scalar "$REPLICA" master "SELECT CONVERT(varchar(10), COUNT(*)) FROM sys.certificates WHERE name = 'dbm_certificate'")"
if [ "$REPLICA_HAS_CERT" = "0" ]; then
  echo "Copying the certificate from primary to replica (as hex text)..."
  CERT_HEX="$(scalar_long "$PRIMARY" master "CONVERT(varchar(max), CERTENCODED(CERT_ID('dbm_certificate')), 1)")"
  KEY_HEX="$(scalar_long "$PRIMARY" master "CONVERT(varchar(max), CERTPRIVATEKEY(CERT_ID('dbm_certificate'), '$AG_CERT_PASSWORD'), 1)")"

  # Guard: a cut or empty value would create a broken certificate.
  for value in "$CERT_HEX" "$KEY_HEX"; do
    if ! [[ "$value" =~ ^0x[0-9A-Fa-f]+$ ]] || [ "${#value}" -lt 100 ]; then
      echo "ERROR: certificate export returned an invalid value (length ${#value})."
      exit 1
    fi
  done

  run "$REPLICA" master "
    CREATE CERTIFICATE dbm_certificate FROM BINARY = $CERT_HEX
    WITH PRIVATE KEY (BINARY = $KEY_HEX, DECRYPTION BY PASSWORD = '$AG_CERT_PASSWORD');"
else
  echo "Replica already has the certificate."
fi

# Same thumbprint on both = same ID card. Stop here if not.
T1="$(scalar "$PRIMARY" master "SELECT CONVERT(varchar(100), thumbprint, 1) FROM sys.certificates WHERE name = 'dbm_certificate'")"
T2="$(scalar "$REPLICA" master "SELECT CONVERT(varchar(100), thumbprint, 1) FROM sys.certificates WHERE name = 'dbm_certificate'")"
if [ -z "$T1" ] || [ "$T1" != "$T2" ]; then
  echo "ERROR: certificate thumbprints differ. primary=$T1 replica=$T2"; exit 1
fi
echo "Thumbprints match: $T1"

# ---------- 2. mirroring endpoint + login (the private 'phone line', port 5022) -
step "2. Endpoints and logins"
for host in "$PRIMARY" "$REPLICA"; do
  run "$host" master "
    IF NOT EXISTS (SELECT 1 FROM sys.database_mirroring_endpoints)
        CREATE ENDPOINT [Hadr_endpoint] AS TCP (LISTENER_PORT = 5022)
        FOR DATABASE_MIRRORING (ROLE = ALL, AUTHENTICATION = CERTIFICATE dbm_certificate,
                                ENCRYPTION = REQUIRED ALGORITHM AES);"
  run "$host" master "ALTER ENDPOINT [Hadr_endpoint] STATE = STARTED;"
  run "$host" master "
    IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'dbm_login')
        CREATE LOGIN dbm_login FROM CERTIFICATE dbm_certificate;"
  run "$host" master "GRANT CONNECT ON ENDPOINT::Hadr_endpoint TO dbm_login;"
  echo "$host: endpoint and login ready."
done

# ---------- 3. MainDb + heartbeat table on the PRIMARY -------------------------
step "3. MainDb and the consistency heartbeat"
# WHY we create MainDb here: the heartbeat table must exist before the host starts,
# and an Availability Group needs an existing database. EF migrations later run on
# the existing database without problems, and they replicate to the replica.
run "$PRIMARY" master "IF DB_ID('$DB') IS NULL CREATE DATABASE [$DB];"
run "$PRIMARY" "$DB" "IF SCHEMA_ID('consistency') IS NULL EXEC('CREATE SCHEMA consistency');"
run "$PRIMARY" "$DB" "
  IF OBJECT_ID('consistency.Heartbeat', 'U') IS NULL
      CREATE TABLE consistency.Heartbeat (
          Id     INT       NOT NULL PRIMARY KEY,
          Beat   BIGINT    NOT NULL,
          RowVer ROWVERSION NOT NULL);"
run "$PRIMARY" "$DB" "
  IF NOT EXISTS (SELECT 1 FROM consistency.Heartbeat WHERE Id = 1)
      INSERT INTO consistency.Heartbeat (Id, Beat) VALUES (1, 0);"

# ---------- 4. Availability Group on the primary --------------------------------
step "4. Availability Group"
AG_EXISTS="$(scalar "$PRIMARY" master "SELECT CONVERT(varchar(10), COUNT(*)) FROM sys.availability_groups WHERE name = '$AG'")"
if [ "$AG_EXISTS" = "0" ]; then
  # FULL recovery + one full backup are required by SQL Server before a database
  # can join a group. We never restore this backup (seeding copies the data).
  run "$PRIMARY" master "ALTER DATABASE [$DB] SET RECOVERY FULL;"
  run "$PRIMARY" master "BACKUP DATABASE [$DB] TO DISK = '/var/opt/mssql/data/${DB}_init.bak' WITH INIT;"

  # CLUSTER_TYPE = NONE      -> no cluster, no automatic failover (read copy only)
  # ASYNCHRONOUS_COMMIT      -> primary never waits for the replica (fast, causes lag)
  # SEEDING_MODE = AUTOMATIC -> SQL Server copies the database to the replica itself
  # replica READ_ONLY        -> only connections with ApplicationIntent=ReadOnly are accepted
  run "$PRIMARY" master "
    CREATE AVAILABILITY GROUP [$AG] WITH (CLUSTER_TYPE = NONE)
    FOR DATABASE [$DB]
    REPLICA ON
      N'$PRIMARY' WITH (ENDPOINT_URL = N'tcp://$PRIMARY:5022',
          AVAILABILITY_MODE = ASYNCHRONOUS_COMMIT, FAILOVER_MODE = MANUAL,
          SEEDING_MODE = AUTOMATIC, SECONDARY_ROLE (ALLOW_CONNECTIONS = ALL)),
      N'$REPLICA' WITH (ENDPOINT_URL = N'tcp://$REPLICA:5022',
          AVAILABILITY_MODE = ASYNCHRONOUS_COMMIT, FAILOVER_MODE = MANUAL,
          SEEDING_MODE = AUTOMATIC, SECONDARY_ROLE (ALLOW_CONNECTIONS = READ_ONLY));"
else
  echo "Availability group '$AG' already exists on the primary."
fi

# ---------- 5. Replica joins ----------------------------------------------------
step "5. Replica joins the group"
JOINED="$(scalar "$REPLICA" master "SELECT CONVERT(varchar(10), COUNT(*)) FROM sys.availability_groups WHERE name = '$AG'")"
if [ "$JOINED" = "0" ]; then
  run "$REPLICA" master "ALTER AVAILABILITY GROUP [$AG] JOIN WITH (CLUSTER_TYPE = NONE);"
else
  echo "Replica already joined."
fi
# Lets the group create MainDb on the replica during automatic seeding.
run "$REPLICA" master "ALTER AVAILABILITY GROUP [$AG] GRANT CREATE ANY DATABASE;"

# ---------- 6. Wait until seeding finished --------------------------------------
step "6. Waiting for MainDb to be copied to the replica"
STATE=""
for _ in $(seq 1 90); do
  STATE="$(scalar "$REPLICA" master "SELECT TOP 1 synchronization_state_desc FROM sys.dm_hadr_database_replica_states WHERE is_local = 1" || true)"
  case "$STATE" in SYNCHRONIZING|SYNCHRONIZED) break ;; esac
  sleep 2
done
case "$STATE" in
  SYNCHRONIZING|SYNCHRONIZED) echo "MainDb on the replica: $STATE" ;;
  *) echo "ERROR: replica did not reach SYNCHRONIZING in 3 minutes (state: '${STATE:-none}')."; exit 1 ;;
esac

# ---------- 7. End-to-end verification ------------------------------------------
step "7. Verification"
# 7a. A write on the primary must appear on the replica. We use the heartbeat itself:
#     it is exactly what the app's consistency token is made of.
TOKEN="$(scalar "$PRIMARY" "$DB" "UPDATE consistency.Heartbeat SET Beat = Beat + 1 OUTPUT CONVERT(bigint, inserted.RowVer) WHERE Id = 1")"
if ! [[ "$TOKEN" =~ ^[0-9]+$ ]]; then echo "ERROR: could not read the heartbeat token on the primary ('$TOKEN')."; exit 1; fi

SEEN=""
for _ in $(seq 1 30); do
  SEEN="$(scalar "$REPLICA" "$DB" "SELECT CONVERT(bigint, RowVer) FROM consistency.Heartbeat WHERE Id = 1" -K ReadOnly 2>/dev/null || true)"
  if [[ "$SEEN" =~ ^[0-9]+$ ]] && [ "$SEEN" -ge "$TOKEN" ]; then break; fi
  sleep 1
done
if ! [[ "$SEEN" =~ ^[0-9]+$ ]] || [ "$SEEN" -lt "$TOKEN" ]; then
  echo "FAIL 7a: replica heartbeat '$SEEN' did not reach primary token '$TOKEN'."; exit 1
fi
echo "PASS 7a: primary token $TOKEN reached the replica (replica sees $SEEN)."

# 7b. A write on the replica must be refused (database is read-only).
if run "$REPLICA" "$DB" "UPDATE consistency.Heartbeat SET Beat = Beat WHERE Id = 1" -K ReadOnly >/dev/null 2>&1; then
  echo "FAIL 7b: a write on the replica was accepted."; exit 1
fi
echo "PASS 7b: writes on the replica are refused."

# 7c. A connection WITHOUT ReadOnly intent must be refused (READ_ONLY mode is active).
if run "$REPLICA" "$DB" "SELECT 1" >/dev/null 2>&1; then
  echo "FAIL 7c: replica accepted a connection without ApplicationIntent=ReadOnly."; exit 1
fi
echo "PASS 7c: connections without ReadOnly intent are refused."

echo ""
echo "Group health (expect PRIMARY/SECONDARY, CONNECTED, HEALTHY):"
run "$PRIMARY" master "
  SELECT ar.replica_server_name, ars.role_desc, ars.connected_state_desc, ars.synchronization_health_desc
  FROM sys.availability_replicas ar
  JOIN sys.dm_hadr_availability_replica_states ars ON ar.replica_id = ars.replica_id;"

echo ""
echo "REPLICA SETUP COMPLETE"