using System.Data.Common;
using System.Globalization;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;

namespace Bing.Data.Sql;

/// <summary>按实际列布局绑定生成的成员写入器；布局仅在当前结果集内使用。</summary>
internal sealed class SqlStaticResultReader<TResult>
{
    private readonly EntityMappingMetadata _mapping;
    private readonly IFieldValueConverterSelector _converters;
    private readonly DatabaseContext _context;

    internal SqlStaticResultReader(EntityMappingMetadata mapping, IFieldValueConverterSelector converters,
        DatabaseContext context)
    {
        _mapping = mapping;
        _converters = converters;
        _context = context;
        if (mapping != null && mapping.Model?.GeneratedFactory == null)
            throw new NotSupportedException($"结果类型 {typeof(TResult).FullName} 缺少生成构造工厂。");
    }

    internal Func<TResult> Bind(DbDataReader reader)
    {
        if (_mapping == null)
        {
            var scalarColumn = new ColumnMappingMetadata { ClrType = typeof(TResult), ColumnName = reader.GetName(0) };
            var passthrough = IsPassthrough(scalarColumn);
            return () =>
            {
                if (reader.IsDBNull(0)) return default;
                return (TResult)ReadValue(reader, 0, scalarColumn, typeof(TResult), passthrough);
            };
        }
        // 窄布局使用有界扫描，省去每个结果集的字典和 HashSet；宽布局保留线性建索引。
        // 索引只属于当前绑定，不缓存 Reader，也不把可变映射长期保存在全局缓存中。
        var smallColumns = _mapping.Columns.Count <= 8 ? _mapping.Columns.Values.ToArray() : null;
        var columns = smallColumns == null ? CreateColumnIndex(_mapping.Columns) : null;
        // 目标数受实际列数及映射成员数共同限制，避免 List 扩容后再次复制为数组。
        var bindings = new (int Ordinal, ColumnMappingMetadata Column, EntityPropertyMetadata Member, bool Passthrough)[Math.Min(reader.FieldCount, _mapping.Columns.Count)];
        var bindingCount = 0;
        var members = smallColumns == null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : null;
        var hasReadOnlyColumn = false;
        for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
        {
            var name = reader.GetName(ordinal);
            ColumnMappingMetadata column;
            if (smallColumns != null)
            {
                column = FindSmallColumn(smallColumns, name);
                if (column == null) continue;
            }
            else if (!columns.TryGetValue(name, out column)) continue;
            if (column == null)
                throw new InvalidOperationException($"结果列 {name} 对应多个目标成员或重复赋值。");
            var member = _mapping.Model.Properties[column.PropertyName];
            if (member.HasNonPublicSetter)
                throw new NotSupportedException($"结果成员 {typeof(TResult).FullName}.{member.PropertyName} 缺少生成写入器。");
            // 与兼容路径一致：真正的只读计算属性不参与结果赋值。
            if (!member.CanWrite) { hasReadOnlyColumn = true; continue; }
            if (members != null)
            {
                if (!members.Add(column.PropertyName))
                    throw new InvalidOperationException($"结果列 {name} 对应多个目标成员或重复赋值。");
            }
            else
            {
                for (var index = 0; index < bindingCount; index++)
                    if (string.Equals(bindings[index].Column.PropertyName, column.PropertyName, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException($"结果列 {name} 对应多个目标成员或重复赋值。");
            }
            if (member.GeneratedSetter == null)
                throw new NotSupportedException($"结果成员 {typeof(TResult).FullName}.{member.PropertyName} 缺少生成写入器。");
            bindings[bindingCount++] = (ordinal, column, member, IsPassthrough(column));
        }
        if (bindingCount == 0 && !hasReadOnlyColumn)
            throw new InvalidOperationException($"结果集没有可绑定到 {typeof(TResult).FullName} 的列。");
        var create = _mapping.Model.GeneratedFactory;
        return () =>
        {
            var instance = create();
            for (var index = 0; index < bindingCount; index++)
            {
                var binding = bindings[index];
                // 沿用 Dapper 默认 ApplyNullValues=false：缺列与 DBNull 均保留构造初始值。
                if (reader.IsDBNull(binding.Ordinal)) continue;
                // 新模型直接读取并写入属性，避免数值在 object 委托之间装箱。
                // 自定义转换器和不匹配的实际存储类型继续执行原有转换。
                if (binding.Passthrough && binding.Member.GeneratedReaderSetter?.Invoke(instance, reader, binding.Ordinal) == true)
                    continue;
                binding.Member.GeneratedSetter(instance, ReadValue(reader, binding.Ordinal, binding.Column,
                    binding.Member.ClrType, binding.Passthrough));
            }
            return (TResult)instance;
        };
    }

    /// <summary>宽布局索引只在绑定阶段使用，不由返回的逐行读取闭包持有。</summary>
    private static Dictionary<string, ColumnMappingMetadata> CreateColumnIndex(IReadOnlyDictionary<string, ColumnMappingMetadata> mappings)
    {
        var nameCount = mappings.Count;
        foreach (var column in mappings.Values)
            if (!string.Equals(column.ColumnName, column.PropertyName, StringComparison.OrdinalIgnoreCase)) nameCount++;
        var columns = new Dictionary<string, ColumnMappingMetadata>(nameCount, StringComparer.OrdinalIgnoreCase);
        foreach (var column in mappings.Values)
        {
            AddName(column.ColumnName, column);
            AddName(column.PropertyName, column);
        }
        return columns;

        void AddName(string name, ColumnMappingMetadata column)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (columns.TryGetValue(name, out var existing) && !ReferenceEquals(existing, column)) columns[name] = null;
            else if (!columns.ContainsKey(name)) columns.Add(name, column);
        }
    }

    /// <summary>保持物理列名、属性别名及大小写冲突的规则与宽布局索引一致。</summary>
    private static ColumnMappingMetadata FindSmallColumn(ColumnMappingMetadata[] columns, string name)
    {
        ColumnMappingMetadata match = null;
        foreach (var column in columns)
        {
            if (!(!string.IsNullOrEmpty(column.ColumnName) && string.Equals(column.ColumnName, name, StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(column.PropertyName) && string.Equals(column.PropertyName, name, StringComparison.OrdinalIgnoreCase))) continue;
            if (match != null && !ReferenceEquals(match, column))
                throw new InvalidOperationException($"结果列 {name} 对应多个目标成员或重复赋值。");
            match = column;
        }
        return match;
    }

    private bool IsPassthrough(ColumnMappingMetadata column) => _converters == null ||
        _converters is DefaultFieldValueConverterSelector selector && selector.UsesDefaultConverter &&
        column.ConverterKind == FieldValueConverterKind.None;

    /// <summary>紧凑绑定项共用读取逻辑，不为每一列创建捕获 Reader 的闭包。</summary>
    private object ReadValue(DbDataReader reader, int ordinal, ColumnMappingMetadata column, Type target, bool passthrough)
    {
        var type = Nullable.GetUnderlyingType(target) ?? target;
        if (passthrough)
        {
            // SQLite 等 Provider 的同一列可在不同行返回不同存储类型，必须逐行核对类型。
            // 只在语义相同的分支使用强类型 getter，其他值继续执行原有转换规则。
            if (type == typeof(int))
            {
                var fieldType = reader.GetFieldType(ordinal);
                if (fieldType == typeof(long)) return checked((int)reader.GetInt64(ordinal));
                if (fieldType == typeof(int)) return reader.GetInt32(ordinal);
                return (int)ConvertValue(reader.GetValue(ordinal), target);
            }
            if (type == typeof(long)) return reader.GetFieldType(ordinal) == typeof(long)
                ? reader.GetInt64(ordinal) : (long)ConvertValue(reader.GetValue(ordinal), target);
            if (type == typeof(string)) return reader.GetFieldType(ordinal) == typeof(string)
                ? reader.GetString(ordinal) : (string)ConvertValue(reader.GetValue(ordinal), target);
            if (type == typeof(double)) return reader.GetFieldType(ordinal) == typeof(double)
                ? reader.GetDouble(ordinal) : (double)ConvertValue(reader.GetValue(ordinal), target);
            if (type == typeof(short)) return reader.GetFieldType(ordinal) == typeof(short)
                ? reader.GetInt16(ordinal) : (short)ConvertValue(reader.GetValue(ordinal), target);
            if (type == typeof(byte)) return reader.GetFieldType(ordinal) == typeof(byte)
                ? reader.GetByte(ordinal) : (byte)ConvertValue(reader.GetValue(ordinal), target);
            if (type == typeof(char)) return reader.GetFieldType(ordinal) == typeof(char)
                ? reader.GetChar(ordinal) : (char)ConvertValue(reader.GetValue(ordinal), target);
            if (type == typeof(bool)) return reader.GetFieldType(ordinal) == typeof(bool)
                ? reader.GetBoolean(ordinal) : (bool)ConvertValue(reader.GetValue(ordinal), target);
            if (type == typeof(float)) return reader.GetFieldType(ordinal) == typeof(float)
                ? reader.GetFloat(ordinal) : (float)ConvertValue(reader.GetValue(ordinal), target);
            if (type == typeof(decimal)) return reader.GetFieldType(ordinal) == typeof(decimal)
                ? reader.GetDecimal(ordinal) : (decimal)ConvertValue(reader.GetValue(ordinal), target);
            if (type == typeof(DateTime)) return reader.GetFieldType(ordinal) == typeof(DateTime)
                ? reader.GetDateTime(ordinal) : (DateTime)ConvertValue(reader.GetValue(ordinal), target);
            if (type == typeof(Guid)) return reader.GetFieldType(ordinal) == typeof(Guid)
                ? reader.GetGuid(ordinal) : (Guid)ConvertValue(reader.GetValue(ordinal), target);
        }
        var raw = reader.GetValue(ordinal);
        var converted = passthrough ? raw : _converters.ConvertFromProvider(raw, column, _context);
        return ConvertValue(converted, target);
    }

    internal static bool IsScalar(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) ||
            type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset) ||
            type == typeof(TimeSpan) || type == typeof(byte[]);
    }

    internal static object ConvertValue(object value, Type target)
    {
        var nullable = Nullable.GetUnderlyingType(target);
        if (value == null || value == DBNull.Value)
        {
            if (!target.IsValueType || nullable != null) return null;
            throw new InvalidCastException($"数据库 NULL 无法写入非空类型 {target.FullName}。");
        }
        target = nullable ?? target;
        if (target.IsInstanceOfType(value)) return value;
        if (target.IsEnum) return value is string text ? Enum.Parse(target, text, true) : EnumValueConverter.ToEnumChecked(target, value);
        if (target == typeof(Guid)) return value is byte[] bytes ? new Guid(bytes) : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture));
        if (target == typeof(DateTimeOffset)) return DateTimeOffset.Parse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        if (target == typeof(TimeSpan)) return TimeSpan.Parse(Convert.ToString(value, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }
}
