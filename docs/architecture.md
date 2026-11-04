# CloudHubs architecture

This document describes the architecture implemented in the current solution. The application is a layered ASP.NET Core MVC web app that hosts both browser pages and JSON APIs. Its business capabilities are organized into modules for organizations, platform administration, projects, subscriptions, and users.

## Solution structure

```text
CloudHubs/
├── CloudHubs.slnx
├── src/
│   ├── CloudHubs.Domain/
│   │   ├── Common/                   Shared entity base types
│   │   ├── Entities/                 Organization, user, project, subscription, and platform entities
│   │   └── Enums/                    Roles, permissions, statuses, and billing intervals
│   ├── CloudHubs.Application/
│   │   └── Modules/
│   │       ├── Organizations/
│   │       │   ├── DTOs/
│   │       │   ├── Interfaces/       Repository ports
│   │       │   └── Services/         Organization and tenant use cases
│   │       ├── Platform/             Settings and audit services/ports
│   │       ├── Projects/             Platform and organization project use cases
│   │       ├── Subscriptions/        Plans, purchases, and account subscriptions
│   │       └── Users/                User use cases and repository port
│   ├── CloudHubs.Infrastructure/
│   │   ├── Data/
│   │   │   ├── Configurations/       EF Core entity mappings
│   │   │   ├── Migrations/           Database schema history
│   │   │   └── CloudHubsDbContext.cs
│   │   ├── Modules/                  Repository implementations by capability
│   │   ├── Services/                 Infrastructure integrations, such as email
│   │   └── DependencyInjection.cs
│   └── CloudHubs.Web/
│       ├── Areas/
│       │   ├── Organization/         Organization portal controllers and views
│       │   └── Superuser/            Platform administration controllers and views
│       ├── Controllers/
│       │   ├── Api/                  JSON API controllers
│       │   └── SignupController.cs   Subscription signup pages
│       ├── Middleware/               Exception handling and audit logging
│       ├── Security/                 Credential validation, cookies, and token issuance
│       ├── ViewModels/               UI/API boundary models grouped by capability
│       ├── Views/                    Signup Razor views
│       ├── wwwroot/                  Static assets
│       └── Program.cs                Composition root and HTTP pipeline
└── tests/
    └── CloudHubs.UnitTests/           Domain and Application unit tests
```

The solution currently has four production projects. There is no separate Contracts project: application DTOs live with their corresponding Application module, while MVC/API binding models live in `CloudHubs.Web/ViewModels`.

## Project responsibilities and dependencies

| Project | Responsibility | Depends on |
|---|---|---|
| `CloudHubs.Domain` | Entities, shared entity behavior, enums, and domain rules | No other CloudHubs project |
| `CloudHubs.Application` | Use cases, application DTOs, and repository interfaces | Domain |
| `CloudHubs.Infrastructure` | EF Core persistence, repository implementations, migrations, and external integrations | Application and Domain |
| `CloudHubs.Web` | MVC pages, JSON APIs, authentication, middleware, and application startup | Application and Infrastructure |
| `CloudHubs.UnitTests` | Domain and Application unit tests | Domain and Application |

Dependency direction:

```text
CloudHubs.Web ────────> CloudHubs.Application ────────> CloudHubs.Domain
      │                          ▲
      └──> CloudHubs.Infrastructure ──────────────────┘

CloudHubs.Infrastructure implements the repository interfaces declared by Application.
CloudHubs.UnitTests ───> CloudHubs.Application and CloudHubs.Domain
```

`CloudHubs.Application.DependencyInjection` registers use-case services. `CloudHubs.Infrastructure.DependencyInjection` registers the EF Core context and repository implementations. `CloudHubs.Web/Program.cs` composes both into the host and registers web-specific services.

## Application modules

Application and Infrastructure use matching capability groupings:

- **Organizations** — organization lifecycle, membership and role management, permission checks, and tenant-scoped operations.
- **Platform** — platform settings and audit-log operations.
- **Projects** — organization projects and platform project catalog.
- **Subscriptions** — subscription plans, purchase requests, and account subscriptions, including plan-based organization limits.
- **Users** — user lookup and management.

Use cases depend on repository interfaces from Application; Infrastructure provides their EF Core-backed implementations. Application DTOs are used for application-level inputs/results and projections. Web view models are separate boundary types for forms and pages.

## HTTP surfaces and request flow

`CloudHubs.Web` serves the browser UI and JSON APIs from one ASP.NET Core host.

| Surface | Location | Main responsibilities |
|---|---|---|
| Organization portal | `Areas/Organization` | Login, organization selection/creation, projects, and members |
| Superuser portal | `Areas/Superuser` | Users, organizations, plans, purchases, projects, settings, and audit log |
| Subscription signup | `Controllers/SignupController.cs`, `Views/Signup` | Account signup, purchase completion, and signup-link validation |
| Tenant API | `Controllers/Api` | Tenant token issuance and organization-scoped project/member operations |
| Platform API | `Controllers/Api` | Superuser token issuance and platform organization endpoints |

Typical request path:

1. `Program.cs` configures the host, dependency injection, logging, authentication, rate limiting, and persistence.
2. The HTTP pipeline applies request logging, global exception handling, routing, CORS, rate limiting, authentication, authorization, audit middleware, and static-file handling.
3. An MVC area, page controller, or API controller binds input and calls an Application service.
4. Application services enforce use-case rules and call repository interfaces.
5. Infrastructure repositories use `CloudHubsDbContext` and EF Core to query or update PostgreSQL; integrations such as SMTP are implemented in Infrastructure.
6. Controllers return a Razor view/redirect or an API response.

API endpoints are routed under `/api`; health checks are exposed at `/health`, `/health/live`, and `/health/ready`. OpenAPI is mapped in Development.

## Persistence and domain model

`CloudHubs.Infrastructure/Data/CloudHubsDbContext.cs` is the EF Core context. Entity-specific mapping is in `Data/Configurations`, discovered from the Infrastructure assembly. Schema changes and the model snapshot are in `Data/Migrations`; PostgreSQL is configured by the Web host through Infrastructure dependency injection.

The principal domain concepts are:

- Organizations, organization memberships, and organization roles/permissions
- Users and platform roles
- Organization projects and platform projects
- Subscription plans, organization subscriptions, account subscriptions, and purchase requests
- Platform settings and audit log entries

Persistence interfaces are declared in Application modules and implementations are grouped under `CloudHubs.Infrastructure/Modules`. Keep schema changes in EF migrations; do not treat generated `bin/` or `obj/` output as source.

## Authentication and cross-cutting behavior

- **Superuser browser pages** use the default cookie authentication scheme and the `Superuser` role.
- **Organization portal pages** use a separate tenant cookie scheme and organization-member authorization.
- **JSON APIs** use bearer JWT authentication. Tenant tokens carry the `OrganizationMember` role; the platform organization API requires `Superuser`.
- JWT signing key, issuer, and audience use the `Superuser:Jwt` configuration section. The signing key must be at least 32 UTF-8 bytes.
- Optional superuser bootstrap credentials use `Superuser:Id` and `Superuser:Password` (and optionally `Superuser:Name`). Bootstrap runs when the host starts and persists a password hash.
- Tenant login has a fixed-window rate-limit policy of 10 requests per minute per remote IP.
- `GlobalExceptionHandler` logs failures and returns Problem Details for API requests or a safe HTML error response for browser requests. Client-aborted requests are logged at debug level.
- `SuperuserAuditMiddleware` records qualifying write operations for authenticated superuser and tenant actions through the audit application service.
- Organization-created email notifications are implemented in Infrastructure and use the `Email:Smtp` configuration section when enabled in platform settings.

Do not put real passwords, signing keys, SMTP credentials, or database credentials in source control. Supply environment-specific configuration through a secret store or environment variables.

## Configuration and local development

Important configuration groups consumed by the application include:

- `ConnectionStrings:DefaultConnection` — PostgreSQL connection
- `Superuser:Jwt:*` — JWT signing key, issuer, and audience
- `Superuser:Id`, `Superuser:Password`, and optional `Superuser:Name` — initial superuser bootstrap
- `Email:Smtp:*` — SMTP host, port, optional credentials, sender, and TLS setting

The solution targets .NET 10. Repository-wide compiler settings are in `Directory.Build.props`; centralized package versions are in `Directory.Packages.props`. CI restores, builds, and tests the solution using .NET 10.

```bash
dotnet restore
dotnet build CloudHubs.slnx --no-restore
dotnet test CloudHubs.slnx --no-build
```

The current test project contains Domain and Application unit tests using repository fakes; the solution does not currently define a separate integration-test project.
