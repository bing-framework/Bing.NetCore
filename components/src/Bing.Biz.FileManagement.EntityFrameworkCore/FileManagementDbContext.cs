using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Bing.FileStorage.RemoteDrive;

namespace Bing.Biz.FileManagement.EntityFrameworkCore;

/// <summary>独立文件元数据库；可使用与 Admin 相同连接，不扩展旧 Commons.File 表。</summary>
public sealed class FileManagementDbContext : DbContext
{
    public FileManagementDbContext(DbContextOptions<FileManagementDbContext> options) : base(options) { }
    protected override void OnModelCreating(ModelBuilder model)
    {
        var file = model.Entity<FileRecord>();
        file.ToTable("BingFileRecords"); file.HasKey(x => x.Id); file.Property(x => x.Id).ValueGeneratedNever();
        file.Property(x => x.Version).IsConcurrencyToken();
        file.HasIndex(x => x.ConnectionId);
        file.HasIndex(x => x.RequestKey).IsUnique();
        file.HasIndex(x => new { x.ScopeKey, x.CreatorId, x.State, x.CreatedAt, x.RequestKey });
        file.HasIndex(x => new { x.ScopeKey, x.State, x.CreatedAt, x.RequestKey });
        file.HasIndex(x => new { x.ScopeKey, x.Container, x.State, x.CreatedAt });
        file.Property(x => x.RequestKey).HasMaxLength(80).IsRequired(); file.Property(x => x.ScopeKey).HasMaxLength(80).IsRequired();
        file.Property(x => x.TenantId).HasMaxLength(128); file.Property(x => x.CreatorId).HasMaxLength(200).IsRequired();
        file.Property(x => x.StoreName).HasMaxLength(64).IsRequired(); file.Property(x => x.Container).HasMaxLength(64).IsRequired();
        file.Property(x => x.ObjectKey).HasMaxLength(512).IsRequired(); file.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        file.Property(x => x.ContentType).HasMaxLength(256).IsRequired(); file.Property(x => x.Sha256).HasMaxLength(64);
        file.Property(x => x.ContentPolicyJson).HasMaxLength(32768).IsRequired();
        var session = model.Entity<FileUploadSession>();
        session.ToTable("BingFileUploadSessions"); session.HasKey(x => x.Id); session.Property(x => x.Id).ValueGeneratedNever();
        session.HasIndex(x => x.FileId).IsUnique(); session.HasIndex(x => new { x.State, x.ExpiresAt });
        session.HasIndex(x => new { x.OwnerId, x.ExpiresAt, x.FileId });
        session.Property(x => x.OwnerId).HasMaxLength(200).IsRequired(); session.Property(x => x.BackendUploadId).HasMaxLength(2048);
        session.Property(x => x.BackendMetadataJson).HasMaxLength(4096).IsRequired();
        session.Property(x => x.RequestedStorageTargetId).HasMaxLength(128);
        session.HasOne<FileRecord>().WithOne().HasForeignKey<FileUploadSession>(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        var part = model.Entity<FileUploadPart>();
        part.ToTable("BingFileUploadParts"); part.HasKey(x => new { x.SessionId, x.Number });
        part.Property(x => x.Sha256).HasMaxLength(64).IsRequired(); part.Property(x => x.ETag).HasMaxLength(256);
        part.HasOne<FileUploadSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        var binding = model.Entity<FileBinding>();
        binding.ToTable("BingFileBindings"); binding.HasKey(x => x.Id); binding.Property(x => x.Id).ValueGeneratedNever();
        binding.HasIndex(x => new { x.FileId, x.BusinessType, x.BusinessId }).IsUnique();
        binding.Property(x => x.BusinessType).HasMaxLength(100).IsRequired(); binding.Property(x => x.BusinessId).HasMaxLength(200).IsRequired();
        binding.Property(x => x.CreatorId).HasMaxLength(200).IsRequired();
        binding.HasOne<FileRecord>().WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        var operation = model.Entity<FileOperation>();
        operation.ToTable("BingFileOperations"); operation.HasKey(x => x.Id); operation.Property(x => x.Id).ValueGeneratedNever();
        operation.Property(x => x.Version).IsConcurrencyToken(); operation.HasIndex(x => new { x.State, x.NextAttemptAt, x.LeaseUntil });
        operation.HasIndex(x => new { x.CreatedAt, x.Id }); operation.HasIndex(x => x.RecoveryOperationId);
        operation.Property(x => x.CreatedAt).HasDefaultValue(0L); operation.Property(x => x.FinishedAt).HasDefaultValue(0L);
        operation.Property(x => x.Payload).HasMaxLength(4096).IsRequired(); operation.Property(x => x.LeaseToken).HasMaxLength(64);
        operation.Property(x => x.LastError).HasMaxLength(200); operation.HasIndex(x => x.FileId);
        operation.HasOne<FileRecord>().WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        var import = model.Entity<LegacyFileImport>();
        import.ToTable("BingLegacyFileImports"); import.HasKey(x => x.Id); import.Property(x => x.Id).ValueGeneratedNever();
        import.HasIndex(x => new { x.Source, x.LegacyId }).IsUnique();
        import.Property(x => x.Source).HasMaxLength(100).IsRequired(); import.Property(x => x.Status).HasMaxLength(40).IsRequired();
        import.Property(x => x.Issue).HasMaxLength(2000);
        var transfer = model.Entity<FileTransferAttempt>();
        transfer.ToTable("BingFileTransferAttempts"); transfer.HasKey(x => x.Id); transfer.Property(x => x.Id).ValueGeneratedNever();
        transfer.Property(x => x.Version).IsConcurrencyToken();
        transfer.HasIndex(x => new { x.State, x.NextCheckAt }); transfer.HasIndex(x => x.FileId);
        transfer.Property(x => x.ScopeKey).HasMaxLength(80).IsRequired(); transfer.Property(x => x.TenantId).HasMaxLength(128);
        transfer.Property(x => x.StoreName).HasMaxLength(64).IsRequired(); transfer.Property(x => x.Container).HasMaxLength(64).IsRequired();
        transfer.Property(x => x.ObjectKey).HasMaxLength(512).IsRequired(); transfer.Property(x => x.LeaseToken).HasMaxLength(64).IsRequired();
        // 导入复制时 FileRecord 尚未创建，不能用外键使暂存登记依赖正式文件存在。

        var audit = model.Entity<FileOperationAudit>();
        audit.ToTable("BingFileOperationAudits"); audit.HasKey(x => x.Id); audit.Property(x => x.Id).ValueGeneratedNever();
        audit.HasIndex(x => x.RequestKey).IsUnique(); audit.HasIndex(x => x.OperationId).IsUnique();
        audit.HasIndex(x => new { x.ScopeKey, x.OperationId, x.CreatedAt });
        audit.Property(x => x.RequestKey).HasMaxLength(80).IsRequired(); audit.Property(x => x.ScopeKey).HasMaxLength(80).IsRequired();
        audit.Property(x => x.ActorId).HasMaxLength(200).IsRequired(); audit.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        audit.Property(x => x.TraceId).HasMaxLength(64).IsRequired();

        var batch = model.Entity<FileMigrationBatch>();
        batch.ToTable("BingFileMigrationBatches"); batch.HasKey(x => x.Id); batch.Property(x => x.Id).ValueGeneratedNever();
        batch.Property(x => x.Version).IsConcurrencyToken(); batch.HasIndex(x => x.RequestKey).IsUnique();
        batch.HasIndex(x => new { x.ScopeKey, x.CreatedAt, x.Id }); batch.HasIndex(x => new { x.State, x.CreatedAt, x.Id });
        batch.Property(x => x.RequestKey).HasMaxLength(80).IsRequired(); batch.Property(x => x.ScopeKey).HasMaxLength(80).IsRequired();
        batch.Property(x => x.ActorId).HasMaxLength(200).IsRequired(); batch.Property(x => x.SourceStore).HasMaxLength(64).IsRequired();
        batch.Property(x => x.TargetStore).HasMaxLength(64).IsRequired(); batch.Property(x => x.Container).HasMaxLength(64);
        batch.Property(x => x.AfterKey).HasMaxLength(80);

        var batchItem = model.Entity<FileMigrationBatchItem>();
        batchItem.ToTable("BingFileMigrationBatchItems"); batchItem.HasKey(x => new { x.BatchId, x.FileId });
        batchItem.Property(x => x.Version).IsConcurrencyToken(); batchItem.HasIndex(x => new { x.BatchId, x.State, x.FileId });
        batchItem.HasIndex(x => x.OperationId); batchItem.Property(x => x.ErrorCode).HasMaxLength(80);

        var controlLock = model.Entity<FileControlLock>();
        controlLock.ToTable("BingFileControlLocks"); controlLock.HasKey(x => x.Id);
        controlLock.Property(x => x.Id).HasMaxLength(80).ValueGeneratedNever(); controlLock.Property(x => x.Version).IsConcurrencyToken();

        var reservation = model.Entity<FileUploadReservation>();
        reservation.ToTable("BingFileUploadReservations"); reservation.HasKey(x => x.FileId);
        reservation.Property(x => x.FileId).ValueGeneratedNever(); reservation.Property(x => x.ScopeKey).HasMaxLength(80).IsRequired();
        reservation.Property(x => x.OwnerId).HasMaxLength(200).IsRequired(); reservation.HasIndex(x => new { x.ScopeKey, x.OwnerId });

        var ioLease = model.Entity<FileIoLease>();
        ioLease.ToTable("BingFileIoLeases"); ioLease.HasKey(x => x.Id); ioLease.Property(x => x.Id).ValueGeneratedNever();
        ioLease.Property(x => x.PoolKey).HasMaxLength(128).IsRequired(); ioLease.Property(x => x.Token).HasMaxLength(128).IsConcurrencyToken().IsRequired();
        ioLease.Property(x => x.ExpiresAt).IsConcurrencyToken();
        ioLease.HasIndex(x => new { x.PoolKey, x.ExpiresAt });

        var outbox = model.Entity<FileOutboxEvent>();
        outbox.ToTable("BingFileOutboxEvents"); outbox.HasKey(x => x.Id); outbox.Property(x => x.Id).ValueGeneratedNever();
        outbox.Property(x => x.Version).IsConcurrencyToken(); outbox.HasIndex(x => new { x.FileId, x.FileVersion, x.Kind }).IsUnique();
        outbox.HasIndex(x => new { x.Delivered, x.NextAttemptAt, x.LeaseUntil });
        outbox.Property(x => x.ScopeKey).HasMaxLength(80).IsRequired(); outbox.Property(x => x.Kind).HasMaxLength(80).IsRequired();
        outbox.Property(x => x.LeaseToken).HasMaxLength(128); outbox.Property(x => x.ErrorCode).HasMaxLength(200);

        var delivery = model.Entity<FileEventDelivery>();
        delivery.ToTable("BingFileEventDeliveries"); delivery.HasKey(x => new { x.EventId, x.Consumer });
        delivery.Property(x => x.Consumer).HasMaxLength(100).IsRequired(); delivery.Property(x => x.ErrorCode).HasMaxLength(200);
        delivery.HasIndex(x => new { x.Delivered, x.NextAttemptAt });

        var governanceAudit = model.Entity<FileGovernanceAudit>();
        governanceAudit.ToTable("BingFileGovernanceAudits"); governanceAudit.HasKey(x => x.Id);
        governanceAudit.Property(x => x.Id).ValueGeneratedNever(); governanceAudit.Property(x => x.ScopeKey).HasMaxLength(80).IsRequired();
        governanceAudit.Property(x => x.ActorId).HasMaxLength(200).IsRequired(); governanceAudit.Property(x => x.Action).HasMaxLength(80).IsRequired();
        governanceAudit.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        governanceAudit.Property(x => x.RequestJson).HasMaxLength(32768).IsRequired(); governanceAudit.Property(x => x.ResultJson).HasMaxLength(32768).IsRequired();
        governanceAudit.HasIndex(x => new { x.ScopeKey, x.CreatedAt, x.Id });
        governanceAudit.HasIndex(x => new { x.ScopeKey, x.TargetId, x.CreatedAt, x.Id });

        var historyArchive = model.Entity<FileHistoryArchive>();
        historyArchive.ToTable("BingFileHistoryArchives"); historyArchive.HasKey(x => x.Id);
        historyArchive.Property(x => x.Id).ValueGeneratedNever(); historyArchive.Property(x => x.ScopeKey).HasMaxLength(80).IsRequired();
        historyArchive.Property(x => x.ActorId).HasMaxLength(200).IsRequired();
        historyArchive.HasIndex(x => new { x.Kind, x.EntityId }).IsUnique();
        historyArchive.HasIndex(x => new { x.ScopeKey, x.Kind, x.ArchivedAt, x.Id });

        var connection = model.Entity<RemoteDriveConnection>();
        connection.ToTable("BingRemoteDriveConnections"); connection.HasKey(x => x.Id); connection.Property(x => x.Id).ValueGeneratedNever();
        connection.Property(x => x.Version).IsConcurrencyToken(); connection.HasIndex(x => x.StoreName).IsUnique();
        connection.HasIndex(x => new { x.ScopeKey, x.Ownership, x.OwnerId, x.State });
        connection.Property(x => x.TemplateName).HasMaxLength(64).IsRequired(); connection.Property(x => x.Provider).HasMaxLength(64).IsRequired();
        connection.Property(x => x.StoreName).HasMaxLength(128).IsRequired(); connection.Property(x => x.ScopeKey).HasMaxLength(80).IsRequired();
        connection.Property(x => x.TenantId).HasMaxLength(128); connection.Property(x => x.OwnerId).HasMaxLength(200).IsRequired();
        connection.Property(x => x.DisplayName).HasMaxLength(100).IsRequired(); connection.Property(x => x.RemoteAccountId).HasMaxLength(200).IsRequired();
        connection.Property(x => x.DriveId).HasMaxLength(200).IsRequired(); connection.Property(x => x.RootFolderId).HasMaxLength(512).IsRequired();
        connection.Property(x => x.EncryptedCredentials).HasMaxLength(32768).IsRequired(); connection.Property(x => x.EncryptedAuthorization).HasMaxLength(32768).IsRequired();
        connection.Property(x => x.LeaseToken).HasMaxLength(64);

        var remoteObject = model.Entity<RemoteDriveObject>();
        remoteObject.ToTable("BingRemoteDriveObjects"); remoteObject.HasKey(x => x.Id);
        remoteObject.Property(x => x.Id).HasMaxLength(64).ValueGeneratedNever(); remoteObject.Property(x => x.Version).IsConcurrencyToken();
        remoteObject.HasIndex(x => new { x.ConnectionId, x.Deleted });
        remoteObject.Property(x => x.StoreName).HasMaxLength(128).IsRequired(); remoteObject.Property(x => x.ScopeKey).HasMaxLength(80).IsRequired();
        remoteObject.Property(x => x.Container).HasMaxLength(64).IsRequired(); remoteObject.Property(x => x.ObjectKey).HasMaxLength(512).IsRequired();
        remoteObject.Property(x => x.RemoteFileJson).HasMaxLength(32768).IsRequired(); remoteObject.Property(x => x.MetadataJson).HasMaxLength(32768).IsRequired();
        remoteObject.Property(x => x.ContentType).HasMaxLength(256).IsRequired(); remoteObject.Property(x => x.Sha256).HasMaxLength(128).IsRequired();

        var remoteTransfer = model.Entity<RemoteDriveTransfer>();
        remoteTransfer.ToTable("BingRemoteDriveTransfers"); remoteTransfer.HasKey(x => x.Id); remoteTransfer.Property(x => x.Id).ValueGeneratedNever();
        remoteTransfer.Property(x => x.Version).IsConcurrencyToken(); remoteTransfer.HasIndex(x => new { x.State, x.ExpiresAt, x.LeaseUntil });
        remoteTransfer.HasIndex(x => x.ConnectionId);
        remoteTransfer.Property(x => x.CleanupNextAttemptAt).HasDefaultValue(0L);
        remoteTransfer.Property(x => x.CleanupErrorCode).HasMaxLength(64);
        remoteTransfer.HasIndex(x => new { x.CleanupCompleted, x.CleanupNextAttemptAt, x.ExpiresAt, x.Id }).HasDatabaseName("IX_BingRemoteDriveTransfers_CleanupRetry");
        remoteTransfer.Property(x => x.StoreName).HasMaxLength(128).IsRequired(); remoteTransfer.Property(x => x.ScopeKey).HasMaxLength(80).IsRequired();
        remoteTransfer.Property(x => x.Container).HasMaxLength(64).IsRequired(); remoteTransfer.Property(x => x.ObjectKey).HasMaxLength(512).IsRequired();
        remoteTransfer.Property(x => x.StageStoreName).HasMaxLength(128).IsRequired(); remoteTransfer.Property(x => x.StageContainer).HasMaxLength(64).IsRequired();
        remoteTransfer.Property(x => x.StageObjectKey).HasMaxLength(512).IsRequired(); remoteTransfer.Property(x => x.StageUploadId).HasMaxLength(2048);
        remoteTransfer.Property(x => x.StageMetadataJson).HasMaxLength(32768).IsRequired(); remoteTransfer.Property(x => x.MetadataJson).HasMaxLength(32768).IsRequired();
        remoteTransfer.Property(x => x.ManifestJson).HasMaxLength(16777216).IsRequired();
        remoteTransfer.Property(x => x.CheckpointJson).HasMaxLength(16777216).IsRequired();
        if (Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true)
        {
            remoteTransfer.Property(x => x.ManifestJson).HasColumnType("longtext");
            remoteTransfer.Property(x => x.CheckpointJson).HasColumnType("longtext");
        }
        remoteTransfer.Property(x => x.PreviousRemoteFileJson).HasMaxLength(32768); remoteTransfer.Property(x => x.ContentType).HasMaxLength(256).IsRequired();
        remoteTransfer.Property(x => x.LeaseToken).HasMaxLength(64);

        var preference = model.Entity<FileStoragePreference>();
        preference.ToTable("BingFileStoragePreferences"); preference.HasKey(x => x.Id);
        preference.Property(x => x.Id).HasMaxLength(64).ValueGeneratedNever(); preference.Property(x => x.Version).IsConcurrencyToken();
        preference.HasIndex(x => new { x.ScopeKey, x.OwnerId, x.Container }).IsUnique();
        preference.Property(x => x.ScopeKey).HasMaxLength(80).IsRequired(); preference.Property(x => x.OwnerId).HasMaxLength(200).IsRequired();
        preference.Property(x => x.Container).HasMaxLength(64).IsRequired(); preference.Property(x => x.TargetId).HasMaxLength(128).IsRequired();

        var copyRequest = model.Entity<FileBindingCopyRequest>();
        copyRequest.ToTable("BingFileBindingCopyRequests"); copyRequest.HasKey(x => x.Id); copyRequest.Property(x => x.Id).ValueGeneratedNever();
        copyRequest.Property(x => x.Version).IsConcurrencyToken(); copyRequest.HasIndex(x => x.RequestKey).IsUnique();
        copyRequest.HasIndex(x => new { x.ScopeKey, x.OwnerId, x.State, x.CreatedAt }); copyRequest.HasIndex(x => x.CopyFileId).IsUnique();
        copyRequest.Property(x => x.RequestKey).HasMaxLength(64).IsRequired(); copyRequest.Property(x => x.ScopeKey).HasMaxLength(80).IsRequired();
        copyRequest.Property(x => x.OwnerId).HasMaxLength(200).IsRequired(); copyRequest.Property(x => x.BusinessType).HasMaxLength(100).IsRequired();
        copyRequest.Property(x => x.BusinessId).HasMaxLength(200).IsRequired(); copyRequest.Property(x => x.ErrorCode).HasMaxLength(80);
    }
}
public static class FileManagementEfRegistration
{
    /// <summary>每次仓储调用使用独立 DbContext，允许长任务续租；不继承请求的数据库长事务。</summary>
    public static IServiceCollection AddBingFileManagementEntityFrameworkCore(this IServiceCollection services, Action<DbContextOptionsBuilder> configure)
    {
        services.AddDbContextFactory<FileManagementDbContext>(configure);
        services.AddScoped<EfFileManagementStore>();
        services.AddScoped<IFileManagementStore>(provider => provider.GetRequiredService<EfFileManagementStore>());
        services.AddScoped<IRemoteDriveStateStore>(provider => provider.GetRequiredService<EfFileManagementStore>());
        services.AddScoped<IFileStorageTargetStore>(provider => provider.GetRequiredService<EfFileManagementStore>());
        return services;
    }
}
