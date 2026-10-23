# Change Impact Analysis

## T02: Explicit Provider Settings and Default Test Gates

- Changed files: MySQL, PostgreSQL, SQLite, and SQL Server integration test project files; MySQL fixture registration test; database integration test documentation.
- Changed projects: `Bing.Dapper.MySql.Tests.Integration`, `Bing.Dapper.PostgreSql.Tests.Integration`, `Bing.Dapper.Sqlite.Tests.Integration`, and `Bing.Dapper.SqlServer.Tests.Integration`.
- Public contracts: none.
- Runtime paths: test configuration selection and MySQL fixture construction only.
- Providers: MySQL, PostgreSQL, SQLite, SQL Server.
- TFMs: `net6.0` and `net8.0` for affected test projects.
- Build and packaging: no production package or packaging changes.
- Benchmarks: not applicable; no production hot path or builder behavior changes.
- Documentation only: false; project configuration and test organization change.
- Affected dependents: local `dotnet test`, the existing Provider runner, and the solution test graph.
- Risk: medium. Default tests could accidentally enable an external database, while explicit Provider runs must keep their current gates and fail-fast behavior.
- Planned evidence: MSBuild default and explicit settings property checks; default test process with external gates cleared; a deliberately invalid enabled Provider subprocess; Provider runner self-test and preflight; MySQL no-database registration test outside the database collection.

## T04: EventBus Backoff Regression Test

- Changed files: `framework/tests/Bing.EventBus.Tests/Ordering/OrderedEventConsumerTest.cs`.
- Changed projects: `Bing.EventBus.Tests`.
- Public contracts: none.
- Runtime paths: none; test observes existing hosted worker retry timing.
- Providers: none.
- TFMs: project-supported test frameworks only.
- Build and packaging: none.
- Benchmarks: not applicable.
- Documentation only: false; test assertions change.
- Affected dependents: EventBus test project and solution test graph.
- Risk: low. The test must synchronize on three attempts and compare monotonic timestamps with the existing 100 ms retry contract.
- Planned evidence: targeted regression and full EventBus project run on each supported TFM after confirming the project TFM list.

## T03: Self-Contained Serilog Tests

- Changed files: Logging integration test startup, in-memory sink, log assertions, file sink test, appsettings.
- Changed projects: `Bing.Logging.Serilog.Tests.Integration`.
- Public contracts: none.
- Runtime paths: test host logging provider registration, structured event capture, and a temporary file sink host.
- Providers: in-memory and file sinks only; no Seq endpoint.
- TFMs: `net6.0` and `net8.0`.
- Build and packaging: no package version changes; existing file sink dependency is reused.
- Benchmarks: not applicable; no production logging implementation changes.
- Documentation only: false; test host configuration and assertions change.
- Affected dependents: Logging integration tests and solution test graph.
- Risk: medium. Host disposal must release Serilog before the file sink's temporary directory is removed; structured event assertions must verify real captured events.
- Planned evidence: precise logging tests and full Logging project run on each TFM, with unique TRX paths.

## T04: UnitOfWork Cancellation Propagation

- Changed files: the existing UnitOfWork interceptor implementation and `UnitOfWorkAdapterTest.cs`; both were already modified before this task and have T01 snapshots.
- Changed projects: `Bing.Ddd.Application.AspectCore` and `Bing.Aop.AspectCore.Tests`.
- Public contracts: none; all added cancellation methods and signal helpers are test-only.
- Runtime paths: local UnitOfWork interception for asynchronous cancellation, with no global proxy replacement.
- Providers: none.
- TFMs: `net6.0` and `net8.0`.
- Build and packaging: no package or API baseline changes.
- Benchmarks: not applicable; no performance hot path is changed outside the existing interceptor.
- Documentation only: false; tests and the interceptor change.
- Affected dependents: AOP test project and solution test graph.
- Risk: high. Canceled status, original cancellation token, zero commit, zero callback, and single-consumption ValueTask semantics must hold for Task, Task<T>, ValueTask, and ValueTask<T>.
- Planned evidence: pre-canceled and start-signal-controlled cancellation regressions on both TFMs before and after the local adapter adjustment; full AOP project run on both TFMs.

## T04: Explicit Database Test Scopes

- Changed files: MySQL and SQL Server integration fixtures, their query/executor test classes, MySQL registration tests, SQL Server fixture scope contract test, and SQL Server multi-provider query test.
- Changed projects: `Bing.Dapper.MySql.Tests.Integration` and `Bing.Dapper.SqlServer.Tests.Integration`.
- Public contracts: none; fixture helpers are test-only and now require an explicit scoped `IServiceProvider`.
- Runtime paths: scoped SQL factory resolution, query/executor creation, operation execution, and disposal in real provider tests.
- Providers: MySQL and SQL Server fixtures; the SQL Server multi-provider case also routes to PostgreSQL.
- TFMs: `net6.0` and `net8.0`.
- Build and packaging: no production package changes.
- Benchmarks: not applicable; test-only lifecycle changes.
- Documentation only: false; integration test fixture and lifecycle changes.
- Affected dependents: MySQL and SQL Server provider integration tests and the solution test graph.
- Risk: high. Root scope resolution must fail under `ValidateScopes`; each test operation must create and dispose SQL objects before its explicit scope is disposed, including the multi-provider lane's per-key database context.
- Planned evidence: fixture root-resolution rejection and scoped construction tests, MySQL registration without external database access, both provider projects on each TFM, the separate MySQL cross-database test, the SQL Server multi-provider test in an isolated child process, and local SQLite integration on both TFMs.
