#!/bin/bash
# Inicializa la BD en Docker: 01 solo si BonusTest no existe (no es idempotente); 02 y 03 siempre (lo son).
set -euo pipefail
SQLCMD=(/opt/mssql-tools18/bin/sqlcmd -S sql -U sa -P "$MSSQL_SA_PASSWORD" -C -I -b)

exists=$("${SQLCMD[@]}" -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = 'BonusTest'")
if [ "$exists" = "0" ]; then
  "${SQLCMD[@]}" -i /database/01_schema_seed.sql
else
  echo "BonusTest ya existe: se omite 01_schema_seed.sql"
fi
"${SQLCMD[@]}" -i /database/02_bonus.sql
"${SQLCMD[@]}" -i /database/03_bonus_payment_detail.sql
echo "Base de datos lista."
