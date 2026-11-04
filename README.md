# CloudHubs

A modular monolith built with Clean Architecture on .NET 10, ASP.NET Core MVC, and PostgreSQL.

```bash
dotnet build
dotnet test
dotnet run --project src/CloudHubs.Web
```

## Structure

```text
CloudHubs/
├── src/
│   ├── CloudHubs.Domain/                Entities and rules. References nothing.
│   │   ├── Common/                      BaseEntity, AuditableEntity
│   │   ├── Entities/
│   │   └── Enums/
│   │
│   ├── CloudHubs.Application/           Use cases. References Domain only.
│   │   ├── Modules/
│   │   │   ├── Organizations/           DTOs/ Interfaces/ Services/
│   │   │   ├── Projects/
│   │   │   ├── Subscriptions/
│   │   │   ├── Users/
│   │   │   └── Platform/                Platform settings and audit log
│   │   └── DependencyInjection.cs       AddApplication()
│   │
│   ├── CloudHubs.Infrastructure/        EF Core, email. Implements Application interfaces.
│   │   ├── Data/
│   │   │   ├── CloudHubsDbContext.cs
│   │   │   ├── Configurations/          One IEntityTypeConfiguration per entity
│   │   │   └── Migrations/
│   │   ├── Modules/<Module>/            Repositories
│   │   ├── Services/                    SMTP email
│   │   └── DependencyInjection.cs       AddInfrastructure()
│   │
│   └── CloudHubs.Web/                   The only entry point.
│       ├── Areas/
│       │   ├── Superuser/               Admin panel at /su
│       │   └── Organization/            Member portal at /organization
│       ├── Controllers/                 Public signup
│       │   └── Api/                     JSON API (/api/...)
│       ├── ViewModels/<Module>/         Plus Auth/ for login and token models
│       ├── Views/                       Views for root controllers
│       ├── Security/                    Credential validation, token issuing
│       ├── Middleware/                  Exception handling, audit logging
│       └── Program.cs
│
└── tests/
    └── CloudHubs.UnitTests/
        ├── Application/<Module>/
        ├── Domain/
        └── Fakes/                       In-memory repository stubs
```

Dependencies point inward: `Web → Application + Infrastructure → Application → Domain`.

Each folder is one namespace, for example `src/CloudHubs.Application/Modules/Users/Services` is `CloudHubs.Application.Modules.Users.Services`.

## Modules

| Module        | Owns                                                        |
| ------------- | ----------------------------------------------------------- |
| Organizations | Organizations, memberships, custom roles and permissions    |
| Projects      | Platform projects and organization (tenant) projects        |
| Subscriptions | Plans, account subscriptions, purchases                     |
| Users         | User accounts                                               |
| Platform      | Platform-wide settings and the audit log                    |

All modules share one database (`CloudHubsDbContext`). Module boundaries are folders and namespaces, not separate assemblies, so keep cross-module calls going through a module's services, not its repositories, where you can.

## Adding a feature

1. Entity in `Domain/Entities` (inherit `AuditableEntity` if it has `CreatedAt`/`UpdatedAt`), enums in `Domain/Enums`.
2. EF mapping in `Infrastructure/Data/Configurations/<Entity>Configuration.cs`, then `dotnet ef migrations add <Name> --project src/CloudHubs.Infrastructure --startup-project src/CloudHubs.Web`.
3. Repository interface in `Application/Modules/<Module>/Interfaces`, implementation in `Infrastructure/Modules/<Module>`, registered in `Infrastructure/DependencyInjection.cs`.
4. Service in `Application/Modules/<Module>/Services`, registered in `Application/DependencyInjection.cs`.
5. Controller in the right area. Views go in `Areas/<Area>/Views/<Controller>/` so `View("Name")` finds them by convention.
