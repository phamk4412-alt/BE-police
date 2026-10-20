# Police Backend

Backend duoc tach rieng tu project goc, giu frontend o repo/doc lap khac.

## Cong nghe

- ASP.NET Core .NET 9
- Entity Framework Core
- SignalR

## Cau truc

```text
BACKEND/
|-- Config/
|   `-- Cau hinh CORS, database, DI, role/policy.
|-- Controllers/
|   `-- Handler cho tung nhom API: Auth, User, Police, Support, Admin.
|-- Data/
|   `-- Maps/
|-- Database/
|   `-- DbContext va bootstrap du lieu.
|-- Middleware/
|   `-- Middleware xu ly request truoc khi vao endpoint.
|-- Models/
|   `-- DTO/request/response/domain model dung chung.
|-- Modules/
|   |-- Admin/
|   |-- Police/
|   |-- Support/
|   `-- User/
|-- Routes/
|   `-- Route dung chung va realtime endpoint.
|-- Services/
|   `-- Realtime/
|-- Utils/
|   `-- Helper nho, khong giu business logic chinh.
|-- Program.cs
|-- PoliceBackend.csproj
|-- appsettings.json
`-- appsettings.example.json
```

## Tim file nhanh

- Muon sua URL/API nao duoc map o dau: vao `Modules/<Nhom>/<Nhom>Module.cs`.
- Muon sua logic xu ly request: vao `Controllers/<Nhom>Controller.cs`.
- Muon sua nghiep vu chinh, truy van, xuat CSV, auth, audit: vao `Services/`.
- Muon sua model request/response: vao `Models/`.
- Muon sua cau hinh database, CORS, policy, dependency injection: vao `Config/`.
- Muon sua DbContext hoac du lieu khoi tao: vao `Database/`.
- Muon sua SignalR realtime: vao `Services/Realtime/IncidentHub.cs` va `Routes/RealtimeRoutes.cs`.
- Muon sua file ban do/tai nguyen tinh: vao `Data/Maps/`.

## Chay local

1. Cai .NET SDK 9.0 tren may chay backend.
2. Chinh `appsettings.json` hoac bien moi truong:
   - `POLICE_DATABASE_PROVIDER`
   - `POLICE_SQLSERVER_CONNECTION`
   - `POLICE_POSTGRES_CONNECTION`
   - `GOOGLE_CLIENT_ID`: OAuth Client ID (Web) tu Google Cloud Console, giong voi `VITE_GOOGLE_CLIENT_ID` o frontend.
   - `AUTH_JWT_SECRET`: chuoi ngau nhien >= 32 ky tu de ky token dang nhap. Thieu bien nay thi moi lan restart se dang xuat tat ca.
   - `POLICE_ADMIN_EMAILS`: danh sach email Google (cach nhau bang dau phay) tu dong duoc cap vai tro admin khi dang nhap.
3. Chay:

```powershell
dotnet run --project .\PoliceBackend.csproj --urls "http://0.0.0.0:5055"
```

Hoac dung script:

```powershell
.\start-server.ps1
```

## Dang nhap

- Frontend lay ID token tu Google Identity Services va goi `POST /api/auth/google`.
- Backend kiem tra token voi Google, tao/cap nhat dong trong bang `AccountProfiles` va tra ve token dang nhap rieng (JWT, han 7 ngay).
- Frontend gui token trong header `Authorization: Bearer ...`. Vai tro va trang thai khoa duoc doc tu database moi request.
- Tai khoan moi mac dinh la `user`. Admin doi vai tro qua `PATCH /api/admin/accounts/{id}/role`.

## Ghi chu

- File ban do da duoc copy vao `Data/Maps/hcm-boundary.geojson`.
- Khong co `package.json` vi backend hien tai la project .NET, khong phai Node.js.
- Route cu nhu `/api/auth/*`, `/api/incidents`, `/api/audit-logs` van duoc giu lai de tuong thich.
