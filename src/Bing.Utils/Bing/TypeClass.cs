using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bing
{
    /// <summary>
    /// 类类型
    /// </summary>
    public static class TypeClass
    {
        /// <summary>
        /// void
        /// </summary>
        public static Type VoidClass { get; } = typeof(void);

        /// <summary>
        /// object
        /// </summary>
        public static Type ObjectClass { get; } = typeof(object);

        /// <summary>
        /// object[]
        /// </summary>
        public static Type ObjectArrayClass { get; } = typeof(object[]);

        /// <summary>
        /// byte
        /// </summary>
        public static Type ByteClass { get; } = typeof(byte);

        /// <summary>
        /// byte?
        /// </summary>
        public static Type ByteNullableClass { get; } = typeof(byte?);

        /// <summary>
        /// byte[]
        /// </summary>
        public static Type ByteArrayClass { get; } = typeof(byte[]);

        /// <summary>
        /// sbyte
        /// </summary>
        public static Type SByteClass { get; } = typeof(sbyte);

        /// <summary>
        /// sbyte?
        /// </summary>
        public static Type SByteNullableClass { get; } = typeof(sbyte?);

        /// <summary>
        /// int16
        /// </summary>
        public static Type Int16Class { get; } = typeof(short);

        /// <summary>
        /// int16?
        /// </summary>
        public static Type Int16NullableClass { get; } = typeof(short?);

        /// <summary>
        /// uint16
        /// </summary>
        public static Type UInt16Class { get; } = typeof(ushort);

        /// <summary>
        /// uint16?
        /// </summary>
        public static Type UInt16NullableClass { get; } = typeof(ushort?);

        /// <summary>
        /// int32
        /// </summary>
        public static Type Int32Class { get; } = typeof(int);

        /// <summary>
        /// int32?
        /// </summary>
        public static Type Int32NullableClass { get; } = typeof(int?);

        /// <summary>
        /// uint32
        /// </summary>
        public static Type UInt32Class { get; } = typeof(uint);

        /// <summary>
        /// uint32?
        /// </summary>
        public static Type UInt32NullableClass { get; } = typeof(uint?);

        /// <summary>
        /// int64
        /// </summary>
        public static Type Int64Class { get; } = typeof(long);

        /// <summary>
        /// int64?
        /// </summary>
        public static Type Int64NullableClass { get; } = typeof(long?);

        /// <summary>
        /// uint64
        /// </summary>
        public static Type UInt64Class { get; } = typeof(ulong);

        /// <summary>
        /// uint64?
        /// </summary>
        public static Type UInt64NullableClass { get; } = typeof(ulong?);

        /// <summary>
        /// short
        /// </summary>
        public static Type ShortClass => Int16Class;

        /// <summary>
        /// short?
        /// </summary>
        public static Type ShortNullableClass => Int16NullableClass;

        /// <summary>
        /// ushort
        /// </summary>
        public static Type UShortClass => UInt16Class;

        /// <summary>
        /// ushort?
        /// </summary>
        public static Type UShortNullableClass => UInt16NullableClass;

        /// <summary>
        /// int
        /// </summary>
        public static Type IntClass => Int32Class;

        /// <summary>
        /// int?
        /// </summary>
        public static Type IntNullableClass => Int32NullableClass;

        /// <summary>
        /// uint
        /// </summary>
        public static Type UIntClass => UInt32Class;

        /// <summary>
        /// uint?
        /// </summary>
        public static Type UIntNullableClass => UInt32NullableClass;

        /// <summary>
        /// long
        /// </summary>
        public static Type LongClass => Int64Class;

        /// <summary>
        /// long?
        /// </summary>
        public static Type LongNullableClass => Int64NullableClass;

        /// <summary>
        /// ulong
        /// </summary>
        public static Type ULongClass => UInt64Class;

        /// <summary>
        /// ulong?
        /// </summary>
        public static Type ULongNullableClass => UInt64NullableClass;

        /// <summary>
        /// float
        /// </summary>
        public static Type FloatClass { get; } = typeof(float);

        /// <summary>
        /// float?
        /// </summary>
        public static Type FloatNullableClass { get; } = typeof(float?);

        /// <summary>
        /// double
        /// </summary>
        public static Type DoubleClass { get; } = typeof(double);

        /// <summary>
        /// double?
        /// </summary>
        public static Type DoubleNullableClass { get; } = typeof(double?);

        /// <summary>
        /// decimal
        /// </summary>
        public static Type DecimalClass { get; } = typeof(decimal);

        /// <summary>
        /// decimal?
        /// </summary>
        public static Type DecimalNullableClass { get; } = typeof(decimal?);

        /// <summary>
        /// string
        /// </summary>
        public static Type StringClass { get; } = typeof(string);

        /// <summary>
        /// DateTime
        /// </summary>
        public static Type DateTimeClass { get; } = typeof(DateTime);

        /// <summary>
        /// DateTime?
        /// </summary>
        public static Type DateTimeNullableClass { get; } = typeof(DateTime?);

        /// <summary>
        /// DateTimeOffset
        /// </summary>
        public static Type DateTimeOffsetClass { get; } = typeof(DateTimeOffset);

        /// <summary>
        /// DateTimeOffset?
        /// </summary>
        public static Type DateTimeOffsetNullableClass { get; } = typeof(DateTimeOffset?);

        /// <summary>
        /// TimeSpan
        /// </summary>
        public static Type TimeSpanClass { get; } = typeof(TimeSpan);

        /// <summary>
        /// TimeSpan?
        /// </summary>
        public static Type TimeSpanNullableClass { get; } = typeof(TimeSpan?);

        /// <summary>
        /// Guid
        /// </summary>
        public static Type GuidClass { get; } = typeof(Guid);

        /// <summary>
        /// Guid?
        /// </summary>
        public static Type GuidNullableClass { get; } = typeof(Guid?);

        /// <summary>
        /// bool
        /// </summary>
        public static Type BooleanClass { get; } = typeof(bool);

        /// <summary>
        /// bool?
        /// </summary>
        public static Type BooleanNullableClass { get; } = typeof(bool?);

        /// <summary>
        /// char
        /// </summary>
        public static Type CharClass { get; } = typeof(char);

        /// <summary>
        /// char?
        /// </summary>
        public static Type CharNullableClass { get; } = typeof(char?);

        /// <summary>
        /// Enum
        /// </summary>
        public static Type EnumClass { get; } = typeof(Enum);

        /// <summary>
        /// ValueTuple
        /// </summary>
        public static Type ValueTupleClass { get; } = typeof(ValueTuple);

        /// <summary>
        /// Task
        /// </summary>
        public static Type TaskClass { get; } = typeof(Task);

        /// <summary>
        /// Generic Task
        /// </summary>
        public static Type GenericTaskClass { get; } = typeof(Task<>);

        /// <summary>
        /// Generic List
        /// </summary>
        public static Type GenericListClass { get; } = typeof(List<>);
    }
}
