# EfVsDapper

Sample project comparing EF Core and Dapper.

## 1. Start PostgreSQL via Docker Compose (current repo setup)
The repository already contains `docker-compose.yml` that starts ONLY PostgreSQL (the API runs locally on the host machine).

```bash
docker compose up -d
```
(or `docker-compose up -d` for older Docker)

Check:
```bash
docker ps
# You should see a container named: postgres
```

Connection string in `API/appsettings.json` is correct for this scenario:
```
Host=localhost;Port=5432;Database=efvsdapper_db;Username=postgres;Password=postgres
```
If somewhere you accidentally use `Host=postgres` while the API runs on the host, it will fail (no such DNS name). `Program.cs` additionally normalizes this.

## 2. Verify the database
```bash
docker exec -it postgres psql -U postgres -d efvsdapper_db -c "\dt"
```
(Container name is `postgres` because it is set that way in compose.)

## 3. Apply migrations (?????????? ????? ??)
The app automatically runs `Database.Migrate()` on startup and seeds data if the DB is empty. If you prefer to apply migrations manually (or after changing the model):

Install EF tooling if needed:
```bash
dotnet tool install --global dotnet-ef
```
Add a new migration after model changes (example):
```bash
dotnet ef migrations add AddSomething --project API --startup-project API
```
Apply migrations to the database:
```bash
dotnet ef database update --project API --startup-project API
```
List migrations:
```bash
dotnet ef migrations list --project API --startup-project API
```

(Existing initial migration: `InitialCreate`.)

## 4. Run the API (on the host)
```bash
dotnet restore
dotnet build
dotnet run --project API
```
Swagger: https://localhost:****/swagger

## 5. (Optional) Run BOTH API and DB in Docker
If you later want to containerize the API as well, extend `docker-compose.yml`:
```yaml
services:
  postgres:
    image: postgres:16
    container_name: postgres
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: efvsdapper_db
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
  api:
    build:
      context: .
      dockerfile: API/Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: Host=postgres;Port=5432;Database=efvsdapper_db;Username=postgres;Password=postgres
    depends_on:
      - postgres
    ports:
      - "5000:8080"
volumes:
  pgdata:
```
Then the connection string must use `Host=postgres` (service name inside compose network).

## 6. Clean up
```bash
docker compose down -v
```

## 7. Useful commands
- Logs: `docker logs -f postgres`
- Recreate a clean DB: `docker compose down -v && docker compose up -d`
- Check port usage: `netstat -ano | find "5432"` (Windows) / `lsof -i :5432` (Linux/macOS)

Done. Run and experiment.