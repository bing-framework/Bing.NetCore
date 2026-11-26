using System.IO;

namespace Bing.Data.Sql.Benchmarks;

/// <summary>单个管理连接按100ms间隔采样；累计会话观测次数，不换算成等待时长或CPU。</summary>
internal sealed class SqlDatabaseWaitSamples
{
    private long _samples, _active, _locks, _walSync, _walWrite, _otherIo, _noWait, _otherWait;

    internal void Record(int active, int locks, int walSync, int walWrite, int otherIo, int noWaitEvent, int otherWait)
    {
        if (active < 0 || locks < 0 || walSync < 0 || walWrite < 0 || otherIo < 0 || noWaitEvent < 0 || otherWait < 0 ||
            (long)locks + walSync + walWrite + otherIo + noWaitEvent + otherWait != active)
            throw new InvalidDataException("Database wait observations must be nonnegative, exhaustive and mutually exclusive.");
        _samples++;
        _active += active; _locks += locks; _walSync += walSync; _walWrite += walWrite;
        _otherIo += otherIo; _noWait += noWaitEvent; _otherWait += otherWait;
    }

    internal Summary Snapshot() => new(100, _samples, _active, _locks, _walSync, _walWrite, _otherIo, _noWait, _otherWait);

    internal sealed record Summary(int IntervalMilliseconds, long SampleCount, long ActiveSessionObservations,
        long LockObservations, long WalSyncObservations, long WalWriteObservations, long OtherIoObservations,
        long NoWaitEventObservations, long OtherWaitObservations);
}
