using Bing.Biz.FileManagement;
using Bing.Biz.FileManagement.AspNetCore;
using Bing.FileStorage;
using Microsoft.AspNetCore.DataProtection;

namespace Bing.Admin.Apis.Commons;

/// <summary>FreeSQL 宿主使用与 EF Core 宿主相同的上传、授权和下载行为。</summary>
public class FileController : FileManagementControllerBase
{
    public FileController(FileManagementService files, IFileManagementStore store, IFileStorageFactory storage,
        FileManagementOptions options, IDataProtectionProvider protection) : base(files, store, storage, options, protection) { }
}
