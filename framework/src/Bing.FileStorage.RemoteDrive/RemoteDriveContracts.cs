namespace Bing.FileStorage.RemoteDrive;

/// <summary>
/// 短事务存储端口。
/// </summary>
/// <remarks>成功 CAS 必须把传入对象的 Version 更新为新版本。</remarks>
public interface IRemoteDriveStateStore
{
    /// <summary>
    /// 异步查找远端网盘连接。
    /// </summary>
    /// <param name="id">待查找或更新的记录标识。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>连接记录；不存在时为 null。</returns>
    Task<RemoteDriveConnection?> FindConnectionAsync(Guid id, CancellationToken ct = default);
    /// <summary>
    /// 异步分页查询可见的远端网盘连接。
    /// </summary>
    /// <param name="scopeKey">连接所属的隔离范围键。</param>
    /// <param name="ownerId">连接所有者标识。</param>
    /// <param name="includeShared">为 true 时包含共享连接，否则仅查询所有者连接。</param>
    /// <param name="skip">跳过的记录数。</param>
    /// <param name="take">最多读取的记录数。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>当前页可见的连接记录。</returns>
    Task<IReadOnlyList<RemoteDriveConnection>> ListConnectionsAsync(string scopeKey, string? ownerId, bool includeShared, int skip, int take, CancellationToken ct = default);
    /// <summary>
    /// 异步尝试创建远端网盘连接。
    /// </summary>
    /// <param name="connection">待持久化的连接记录。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>操作成功时为 true；记录不存在、版本不匹配或租约条件不满足时为 false。</returns>
    Task<bool> TryCreateConnectionAsync(RemoteDriveConnection connection, CancellationToken ct = default);
    /// <summary>
    /// 异步按版本尝试更新远端网盘连接。
    /// </summary>
    /// <param name="connection">待持久化的连接记录。</param>
    /// <param name="version">预期的记录版本。</param>
    /// <param name="leaseToken">操作要求的租约令牌。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>操作成功时为 true；记录不存在、版本不匹配或租约条件不满足时为 false。</returns>
    Task<bool> TryUpdateConnectionAsync(RemoteDriveConnection connection, long version, string? leaseToken = null, CancellationToken ct = default);
    /// <summary>
    /// 异步按版本尝试取得连接租约。
    /// </summary>
    /// <param name="id">待查找或更新的记录标识。</param>
    /// <param name="version">预期的记录版本。</param>
    /// <param name="token">租约持有者令牌。</param>
    /// <param name="until">租约到期的 Unix 毫秒时间。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>操作成功时为 true；记录不存在、版本不匹配或租约条件不满足时为 false。</returns>
    Task<bool> TryClaimConnectionAsync(Guid id, long version, string token, long until, CancellationToken ct = default);
    /// <summary>
    /// 异步查找已发布的网盘对象。
    /// </summary>
    /// <param name="id">待查找或更新的记录标识。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>网盘对象；不存在时为 null。</returns>
    Task<RemoteDriveObject?> FindObjectAsync(string id, CancellationToken ct = default);
    /// <summary>
    /// 异步按版本尝试删除网盘对象。
    /// </summary>
    /// <param name="id">待查找或更新的记录标识。</param>
    /// <param name="version">预期的记录版本。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>操作成功时为 true；记录不存在、版本不匹配或租约条件不满足时为 false。</returns>
    Task<bool> TryDeleteObjectAsync(string id, long version, CancellationToken ct = default);
    /// <summary>
    /// 异步查找网盘传输记录。
    /// </summary>
    /// <param name="id">待查找或更新的记录标识。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>传输记录；不存在时为 null。</returns>
    Task<RemoteDriveTransfer?> FindTransferAsync(Guid id, CancellationToken ct = default);
    /// <summary>
    /// 异步尝试创建网盘传输记录。
    /// </summary>
    /// <param name="transfer">待持久化的传输记录。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>操作成功时为 true；记录不存在、版本不匹配或租约条件不满足时为 false。</returns>
    Task<bool> TryCreateTransferAsync(RemoteDriveTransfer transfer, CancellationToken ct = default);
    /// <summary>
    /// 异步按版本尝试更新网盘传输的业务字段。
    /// </summary>
    /// <remarks>按版本保存业务字段，不回写快照中的租约；仅传入有效令牌且目标 LeaseToken=null、LeaseUntil=0 时显式释放。</remarks>
    /// <param name="transfer">待持久化的传输记录。</param>
    /// <param name="version">预期的记录版本。</param>
    /// <param name="leaseToken">操作要求的租约令牌。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>操作成功时为 true；记录不存在、版本不匹配或租约条件不满足时为 false。</returns>
    Task<bool> TryUpdateTransferAsync(RemoteDriveTransfer transfer, long version, string? leaseToken = null, CancellationToken ct = default);
    /// <summary>
    /// 异步按版本尝试取得传输租约。
    /// </summary>
    /// <param name="id">待查找或更新的记录标识。</param>
    /// <param name="version">预期的记录版本。</param>
    /// <param name="token">租约持有者令牌。</param>
    /// <param name="until">租约到期的 Unix 毫秒时间。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>操作成功时为 true；记录不存在、版本不匹配或租约条件不满足时为 false。</returns>
    Task<bool> TryClaimTransferAsync(Guid id, long version, string token, long until, CancellationToken ct = default);
    /// <summary>
    /// 异步续期指定传输租约。
    /// </summary>
    /// <param name="id">待查找或更新的记录标识。</param>
    /// <param name="token">租约持有者令牌。</param>
    /// <param name="until">租约到期的 Unix 毫秒时间。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>操作成功时为 true；记录不存在、版本不匹配或租约条件不满足时为 false。</returns>
    Task<bool> RenewTransferAsync(Guid id, string token, long until, CancellationToken ct = default);
    /// <summary>
    /// 异步在事务内尝试更新传输并发布网盘对象。
    /// </summary>
    /// <remarks>在同一事务内验证有效租约、更新业务字段与发布对象，保留数据库中的续租结果；expectedObjectVersion=0 代表插入。</remarks>
    /// <param name="value">待处理的值。</param>
    /// <param name="expectedObjectVersion">预期对象版本；0 表示新建对象。</param>
    /// <param name="transfer">待持久化的传输记录。</param>
    /// <param name="transferVersion">预期传输记录版本。</param>
    /// <param name="leaseToken">操作要求的租约令牌。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>操作成功时为 true；记录不存在、版本不匹配或租约条件不满足时为 false。</returns>
    Task<bool> TryPublishAsync(RemoteDriveObject value, long expectedObjectVersion, RemoteDriveTransfer transfer, long transferVersion, string leaseToken, CancellationToken ct = default);
    /// <summary>
    /// 异步尝试安排传输清理重试。
    /// </summary>
    /// <remarks>仅对版本匹配、无有效租约且待清理的记录安排重试，不修改检查点或完成标记。</remarks>
    /// <param name="id">待查找或更新的记录标识。</param>
    /// <param name="version">预期的记录版本。</param>
    /// <param name="now">当前 Unix 毫秒时间。</param>
    /// <param name="nextAttemptAt">下次清理重试的 Unix 毫秒时间。</param>
    /// <param name="errorCode">清理失败的错误分类代码。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>操作成功时为 true；记录不存在、版本不匹配或租约条件不满足时为 false。</returns>
    Task<bool> TryDeferCleanupAsync(Guid id, long version, long now, long nextAttemptAt, string errorCode, CancellationToken ct = default);
    /// <summary>
    /// 异步查询到期的待清理传输。
    /// </summary>
    /// <remarks>返回到达重试时间的未清理 Verified/Aborted 及过期传输；按重试时间公平排序，数据库内限量。</remarks>
    /// <param name="now">当前 Unix 毫秒时间。</param>
    /// <param name="take">最多读取的记录数。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>到期且待清理的传输记录。</returns>
    Task<IReadOnlyList<RemoteDriveTransfer>> ListCleanupAsync(long now, int take, CancellationToken ct = default);
}

/// <summary>
/// 保护远端网盘连接凭据。
/// </summary>
public interface IRemoteDriveCredentialProtector
{
    /// <summary>
    /// 保护指定连接的明文凭据。
    /// </summary>
    /// <param name="connectionId">凭据所属连接标识。</param>
    /// <param name="plaintext">待保护的明文凭据。</param>
    /// <returns>受保护的凭据密文。</returns>
    string Protect(Guid connectionId, string plaintext);
    /// <summary>
    /// 还原指定连接的受保护凭据。
    /// </summary>
    /// <param name="connectionId">凭据所属连接标识。</param>
    /// <param name="ciphertext">待还原的受保护凭据。</param>
    /// <returns>还原后的凭据明文。</returns>
    string Unprotect(Guid connectionId, string ciphertext);
}

/// <summary>
/// 提供远端网盘连接的可用凭据。
/// </summary>
public interface IRemoteDriveCredentialProvider
{
    /// <summary>
    /// 异步获取指定连接的可用凭据。
    /// </summary>
    /// <param name="connectionId">凭据所属连接标识。</param>
    /// <param name="refresh">为 true 时请求刷新凭据，否则读取可用凭据。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>可用于连接的网盘凭据。</returns>
    Task<RemoteDriveCredentials> GetAsync(Guid connectionId, bool refresh = false, CancellationToken ct = default);
}

/// <summary>
/// 网盘操作的连接与即时凭据。
/// </summary>
/// <remarks>远端协议只处理明确连接；凭据按需读取以支持长上传期间轮换。</remarks>
public sealed class RemoteDriveContext
{
    /// <summary>
    /// 初始化一个 <see cref="RemoteDriveContext" /> 类型的实例。
    /// </summary>
    /// <param name="connection">当前实例使用的连接对象或连接配置。</param>
    /// <param name="credentials">用于按需读取或刷新网盘凭据的委托。</param>
    public RemoteDriveContext(RemoteDriveConnection connection, Func<bool, CancellationToken, Task<RemoteDriveCredentials>> credentials)
    { Connection = connection; Credentials = credentials; }
    /// <summary>
    /// 获取本次操作绑定的网盘连接。
    /// </summary>
    public RemoteDriveConnection Connection { get; }
    /// <summary>
    /// 获取本次操作即时取得的网盘凭据。
    /// </summary>
    public Func<bool, CancellationToken, Task<RemoteDriveCredentials>> Credentials { get; }
}

/// <summary>
/// 可重复打开的网盘上传内容源。
/// </summary>
/// <remarks>可重复读取的持久内容源；回调外不得保留流。</remarks>
public sealed class RemoteDriveContent
{
    /// <summary>
    /// 保存远程盘内容读取委托。
    /// </summary>
    private readonly Func<long, long, Func<Stream, CancellationToken, Task>, CancellationToken, Task> _read;
    /// <summary>
    /// 初始化一个 <see cref="RemoteDriveContent" /> 类型的实例。
    /// </summary>
    /// <param name="length">内容的长度，单位为字节。</param>
    /// <param name="read">用于按偏移量和长度读取远端文件内容的委托。</param>
    public RemoteDriveContent(long length, Func<long, long, Func<Stream, CancellationToken, Task>, CancellationToken, Task> read) { Length = length; _read = read; }
    /// <summary>
    /// 获取内容字节数。
    /// </summary>
    public long Length { get; }
    /// <summary>
    /// 将远程文件内容交给消费回调读取。
    /// </summary>
    /// <param name="consume">接收内容流及取消令牌并读取数据的回调。</param>
    /// <param name="ct">用于取消内容读取的令牌。</param>
    public Task ReadAsync(Func<Stream, CancellationToken, Task> consume, CancellationToken ct = default) => _read(0, Length, consume, ct);
    /// <summary>
    /// 将远程文件内容交给消费回调读取。
    /// </summary>
    /// <param name="offset">要读取的起始偏移量。</param>
    /// <param name="length">要读取的字节数。</param>
    /// <param name="consume">接收内容流及取消令牌并读取数据的回调。</param>
    /// <param name="ct">用于取消内容读取的令牌。</param>
    /// <remarks>
    /// 读取范围必须位于文件长度之内；越界时抛出参数异常。
    /// </remarks>
    public Task ReadRangeAsync(long offset, long length, Func<Stream, CancellationToken, Task> consume, CancellationToken ct = default)
    {
        if (offset < 0 || length < 0 || offset > Length || length > Length - offset) throw new ArgumentOutOfRangeException(nameof(offset));
        return _read(offset, length, consume, ct);
    }
}

/// <summary>
/// 提供远端网盘授权及文件操作协议。
/// </summary>
public interface IRemoteDriveClient
{
    /// <summary>
    /// 获取网盘协议提供程序名称。
    /// </summary>
    string Provider { get; }
    /// <summary>
    /// 异步启动网盘授权。
    /// </summary>
    /// <param name="redirectUri">授权完成后的回调地址。</param>
    /// <param name="state">关联授权请求的状态值。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>网盘授权会话。</returns>
    Task<RemoteDriveAuthorization> BeginAuthorizationAsync(string redirectUri, string state, CancellationToken ct = default);
    /// <summary>
    /// 异步完成网盘授权。
    /// </summary>
    /// <remarks>扫码未确认返回 null，失败必须抛异常，不得伪造令牌。</remarks>
    /// <param name="authorization">待完成的授权会话。</param>
    /// <param name="code">授权回调返回的可选授权码。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>授权凭据；扫码尚未确认时为 null。</returns>
    Task<RemoteDriveCredentials?> CompleteAuthorizationAsync(RemoteDriveAuthorization authorization, string? code, CancellationToken ct = default);
    /// <summary>
    /// 异步刷新网盘凭据。
    /// </summary>
    /// <param name="credentials">执行网盘操作所需的凭据。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>可用于连接的网盘凭据。</returns>
    Task<RemoteDriveCredentials> RefreshAsync(RemoteDriveCredentials credentials, CancellationToken ct = default);
    /// <summary>
    /// 异步获取网盘账户信息。
    /// </summary>
    /// <param name="credentials">执行网盘操作所需的凭据。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>网盘账户信息。</returns>
    Task<RemoteDriveAccount> GetAccountAsync(RemoteDriveCredentials credentials, CancellationToken ct = default);
    /// <summary>
    /// 异步查询指定网盘文件。
    /// </summary>
    /// <param name="context">本次操作使用的上下文。</param>
    /// <param name="fileId">远端文件标识。</param>
    /// <param name="parentId">远端父目录标识。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>匹配的远端文件；不存在时为 null。</returns>
    Task<RemoteDriveFile?> GetFileAsync(RemoteDriveContext context, string fileId, string parentId, CancellationToken ct = default);
    /// <summary>
    /// 异步按父目录和名称查找网盘文件。
    /// </summary>
    /// <param name="context">本次操作使用的上下文。</param>
    /// <param name="parentId">远端父目录标识。</param>
    /// <param name="name">目标对象名称。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>匹配的远端文件；不存在时为 null。</returns>
    Task<RemoteDriveFile?> FindByNameAsync(RemoteDriveContext context, string parentId, string name, CancellationToken ct = default);
    /// <summary>
    /// 异步上传内容并保存传输检查点。
    /// </summary>
    /// <param name="context">本次操作使用的上下文。</param>
    /// <param name="content">可重复读取的持久内容源。</param>
    /// <param name="checkpoint">用于恢复或终止传输的检查点。</param>
    /// <param name="persist">保存上传进度检查点的异步回调。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>上传完成后的远端文件信息。</returns>
    Task<RemoteDriveFile> UploadAsync(RemoteDriveContext context, RemoteDriveContent content, RemoteDriveCheckpoint checkpoint,
        Func<RemoteDriveCheckpoint, CancellationToken, Task> persist, CancellationToken ct = default);
    /// <summary>
    /// 异步获取网盘文件下载信息。
    /// </summary>
    /// <param name="context">本次操作使用的上下文。</param>
    /// <param name="file">目标远端文件。</param>
    /// <param name="ct">取消操作的令牌。</param>
    /// <returns>远端文件下载信息。</returns>
    Task<RemoteDriveDownload> DownloadAsync(RemoteDriveContext context, RemoteDriveFile file, CancellationToken ct = default);
    /// <summary>
    /// 异步删除网盘文件。
    /// </summary>
    /// <param name="context">本次操作使用的上下文。</param>
    /// <param name="file">目标远端文件。</param>
    /// <param name="ct">取消操作的令牌。</param>
    Task DeleteAsync(RemoteDriveContext context, RemoteDriveFile file, CancellationToken ct = default);
    /// <summary>
    /// 异步终止检查点对应的远端上传。
    /// </summary>
    /// <param name="context">本次操作使用的上下文。</param>
    /// <param name="checkpoint">用于恢复或终止传输的检查点。</param>
    /// <param name="ct">取消操作的令牌。</param>
    Task AbortAsync(RemoteDriveContext context, RemoteDriveCheckpoint checkpoint, CancellationToken ct = default);
}
