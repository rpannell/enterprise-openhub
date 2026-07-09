#!/bin/sh
# One-shot dev-data seeder for OpenHub.SalesOrg.Api — creates a small, realistic
# sales hierarchy (roles -> nested org units -> people -> assignments) via the
# API's own REST endpoints, so the new "Sales Hierarchy" viewer in CRM.UI
# (src/CRM/OpenHub.CRM.UI/src/salesorg/SalesHierarchyView.tsx) has something
# non-trivial to show: a manager chain and a set of reports for at least one
# sales person.
#
# Requires: bash/sh, curl, jq. Not run automatically anywhere — this is a
# manual dev convenience, safe to re-run (each run creates a fresh set of
# records; the API has no "upsert by name" semantics, so re-running duplicates
# rather than updates).
#
# --- Prerequisites -----------------------------------------------------
# 1. Start the platform locally so Keycloak + Postgres + SalesOrg.Api are up:
#      cd src/Platform/OpenHub.Platform.AppHost && dotnet run
#    Keycloak listens on http://localhost:8080 (realm "client-a"), SalesOrg.Api
#    on http://localhost:5500 (see AppHost.cs).
#
# 2. Get a bearer token for the client-a tenant, user alice (who has the
#    "sales-org-admin" role in the dev realm — see
#    src/Platform/OpenHub.Platform.AppHost/Keycloak/client-a-realm.json).
#
#    The browser-facing client "micro-ui-shell" does NOT have
#    directAccessGrantsEnabled (Keycloak's password grant), so a plain
#    `curl ... grant_type=password` against it will be rejected out of the
#    box. Two ways to get a token for this script:
#
#    a) Easiest: log into the app in a browser as alice/P@ssw0rd1, open
#       devtools -> Network, find any request to /crmapi or /salesorgapi, and
#       copy its "Authorization: Bearer <token>" header value. Export it:
#         export BEARER_TOKEN="eyJ..."
#       (Access tokens are short-lived — grab a fresh one if you get 401s.)
#
#    b) For a scriptable token, temporarily flip
#       "directAccessGrantsEnabled": true for the "micro-ui-shell" client in
#       client-a-realm.json (or via the Keycloak admin console at
#       http://localhost:8080), restart Keycloak, then:
#         curl -s -X POST \
#           "http://localhost:8080/realms/client-a/protocol/openid-connect/token" \
#           -H "Content-Type: application/x-www-form-urlencoded" \
#           -d "grant_type=password" \
#           -d "client_id=micro-ui-shell" \
#           -d "username=alice" \
#           -d "password=P@ssw0rd1" \
#         | jq -r .access_token
#       Set the result as BEARER_TOKEN. Revert the realm change afterwards —
#       don't leave direct grants enabled.
#
# --- Usage ---------------------------------------------------------------
#   export BEARER_TOKEN="eyJ..."
#   ./seed-dev-data.sh
#
# Optional overrides:
#   SALESORG_API_BASE  (default: http://localhost:5500)
set -eu

API="${SALESORG_API_BASE:-http://localhost:5500}"
: "${BEARER_TOKEN:?Set BEARER_TOKEN to a valid client-a bearer token first — see the comment header in this script.}"

auth_curl() {
  curl -sS -X "$1" "$API$2" \
    -H "Authorization: Bearer $BEARER_TOKEN" \
    -H "Content-Type: application/json" \
    -d "$3"
}

echo "[seed-salesorg] creating sales roles..."
ROLE_REP_ID=$(auth_curl POST /api/v1/sales-roles \
  '{"name":"Sales Representative","description":"Front-line account owner.","hierarchyLevel":1}' \
  | jq -r .id)
ROLE_TM_ID=$(auth_curl POST /api/v1/sales-roles \
  '{"name":"Territory Manager","description":"Leads a territory of sales representatives.","hierarchyLevel":2}' \
  | jq -r .id)
ROLE_DM_ID=$(auth_curl POST /api/v1/sales-roles \
  '{"name":"District Manager","description":"Leads multiple territories within a district.","hierarchyLevel":3}' \
  | jq -r .id)
echo "  Sales Representative = $ROLE_REP_ID"
echo "  Territory Manager    = $ROLE_TM_ID"
echo "  District Manager     = $ROLE_DM_ID"

echo "[seed-salesorg] creating org units..."
UNIT_DISTRICT_ID=$(auth_curl POST /api/v1/sales-org-units \
  '{"name":"Carolinas District","description":"NC + SC.","organizationUnitType":"district"}' \
  | jq -r .id)
UNIT_TERRITORY_ID=$(auth_curl POST /api/v1/sales-org-units \
  "{\"name\":\"Charlotte Territory\",\"description\":\"Greater Charlotte metro.\",\"organizationUnitType\":\"territory\",\"parentOrganizationUnitId\":\"$UNIT_DISTRICT_ID\"}" \
  | jq -r .id)
echo "  Carolinas District  = $UNIT_DISTRICT_ID"
echo "  Charlotte Territory = $UNIT_TERRITORY_ID (child of Carolinas District)"

echo "[seed-salesorg] creating sales people..."
PERSON_DM_ID=$(auth_curl POST /api/v1/sales-people \
  '{"firstName":"Dana","lastName":"Dominguez","email":"dana.dominguez@example.com","jobTitle":"District Manager"}' \
  | jq -r .id)
PERSON_TM_ID=$(auth_curl POST /api/v1/sales-people \
  '{"firstName":"Tariq","lastName":"Muller","email":"tariq.muller@example.com","jobTitle":"Territory Manager"}' \
  | jq -r .id)
PERSON_REP1_ID=$(auth_curl POST /api/v1/sales-people \
  '{"firstName":"Rosa","lastName":"Espinoza","email":"rosa.espinoza@example.com","jobTitle":"Sales Representative"}' \
  | jq -r .id)
PERSON_REP2_ID=$(auth_curl POST /api/v1/sales-people \
  '{"firstName":"Priya","lastName":"Patel","email":"priya.patel@example.com","jobTitle":"Sales Representative"}' \
  | jq -r .id)
echo "  Dana Dominguez (District Manager)  = $PERSON_DM_ID"
echo "  Tariq Muller (Territory Manager)   = $PERSON_TM_ID"
echo "  Rosa Espinoza (Sales Rep)          = $PERSON_REP1_ID"
echo "  Priya Patel (Sales Rep)            = $PERSON_REP2_ID"

echo "[seed-salesorg] creating assignments..."
TODAY=$(date +%Y-%m-%d)
auth_curl POST /api/v1/sales-assignments \
  "{\"salesPersonId\":\"$PERSON_DM_ID\",\"organizationUnitId\":\"$UNIT_DISTRICT_ID\",\"roleId\":\"$ROLE_DM_ID\",\"effectiveStartDate\":\"$TODAY\"}" \
  > /dev/null
auth_curl POST /api/v1/sales-assignments \
  "{\"salesPersonId\":\"$PERSON_TM_ID\",\"organizationUnitId\":\"$UNIT_TERRITORY_ID\",\"roleId\":\"$ROLE_TM_ID\",\"effectiveStartDate\":\"$TODAY\"}" \
  > /dev/null
auth_curl POST /api/v1/sales-assignments \
  "{\"salesPersonId\":\"$PERSON_REP1_ID\",\"organizationUnitId\":\"$UNIT_TERRITORY_ID\",\"roleId\":\"$ROLE_REP_ID\",\"effectiveStartDate\":\"$TODAY\"}" \
  > /dev/null
auth_curl POST /api/v1/sales-assignments \
  "{\"salesPersonId\":\"$PERSON_REP2_ID\",\"organizationUnitId\":\"$UNIT_TERRITORY_ID\",\"roleId\":\"$ROLE_REP_ID\",\"effectiveStartDate\":\"$TODAY\"}" \
  > /dev/null

echo "[seed-salesorg] done. Try the hierarchy viewer with Rosa Espinoza or Priya Patel selected:"
echo "  manager chain should show Tariq Muller then Dana Dominguez;"
echo "  Tariq Muller's reports should show Rosa Espinoza and Priya Patel;"
echo "  Dana Dominguez's reports should include everyone in the Charlotte Territory subtree."
