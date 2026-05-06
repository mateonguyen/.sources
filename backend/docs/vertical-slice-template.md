# Vertical Slice Template

This template is the baseline pattern for module expansion after snapshot slice stabilization.

## 1. Permission constants

- Define module permissions in `Application/Security/Permissions.cs`.
- Use `<module>:<action>` convention.
- Add policy automatically through `Api/Common/Extensions/AuthorizationExtensions.cs`.

## 2. Domain and mapping

- Add entity in `Domain/Entities/<Module>/`.
- Add EF configuration in `Infrastructure/Persistence/Configurations/`.
- Keep all table/index/column constraints in Flyway SQL first, then update EF mapping.

## 3. Application service pattern

- Add DTOs in `Application/Features/<Module>/<Module>Dtos.cs`.
- Add service in `Application/Features/<Module>/<Module>Service.cs`.
- Place business rules only in service layer.
- Register service and validators in `Application/DependencyInjection.cs`.

## 4. Controller pattern

- Add controller in `Api/Controllers/V1/<Module>Controller.cs`.
- Inject only service interfaces.
- Use `[HasPermission(...)]` for each endpoint.
- Return standardized `ApiResponse` envelope.

## 5. Validator pattern

- Use FluentValidation validators alongside request DTOs.
- Keep structural validation in validators and business validation in services.

## 6. Integration test pattern

- Add test file in `tests/Api.IntegrationTests/`:
  - `<Module>AuthzTests.cs`
  - `<Module>FlowTests.cs`
- Use `ApiTestWebApplicationFactory` with deterministic seeding.
- Cover:
  - success path
  - permission denial
  - business rule violation
  - side effects (status changes, audit log, files)

## 7. Flyway pattern

- Create versioned SQL under `db/flyway/sql/`:
  - `Vx__<module>_schema.sql`
  - optional `R__seed_<module>.sql`
- Flyway SQL remains source of truth.
- EF migration is not used for deployment.
