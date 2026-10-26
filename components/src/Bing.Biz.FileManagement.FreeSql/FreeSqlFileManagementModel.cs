using System.Runtime.CompilerServices;
using Bing.Biz.FileManagement;
using FreeSql;

namespace Bing.Biz.FileManagement.FreeSql;

internal static class FreeSqlFileManagementModel
{
    private static readonly ConditionalWeakTable<IFreeSql, object> Configured = new();
    private static readonly object Gate = new();

    internal static void Configure(IFreeSql orm)
    {
        lock (Gate)
        {
            if (Configured.TryGetValue(orm, out _))
                return;

            ConfigureFile(orm);
            ConfigureSession(orm);
            ConfigurePart(orm);
            ConfigureBinding(orm);
            ConfigureOperation(orm);
            ConfigureImport(orm);
            ConfigureTransfer(orm);
            ConfigureOperationAudit(orm);
            ConfigureMigrationBatch(orm);
            ConfigureMigrationBatchItem(orm);
            ConfigureControlLock(orm);
            ConfigureUploadReservation(orm);
            ConfigureIoLease(orm);
            ConfigureOutbox(orm);
            ConfigureGovernance(orm);
            Configured.Add(orm, new object());
        }
    }

    private static void ConfigureFile(IFreeSql orm)
    {
        var contentPolicyDbType = orm.Ado.DataType switch
        {
            DataType.MySql or DataType.OdbcMySql or DataType.CustomMySql => "mediumtext",
            DataType.SqlServer or DataType.OdbcSqlServer or DataType.CustomSqlServer => "nvarchar(max)",
            DataType.Oracle or DataType.OdbcOracle or DataType.CustomOracle => "clob",
            _ => "text"
        };
        orm.CodeFirst.ConfigEntity<FileRecord>(table =>
        {
            table.Name("BingFileRecords");
            table.Property(x => x.Id).IsPrimary(true);
            table.Property(x => x.RequestKey).StringLength(80).IsNullable(false);
            table.Property(x => x.ScopeKey).StringLength(80).IsNullable(false);
            table.Property(x => x.TenantId).StringLength(128).IsNullable(true);
            table.Property(x => x.StoreName).StringLength(64).IsNullable(false);
            table.Property(x => x.Container).StringLength(64).IsNullable(false);
            table.Property(x => x.ObjectKey).StringLength(512).IsNullable(false);
            table.Property(x => x.FileName).StringLength(255).IsNullable(false);
            table.Property(x => x.ContentType).StringLength(256).IsNullable(false);
            table.Property(x => x.ContentPolicyJson).DbType(contentPolicyDbType).IsNullable(false);
            table.Property(x => x.CreatorId).StringLength(200).IsNullable(false);
            table.Property(x => x.Sha256).StringLength(64).IsNullable(true);
            table.Index("UX_BingFileRecords_RequestKey", nameof(FileRecord.RequestKey), true);
            table.Index("IX_BingFileRecords_Scope_Creator_State_Created", "ScopeKey,CreatorId,State,CreatedAt,RequestKey");
            table.Index("IX_BingFileRecords_Scope_State_Created", "ScopeKey,State,CreatedAt,RequestKey");
            table.Index("IX_BingFileRecords_Scope_Container_State", "ScopeKey,Container,State,CreatedAt");
        });
    }

    private static void ConfigureSession(IFreeSql orm)
    {
        orm.CodeFirst.ConfigEntity<FileUploadSession>(table =>
        {
            table.Name("BingFileUploadSessions");
            table.Property(x => x.Id).IsPrimary(true);
            table.Property(x => x.OwnerId).StringLength(200).IsNullable(false);
            table.Property(x => x.BackendUploadId).StringLength(2048).IsNullable(true);
            table.Property(x => x.BackendMetadataJson).StringLength(4096).IsNullable(false);
            table.Index("UX_BingFileUploadSessions_FileId", nameof(FileUploadSession.FileId), true);
            table.Index("IX_BingFileUploadSessions_State_Expires", "State,ExpiresAt");
            table.Index("IX_BingFileUploadSessions_Owner_Expires", "OwnerId,ExpiresAt,FileId");
        });
    }

    private static void ConfigurePart(IFreeSql orm)
    {
        orm.CodeFirst.ConfigEntity<FileUploadPart>(table =>
        {
            table.Name("BingFileUploadParts");
            table.Property(x => x.SessionId).IsPrimary(true);
            table.Property(x => x.Number).IsPrimary(true);
            table.Property(x => x.Sha256).StringLength(64).IsNullable(false);
            table.Property(x => x.ETag).StringLength(256).IsNullable(true);
        });
    }

    private static void ConfigureBinding(IFreeSql orm)
    {
        orm.CodeFirst.ConfigEntity<FileBinding>(table =>
        {
            table.Name("BingFileBindings");
            table.Property(x => x.Id).IsPrimary(true);
            table.Property(x => x.BusinessType).StringLength(100).IsNullable(false);
            table.Property(x => x.BusinessId).StringLength(200).IsNullable(false);
            table.Property(x => x.CreatorId).StringLength(200).IsNullable(false);
            table.Index("UX_BingFileBindings_Business", "FileId,BusinessType,BusinessId", true);
        });
    }

    private static void ConfigureOperation(IFreeSql orm)
    {
        orm.CodeFirst.ConfigEntity<FileOperation>(table =>
        {
            table.Name("BingFileOperations");
            table.Property(x => x.Id).IsPrimary(true);
            table.Property(x => x.Payload).StringLength(4096).IsNullable(false);
            table.Property(x => x.LeaseToken).StringLength(64).IsNullable(true);
            table.Property(x => x.LastError).StringLength(200).IsNullable(true);
            table.Index("IX_BingFileOperations_State_Due_Lease", "State,NextAttemptAt,LeaseUntil");
            table.Index("IX_BingFileOperations_FileId", nameof(FileOperation.FileId));
            table.Index("IX_BingFileOperations_Created", "CreatedAt,Id");
            table.Index("IX_BingFileOperations_Recovery", nameof(FileOperation.RecoveryOperationId));
        });
    }

    private static void ConfigureImport(IFreeSql orm)
    {
        orm.CodeFirst.ConfigEntity<LegacyFileImport>(table =>
        {
            table.Name("BingLegacyFileImports");
            table.Property(x => x.Id).IsPrimary(true);
            table.Property(x => x.Source).StringLength(100).IsNullable(false);
            table.Property(x => x.Status).StringLength(40).IsNullable(false);
            table.Property(x => x.Issue).StringLength(2000).IsNullable(true);
            table.Index("UX_BingLegacyFileImports_Source_Legacy", "Source,LegacyId", true);
        });
    }

    private static void ConfigureTransfer(IFreeSql orm)
    {
        orm.CodeFirst.ConfigEntity<FileTransferAttempt>(table =>
        {
            table.Name("BingFileTransferAttempts"); table.Property(x => x.Id).IsPrimary(true);
            table.Property(x => x.ScopeKey).StringLength(80).IsNullable(false); table.Property(x => x.TenantId).StringLength(128).IsNullable(true);
            table.Property(x => x.StoreName).StringLength(64).IsNullable(false); table.Property(x => x.Container).StringLength(64).IsNullable(false);
            table.Property(x => x.ObjectKey).StringLength(512).IsNullable(false); table.Property(x => x.LeaseToken).StringLength(64).IsNullable(false);
            table.Index("IX_BingFileTransferAttempts_State_Due", "State,NextCheckAt"); table.Index("IX_BingFileTransferAttempts_FileId", "FileId");
        });
    }

    private static void ConfigureOperationAudit(IFreeSql orm)
    {
        orm.CodeFirst.ConfigEntity<FileOperationAudit>(table =>
        {
            table.Name("BingFileOperationAudits"); table.Property(x => x.Id).IsPrimary(true);
            table.Property(x => x.RequestKey).StringLength(80).IsNullable(false);
            table.Property(x => x.ScopeKey).StringLength(80).IsNullable(false);
            table.Property(x => x.ActorId).StringLength(200).IsNullable(false);
            table.Property(x => x.Reason).StringLength(500).IsNullable(false);
            table.Property(x => x.TraceId).StringLength(64).IsNullable(false);
            table.Index("UX_BingFileOperationAudits_Request", nameof(FileOperationAudit.RequestKey), true);
            table.Index("UX_BingFileOperationAudits_Operation", nameof(FileOperationAudit.OperationId), true);
            table.Index("IX_BingFileOperationAudits_Scope_Operation", "ScopeKey,OperationId,CreatedAt");
        });
    }

    private static void ConfigureMigrationBatch(IFreeSql orm)
    {
        orm.CodeFirst.ConfigEntity<FileMigrationBatch>(table =>
        {
            table.Name("BingFileMigrationBatches"); table.Property(x => x.Id).IsPrimary(true);
            table.Property(x => x.RequestKey).StringLength(80).IsNullable(false);
            table.Property(x => x.ScopeKey).StringLength(80).IsNullable(false);
            table.Property(x => x.ActorId).StringLength(200).IsNullable(false);
            table.Property(x => x.SourceStore).StringLength(64).IsNullable(false);
            table.Property(x => x.TargetStore).StringLength(64).IsNullable(false);
            table.Property(x => x.Container).StringLength(64).IsNullable(true);
            table.Property(x => x.AfterKey).StringLength(80).IsNullable(true);
            table.Index("UX_BingFileMigrationBatches_Request", nameof(FileMigrationBatch.RequestKey), true);
            table.Index("IX_BingFileMigrationBatches_Scope_Created", "ScopeKey,CreatedAt,Id");
            table.Index("IX_BingFileMigrationBatches_State_Created", "State,CreatedAt,Id");
        });
    }

    private static void ConfigureMigrationBatchItem(IFreeSql orm)
    {
        orm.CodeFirst.ConfigEntity<FileMigrationBatchItem>(table =>
        {
            table.Name("BingFileMigrationBatchItems");
            table.Property(x => x.BatchId).IsPrimary(true);
            table.Property(x => x.FileId).IsPrimary(true);
            table.Property(x => x.ErrorCode).StringLength(80).IsNullable(true);
            table.Index("IX_BingFileMigrationBatchItems_State", "BatchId,State,FileId");
            table.Index("IX_BingFileMigrationBatchItems_Operation", nameof(FileMigrationBatchItem.OperationId));
        });
    }

    private static void ConfigureControlLock(IFreeSql orm)
    {
        orm.CodeFirst.ConfigEntity<FileControlLock>(table =>
        {
            table.Name("BingFileControlLocks");
            table.Property(x => x.Id).IsPrimary(true);
            table.Property(x => x.Id).StringLength(80).IsNullable(false);
        });
    }

    private static void ConfigureUploadReservation(IFreeSql orm)
    {
        orm.CodeFirst.ConfigEntity<FileUploadReservation>(table =>
        {
            table.Name("BingFileUploadReservations"); table.Property(x => x.FileId).IsPrimary(true);
            table.Property(x => x.ScopeKey).StringLength(80).IsNullable(false);
            table.Property(x => x.OwnerId).StringLength(200).IsNullable(false);
            table.Index("IX_BingFileUploadReservations_Scope_Owner", "ScopeKey,OwnerId");
        });
    }

    private static void ConfigureIoLease(IFreeSql orm)
    {
        orm.CodeFirst.ConfigEntity<FileIoLease>(table =>
        {
            table.Name("BingFileIoLeases"); table.Property(x => x.Id).IsPrimary(true);
            table.Property(x => x.PoolKey).StringLength(128).IsNullable(false);
            table.Property(x => x.Token).StringLength(128).IsNullable(false);
            table.Index("IX_BingFileIoLeases_Pool_Expires", "PoolKey,ExpiresAt");
        });
    }

    private static void ConfigureOutbox(IFreeSql orm)
    {
        orm.CodeFirst.ConfigEntity<FileOutboxEvent>(table =>
        {
            table.Name("BingFileOutboxEvents"); table.Property(x => x.Id).IsPrimary(true);
            table.Property(x => x.ScopeKey).StringLength(80).IsNullable(false);
            table.Property(x => x.Kind).StringLength(80).IsNullable(false);
            table.Property(x => x.LeaseToken).StringLength(128).IsNullable(true);
            table.Property(x => x.ErrorCode).StringLength(200).IsNullable(true);
            table.Index("UX_BingFileOutboxEvents_File_Version_Kind", "FileId,FileVersion,Kind", true);
            table.Index("IX_BingFileOutboxEvents_Due", "Delivered,NextAttemptAt,LeaseUntil");
        });
    }

    private static void ConfigureGovernance(IFreeSql orm)
    {
        var jsonDbType = orm.Ado.DataType switch
        {
            DataType.MySql or DataType.OdbcMySql or DataType.CustomMySql => "mediumtext",
            DataType.SqlServer or DataType.OdbcSqlServer or DataType.CustomSqlServer => "nvarchar(max)",
            DataType.Oracle or DataType.OdbcOracle or DataType.CustomOracle => "clob",
            _ => "text"
        };
        orm.CodeFirst.ConfigEntity<FileEventDelivery>(table =>
        {
            table.Name("BingFileEventDeliveries");
            table.Property(x => x.EventId).IsPrimary(true);
            table.Property(x => x.Consumer).IsPrimary(true).StringLength(100).IsNullable(false);
            table.Property(x => x.ErrorCode).StringLength(200).IsNullable(true);
            table.Index("IX_BingFileEventDeliveries_Delivered_NextAttemptAt", "Delivered,NextAttemptAt");
        });
        orm.CodeFirst.ConfigEntity<FileGovernanceAudit>(table =>
        {
            table.Name("BingFileGovernanceAudits"); table.Property(x => x.Id).IsPrimary(true);
            table.Property(x => x.ScopeKey).StringLength(80).IsNullable(false);
            table.Property(x => x.ActorId).StringLength(200).IsNullable(false);
            table.Property(x => x.Action).StringLength(80).IsNullable(false);
            table.Property(x => x.Reason).StringLength(500).IsNullable(false);
            table.Property(x => x.RequestJson).DbType(jsonDbType).IsNullable(false);
            table.Property(x => x.ResultJson).DbType(jsonDbType).IsNullable(false);
            table.Index("IX_BingFileGovernanceAudits_Scope_Created", "ScopeKey,CreatedAt,Id");
            table.Index("IX_BingFileGovernanceAudits_Scope_Target_Created", "ScopeKey,TargetId,CreatedAt,Id");
        });
        orm.CodeFirst.ConfigEntity<FileHistoryArchive>(table =>
        {
            table.Name("BingFileHistoryArchives"); table.Property(x => x.Id).IsPrimary(true);
            table.Property(x => x.ScopeKey).StringLength(80).IsNullable(false);
            table.Property(x => x.ActorId).StringLength(200).IsNullable(false);
            table.Index("UX_BingFileHistoryArchives_Kind_Entity", "Kind,EntityId", true);
            table.Index("IX_BingFileHistoryArchives_Scope_Kind_Archived", "ScopeKey,Kind,ArchivedAt,Id");
        });
    }
}

/// <summary>文件管理 FreeSql 表结构的显式初始化入口。</summary>
public static class FileManagementFreeSqlSchema
{
    /// <summary>导出当前数据库与文件管理模型的差异 DDL，不执行修改；应审核后独立部署。</summary>
    public static string GenerateScript(IFreeSql orm)
    {
        ArgumentNullException.ThrowIfNull(orm);
        FreeSqlFileManagementModel.Configure(orm);
        return orm.CodeFirst.GetComparisonDDLStatements(new[]
        {
            typeof(FileRecord), typeof(FileUploadSession), typeof(FileUploadPart),
            typeof(FileBinding), typeof(FileOperation), typeof(LegacyFileImport), typeof(FileTransferAttempt),
            typeof(FileOperationAudit), typeof(FileMigrationBatch), typeof(FileMigrationBatchItem), typeof(FileControlLock),
            typeof(FileUploadReservation), typeof(FileIoLease), typeof(FileOutboxEvent), typeof(FileEventDelivery),
            typeof(FileGovernanceAudit), typeof(FileHistoryArchive)
        }) ?? "";
    }

    /// <summary>
    /// 显式同步文件管理表结构；注册仓储时不会调用此方法。
    /// </summary>
    /// <param name="orm">宿主创建并拥有的 FreeSql 客户端。</param>
    /// <remarks>适用于测试或受控部署步骤；生产环境应使用审核后的数据库迁移。</remarks>
    public static void SyncStructure(IFreeSql orm)
    {
        ArgumentNullException.ThrowIfNull(orm);
        FreeSqlFileManagementModel.Configure(orm);
        orm.CodeFirst.SyncStructure(new[]
        {
            typeof(FileRecord), typeof(FileUploadSession), typeof(FileUploadPart),
            typeof(FileBinding), typeof(FileOperation), typeof(LegacyFileImport), typeof(FileTransferAttempt),
            typeof(FileOperationAudit), typeof(FileMigrationBatch), typeof(FileMigrationBatchItem), typeof(FileControlLock),
            typeof(FileUploadReservation), typeof(FileIoLease), typeof(FileOutboxEvent), typeof(FileEventDelivery),
            typeof(FileGovernanceAudit), typeof(FileHistoryArchive)
        });
    }
}
