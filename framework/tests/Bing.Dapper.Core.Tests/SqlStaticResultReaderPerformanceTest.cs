using System.Collections;
using System.Data;
using System.Data.Common;
using Bing.Data.Sql;
using Bing.Data.Sql.Builders.Params;
using Bing.Data.Sql.Metadata;
using Xunit;

namespace Bing.Dapper.Core.Tests;

/// <summary>读取优化必须减少重复工作，同时保留空值、转换和结果布局语义。</summary>
public sealed class SqlStaticResultReaderPerformanceTest
{
    [Fact]
    public void Bind_GeneratedTypedAssignment_ShouldBypassObjectSetterAndKeepFallback()
    {
        var objectWrites = 0;
        var typedWrites = 0;
        var member = EntityPropertyMetadata.CreateGenerated("Id", typeof(int), true, true,
            generatedSetter: (row, value) => { objectWrites++; ((Row)row).Id = (int)value; })
            .WithGeneratedReader((row, reader, ordinal) =>
            {
                typedWrites++;
                if (reader.GetFieldType(ordinal) != typeof(long)) return false;
                ((Row)row).Id = checked((int)reader.GetInt64(ordinal)); return true;
            });
        var model = new EntityModelMetadata(typeof(Row), "rows", null, new[] { member }, () => new Row());
        var mapping = new DefaultEntityMappingResolver(new RegisteredEntityModelMetadataProvider(model)).Resolve(typeof(Row), new DatabaseContext());
        var mapper = new SqlStaticResultReader<Row>(mapping, null, null);
        using var numbers = new DataTable(); numbers.Columns.Add("Id", typeof(long)); numbers.Rows.Add(42L);
        using var numericReader = numbers.CreateDataReader(); var read = mapper.Bind(numericReader);
        Assert.True(numericReader.Read()); Assert.Equal(42, read().Id);
        Assert.Equal(1, typedWrites); Assert.Equal(0, objectWrites);
        using var texts = new DataTable(); texts.Columns.Add("Id", typeof(string)); texts.Rows.Add("43");
        using var textReader = texts.CreateDataReader(); var fallback = mapper.Bind(textReader);
        Assert.True(textReader.Read()); Assert.Equal(43, fallback().Id);
        Assert.Equal(2, typedWrites); Assert.Equal(1, objectWrites);
    }

    [Fact]
    public void Bind_CustomConverter_ShouldBypassTypedAssignment()
    {
        var member = EntityPropertyMetadata.CreateGenerated("Id", typeof(int), true, true,
            generatedSetter: (row, value) => ((Row)row).Id = (int)value)
            .WithGeneratedReader((_, _, _) => throw new InvalidOperationException("converter bypassed"));
        var model = new EntityModelMetadata(typeof(Row), "rows", null, new[] { member }, () => new Row());
        var mapping = new DefaultEntityMappingResolver(new RegisteredEntityModelMetadataProvider(model)).Resolve(typeof(Row), new DatabaseContext());
        using var table = new DataTable(); table.Columns.Add("Id", typeof(long)); table.Rows.Add(1L);
        using var reader = table.CreateDataReader();
        var read = new SqlStaticResultReader<Row>(mapping, new DefaultFieldValueConverterSelector(new[] { new StatefulConverter() }), new DatabaseContext()).Bind(reader);
        Assert.True(reader.Read()); Assert.Equal(11, read().Id);
    }

    [Fact]
    public void Bind_DefaultNumericAndStringColumns_ShouldAvoidBoxingProviderValues()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(long));
        table.Columns.Add("Name", typeof(string));
        table.Rows.Add(42L, "one");
        table.Rows.Add(43L, DBNull.Value);
        using var reader = new CountingReader(table.CreateDataReader());
        var read = new SqlStaticResultReader<Row>(Mapping(), new DefaultFieldValueConverterSelector(), new DatabaseContext()).Bind(reader);
        Assert.True(reader.Read());
        var first = read();
        Assert.Equal(42, first.Id);
        Assert.Equal("one", first.Name);
        Assert.True(reader.Read());
        var second = read();
        Assert.Equal(43, second.Id);
        Assert.Equal("initial", second.Name);
        Assert.Equal(0, reader.BoxedReads);
    }

    [Fact]
    public void Bind_DefaultScalarTypes_ShouldUseTypedProviderGetters()
    {
        var decimalResult = ReadScalar<decimal>(typeof(decimal), 12.5m);
        var dateResult = ReadScalar<DateTime>(typeof(DateTime), new DateTime(2026, 11, 12, 8, 30, 0,
            DateTimeKind.Utc));
        var guid = Guid.Parse("7d9b6f7a-2d38-4e56-8e18-4b8fd5a8e3d1");
        var guidResult = ReadScalar<Guid>(typeof(Guid), guid);
        var boolResult = ReadScalar<bool>(typeof(bool), true);
        var floatResult = ReadScalar<float>(typeof(float), 1.25f);
        var shortResult = ReadScalar<short>(typeof(short), (short)12);

        Assert.Equal(12.5m, decimalResult.Value);
        Assert.Equal(new DateTime(2026, 11, 12, 8, 30, 0, DateTimeKind.Utc), dateResult.Value);
        Assert.Equal(guid, guidResult.Value);
        Assert.True(boolResult.Value);
        Assert.Equal(1.25f, floatResult.Value);
        Assert.Equal((short)12, shortResult.Value);
        Assert.Equal(0, decimalResult.BoxedReads + dateResult.BoxedReads + guidResult.BoxedReads +
            boolResult.BoxedReads + floatResult.BoxedReads + shortResult.BoxedReads);

        static (T Value, int BoxedReads) ReadScalar<T>(Type providerType, object value)
        {
            var table = new DataTable();
            table.Columns.Add("value", providerType);
            table.Rows.Add(value);
            using var reader = new CountingReader(table.CreateDataReader());
            var read = new SqlStaticResultReader<T>(null, new DefaultFieldValueConverterSelector(),
                new DatabaseContext()).Bind(reader);
            Assert.True(reader.Read());
            return (read(), reader.BoxedReads);
        }
    }

    [Fact]
    public void Bind_DefaultIntegerAndByteScalars_ShouldUseTypedProviderGetters()
    {
        var intResult = ReadScalar<int>(typeof(int), 42);
        var longStorageIntResult = ReadScalar<int>(typeof(long), 42L);
        var byteResult = ReadScalar<byte>(typeof(byte), (byte)7);

        Assert.Equal(42, intResult.Value);
        Assert.Equal(42, longStorageIntResult.Value);
        Assert.Equal((byte)7, byteResult.Value);
        Assert.Equal(0, intResult.BoxedReads + longStorageIntResult.BoxedReads + byteResult.BoxedReads);

        static (T Value, int BoxedReads) ReadScalar<T>(Type providerType, object value)
        {
            var table = new DataTable();
            table.Columns.Add("value", providerType);
            table.Rows.Add(value);
            using var reader = new CountingReader(table.CreateDataReader());
            var read = new SqlStaticResultReader<T>(null, new DefaultFieldValueConverterSelector(),
                new DatabaseContext()).Bind(reader);
            Assert.True(reader.Read());
            return (read(), reader.BoxedReads);
        }
    }

    [Fact]
    public void Bind_DefaultCharScalar_ShouldUseTypedProviderGetter()
    {
        var table = new DataTable();
        table.Columns.Add("value", typeof(char));
        table.Rows.Add('Z');
        using var reader = new CountingReader(table.CreateDataReader());
        var read = new SqlStaticResultReader<char>(null, new DefaultFieldValueConverterSelector(),
            new DatabaseContext()).Bind(reader);

        Assert.True(reader.Read());
        Assert.Equal('Z', read());
        Assert.Equal(0, reader.BoxedReads);
    }

    [Fact]
    public void Bind_CustomConverterSelection_ShouldKeepPerRowBehavior()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(long));
        table.Rows.Add(1L);
        table.Rows.Add(2L);
        using var reader = table.CreateDataReader();
        var converter = new StatefulConverter();
        var read = new SqlStaticResultReader<Row>(Mapping(),
            new DefaultFieldValueConverterSelector(new[] { converter }), new DatabaseContext()).Bind(reader);
        Assert.True(reader.Read());
        Assert.Equal(11, read().Id);
        Assert.True(reader.Read());
        Assert.Equal(22, read().Id);
        Assert.Equal(2, converter.Selections);
    }

    private static EntityMappingMetadata Mapping()
    {
        var model = new EntityModelMetadata(typeof(Row), "rows", null, new[]
        {
            EntityPropertyMetadata.CreateGenerated("Id", typeof(int), true, true,
                generatedSetter: (row, value) => ((Row)row).Id = (int)value),
            EntityPropertyMetadata.CreateGenerated("Name", typeof(string), true, true,
                generatedSetter: (row, value) => ((Row)row).Name = (string)value)
        }, () => new Row());
        return new DefaultEntityMappingResolver(new RegisteredEntityModelMetadataProvider(model)).Resolve(typeof(Row), new DatabaseContext());
    }

    public sealed class Row { public int Id { get; set; } public string Name { get; set; } = "initial"; }
    private sealed class StatefulConverter : IFieldValueConverter
    {
        public int Selections { get; private set; }
        public bool CanConvert(ColumnMappingMetadata column, DatabaseContext context) { Selections++; return true; }
        public object ConvertFromProvider(object value, ColumnMappingMetadata column, DatabaseContext context) => Convert.ToInt32(value) + 10 * Selections;
        public object ConvertToProvider(object value, ColumnMappingMetadata column, DatabaseContext context) => value;
    }

    private sealed class CountingReader(DbDataReader inner) : DbDataReader
    {
        public int BoxedReads { get; private set; }
        public override object GetValue(int ordinal) { BoxedReads++; return inner.GetValue(ordinal); }
        public override int FieldCount => inner.FieldCount;
        public override bool Read() => inner.Read();
        public override bool NextResult() => inner.NextResult();
        public override string GetName(int ordinal) => inner.GetName(ordinal);
        public override Type GetFieldType(int ordinal) => inner.GetFieldType(ordinal);
        public override bool IsDBNull(int ordinal) => inner.IsDBNull(ordinal);
        public override long GetInt64(int ordinal) => inner.GetInt64(ordinal);
        public override int GetInt32(int ordinal) => inner.GetInt32(ordinal);
        public override short GetInt16(int ordinal) => inner.GetInt16(ordinal);
        public override string GetString(int ordinal) => inner.GetString(ordinal);
        public override bool GetBoolean(int ordinal) => inner.GetBoolean(ordinal);
        public override byte GetByte(int ordinal) => inner.GetByte(ordinal);
        public override char GetChar(int ordinal) => inner.GetChar(ordinal);
        public override DateTime GetDateTime(int ordinal) => inner.GetDateTime(ordinal);
        public override decimal GetDecimal(int ordinal) => inner.GetDecimal(ordinal);
        public override double GetDouble(int ordinal) => inner.GetDouble(ordinal);
        public override float GetFloat(int ordinal) => inner.GetFloat(ordinal);
        public override Guid GetGuid(int ordinal) => inner.GetGuid(ordinal);
        public override int GetOrdinal(string name) => inner.GetOrdinal(name);
        public override string GetDataTypeName(int ordinal) => inner.GetDataTypeName(ordinal);
        public override int GetValues(object[] values) => inner.GetValues(values);
        public override long GetBytes(int ordinal, long offset, byte[] buffer, int bufferOffset, int length) => inner.GetBytes(ordinal, offset, buffer, bufferOffset, length);
        public override long GetChars(int ordinal, long offset, char[] buffer, int bufferOffset, int length) => inner.GetChars(ordinal, offset, buffer, bufferOffset, length);
        public override IEnumerator GetEnumerator() => ((IEnumerable)inner).GetEnumerator();
        public override object this[int ordinal] => inner[ordinal];
        public override object this[string name] => inner[name];
        public override int Depth => inner.Depth;
        public override bool HasRows => inner.HasRows;
        public override bool IsClosed => inner.IsClosed;
        public override int RecordsAffected => inner.RecordsAffected;
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
}
