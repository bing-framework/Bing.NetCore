using Bing.Data.Sql.Generated;

// 旧 Lambda Smoke 改为显式模型登记，避免以全局调用点元数据替代宿主静态模型合同。
[SqlModel(typeof(Order))]
[SqlModel(typeof(Product))]
[SqlModel(typeof(BinaryRow))]
[SqlModel(typeof(FloatingRow))]
[SqlResult(typeof(Summary))]
[SqlResult(typeof(NameResult))]
[SqlResult(typeof(IgnoredSummary))]
[SqlResult(typeof(BoxedSummary))]
[SqlResult(typeof(EscapedSummary))]
[SqlResult(typeof(ComparableSummary))]
[SqlResult(typeof(ValueSummary))]
[SqlResult(typeof(FloatingSummary))]
internal static partial class LegacySmokeModels { }
