# Police Backend

Backend duoc tach rieng tu project goc, giu frontend o repo/doc lap khac.

## Cong nghe

- ASP.NET Core .NET 8
- Entity Framework Core
- SignalR

## Cau truc

```text
BACKEND/
|-- config/
|-- database/
|-- middleware/
|-- services/
|-- utils/
|-- data/
|   |-- maps/
|   |-- reports/
|   `-- crime/
|-- modules/
|   |-- user/
|   |-- police/
|   |-- support/
|   `-- admin/
|-- routes/
|-- controllers/
|-- models/
|-- Program.cs
|-- PoliceBackend.csproj
|-- appsettings.json
`-- appsettings.example.json
```

## Chay local

1. Cai .NET SDK 8.0 tren may chay backend.
2. Chinh `appsettings.json` hoac bien moi truong:
   - `POLICE_DATABASE_PROVIDER`
   - `POLICE_SQLSERVER_CONNECTION`
   - `POLICE_POSTGRES_CONNECTION`
3. Chay:

```powershell
dotnet run --project .\PoliceBackend.csproj --urls "http://0.0.0.0:5055"
```

Hoac dung script:

```powershell
.\start-server.ps1
```

## Ghi chu

- File ban do da duoc copy vao `data/maps/hcm-boundary.geojson`.
- Khong co `package.json` vi backend hien tai la project .NET, khong phai Node.js.
- Route cu nhu `/api/auth/*`, `/api/incidents`, `/api/audit-logs` van duoc giu lai de tuong thich.
