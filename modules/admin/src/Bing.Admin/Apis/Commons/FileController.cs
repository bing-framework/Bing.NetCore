using Bing.Biz.FileManagement;
using Bing.Biz.FileManagement.AspNetCore;
using Bing.FileStorage;
using Microsoft.AspNetCore.DataProtection;

namespace Bing.Admin.Apis.Commons;

/// <summary>文件与附件管理，统一使用 /api/files；旧 Commons.File 记录由导入工具处理。</summary>
public class FileController : FileManagementControllerBase
{
    public FileController(FileManagementService files, IFileManagementStore store, IFileStorageFactory storage,
        FileManagementOptions options, IDataProtectionProvider protection) : base(files, store, storage, options, protection) { }
}
