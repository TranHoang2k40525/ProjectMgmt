# Local MySQL and Ollama

The Compose stack is for an isolated local environment. It does not mount or execute `projectmgmt_schema_mysql.sql`, because that file contains `DROP DATABASE` and the existing database must not be recreated.

## Start safely

1. Confirm port 3306 is not already used by the existing MySQL instance, or change `MYSQL_PORT`.
2. Copy `.env.example` to `.env` and replace both `CHANGE_ME` values.
3. Start services:

```powershell
docker compose config
docker compose up -d
docker compose ps
```

`lower_case_table_names=1` is passed before the MySQL data volume is initialized. MySQL does not support changing it on an already initialized volume.

## Logs and stop

```powershell
docker compose logs -f mysql
docker compose logs -f ollama
docker compose down
```

`docker compose down` preserves named volumes.

## Pull and test Qwen

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\pull-ollama-model.ps1
Invoke-RestMethod http://localhost:11434/api/tags
```

## Destructive reset warning

`docker compose down --volumes` permanently removes the Compose-managed MySQL and Ollama data. It must be run only after confirming the target is this isolated Compose project and the data can be discarded. It is never part of normal startup or CI.
