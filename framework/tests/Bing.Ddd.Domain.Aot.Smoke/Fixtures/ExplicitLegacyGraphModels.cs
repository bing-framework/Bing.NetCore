// 独立 ChangeGraph Smoke 使用的固定模型，保留原有快照和边界测试规则。
// 领域对象 AddChanges 不生成或依赖这些测试模型。
#nullable disable

// 跨程序集实体没有自动生成 Graph 模型；独立 Graph 场景在测试中显式提供。
internal static class ExternalEntityGraphFixture
{
    internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::DomainAotFixtures.ExternalEntity> Model =
        new("external-entity", static (writer, value) =>
        {
            writer.Scalar("Id", value.Id);
            writer.Scalar("Name", value.Name, "外部名称");
        }, static value => value.Id, new[] { "Id", "Name" });
}

public partial class @AuditTimestampDefaultGraph : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::AuditTimestampDefaultGraph> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::AuditTimestampDefaultGraph> writer)
    {
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", this.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::AuditTimestampDefaultGraph> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::AuditTimestampDefaultGraph>(typeof(global::AuditTimestampDefaultGraph).AssemblyQualifiedName + ":" + "global::AuditTimestampDefaultGraph", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Name" }, null);

    }
}

public partial class @Customer : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Customer> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Customer> writer)
    {
            if (!writer.IsIgnored("Id")) { try { writer.Scalar("Id", this.@Id, "Id"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Id", exception); } }
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", this.@Name, "客户名称"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Customer> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Customer>(typeof(global::Customer).AssemblyQualifiedName + ":" + "global::Customer", static (writer, value) => value.CaptureGraphMembers(writer), static value => value == null ? null : (object)value.@Id, new string[] { "Id", "Name" }, null);

    }
}

public partial class @Department : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Department> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Department> writer)
    {
            if (!writer.IsIgnored("Children")) { try { writer.Sequence("Children", this.@Children, __GraphFixture.Model0, static value => value == null ? null : (object)value.@Id, "Children"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Children", exception); } }
            if (!writer.IsIgnored("Id")) { try { writer.Scalar("Id", this.@Id, "Id"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Id", exception); } }
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", this.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
            if (!writer.IsIgnored("Parent")) { try { writer.Object("Parent", this.@Parent, __GraphFixture.Model0, "Parent"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Parent", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Department> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Department>(typeof(global::Department).AssemblyQualifiedName + ":" + "global::Department", static (writer, value) => value.CaptureGraphMembers(writer), static value => value == null ? null : (object)value.@Id, new string[] { "Children", "Id", "Name", "Parent" }, new global::System.Collections.Generic.Dictionary<string, global::System.Type> { { "Children", typeof(global::Department) }, });

    }
}

public partial class @IgnoredSubtreeRoot : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::IgnoredSubtreeRoot> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::IgnoredSubtreeRoot> writer)
    {
            if (!writer.IsIgnored("Visible")) { try { writer.Scalar("Visible", this.@Visible, "Visible"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Visible", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::IgnoredSubtreeRoot> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::IgnoredSubtreeRoot>(typeof(global::IgnoredSubtreeRoot).AssemblyQualifiedName + ":" + "global::IgnoredSubtreeRoot", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Visible" }, null);

    }
}

public partial class @LegacySample : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::LegacySample> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::LegacySample> writer)
    {
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", this.@Name, "旧版名称"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::LegacySample> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::LegacySample>(typeof(global::LegacySample).AssemblyQualifiedName + ":" + "global::LegacySample", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Name" }, null);

    }
}

public partial class @Line : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Line> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Line> writer)
    {
            if (!writer.IsIgnored("Amount")) { try { writer.Scalar("Amount", this.@Amount, "行金额"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Amount", exception); } }
            if (!writer.IsIgnored("Id")) { try { writer.Scalar("Id", this.@Id, "Id"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Id", exception); } }
            if (!writer.IsIgnored("Product")) { try { writer.Object("Product", this.@Product, global::Product.ExplicitGraphModel, "Product"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Product", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Line> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Line>(typeof(global::Line).AssemblyQualifiedName + ":" + "global::Line", static (writer, value) => value.CaptureGraphMembers(writer), static value => value == null ? null : (object)value.@Id, new string[] { "Amount", "Id", "Product" }, null);

    }
}

public partial class @Order : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Order> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Order> writer)
    {
            if (!writer.IsIgnored("Customer")) { try { writer.Object("Customer", this.@Customer, global::Customer.ExplicitGraphModel, "Customer"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Customer", exception); } }
            if (!writer.IsIgnored("Id")) { try { writer.Scalar("Id", this.@Id, "Id"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Id", exception); } }
            if (!writer.IsIgnored("Lines")) { try { writer.Sequence("Lines", this.@Lines, global::Line.ExplicitGraphModel, global::Line.ExplicitGraphModel.KeySelector, "Lines"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Lines", exception); } }
            if (!writer.IsIgnored("PrimaryContact")) { try { writer.Object("PrimaryContact", this.@PrimaryContact, global::Customer.ExplicitGraphModel, "PrimaryContact"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("PrimaryContact", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Order> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Order>(typeof(global::Order).AssemblyQualifiedName + ":" + "global::Order", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Customer", "Id", "Lines", "PrimaryContact" }, new global::System.Collections.Generic.Dictionary<string, global::System.Type> { { "Lines", typeof(global::Line) }, });

    }
}

public partial class @Product : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Product> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::Product> writer)
    {
            if (!writer.IsIgnored("Id")) { try { writer.Scalar("Id", this.@Id, "Id"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Id", exception); } }
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", this.@Name, "商品名称"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Product> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::Product>(typeof(global::Product).AssemblyQualifiedName + ":" + "global::Product", static (writer, value) => value.CaptureGraphMembers(writer), static value => value == null ? null : (object)value.@Id, new string[] { "Id", "Name" }, null);

    }
}

public partial class @ReviewAdapterRoot : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewAdapterRoot> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::ReviewAdapterRoot> writer)
    {
            if (!writer.IsIgnored("Value")) { try { writer.Adapted("Value", this.@Value, "Value"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Value", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewAdapterRoot> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewAdapterRoot>(typeof(global::ReviewAdapterRoot).AssemblyQualifiedName + ":" + "global::ReviewAdapterRoot", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Value" }, null);

    }
}

public partial class @ReviewBudgetChild : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewBudgetChild> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::ReviewBudgetChild> writer)
    {
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", this.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewBudgetChild> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewBudgetChild>(typeof(global::ReviewBudgetChild).AssemblyQualifiedName + ":" + "global::ReviewBudgetChild", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Name" }, null);

    }
}

public partial class @ReviewBudgetRoot : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewBudgetRoot> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::ReviewBudgetRoot> writer)
    {
            if (!writer.IsIgnored("Children")) { try { writer.Sequence("Children", this.@Children, global::ReviewBudgetChild.ExplicitGraphModel, global::ReviewBudgetChild.ExplicitGraphModel.KeySelector, "Children"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Children", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewBudgetRoot> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewBudgetRoot>(typeof(global::ReviewBudgetRoot).AssemblyQualifiedName + ":" + "global::ReviewBudgetRoot", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Children" }, new global::System.Collections.Generic.Dictionary<string, global::System.Type> { { "Children", typeof(global::ReviewBudgetChild) }, });

    }
}

public partial class @ReviewCollectionsRoot : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewCollectionsRoot> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::ReviewCollectionsRoot> writer)
    {
            if (!writer.IsIgnored("Map")) { try { writer.Dictionary("Map", this.@Map, global::ReviewCompositeItem.ExplicitGraphModel, "Map"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Map", exception); } }
            if (!writer.IsIgnored("Members")) { try { writer.Set("Members", this.@Members, global::ReviewCompositeItem.ExplicitGraphModel, global::ReviewCompositeItem.ExplicitGraphModel.KeySelector, "Members"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Members", exception); } }
            if (!writer.IsIgnored("Optional")) { try { writer.Object("Optional", this.@Optional, __GraphFixture.Model2, "Optional"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Optional", exception); } }
            if (!writer.IsIgnored("Ordered")) { try { writer.Sequence("Ordered", this.@Ordered, global::ReviewCompositeItem.ExplicitGraphModel, global::ReviewCompositeItem.ExplicitGraphModel.KeySelector, "Ordered"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Ordered", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewCollectionsRoot> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewCollectionsRoot>(typeof(global::ReviewCollectionsRoot).AssemblyQualifiedName + ":" + "global::ReviewCollectionsRoot", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Map", "Members", "Optional", "Ordered" }, new global::System.Collections.Generic.Dictionary<string, global::System.Type> { { "Members", typeof(global::ReviewCompositeItem) }, { "Ordered", typeof(global::ReviewCompositeItem) }, });

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Nullable<global::ReviewNullableValue>> Model2 = global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Nullable<global::ReviewNullableValue>>.CreateNullableModel<global::ReviewNullableValue>( "global::ReviewNullableValue?", static () => __GraphFixture.Model3 );

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewNullableValue> Model3 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewNullableValue>(typeof(global::ReviewNullableValue).AssemblyQualifiedName + ":" + "global::ReviewNullableValue", static (writer, value) =>
        {
            if (!writer.IsIgnored("Count")) { try { writer.Scalar("Count", value.@Count, "Count"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Count", exception); } }
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", value.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
        }, null, new string[] { "Count", "Name" }, null);

    }
}

public partial class @ReviewCompositeItem : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewCompositeItem> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::ReviewCompositeItem> writer)
    {
            if (!writer.IsIgnored("ItemId")) { try { writer.Scalar("ItemId", this.@ItemId, "ItemId"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("ItemId", exception); } }
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", this.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
            if (!writer.IsIgnored("TenantId")) { try { writer.Scalar("TenantId", this.@TenantId, "TenantId"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("TenantId", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewCompositeItem> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewCompositeItem>(typeof(global::ReviewCompositeItem).AssemblyQualifiedName + ":" + "global::ReviewCompositeItem", static (writer, value) => value.CaptureGraphMembers(writer), static value => { if (value == null) return null; var keys = ((global::Bing.Domain.Entities.IEntity)value).GetKeys(); return keys.Length == 1 ? keys[0] : (object)global::Bing.Domain.ChangeTracking.ChangeGraphKey.Create(keys); }, new string[] { "ItemId", "Name", "TenantId" }, null);

    }
}

public partial class @ReviewDag : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewDag> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::ReviewDag> writer)
    {
            if (!writer.IsIgnored("Left")) { try { writer.Object("Left", this.@Left, __GraphFixture.Model0, "Left"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Left", exception); } }
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", this.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
            if (!writer.IsIgnored("Right")) { try { writer.Object("Right", this.@Right, __GraphFixture.Model0, "Right"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Right", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewDag> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewDag>(typeof(global::ReviewDag).AssemblyQualifiedName + ":" + "global::ReviewDag", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Left", "Name", "Right" }, null);

    }
}

public partial class @ReviewDerivedEntity : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public new global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public new static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewDerivedEntity> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::ReviewDerivedEntity> writer)
    {
            if (!writer.IsIgnored("Extra")) { try { writer.Scalar("Extra", this.@Extra, "Extra"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Extra", exception); } }
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", this.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewDerivedEntity> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewDerivedEntity>(typeof(global::ReviewDerivedEntity).AssemblyQualifiedName + ":" + "global::ReviewDerivedEntity", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Extra", "Name" }, null);

    }
}

public partial class @ReviewDescriptionEntity : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewDescriptionEntity> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::ReviewDescriptionEntity> writer)
    {
            if (!writer.IsIgnored("Hidden")) { try { writer.Scalar("Hidden", this.@Hidden, "隐藏说明"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Hidden", exception); } }
            if (!writer.IsIgnored("Inherited")) { try { writer.Scalar("Inherited", this.@Inherited, "继承说明"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Inherited", exception); } }
            if (!writer.IsIgnored("Overridden")) { try { writer.Scalar("Overridden", this.@Overridden, "基类虚属性"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Overridden", exception); } }
            if (!writer.IsIgnored("Plain")) { try { writer.Scalar("Plain", this.@Plain, "Plain"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Plain", exception); } }
            if (!writer.IsIgnored("Text")) { try { writer.Scalar("Text", this.@Text, "中文\"引号\\斜线\n换行\t制表"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Text", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewDescriptionEntity> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewDescriptionEntity>(typeof(global::ReviewDescriptionEntity).AssemblyQualifiedName + ":" + "global::ReviewDescriptionEntity", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Hidden", "Inherited", "Overridden", "Plain", "Text" }, null);

    }
}

public partial class @ReviewGenericRoot<@T> : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewGenericRoot<T>> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::ReviewGenericRoot<T>> writer)
    {
            if (!writer.IsIgnored("Value")) { try { writer.Adapted("Value", this.@Value, "Value"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Value", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewGenericRoot<T>> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewGenericRoot<T>>(typeof(global::ReviewGenericRoot<T>).AssemblyQualifiedName + ":" + "global::ReviewGenericRoot<T>", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Value" }, null);

    }
}

public partial class @ReviewMatrixRoot : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewMatrixRoot> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::ReviewMatrixRoot> writer)
    {
            if (!writer.IsIgnored("Batches")) { try { writer.Dictionary("Batches", this.@Batches, __GraphFixture.Model1, "Batches"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Batches", exception); } }
            if (!writer.IsIgnored("Child")) { try { writer.Object("Child", this.@Child, global::ExternalEntityGraphFixture.Model, "Child"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Child", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewMatrixRoot> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewMatrixRoot>(typeof(global::ReviewMatrixRoot).AssemblyQualifiedName + ":" + "global::ReviewMatrixRoot", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Batches", "Child" }, null);

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Collections.Generic.List<global::DomainAotFixtures.ExternalEntity>> Model1 = global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Collections.Generic.List<global::DomainAotFixtures.ExternalEntity>>.CreateSequenceModel<global::System.Collections.Generic.List<global::DomainAotFixtures.ExternalEntity>, global::DomainAotFixtures.ExternalEntity>( "global::System.Collections.Generic.List<global::DomainAotFixtures.ExternalEntity>", static value => value, static () => global::ExternalEntityGraphFixture.Model, keySelectorResolver: static () => global::ExternalEntityGraphFixture.Model.KeySelector );

    }
}

public partial class @ReviewNullableRoot : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewNullableRoot> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::ReviewNullableRoot> writer)
    {
            if (!writer.IsIgnored("Value")) { try { writer.Object("Value", this.@Value, __GraphFixture.Model1, "Value"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Value", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewNullableRoot> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewNullableRoot>(typeof(global::ReviewNullableRoot).AssemblyQualifiedName + ":" + "global::ReviewNullableRoot", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Value" }, null);

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Nullable<global::ReviewNullableValue>> Model1 = global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::System.Nullable<global::ReviewNullableValue>>.CreateNullableModel<global::ReviewNullableValue>( "global::ReviewNullableValue?", static () => __GraphFixture.Model2 );

        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewNullableValue> Model2 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewNullableValue>(typeof(global::ReviewNullableValue).AssemblyQualifiedName + ":" + "global::ReviewNullableValue", static (writer, value) =>
        {
            if (!writer.IsIgnored("Count")) { try { writer.Scalar("Count", value.@Count, "Count"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Count", exception); } }
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", value.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
        }, null, new string[] { "Count", "Name" }, null);

    }
}

public partial class @ReviewParentEntity : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewParentEntity> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::ReviewParentEntity> writer)
    {
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", this.@Name, "Name"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewParentEntity> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewParentEntity>(typeof(global::ReviewParentEntity).AssemblyQualifiedName + ":" + "global::ReviewParentEntity", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Name" }, null);

    }
}

public partial class @ReviewPositionRoot : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewPositionRoot> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::ReviewPositionRoot> writer)
    {
            if (!writer.IsIgnored("Items")) { try { writer.SequenceScalar("Items", this.@Items, "Items"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Items", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewPositionRoot> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewPositionRoot>(typeof(global::ReviewPositionRoot).AssemblyQualifiedName + ":" + "global::ReviewPositionRoot", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Items" }, new global::System.Collections.Generic.Dictionary<string, global::System.Type> { { "Items", typeof(string) }, });

    }
}

public partial class @ReviewResourceEntity : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewResourceEntity> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::ReviewResourceEntity> writer)
    {
            if (!writer.IsIgnored("Name")) { try { writer.Scalar("Name", this.@Name, global::ReviewLabels.Name); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Name", exception); } }
            if (!writer.IsIgnored("Text")) { try { writer.Scalar("Text", this.@Text, "常量\"描述\\"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Text", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewResourceEntity> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewResourceEntity>(typeof(global::ReviewResourceEntity).AssemblyQualifiedName + ":" + "global::ReviewResourceEntity", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Name", "Text" }, null);

    }
}

public partial class @ReviewRuntimeRoot : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewRuntimeRoot> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::ReviewRuntimeRoot> writer)
    {
            if (!writer.IsIgnored("Value")) { try { writer.Adapted("Value", this.@Value, "Value"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Value", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewRuntimeRoot> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewRuntimeRoot>(typeof(global::ReviewRuntimeRoot).AssemblyQualifiedName + ":" + "global::ReviewRuntimeRoot", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Value" }, null);

    }
}

public partial class @ReviewScalarRoot : global::Bing.Domain.ChangeTracking.IChangeGraphNode
{
    public global::Bing.Domain.ChangeTracking.ChangeGraphModel ChangeGraphModel => __GraphFixture.Model0;
    public static global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewScalarRoot> ExplicitGraphModel => __GraphFixture.Model0;
    private void CaptureGraphMembers(global::Bing.Domain.ChangeTracking.ChangeGraphWriter<global::ReviewScalarRoot> writer)
    {
            if (!writer.IsIgnored("Date")) { try { writer.Scalar("Date", this.@Date, "Date"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Date", exception); } }
            if (!writer.IsIgnored("Number")) { try { writer.Scalar("Number", this.@Number, "Number"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Number", exception); } }
            if (!writer.IsIgnored("State")) { try { writer.Scalar("State", this.@State, "State"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("State", exception); } }
            if (!writer.IsIgnored("Text")) { try { writer.Scalar("Text", this.@Text, "Text"); } catch (global::System.Exception exception) { throw writer.CreateCaptureException("Text", exception); } }
    }

    private static class __GraphFixture
    {
        internal static readonly global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewScalarRoot> Model0 = new global::Bing.Domain.ChangeTracking.ChangeGraphModel<global::ReviewScalarRoot>(typeof(global::ReviewScalarRoot).AssemblyQualifiedName + ":" + "global::ReviewScalarRoot", static (writer, value) => value.CaptureGraphMembers(writer), null, new string[] { "Date", "Number", "State", "Text" }, null);

    }
}

