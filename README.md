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

## Tro ly AI "Be Coi" (LM Studio)

Backend goi LM Studio (API kieu OpenAI) roi tra loi cho frontend, nen trang https khong phai goi thang `http://localhost`.

1. Mo LM Studio, tai va load model (vd `qwen3-vl-8b`, model co "vl" thi doc duoc anh).
2. Tab Developer: bat **Start Server**, ghi lai port (mac dinh 1234).
3. Dat bien moi truong cho backend (chay cung may voi LM Studio):
   - `LMSTUDIO_BASE_URL`: mac dinh `http://localhost:1234/v1`. Neu LM Studio chay port khac thi doi, vd `http://localhost:1235/v1`.
   - `LMSTUDIO_MODEL`: de trong thi tu lay model dau tien dang load.
   - Tuy chon: `LMSTUDIO_API_KEY`, `LMSTUDIO_TIMEOUT_SECONDS` (120), `LMSTUDIO_MAX_TOKENS` (700), `LMSTUDIO_TEMPERATURE` (0.6), `LMSTUDIO_HISTORY` (10 tin nhan gan nhat gui kem).

```powershell
$env:LMSTUDIO_BASE_URL = "http://localhost:1235/v1"
.\start-server.ps1
```

API (can JWT, moi tai khoan chi thay tin nhan cua minh, luu trong bang `ChatMessages`):

- `GET /api/assistant/status`: kiem tra ket noi LM Studio va model dang dung.
- `GET /api/assistant/messages?limit=50`: lich su chat.
- `POST /api/assistant/chat` voi `{ "Message": "...", "ImageBase64": "data:image/jpeg;base64,..." }` (anh tuy chon, toi da 4 MB).
- `DELETE /api/assistant/messages`: xoa lich su.

Bang `ChatMessages` tu tao khi backend khoi dong (SQL Server, PostgreSQL hoac in-memory).

## Dang nhap

- Frontend lay ID token tu Google Identity Services va goi `POST /api/auth/google`.
- Backend kiem tra token voi Google, tao/cap nhat dong trong bang `AccountProfiles` va tra ve token dang nhap rieng (JWT, han 7 ngay).
- Frontend gui token trong header `Authorization: Bearer ...`. Vai tro va trang thai khoa duoc doc tu database moi request.
- Dang ky bang email: `POST /api/auth/register` (`DisplayName`, `Email`, `Password` >= 8 ky tu). Dang nhap: `POST /api/auth/login`. Mat khau duoc bam bang `PasswordHasher` cua ASP.NET Core Identity.
- Moi email chi co mot tai khoan. Neu email da dang ky bang mat khau, dang nhap Google cung email se vao dung tai khoan do.
- Tai khoan moi mac dinh la `user`. Admin doi vai tro qua `PATCH /api/admin/accounts/{id}/role`.

## Ghi chu

- File ban do da duoc copy vao `Data/Maps/hcm-boundary.geojson`.
- Khong co `package.json` vi backend hien tai la project .NET, khong phai Node.js.
- Route cu nhu `/api/auth/*`, `/api/incidents`, `/api/audit-logs` van duoc giu lai de tuong thich.
