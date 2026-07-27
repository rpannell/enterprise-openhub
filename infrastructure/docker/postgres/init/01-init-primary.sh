#!/bin/bash
# Runs once, only on postgres-1, only when its data directory is first created.
# Sets up: the replication role used by postgres-2/postgres-3, and the
# application databases/roles that Keycloak and Kong expect to already exist.
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    CREATE ROLE replicator WITH REPLICATION LOGIN PASSWORD 'replicator';

    CREATE USER keycloak WITH PASSWORD 'keycloak';
    CREATE DATABASE keycloak OWNER keycloak;

    CREATE USER kong WITH PASSWORD 'kong';
    CREATE DATABASE kong OWNER kong;
EOSQL

# Default pg_hba.conf only allows replication connections from localhost.
# postgres-2/postgres-3 connect over the docker network, so they need an
# explicit entry.
cat >> "$PGDATA/pg_hba.conf" <<-EOF
host    replication     replicator      0.0.0.0/0               scram-sha-256
EOF
