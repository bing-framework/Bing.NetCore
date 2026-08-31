using System.ComponentModel.DataAnnotations;

namespace Bing.Validation;

/// <summary>
/// 提供基于 DataAnnotation 特性的对象验证功能。
/// </summary>
public static class DataAnnotationValidation
{
    /// <summary>
    /// 使用 DataAnnotation 特性验证指定对象的全部可验证属性。
    /// </summary>
    /// <param name="target">待验证的对象。</param>
    /// <returns>验证结果集合；验证通过时返回有效且不包含错误的集合。</returns>
    /// <exception cref="ArgumentNullException">验证目标为空时抛出。</exception>
    public static ValidationResultCollection Validate(object target)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        var result = new ValidationResultCollection();
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(target, null, null);
        var isValid = Validator.TryValidateObject(target, context, validationResults, true);
        if (!isValid)
            result.AddRange(validationResults);
        return result;
    }
}
