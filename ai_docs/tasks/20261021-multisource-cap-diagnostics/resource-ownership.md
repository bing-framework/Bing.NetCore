# Multi-source CAP Resource Ownership

## Verified CAP 8.0.1 behavior

- `MySqlDataStorage.StoreMessageAsync` uses the passed `DbTransaction.Connection` and transaction for Outbox inserts.
- `MySqlCapTransaction.Commit` commits its wrapped transaction before flushing CAP's native message buffer.
- `MySqlCapTransaction.Dispose` disposes its wrapped transaction.
- Without a CAP ambient transaction, CAP storage opens its own connection; this cannot atomically join the business Uow.
- The existing `CapOrderedEventTransactionFactory` already rejects a foreign ambient CAP transaction, uses `AutoCommit=false`, delegates physical completion to its transaction owner, clears the borrowed `DbTransaction` when disposing its wrapper, and flushes only after commit.

## Ownership contract for the new publisher

| Resource | Unique owner | Borrower behavior |
| --- | --- | --- |
| SQL connection and local transaction | Uow root resource | CAP may use the exact transaction for Outbox SQL; CAP does not commit, roll back, or dispose it |
| CAP runtime, storage configuration, dispatcher, lifecycle | One runtime per data source | Root publisher resolves a scoped publisher from that source runtime for each call |
| CAP transaction buffer | One publish invocation and source resource | Restore prior ambient state on return; enqueue only after that resource commits |
| Published business EventId | Caller | Keep distinct from CAP MessageId, Uow root id, operation id, and Job id |
| Consumer idempotency row | Consumer local database transaction | Write with consumer business effects under a unique `(source, EventId, consumer)` key |

CAP source configuration must identify the same physical database as the borrowed resource. An uncomparable database identity is rejected for transactional message publication. Multi-source Uow remains sequential local commits, not distributed atomic commit; a later source failure does not undo an earlier committed resource.
