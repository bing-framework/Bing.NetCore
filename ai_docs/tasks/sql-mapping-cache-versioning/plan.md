# SQL mapping cache versioning plan

Approved source: user implementation request in this task.

T01: Normalize logical identifiers consistently for candidate selection and cache keys.
T02: Preserve physical identifier case in mapping and mutation cache keys.
T03: Freeze mapping configuration and add explicit atomic version publication.
T04: Capture resolver snapshots for Builder/Query lifetime and mutation partitioning.
T05: Retain DatabaseType isolation and document custom resolver contracts.
T06: Add contention, candidate-count and publication benchmarks without speculative algorithm changes.
T07: Update API baseline, migration documentation, traceability and run unit/integration checks.

Acceptance: old objects retain old snapshots; new objects observe published configuration; full SQL assertions and direct resolver tests cover isolation, failures and concurrency.
