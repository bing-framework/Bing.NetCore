using System;
using System.Linq;
using System.Reflection;
using Bing.Domain.ChangeTracking;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Bing.Ddd.Domain.Extensions.Analyzers.Tests;

/// <summary>业务对象中不支持的关系必须产生可定位的诊断。</summary>
public sealed partial class ChangeGraphSourceGeneratorTest
{
    /// <summary>PRD-09：SKU 的普通 POCO 元数据不能被当成受支持的领域对象。</summary>
    [Fact]
    public void PRD09_PocoSkuMetadata_ReportsDiagnosticAndDoesNotEmitRelationshipHook()
    {
        var result = RunGenerator("""
            using Bing.Domain.Entities;

            public partial class ProductDetail : DomainObjectBase<ProductDetail>
            {
                public ProductMetadata Metadata { get; set; }
            }

            public class ProductMetadata
            {
                public string Origin { get; set; }
            }
            """);

        var diagnostic = AssertGeneratorDiagnostic(result, "CG006", DiagnosticSeverity.Warning);
        Assert.Contains("ProductDetail.Metadata", diagnostic.GetMessage());
        Assert.True(result.EmitSucceeded);
        Assert.DoesNotContain("AddObjectChanges(nameof(Metadata)",
            GetGeneratedAddChangesMethod(result, "ProductDetail").ToFullString());
    }

    /// <summary>CMP-06：不支持的接口类型付款关系应提示，不能默默宣称生成支持。</summary>
    [Fact]
    public void CMP06_InterfacePayment_ReportsUnsupportedRelationship()
    {
        var result = RunGenerator("""
            using Bing.Domain.ChangeTracking;
            using Bing.Domain.Entities;

            public interface IPayment : IChangeTrackable { }

            public partial class CardPayment : DomainObjectBase<CardPayment>, IPayment
            {
                public int Digits { get; set; }
            }

            public partial class Order : DomainObjectBase<Order>
            {
                public IPayment Payment { get; set; }
            }
            """);

        Assert.True(result.EmitSucceeded);
        var hook = GetGeneratedAddChangesMethod(result, "Order").ToFullString();
        if (hook.Contains("AddObjectChanges(nameof(Payment)"))
        {
            // 支持接口关系时必须实际经生成的父 hook 调用子对象，而不只检查生成文本。
            var assembly = Assembly.Load(result.AssemblyImage);
            var orderType = assembly.GetType("Order", throwOnError: true);
            var cardType = assembly.GetType("CardPayment", throwOnError: true);
            var before = Activator.CreateInstance(orderType);
            var after = Activator.CreateInstance(orderType);
            var oldCard = Activator.CreateInstance(cardType);
            var newCard = Activator.CreateInstance(cardType);
            cardType.GetProperty("Digits").SetValue(oldCard, 1);
            cardType.GetProperty("Digits").SetValue(newCard, 2);
            orderType.GetProperty("Payment").SetValue(before, oldCard);
            orderType.GetProperty("Payment").SetValue(after, newCard);
            var change = Assert.Single(((IChangeTrackable)before).GetChanges(after));
            Assert.Equal("Payment.Digits", change.PropertyName);
            Assert.Equal("Digits", change.Description);
            Assert.Equal("1", change.OldValue);
            Assert.Equal("2", change.NewValue);
            return;
        }
        var diagnostics = result.Diagnostics.Where(item => item.Id is "CG006" or "CG011").ToArray();
        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.True(diagnostic.Location.IsInSource);
            Assert.Contains("Order.Payment", diagnostic.GetMessage());
        });
    }
}
