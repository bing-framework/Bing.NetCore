namespace Bing.Domain.Entities.Events;

/// <summary>
/// 表示领域事件测试中的员工。
/// </summary>
internal class Employee : AggregateRoot<Employee, int>
{
    /// <summary>
    /// 获取或设置员工名。
    /// </summary>
    public string FirstName { get; private set; }

    /// <summary>
    /// 获取或设置员工姓氏。
    /// </summary>
    public string LastName { get; private set; }

    /// <summary>
    /// 获取或设置员工编号。
    /// </summary>
    public string Number { get; private set; }

    /// <summary>
    /// 初始化一个 <see cref="Employee"/> 类型的实例。
    /// </summary>
    /// <param name="firstName">员工名。</param>
    /// <param name="lastName">员工姓氏。</param>
    /// <param name="number">员工编号。</param>
    public Employee(
        string firstName,
        string lastName,
        string number)
    {
        FirstName = firstName;
        LastName = lastName;
        Number = number;
    }
}
