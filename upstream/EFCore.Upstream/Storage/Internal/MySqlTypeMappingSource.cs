// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Pomelo.EntityFrameworkCore.MySql.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace Pomelo.EntityFrameworkCore.MySql.Storage.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class MySqlTypeMappingSource : RelationalTypeMappingSource
    {
        private readonly RelationalTypeMapping _sqlVariant
            = new MySqlSqlVariantTypeMapping("sql_variant");

        private readonly FloatTypeMapping _real
            = new MySqlFloatTypeMapping("real");

        private readonly ByteTypeMapping _byte
            = new MySqlByteTypeMapping("tinyint", DbType.Byte);

        private readonly ShortTypeMapping _short
            = new MySqlShortTypeMapping("smallint", DbType.Int16);

        private readonly LongTypeMapping _long
            = new MySqlLongTypeMapping("bigint", DbType.Int64);

        private readonly MySqlByteArrayTypeMapping _rowversion
            = new MySqlByteArrayTypeMapping(
                "rowversion",
                size: 8,
                comparer: new ValueComparer<byte[]>(
                    (v1, v2) => StructuralComparisons.StructuralEqualityComparer.Equals(v1, v2),
                    v => StructuralComparisons.StructuralEqualityComparer.GetHashCode(v),
                    v => v == null ? null : v.ToArray()),
                storeTypePostfix: StoreTypePostfix.None);

        private readonly IntTypeMapping _int
            = new IntTypeMapping("int", DbType.Int32);

        private readonly BoolTypeMapping _bool
            = new BoolTypeMapping("bit");

        private readonly MySqlStringTypeMapping _fixedLengthUnicodeString
            = new MySqlStringTypeMapping(unicode: true, fixedLength: true);

        private readonly MySqlStringTypeMapping _variableLengthUnicodeString
            = new MySqlStringTypeMapping(unicode: true);

        private readonly MySqlStringTypeMapping _variableLengthMaxUnicodeString
            = new MySqlStringTypeMapping("nvarchar(max)", unicode: true, storeTypePostfix: StoreTypePostfix.None);

        private readonly MySqlStringTypeMapping _fixedLengthAnsiString
            = new MySqlStringTypeMapping(fixedLength: true);

        private readonly MySqlStringTypeMapping _variableLengthAnsiString
            = new MySqlStringTypeMapping();

        private readonly MySqlStringTypeMapping _variableLengthMaxAnsiString
            = new MySqlStringTypeMapping("varchar(max)", storeTypePostfix: StoreTypePostfix.None);

        private readonly MySqlByteArrayTypeMapping _variableLengthBinary
            = new MySqlByteArrayTypeMapping();

        private readonly MySqlByteArrayTypeMapping _variableLengthMaxBinary
            = new MySqlByteArrayTypeMapping("varbinary(max)", storeTypePostfix: StoreTypePostfix.None);

        private readonly MySqlByteArrayTypeMapping _fixedLengthBinary
            = new MySqlByteArrayTypeMapping(fixedLength: true);

        private readonly MySqlDateTimeTypeMapping _date
            = new MySqlDateTimeTypeMapping("date", DbType.Date);

        private readonly MySqlDateTimeTypeMapping _datetime
            = new MySqlDateTimeTypeMapping("datetime", DbType.DateTime);

        private readonly MySqlDateTimeTypeMapping _datetime2
            = new MySqlDateTimeTypeMapping("datetime2", DbType.DateTime2);

        private readonly DoubleTypeMapping _double
            = new MySqlDoubleTypeMapping("float");

        private readonly MySqlDateTimeOffsetTypeMapping _datetimeoffset
            = new MySqlDateTimeOffsetTypeMapping("datetimeoffset");

        private readonly GuidTypeMapping _uniqueidentifier
            = new GuidTypeMapping("uniqueidentifier", DbType.Guid);

        private readonly DecimalTypeMapping _decimal
            = new MySqlDecimalTypeMapping("decimal(18, 2)", precision: 18, scale: 2, storeTypePostfix: StoreTypePostfix.PrecisionAndScale);

        private readonly DecimalTypeMapping _money
            = new MySqlDecimalTypeMapping("money");

        private readonly TimeSpanTypeMapping _time
            = new MySqlTimeSpanTypeMapping("time");

        private readonly MySqlStringTypeMapping _xml
            = new MySqlStringTypeMapping("xml", unicode: true, storeTypePostfix: StoreTypePostfix.None);

        private readonly Dictionary<Type, RelationalTypeMapping> _clrTypeMappings;

        private readonly Dictionary<string, RelationalTypeMapping> _storeTypeMappings;

        // These are disallowed only if specified without any kind of length specified in parenthesis.
        private readonly HashSet<string> _disallowedMappings
            = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "binary",
                "binary varying",
                "varbinary",
                "char",
                "character",
                "char varying",
                "character varying",
                "varchar",
                "national char",
                "national character",
                "nchar",
                "national char varying",
                "national character varying",
                "nvarchar"
            };

        private readonly IReadOnlyDictionary<string, Func<Type, RelationalTypeMapping>> _namedClrMappings
            = new Dictionary<string, Func<Type, RelationalTypeMapping>>(StringComparer.Ordinal)
            {
                {
                    "Microsoft.MySql.Types.SqlHierarchyId",
                    t => MySqlUdtTypeMapping.CreateSqlHierarchyIdMapping(t)
                },
                {
                    "Microsoft.MySql.Types.SqlGeography",
                    t => MySqlUdtTypeMapping.CreateSqlSpatialMapping(t, "geography")
                },
                {
                    "Microsoft.MySql.Types.SqlGeometry",
                    t => MySqlUdtTypeMapping.CreateSqlSpatialMapping(t, "geometry")
                }
            };

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public MySqlTypeMappingSource(
            [NotNull] TypeMappingSourceDependencies dependencies,
            [NotNull] RelationalTypeMappingSourceDependencies relationalDependencies)
            : base(dependencies, relationalDependencies)
        {
            _clrTypeMappings
                = new Dictionary<Type, RelationalTypeMapping>
                {
                    { typeof(int), _int },
                    { typeof(long), _long },
                    { typeof(DateTime), _datetime2 },
                    { typeof(Guid), _uniqueidentifier },
                    { typeof(bool), _bool },
                    { typeof(byte), _byte },
                    { typeof(double), _double },
                    { typeof(DateTimeOffset), _datetimeoffset },
                    { typeof(short), _short },
                    { typeof(float), _real },
                    { typeof(decimal), _decimal },
                    { typeof(TimeSpan), _time }
                };

            _storeTypeMappings
                = new Dictionary<string, RelationalTypeMapping>(StringComparer.OrdinalIgnoreCase)
                {
                    { "bigint", _long },
                    { "binary varying", _variableLengthBinary },
                    { "binary", _fixedLengthBinary },
                    { "bit", _bool },
                    { "char varying", _variableLengthAnsiString },
                    { "char", _fixedLengthAnsiString },
                    { "character varying", _variableLengthAnsiString },
                    { "character", _fixedLengthAnsiString },
                    { "date", _date },
                    { "datetime", _datetime },
                    { "datetime2", _datetime2 },
                    { "datetimeoffset", _datetimeoffset },
                    { "dec", _decimal },
                    { "decimal", _decimal },
                    { "double precision", _double },
                    { "float", _double },
                    { "image", _variableLengthBinary },
                    { "int", _int },
                    { "money", _money },
                    { "national char varying", _variableLengthUnicodeString },
                    { "national character varying", _variableLengthUnicodeString },
                    { "national character", _fixedLengthUnicodeString },
                    { "nchar", _fixedLengthUnicodeString },
                    { "ntext", _variableLengthUnicodeString },
                    { "numeric", _decimal },
                    { "nvarchar", _variableLengthUnicodeString },
                    { "nvarchar(max)", _variableLengthMaxUnicodeString },
                    { "real", _real },
                    { "rowversion", _rowversion },
                    { "smalldatetime", _datetime },
                    { "smallint", _short },
                    { "smallmoney", _money },
                    { "sql_variant", _sqlVariant },
                    { "text", _variableLengthAnsiString },
                    { "time", _time },
                    { "timestamp", _rowversion },
                    { "tinyint", _byte },
                    { "uniqueidentifier", _uniqueidentifier },
                    { "varbinary", _variableLengthBinary },
                    { "varbinary(max)", _variableLengthMaxBinary },
                    { "varchar", _variableLengthAnsiString },
                    { "varchar(max)", _variableLengthMaxAnsiString },
                    { "xml", _xml }
                };
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override void ValidateMapping(CoreTypeMapping mapping, IProperty property)
        {
            var relationalMapping = mapping as RelationalTypeMapping;

            if (_disallowedMappings.Contains(relationalMapping?.StoreType))
            {
                if (property == null)
                {
                    throw new ArgumentException(MySqlStrings.UnqualifiedDataType(relationalMapping.StoreType));
                }

                throw new ArgumentException(MySqlStrings.UnqualifiedDataTypeOnProperty(relationalMapping.StoreType, property.Name));
            }
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override RelationalTypeMapping FindMapping(in RelationalTypeMappingInfo mappingInfo)
            => FindRawMapping(mappingInfo)?.Clone(mappingInfo)
                ?? base.FindMapping(mappingInfo);

        private RelationalTypeMapping FindRawMapping(RelationalTypeMappingInfo mappingInfo)
        {
            var clrType = mappingInfo.ClrType;
            var storeTypeName = mappingInfo.StoreTypeName;
            var storeTypeNameBase = mappingInfo.StoreTypeNameBase;

            if (storeTypeName != null)
            {
                if (clrType == typeof(float)
                    && mappingInfo.Size != null
                    && mappingInfo.Size <= 24
                    && (storeTypeNameBase.Equals("float", StringComparison.OrdinalIgnoreCase)
                        || storeTypeNameBase.Equals("double precision", StringComparison.OrdinalIgnoreCase)))
                {
                    return _real;
                }

                if (_storeTypeMappings.TryGetValue(storeTypeName, out var mapping)
                    || _storeTypeMappings.TryGetValue(storeTypeNameBase, out mapping))
                {
                    return clrType == null
                           || mapping.ClrType == clrType
                        ? mapping
                        : null;
                }
            }

            if (clrType != null)
            {
                if (_clrTypeMappings.TryGetValue(clrType, out var mapping))
                {
                    return mapping;
                }

                if (_namedClrMappings.TryGetValue(clrType.FullName, out var mappingFunc))
                {
                    return mappingFunc(clrType);
                }

                if (clrType == typeof(string))
                {
                    var isAnsi = mappingInfo.IsUnicode == false;
                    var isFixedLength = mappingInfo.IsFixedLength == true;
                    var maxSize = isAnsi ? 8000 : 4000;

                    var size = mappingInfo.Size ?? (mappingInfo.IsKeyOrIndex ? (int?)(isAnsi ? 900 : 450) : null);
                    if (size > maxSize)
                    {
                        size = isFixedLength ? maxSize : (int?)null;
                    }

                    return size == null
                        ? isAnsi ? _variableLengthMaxAnsiString : _variableLengthMaxUnicodeString
                        : new MySqlStringTypeMapping(
                            unicode: !isAnsi,
                            size: size,
                            fixedLength: isFixedLength);
                }

                if (clrType == typeof(byte[]))
                {
                    if (mappingInfo.IsRowVersion == true)
                    {
                        return _rowversion;
                    }

                    var isFixedLength = mappingInfo.IsFixedLength == true;

                    var size = mappingInfo.Size ?? (mappingInfo.IsKeyOrIndex ? (int?)900 : null);
                    if (size > 8000)
                    {
                        size = isFixedLength ? 8000 : (int?)null;
                    }

                    return size == null
                        ? _variableLengthMaxBinary
                        : new MySqlByteArrayTypeMapping(size: size, fixedLength: isFixedLength);
                }
            }

            return null;
        }
    }
}