#!/bin/bash
# Bootstraps this node as a streaming replica of postgres-1, then hands off
# to the normal postgres entrypoint. Only runs the clone step the first time
# (when the bind-mounted data directory is still empty) so restarts don't
# re-clone.
set -e

if [ -z "$(ls -A "$PGDATA" 2>/dev/null)" ]; then
    echo "postgres-replica: data directory empty, cloning from postgres-1 via pg_basebackup..."
    until pg_basebackup -h postgres-1 -D "$PGDATA" -U replicator -Fp -Xs -P -R; do
        echo "postgres-replica: postgres-1 not ready yet, retrying in 5s..."
        sleep 5
    done
    chmod 0700 "$PGDATA"
fi

exec docker-entrypoint.sh postgres
