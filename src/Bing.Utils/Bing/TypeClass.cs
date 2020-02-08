using System;

namespace Bing
{
    /// <summary>
    /// 类类型
    /// </summary>
    public static class TypeClass
    {
        /// <summary>
        /// object
        /// </summary>
        public static Type ObjectClass { get; } = typeof(object);

        /// <summary>
        /// int32
        /// </summary>
        public static Type Int32Class { get; } = typeof(int);

        /// <summary>
        /// int32?
        /// </summary>
        public static Type Int32NullableClass { get; } = typeof(int?);

        /// <summary>
        /// int64
        /// </summary>
        public static Type Int64Class { get; } = typeof(long);

        /// <summary>
        /// int64?
        /// </summary>
        public static Type Int64NullableClass { get; } = typeof(long?);

        /// <summary>
        /// int
        /// </summary>
        public static Type IntClass => Int32Class;

        /// <summary>
        /// int?
        /// </summary>
        public static Type IntNullableClass => Int32NullableClass;

        /// <summary>
        /// long
        /// </summary>
        public static Type LongClass => Int64Class;

        /// <summary>
        /// long?
        /// </summary>
        public static Type LongNullableClass => Int64NullableClass;

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
        /// TimeSpan
        /// </summary>
        public static Type TimeSpanClass { get; } = typeof(TimeSpan);

        /// <summary>
        /// TimeSpan?
        /// </summary>
        public static Type TimeSpanNullableClass { get; } = typeof(TimeSpan?);
    }
}
