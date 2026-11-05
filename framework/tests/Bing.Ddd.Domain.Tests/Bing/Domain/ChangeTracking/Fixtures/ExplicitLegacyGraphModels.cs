// 独立 ChangeGraph 测试使用的固定模型，保留原回归用例的采集规则。
// 领域对象 AddChanges 不生成或依赖这些测试模型。
#nullable disable
namespace Bing.Domain.ChangeTracking
{

public partial class @AcceptanceGraph : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.AcceptanceGraph> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.AcceptanceGraph> writer)
    {
            if (!writer.IsIgnored("ArrayOfLists")) { try { writer.Sequence("ArrayOfLists", this.@ArrayOfLists, __GraphFixture.Model1, null, "ArrayOfLists"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("ArrayOfLists", exception); } }
            if (!writer.IsIgnored("Branches")) { try { writer.Sequence("Branches", this.@Branches, __GraphFixture.Model3, null, "Branches"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Branches", exception); } }
            if (!writer.IsIgnored("Child")) { try { writer.Object("Child", this.@Child, __GraphFixture.Model2, "Child"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Child", exception); } }
            if (!writer.IsIgnored("Children")) { try { writer.Sequence("Children", this.@Children, __GraphFixture.Model2, static value => value == null ? null : (object)((global::Bing.Domain.Entities.IKey<int>)value).@Id, "Children"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Children", exception); } }
            if (!writer.IsIgnored("Date")) { try { writer.Scalar("Date", this.@Date, "Date"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Date", exception); } }
            if (!writer.IsIgnored("Groups")) { try { writer.Dictionary("Groups", this.@Groups, __GraphFixture.Model1, "Groups"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Groups", exception); } }
            if (!writer.IsIgnored("Jagged")) { try { writer.Sequence("Jagged", this.@Jagged, __GraphFixture.Model4, null, "Jagged"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Jagged", exception); } }
            if (!writer.IsIgnored("ListOfArrays")) { try { writer.Sequence("ListOfArrays", this.@ListOfArrays, __GraphFixture.Model4, null, "ListOfArrays"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("ListOfArrays", exception); } }
            if (!writer.IsIgnored("Map")) { try { writer.Dictionary("Map", this.@Map, __GraphFixture.Model2, "Map"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Map", exception); } }
            if (!writer.IsIgnored("Mixed")) { try { writer.Dictionary("Mixed", this.@Mixed, __GraphFixture.Model5, "Mixed"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Mixed", exception); } }
            if (!writer.IsIgnored("Number")) { try { writer.Scalar("Number", this.@Number, "Number"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Number", exception); } }
            if (!writer.IsIgnored("Numbers")) { try { writer.DictionaryScalar("Numbers", this.@Numbers, "Numbers"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Numbers", exception); } }
            if (!writer.IsIgnored("State")) { try { writer.Scalar("State", this.@State, "State"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("State", exception); } }
            if (!writer.IsIgnored("Tags")) { try { writer.SequenceScalar("Tags", this.@Tags, "Tags"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Tags", exception); } }
            if (!writer.IsIgnored("Text")) { try { writer.Scalar("Text", this.@Text, "Text"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Text", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.AcceptanceGraph> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.AcceptanceGraph>(typeof(global::Bing.Domain.ChangeTracking.AcceptanceGraph).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.AcceptanceGraph", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "ArrayOfLists", "Branches", "Child", "Children", "Date", "Groups", "Jagged", "ListOfArrays", "Map", "Mixed", "Number", "Numbers", "State", "Tags", "Text" }, new global::System.Collections.Generic.Dictionary<string, global::System.Type> { { "ArrayOfLists", typeof(global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.AcceptanceChild>) }, { "Branches", typeof(global::Bing.Domain.ChangeTracking.AcceptanceBranch) }, { "Children", typeof(global::Bing.Domain.ChangeTracking.AcceptanceChild) }, { "Jagged", typeof(global::Bing.Domain.ChangeTracking.AcceptanceChild[]) }, { "ListOfArrays", typeof(global::Bing.Domain.ChangeTracking.AcceptanceChild[]) }, { "Tags", typeof(string) }, });

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.AcceptanceChild>> Model1 = global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.AcceptanceChild>>.CreateSequenceModel<global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.AcceptanceChild>, global::Bing.Domain.ChangeTracking.AcceptanceChild>( "global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.AcceptanceChild>", static value => value, static () => __GraphFixture.Model2, static value => value == null ? null : (object)((global::Bing.Domain.Entities.IKey<int>)value).@Id );

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.AcceptanceChild> Model2 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.AcceptanceChild>(typeof(global::Bing.Domain.ChangeTracking.AcceptanceChild).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.AcceptanceChild", static (writer, value) =>
        {
            if (!writer.IsIgnored("Id")) { try { writer.Scalar("Id", value.@Id, "Id"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Id", exception); } }
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", value.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
        }, static value => value == null ? null : (object)((global::Bing.Domain.Entities.IKey<int>)value).@Id, new string[] { "Id", "Name" }, null);

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.AcceptanceBranch> Model3 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.AcceptanceBranch>(typeof(global::Bing.Domain.ChangeTracking.AcceptanceBranch).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.AcceptanceBranch", static (writer, value) =>
        {
            if (!writer.IsIgnored("Children")) { try { writer.Sequence("Children", value.@Children, __GraphFixture.Model2, static value => value == null ? null : (object)((global::Bing.Domain.Entities.IKey<int>)value).@Id, "Children"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Children", exception); } }
        }, null, new string[] { "Children" }, new global::System.Collections.Generic.Dictionary<string, global::System.Type> { { "Children", typeof(global::Bing.Domain.ChangeTracking.AcceptanceChild) }, });

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.AcceptanceChild[]> Model4 = global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.AcceptanceChild[]>.CreateSequenceModel<global::Bing.Domain.ChangeTracking.AcceptanceChild[], global::Bing.Domain.ChangeTracking.AcceptanceChild>( "global::Bing.Domain.ChangeTracking.AcceptanceChild[]", static value => value, static () => __GraphFixture.Model2, static value => value == null ? null : (object)((global::Bing.Domain.Entities.IKey<int>)value).@Id );

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Collections.Generic.List<global::System.Collections.Generic.Dictionary<string, global::Bing.Domain.ChangeTracking.AcceptanceChild>>> Model5 = global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Collections.Generic.List<global::System.Collections.Generic.Dictionary<string, global::Bing.Domain.ChangeTracking.AcceptanceChild>>>.CreateSequenceModel<global::System.Collections.Generic.List<global::System.Collections.Generic.Dictionary<string, global::Bing.Domain.ChangeTracking.AcceptanceChild>>, global::System.Collections.Generic.Dictionary<string, global::Bing.Domain.ChangeTracking.AcceptanceChild>>( "global::System.Collections.Generic.List<global::System.Collections.Generic.Dictionary<string, global::Bing.Domain.ChangeTracking.AcceptanceChild>>", static value => value, static () => __GraphFixture.Model6, null );

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Collections.Generic.Dictionary<string, global::Bing.Domain.ChangeTracking.AcceptanceChild>> Model6 = global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Collections.Generic.Dictionary<string, global::Bing.Domain.ChangeTracking.AcceptanceChild>>.CreateDictionaryModel<global::System.Collections.Generic.Dictionary<string, global::Bing.Domain.ChangeTracking.AcceptanceChild>, string, global::Bing.Domain.ChangeTracking.AcceptanceChild>( "global::System.Collections.Generic.Dictionary<string, global::Bing.Domain.ChangeTracking.AcceptanceChild>", static value => value, static () => __GraphFixture.Model2 );

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @ActualAuditEnvelope : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.ActualAuditEnvelope> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.ActualAuditEnvelope> writer)
    {
            if (!writer.IsIgnored("Child")) { try { writer.Object("Child", this.@Child, global::Bing.Domain.ChangeTracking.ActualAuditRoot.ExplicitGraphModel, "Child"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Child", exception); } }
            if (!writer.IsIgnored("Children")) { try { writer.Sequence("Children", this.@Children, global::Bing.Domain.ChangeTracking.ActualAuditRoot.ExplicitGraphModel, global::Bing.Domain.ChangeTracking.ActualAuditRoot.ExplicitGraphModel.KeySelector, "Children"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Children", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.ActualAuditEnvelope> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.ActualAuditEnvelope>(typeof(global::Bing.Domain.ChangeTracking.ActualAuditEnvelope).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.ActualAuditEnvelope", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Child", "Children" }, new global::System.Collections.Generic.Dictionary<string, global::System.Type> { { "Children", typeof(global::Bing.Domain.ChangeTracking.ActualAuditRoot) }, });

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @ActualAuditRoot : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.ActualAuditRoot> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.ActualAuditRoot> writer)
    {
            if (!writer.IsIgnored("BusinessDate")) { try { writer.Scalar("BusinessDate", this.@BusinessDate, "BusinessDate"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("BusinessDate", exception); } }
            if (!writer.IsIgnored("CreatorId")) { try { writer.Scalar("CreatorId", this.@CreatorId, "CreatorId"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("CreatorId", exception); } }
            if (!writer.IsIgnored("DeleterId")) { try { writer.Scalar("DeleterId", this.@DeleterId, "DeleterId"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("DeleterId", exception); } }
            if (!writer.IsIgnored("Id")) { try { writer.Scalar("Id", this.@Id, "Id"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Id", exception); } }
            if (!writer.IsIgnored("LastModifierId")) { try { writer.Scalar("LastModifierId", this.@LastModifierId, "LastModifierId"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("LastModifierId", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.ActualAuditRoot> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.ActualAuditRoot>(typeof(global::Bing.Domain.ChangeTracking.ActualAuditRoot).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.ActualAuditRoot", static (writer, value) => value.CaptureGraphMembers(writer), static value => { if (value == null) return null; var keys = ((global::Bing.Domain.Entities.IEntity)value).GetKeys(); return keys.Length == 1 ? keys[0] : (object)global::Bing.Domain.ChangeTracking.ChangeGraphKey.Create(keys); }, new string[] { "BusinessDate", "CreatorId", "DeleterId", "Id", "LastModifierId" }, null);

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @BusinessTimestampGraph : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.BusinessTimestampGraph> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.BusinessTimestampGraph> writer)
    {
            if (!writer.IsIgnored("DeletionTime")) { try { writer.Scalar("DeletionTime", this.@DeletionTime, "DeletionTime"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("DeletionTime", exception); } }
            if (!writer.IsIgnored("LastModificationTime")) { try { writer.Scalar("LastModificationTime", this.@LastModificationTime, "LastModificationTime"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("LastModificationTime", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.BusinessTimestampGraph> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.BusinessTimestampGraph>(typeof(global::Bing.Domain.ChangeTracking.BusinessTimestampGraph).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.BusinessTimestampGraph", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "DeletionTime", "LastModificationTime" }, null);

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @CustomKeyGraph : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.CustomKeyGraph> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.CustomKeyGraph> writer)
    {
            if (!writer.IsIgnored("Lines")) { try { writer.Sequence("Lines", this.@Lines, __GraphFixture.Model1, static value => value == null ? null : (object)((global::Bing.Domain.Entities.IKey<int>)value).@Id, "Lines"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Lines", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.CustomKeyGraph> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.CustomKeyGraph>(typeof(global::Bing.Domain.ChangeTracking.CustomKeyGraph).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.CustomKeyGraph", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Lines" }, new global::System.Collections.Generic.Dictionary<string, global::System.Type> { { "Lines", typeof(global::Bing.Domain.ChangeTracking.CustomKeyLine) }, });

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.CustomKeyLine> Model1 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.CustomKeyLine>(typeof(global::Bing.Domain.ChangeTracking.CustomKeyLine).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.CustomKeyLine", static (writer, value) =>
        {
            if (!writer.IsIgnored("ClientKey")) { try { writer.Scalar("ClientKey", value.@ClientKey, "ClientKey"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("ClientKey", exception); } }
            if (!writer.IsIgnored("Id")) { try { writer.Scalar("Id", value.@Id, "Id"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Id", exception); } }
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", value.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
        }, static value => value == null ? null : (object)((global::Bing.Domain.Entities.IKey<int>)value).@Id, new string[] { "ClientKey", "Id", "Name" }, null);

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @DomainComparerRoot : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.DomainComparerRoot> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.DomainComparerRoot> writer)
    {
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", this.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.DomainComparerRoot> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.DomainComparerRoot>(typeof(global::Bing.Domain.ChangeTracking.DomainComparerRoot).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.DomainComparerRoot", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Name" }, null);

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @IgnoredBranchGraph : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.IgnoredBranchGraph> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.IgnoredBranchGraph> writer)
    {
            if (!writer.IsIgnored("Visible")) { try { writer.Scalar("Visible", this.@Visible, "Visible"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Visible", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.IgnoredBranchGraph> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.IgnoredBranchGraph>(typeof(global::Bing.Domain.ChangeTracking.IgnoredBranchGraph).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.IgnoredBranchGraph", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Visible" }, null);

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @IgnoredScalarGraph : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.IgnoredScalarGraph> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.IgnoredScalarGraph> writer)
    {
            if (!writer.IsIgnored("Visible")) { try { writer.Scalar("Visible", this.@Visible, "Visible"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Visible", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.IgnoredScalarGraph> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.IgnoredScalarGraph>(typeof(global::Bing.Domain.ChangeTracking.IgnoredScalarGraph).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.IgnoredScalarGraph", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Visible" }, null);

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @InheritedIgnoreGraph : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.InheritedIgnoreGraph> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.InheritedIgnoreGraph> writer)
    {
            if (!writer.IsIgnored("Hidden")) { try { writer.Object("Hidden", this.@Hidden, __GraphFixture.Model1, "Hidden"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Hidden", exception); } }
            if (!writer.IsIgnored("Inherited")) { try { writer.Object("Inherited", this.@Inherited, __GraphFixture.Model2, "Inherited"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Inherited", exception); } }
            if (!writer.IsIgnored("Override")) { try { writer.Object("Override", this.@Override, __GraphFixture.Model3, "Override"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Override", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.InheritedIgnoreGraph> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.InheritedIgnoreGraph>(typeof(global::Bing.Domain.ChangeTracking.InheritedIgnoreGraph).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.InheritedIgnoreGraph", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Hidden", "Inherited", "Override" }, null);

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.HiddenIgnoreNode> Model1 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.HiddenIgnoreNode>(typeof(global::Bing.Domain.ChangeTracking.HiddenIgnoreNode).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.HiddenIgnoreNode", static (writer, value) =>
        {
            if (!writer.IsIgnored("Value")) { try { writer.Scalar("Value", value.@Value, "Value"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Value", exception); } }
        }, null, new string[] { "Value" }, null);

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.InheritedIgnoreNode> Model2 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.InheritedIgnoreNode>(typeof(global::Bing.Domain.ChangeTracking.InheritedIgnoreNode).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.InheritedIgnoreNode", static (writer, value) =>
        {
        }, null, new string[] {  }, null);

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.OverrideIgnoreNode> Model3 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.OverrideIgnoreNode>(typeof(global::Bing.Domain.ChangeTracking.OverrideIgnoreNode).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.OverrideIgnoreNode", static (writer, value) =>
        {
        }, null, new string[] {  }, null);

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @LegacyContainer : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.LegacyContainer> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.LegacyContainer> writer)
    {
            if (!writer.IsIgnored("Child")) { try { writer.Object("Child", this.@Child, global::Bing.Domain.ChangeTracking.LegacyNestedChild.ExplicitGraphModel, "Child"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Child", exception); } }
            if (!writer.IsIgnored("Children")) { try { writer.Sequence("Children", this.@Children, global::Bing.Domain.ChangeTracking.LegacyNestedChild.ExplicitGraphModel, global::Bing.Domain.ChangeTracking.LegacyNestedChild.ExplicitGraphModel.KeySelector, "Children"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Children", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.LegacyContainer> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.LegacyContainer>(typeof(global::Bing.Domain.ChangeTracking.LegacyContainer).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.LegacyContainer", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Child", "Children" }, new global::System.Collections.Generic.Dictionary<string, global::System.Type> { { "Children", typeof(global::Bing.Domain.ChangeTracking.LegacyNestedChild) }, });

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @LegacyNestedChild : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.LegacyNestedChild> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.LegacyNestedChild> writer)
    {
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", this.@Name, "name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.LegacyNestedChild> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.LegacyNestedChild>(typeof(global::Bing.Domain.ChangeTracking.LegacyNestedChild).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.LegacyNestedChild", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Name" }, null);

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @LocalIgnoreGraph : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.LocalIgnoreGraph> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.LocalIgnoreGraph> writer)
    {
            if (!writer.IsIgnored("Child")) { try { writer.Object("Child", this.@Child, __GraphFixture.Model1, "Child"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Child", exception); } }
            if (!writer.IsIgnored("Items")) { try { writer.Sequence("Items", this.@Items, __GraphFixture.Model1, null, "Items"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Items", exception); } }
            if (!writer.IsIgnored("Map")) { try { writer.Dictionary("Map", this.@Map, __GraphFixture.Model1, "Map"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Map", exception); } }
            if (!writer.IsIgnored("Nested")) { try { writer.Sequence("Nested", this.@Nested, __GraphFixture.Model2, null, "Nested"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Nested", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.LocalIgnoreGraph> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.LocalIgnoreGraph>(typeof(global::Bing.Domain.ChangeTracking.LocalIgnoreGraph).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.LocalIgnoreGraph", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Child", "Items", "Map", "Nested" }, new global::System.Collections.Generic.Dictionary<string, global::System.Type> { { "Items", typeof(global::Bing.Domain.ChangeTracking.LocalIgnoreNode) }, { "Nested", typeof(global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.LocalIgnoreNode>) }, });

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.LocalIgnoreNode> Model1 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.LocalIgnoreNode>(typeof(global::Bing.Domain.ChangeTracking.LocalIgnoreNode).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.LocalIgnoreNode", static (writer, value) =>
        {
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", value.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
        }, null, new string[] { "Name" }, null);

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.LocalIgnoreNode>> Model2 = global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.LocalIgnoreNode>>.CreateSequenceModel<global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.LocalIgnoreNode>, global::Bing.Domain.ChangeTracking.LocalIgnoreNode>( "global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.LocalIgnoreNode>", static value => value, static () => __GraphFixture.Model1, null );

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @NestedCollectionGraph : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.NestedCollectionGraph> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.NestedCollectionGraph> writer)
    {
            if (!writer.IsIgnored("Groups")) { try { writer.Sequence("Groups", this.@Groups, __GraphFixture.Model1, null, "Groups"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Groups", exception); } }
            if (!writer.IsIgnored("Map")) { try { writer.Dictionary("Map", this.@Map, __GraphFixture.Model1, "Map"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Map", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.NestedCollectionGraph> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.NestedCollectionGraph>(typeof(global::Bing.Domain.ChangeTracking.NestedCollectionGraph).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.NestedCollectionGraph", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Groups", "Map" }, new global::System.Collections.Generic.Dictionary<string, global::System.Type> { { "Groups", typeof(global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.OrderProduct>) }, });

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.OrderProduct>> Model1 = global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.OrderProduct>>.CreateSequenceModel<global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.OrderProduct>, global::Bing.Domain.ChangeTracking.OrderProduct>( "global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.OrderProduct>", static value => value, static () => __GraphFixture.Model2, null );

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.OrderProduct> Model2 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.OrderProduct>(typeof(global::Bing.Domain.ChangeTracking.OrderProduct).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.OrderProduct", static (writer, value) =>
        {
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", value.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
        }, null, new string[] { "Name" }, null);

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @NestedIgnorePathsGraph : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.NestedIgnorePathsGraph> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.NestedIgnorePathsGraph> writer)
    {
            if (!writer.IsIgnored("Groups")) { try { writer.Sequence("Groups", this.@Groups, __GraphFixture.Model1, null, "Groups"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Groups", exception); } }
            if (!writer.IsIgnored("Map")) { try { writer.Dictionary("Map", this.@Map, __GraphFixture.Model1, "Map"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Map", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.NestedIgnorePathsGraph> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.NestedIgnorePathsGraph>(typeof(global::Bing.Domain.ChangeTracking.NestedIgnorePathsGraph).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.NestedIgnorePathsGraph", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Groups", "Map" }, new global::System.Collections.Generic.Dictionary<string, global::System.Type> { { "Groups", typeof(global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.NestedIgnorePathNode>) }, });

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.NestedIgnorePathNode>> Model1 = global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.NestedIgnorePathNode>>.CreateSequenceModel<global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.NestedIgnorePathNode>, global::Bing.Domain.ChangeTracking.NestedIgnorePathNode>( "global::System.Collections.Generic.List<global::Bing.Domain.ChangeTracking.NestedIgnorePathNode>", static value => value, static () => __GraphFixture.Model2, null );

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.NestedIgnorePathNode> Model2 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.NestedIgnorePathNode>(typeof(global::Bing.Domain.ChangeTracking.NestedIgnorePathNode).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.NestedIgnorePathNode", static (writer, value) =>
        {
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", value.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
        }, null, new string[] { "Name" }, null);

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @NullableValueGraph : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.NullableValueGraph> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.NullableValueGraph> writer)
    {
            if (!writer.IsIgnored("Value")) { try { writer.Object("Value", this.@Value, __GraphFixture.Model1, "Value"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Value", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.NullableValueGraph> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.NullableValueGraph>(typeof(global::Bing.Domain.ChangeTracking.NullableValueGraph).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.NullableValueGraph", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Value" }, null);

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Nullable<global::Bing.Domain.ChangeTracking.MutableValue>> Model1 = global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Nullable<global::Bing.Domain.ChangeTracking.MutableValue>>.CreateNullableModel<global::Bing.Domain.ChangeTracking.MutableValue>( "global::Bing.Domain.ChangeTracking.MutableValue?", static () => __GraphFixture.Model2 );

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.MutableValue> Model2 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.MutableValue>(typeof(global::Bing.Domain.ChangeTracking.MutableValue).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.MutableValue", static (writer, value) =>
        {
            if (!writer.IsIgnored("Names")) { try { writer.SequenceScalar("Names", value.@Names, "Names"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Names", exception); } }
        }, null, new string[] { "Names" }, new global::System.Collections.Generic.Dictionary<string, global::System.Type> { { "Names", typeof(string) }, });

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @OrderGraph : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.OrderGraph> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.OrderGraph> writer)
    {
            if (!writer.IsIgnored("CompositeLines")) { try { writer.Sequence("CompositeLines", this.@CompositeLines, __GraphFixture.Model1, static value => { if (value == null) return null; var keys = ((global::Bing.Domain.Entities.IEntity)value).GetKeys(); return keys.Length == 1 ? keys[0] : (object)global::Bing.Domain.ChangeTracking.ChangeGraphKey.Create(keys); }, "CompositeLines"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("CompositeLines", exception); } }
            if (!writer.IsIgnored("Customer")) { try { writer.Object("Customer", this.@Customer, __GraphFixture.Model2, "Customer"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Customer", exception); } }
            if (!writer.IsIgnored("Id")) { try { writer.Scalar("Id", this.@Id, "Id"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Id", exception); } }
            if (!writer.IsIgnored("LineMap")) { try { writer.Dictionary("LineMap", this.@LineMap, __GraphFixture.Model3, "LineMap"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("LineMap", exception); } }
            if (!writer.IsIgnored("LineSet")) { try { writer.Set("LineSet", this.@LineSet, __GraphFixture.Model3, static value => value == null ? null : (object)((global::Bing.Domain.Entities.IKey<int>)value).@Id, "LineSet"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("LineSet", exception); } }
            if (!writer.IsIgnored("Lines")) { try { writer.Sequence("Lines", this.@Lines, __GraphFixture.Model3, static value => value == null ? null : (object)((global::Bing.Domain.Entities.IKey<int>)value).@Id, "Lines"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Lines", exception); } }
            if (!writer.IsIgnored("TagSet")) { try { writer.SetScalar("TagSet", this.@TagSet, "TagSet"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("TagSet", exception); } }
            if (!writer.IsIgnored("Tags")) { try { writer.SequenceScalar("Tags", this.@Tags, "Tags"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Tags", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.OrderGraph> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.OrderGraph>(typeof(global::Bing.Domain.ChangeTracking.OrderGraph).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.OrderGraph", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "CompositeLines", "Customer", "Id", "LineMap", "LineSet", "Lines", "TagSet", "Tags" }, new global::System.Collections.Generic.Dictionary<string, global::System.Type> { { "CompositeLines", typeof(global::Bing.Domain.ChangeTracking.CompositeLine) }, { "LineSet", typeof(global::Bing.Domain.ChangeTracking.OrderLine) }, { "Lines", typeof(global::Bing.Domain.ChangeTracking.OrderLine) }, { "TagSet", typeof(string) }, { "Tags", typeof(string) }, });

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.CompositeLine> Model1 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.CompositeLine>(typeof(global::Bing.Domain.ChangeTracking.CompositeLine).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.CompositeLine", static (writer, value) =>
        {
            if (!writer.IsIgnored("Lot")) { try { writer.Scalar("Lot", value.@Lot, "Lot"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Lot", exception); } }
            if (!writer.IsIgnored("Sku")) { try { writer.Scalar("Sku", value.@Sku, "Sku"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Sku", exception); } }
            if (!writer.IsIgnored("Value")) { try { writer.Scalar("Value", value.@Value, "Value"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Value", exception); } }
        }, static value => { if (value == null) return null; var keys = ((global::Bing.Domain.Entities.IEntity)value).GetKeys(); return keys.Length == 1 ? keys[0] : (object)global::Bing.Domain.ChangeTracking.ChangeGraphKey.Create(keys); }, new string[] { "Lot", "Sku", "Value" }, null);

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.OrderCustomer> Model2 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.OrderCustomer>(typeof(global::Bing.Domain.ChangeTracking.OrderCustomer).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.OrderCustomer", static (writer, value) =>
        {
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", value.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
        }, null, new string[] { "Name" }, null);

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.OrderLine> Model3 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.OrderLine>(typeof(global::Bing.Domain.ChangeTracking.OrderLine).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.OrderLine", static (writer, value) =>
        {
            if (!writer.IsIgnored("Amount")) { try { writer.Scalar("Amount", value.@Amount, "Amount"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Amount", exception); } }
            if (!writer.IsIgnored("Id")) { try { writer.Scalar("Id", value.@Id, "Id"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Id", exception); } }
            if (!writer.IsIgnored("Product")) { try { writer.Object("Product", value.@Product, __GraphFixture.Model4, "Product"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Product", exception); } }
        }, static value => value == null ? null : (object)((global::Bing.Domain.Entities.IKey<int>)value).@Id, new string[] { "Amount", "Id", "Product" }, null);

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.OrderProduct> Model4 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.OrderProduct>(typeof(global::Bing.Domain.ChangeTracking.OrderProduct).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.OrderProduct", static (writer, value) =>
        {
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", value.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
        }, null, new string[] { "Name" }, null);

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @PathGraph : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.PathGraph> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.PathGraph> writer)
    {
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.PathGraph> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.PathGraph>(typeof(global::Bing.Domain.ChangeTracking.PathGraph).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.PathGraph", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] {  }, null);

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @PolymorphicBase : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.PolymorphicBase> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.PolymorphicBase> writer)
    {
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", this.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.PolymorphicBase> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.PolymorphicBase>(typeof(global::Bing.Domain.ChangeTracking.PolymorphicBase).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.PolymorphicBase", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Name" }, null);

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @PolymorphicChild : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public new global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public new static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.PolymorphicChild> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.PolymorphicChild> writer)
    {
            if (!writer.IsIgnored("Extra")) { try { writer.Scalar("Extra", this.@Extra, "Extra"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Extra", exception); } }
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", this.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.PolymorphicChild> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.PolymorphicChild>(typeof(global::Bing.Domain.ChangeTracking.PolymorphicChild).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.PolymorphicChild", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Extra", "Name" }, null);

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @PolymorphicHolder : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.PolymorphicHolder> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.PolymorphicHolder> writer)
    {
            if (!writer.IsIgnored("Child")) { try { writer.Object("Child", this.@Child, global::Bing.Domain.ChangeTracking.PolymorphicBase.ExplicitGraphModel, "Child"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Child", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.PolymorphicHolder> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.PolymorphicHolder>(typeof(global::Bing.Domain.ChangeTracking.PolymorphicHolder).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.PolymorphicHolder", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Child" }, null);

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @PrunedGraph : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.PrunedGraph> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.PrunedGraph> writer)
    {
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.PrunedGraph> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.PrunedGraph>(typeof(global::Bing.Domain.ChangeTracking.PrunedGraph).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.PrunedGraph", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] {  }, null);

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @RecursiveGraph : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.RecursiveGraph> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.RecursiveGraph> writer)
    {
            if (!writer.IsIgnored("Root")) { try { writer.Object("Root", this.@Root, __GraphFixture.Model1, "Root"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Root", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.RecursiveGraph> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.RecursiveGraph>(typeof(global::Bing.Domain.ChangeTracking.RecursiveGraph).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.RecursiveGraph", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Root" }, null);

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.RecursiveNode> Model1 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.RecursiveNode>(typeof(global::Bing.Domain.ChangeTracking.RecursiveNode).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.RecursiveNode", static (writer, value) =>
        {
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", value.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
            if (!writer.IsIgnored("Next")) { try { writer.Object("Next", value.@Next, __GraphFixture.Model1, "Next"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Next", exception); } }
        }, null, new string[] { "Name", "Next" }, null);

    }
}
}

namespace Bing.Domain.ChangeTracking
{

public partial class @ThrowingGraph : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.ThrowingGraph> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Bing.Domain.ChangeTracking.ThrowingGraph> writer)
    {
            if (!writer.IsIgnored("Value")) { try { writer.Scalar("Value", this.@Value, "Value"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Value", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.ThrowingGraph> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Bing.Domain.ChangeTracking.ThrowingGraph>(typeof(global::Bing.Domain.ChangeTracking.ThrowingGraph).AssemblyQualifiedName + ":" + "global::Bing.Domain.ChangeTracking.ThrowingGraph", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Value" }, null);

    }
}
}

