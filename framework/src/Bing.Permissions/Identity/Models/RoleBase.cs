using Bing.Extensions;
using Str = Bing.Helpers.Str;

namespace Bing.Permissions.Identity.Models;

/// <summary>
/// 提供角色默认初始化行为的角色实体扩展。
/// </summary>
public partial class RoleBase<TRole, TKey, TParentId>
{
    /// <summary>
    /// 初始化角色实体，并补充角色类型和拼音简码。
    /// </summary>
    public override void Init()
    {
        base.Init();
        InitType();
        InitPinYin();
    }

    /// <summary>
    /// 在角色类型为空时设置默认类型 <c>Role</c>。
    /// </summary>
    public void InitType()
    {
        if (Type.IsEmpty())
            Type = "Role";
    }

    /// <summary>
    /// 根据角色名称生成并设置拼音简码。
    /// </summary>
    public void InitPinYin() => PinYin = Str.PinYin(Name);
}