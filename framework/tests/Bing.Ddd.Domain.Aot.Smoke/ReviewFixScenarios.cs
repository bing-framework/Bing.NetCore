using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Bing.Domain.ChangeTracking;
using Bing.Domain.Entities;

/// <summary>修复项在真实原生入口的回归。</summary>
public static class ReviewFixScenarios
{
    public static void Run()
    {
        var before = new ReviewDerivedEntity { Name="old", Extra="before" };
        var after = new ReviewDerivedEntity { Name="new", Extra="after" };
        var changes=before.GetChanges(after).ToArray();
        if (!changes.Select(c=>c.PropertyName).SequenceEqual(new[]{"Extra","Name"}) ||
            changes[0].OldValue!="\"before\"" || changes[0].NewValue!="\"after\"")
            throw new InvalidOperationException("原生领域继承字段丢失。");
        Console.WriteLine("PASS | 修复领域继承 GetChanges 入口");
        var descriptions=new ReviewResourceEntity {Name="old",Text="old"}.GetChanges(new ReviewResourceEntity {Name="new",Text="new"}).ToArray();
        if(descriptions[0].Description!="继承资源" || descriptions[1].Description!="常量\"描述\\")
            throw new InvalidOperationException("原生资源或描述常量不一致。");
        Console.WriteLine("PASS | 继承资源静态访问与转义描述常量");
    }
}

public partial class ReviewParentEntity : DomainObjectBase<ReviewParentEntity>
{
    public string Name {get;set;} = "";
}

public partial class ReviewDerivedEntity : ReviewParentEntity
{
    public string Extra {get;set;} = "";
}

public class ReviewBaseLabels { public static string Name => "继承资源"; }
public class ReviewLabels : ReviewBaseLabels { }

public partial class ReviewResourceEntity : DomainObjectBase<ReviewResourceEntity>
{
    [Display(Name="Name", ResourceType=typeof(ReviewLabels))]
    public string Name {get;set;} = "";
    [Description("常量\"描述\\")]
    public string Text {get;set;} = "";
}
