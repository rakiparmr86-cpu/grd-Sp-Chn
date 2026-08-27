# React login, dashboard, and HR user creation

## What this frontend does

The React application in `src/frontend/grd-spchn-web` provides the first ERP web
shell.

The login card uses the supplied GRD artwork from
`src/frontend/grd-spchn-web/public/assets/grd-logo.png`. The asset is stored inside
the React project so the application does not read files from the legacy
`D:\grd_new3\grd-web` repository at runtime.

Authentication flow:

1. The user enters only a user name and password. Login has no role, access-type or
   business-workspace selector.
2. React sends `POST /api/identity/auth/login` to YARP on port `7000`.
3. YARP forwards the request to Identity on port `7001`.
4. Identity verifies the PBKDF2 password, reads the user's assigned access profile
   from MySQL, and returns a JWT plus role, organization-unit scope, and permissions.
5. React keeps the session in `sessionStorage` and renders permitted dashboard
   actions.
6. An HR Manager sees **Create employee user**. Other roles do not see this action.
7. HR loads organization units through YARP and submits the new user to Identity.

The browser never decides a login user's type, role or permissions. During employee
creation, HR selects only from profiles returned by Identity. Identity validates the
profile against MySQL and derives its role and permissions from backend tables.
Director and General Manager profiles are deliberately not HR-assignable even if a
database flag is accidentally changed.

## Backend access-assignment tables

| Table | Owner and purpose |
| --- | --- |
| `identity_users` | Stores credentials, organization scope and `access_profile_code`. |
| `identity_access_profiles` | Defines profile name, role, active state and whether HR may assign it. |
| `identity_access_profile_permissions` | Maps each profile to its permission codes. |
| `identity_user_permissions` | Legacy migration table; retained temporarily but no longer used during login. |

Example backend resolution:

```text
manager.purchase@grd.local
  -> identity_users.access_profile_code = PurchaseManager
  -> identity_access_profiles.role_name = Manager
  -> identity_access_profile_permissions
       procurement.material-request.read
       procurement.material-request.approve
       procurement.purchase-order.create
       procurement.purchase-order.read
       inventory.stock.read
       organization.read
```

Changing a profile's permission rows changes newly issued JWTs after the user signs
in again. No React code change is required.

## Local addresses

| Process | Address | Purpose |
| --- | --- | --- |
| React UI | `http://localhost:5173` | Login and ERP dashboard. |
| API Gateway | `http://localhost:7000` | The only backend address used by React. |
| Identity | `http://localhost:7001` | Login, JWT issuance and HR user creation. |
| Organization | `http://localhost:5218` | Organization-unit choices for user assignment. |

Vite proxies `/api` to `http://localhost:7000`, which avoids local cross-origin
configuration. For another environment, copy `.env.example` to `.env.local` and set
`VITE_API_BASE_URL` to that environment's Gateway origin.

## Local demo accounts

All seeded local accounts use password `1223456`.

| User name | Role / purpose |
| --- | --- |
| `manager.hr@grd.local` | HR Manager; can read organization units and create users. |
| `manager.purchase@grd.local` | Purchase Manager. |
| `supervisor.plant@grd.local` | Plant / receiving Supervisor. |
| `executive.boiler@grd.local` | Consumption-unit Executive. |
| `gm.north@grd.local` | Regional General Manager. |
| `director@grd.local` | Director and local administrative account. |

These credentials are development data only. `1223456` is intentionally easy for a
local demonstration and must never be used in a shared, staging, or production
environment.

## Apply the seed to an existing local database

Docker init SQL runs only when the MySQL volume is first created. If the volume
already exists, run this non-destructive, idempotent local migration:

```powershell
pwsh -NoProfile -File scripts\apply-local-identity-seed.ps1
```

It updates the listed local demo password hashes, inserts the HR Manager when
missing, creates the access-profile tables, seeds profile permissions and assigns
the known demo users to profiles.

## Install and run

First-time frontend package installation requires npm registry access:

```powershell
Set-Location D:\newdata\grd-Sp-Chn\src\frontend\grd-spchn-web
npm install
Set-Location D:\newdata\grd-Sp-Chn
```

Then either run all enabled processes:

```powershell
pwsh -NoProfile -File scripts\start-local-services.ps1
```

or run only the minimum login/user-management processes in separate terminals:

```powershell
.\scripts\start-local-services.ps1 -Service ApiGateway
.\scripts\start-local-services.ps1 -Service Identity
.\scripts\start-local-services.ps1 -Service Organization
.\scripts\start-local-services.ps1 -Service Web
```

Open `http://localhost:5173` and enter `manager.hr@grd.local` with the local demo
password. Identity—not the login form—determines that this account is an HR Manager.

## API authorization

| Endpoint | Authorization | Purpose |
| --- | --- | --- |
| `POST /api/identity/auth/login` | Anonymous | Validate credentials and issue JWT. |
| `GET /api/identity/users/access-profiles` | `identity.user.create` | Return HR-assignable profiles. |
| `POST /api/identity/users` | `identity.user.create` | Insert a user with a validated database-owned profile assignment. |
| `GET /api/organization/units` | `organization.read` | Populate the organization selector. |

Passwords are PBKDF2-SHA256 hashed with a random salt before storage. The create-user
response never returns a password or password hash.

## Production work still required

- Replace demo passwords and signing key with managed secrets.
- Enforce first-login password change and stronger password policy.
- Add refresh-token rotation, logout/revocation, lockout and audit events.
- Add HR scope rules so a regional HR user can assign only descendant units.
- Serve the production React build behind a managed web host/reverse proxy.
- Add browser automation after frontend dependencies are available in CI.
