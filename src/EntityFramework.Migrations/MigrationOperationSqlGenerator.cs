// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Migrations.Model;
using Microsoft.Data.Entity.Migrations.Utilities;
using Microsoft.Data.Entity.Relational;
using Microsoft.Data.Entity.Relational.Metadata;
using Microsoft.Data.Entity.Relational.Model;
using Microsoft.Data.Entity.Utilities;

namespace Microsoft.Data.Entity.Migrations
{
    /// <summary>
    ///     Default migration operation SQL generator, outputs best-effort ANSI-99 compliant SQL.
    /// </summary>
    // TODO: For simplicity we could rename the "abcOperation" parameters to "operation".
    public abstract class MigrationOperationSqlGenerator
    {
        // TODO: Check whether the following formats ar SqlServer specific or not and move
        // to SqlServer provider if they are.
        internal const string DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffK";
        internal const string DateTimeOffsetFormat = "yyyy-MM-ddTHH:mm:ss.fffzzz";

        private readonly IRelationalMetadataExtensionProvider _extensionProvider;
        private readonly RelationalTypeMapper _typeMapper;
        private IModel _targetModel;

        protected MigrationOperationSqlGenerator(
            [NotNull] IRelationalMetadataExtensionProvider extensionProvider,
            [NotNull] RelationalTypeMapper typeMapper)
        {
            Check.NotNull(extensionProvider, "extensionProvider");
            Check.NotNull(typeMapper, "typeMapper");

            _extensionProvider = extensionProvider;
            _typeMapper = typeMapper;
        }

        public virtual IRelationalMetadataExtensionProvider ExtensionProvider
        {
            get { return _extensionProvider; }
        }

        protected virtual RelationalNameBuilder NameBuilder
        {
            get { return ExtensionProvider.NameBuilder; }
        }

        public virtual RelationalTypeMapper TypeMapper
        {
            get { return _typeMapper; }
        }

        public virtual IModel TargetModel
        {
            get { return _targetModel; }
            [param: NotNull] set { _targetModel = Check.NotNull(value, "value"); }
        }

        public virtual IEnumerable<SqlStatement> Generate([NotNull] IEnumerable<MigrationOperation> migrationOperations)
        {
            Check.NotNull(migrationOperations, "migrationOperations");

            return migrationOperations.Select(Generate);
        }

        public virtual SqlStatement Generate([NotNull] MigrationOperation operation)
        {
            Check.NotNull(operation, "operation");

            var builder = new IndentedStringBuilder();

            operation.GenerateSql(this, builder);

            var sqlOperation = operation as SqlOperation;
            var suppressTransaction = sqlOperation != null && sqlOperation.SuppressTransaction;

            // TODO: Should we support implementations of Generate that output more than one SQL statement?
            return new SqlStatement(builder.ToString()) { SuppressTransaction = suppressTransaction };
        }

        public virtual void Generate([NotNull] CreateDatabaseOperation createDatabaseOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(createDatabaseOperation, "createDatabaseOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("CREATE DATABASE ")
                .Append(DelimitIdentifier(createDatabaseOperation.DatabaseName));
        }

        public virtual void Generate([NotNull] DropDatabaseOperation dropDatabaseOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(dropDatabaseOperation, "dropDatabaseOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("DROP DATABASE ")
                .Append(DelimitIdentifier(dropDatabaseOperation.DatabaseName));
        }

        public virtual void Generate([NotNull] CreateSequenceOperation createSequenceOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(createSequenceOperation, "createSequenceOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            var dataType = _typeMapper.GetTypeMapping(
                null, createSequenceOperation.SequenceName, createSequenceOperation.Type, 
                isKey: false, isConcurrencyToken: false).StoreTypeName;

            stringBuilder
                .Append("CREATE SEQUENCE ")
                .Append(DelimitIdentifier(createSequenceOperation.SequenceName))
                .Append(" AS ")
                .Append(dataType)
                .Append(" START WITH ")
                .Append(createSequenceOperation.StartValue)
                .Append(" INCREMENT BY ")
                .Append(createSequenceOperation.IncrementBy);
        }

        public virtual void Generate([NotNull] DropSequenceOperation dropSequenceOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(dropSequenceOperation, "dropSequenceOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("DROP SEQUENCE ")
                .Append(DelimitIdentifier(dropSequenceOperation.SequenceName));
        }

        public abstract void Generate([NotNull] RenameSequenceOperation renameSequenceOperation, [NotNull] IndentedStringBuilder stringBuilder);

        public abstract void Generate([NotNull] MoveSequenceOperation moveSequenceOperation, [NotNull] IndentedStringBuilder stringBuilder);

        public virtual void Generate([NotNull] AlterSequenceOperation alterSequenceOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(alterSequenceOperation, "alterSequenceOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("ALTER SEQUENCE ")
                .Append(DelimitIdentifier(alterSequenceOperation.SequenceName))
                .Append(" INCREMENT BY ")
                .Append(alterSequenceOperation.NewIncrementBy);
        }

        public virtual void Generate([NotNull] CreateTableOperation createTableOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(createTableOperation, "createTableOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("CREATE TABLE ")
                .Append(DelimitIdentifier(createTableOperation.TableName))
                .AppendLine(" (");

            using (stringBuilder.Indent())
            {
                GenerateColumns(createTableOperation, stringBuilder);

                GenerateTableConstraints(createTableOperation, stringBuilder);
            }

            stringBuilder
                .AppendLine()
                .Append(")");
        }

        protected virtual void GenerateTableConstraints([NotNull] CreateTableOperation createTableOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(createTableOperation, "createTableOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            var addPrimaryKeyOperation = createTableOperation.PrimaryKey;

            if (addPrimaryKeyOperation != null)
            {
                stringBuilder.AppendLine(",");

                GeneratePrimaryKey(addPrimaryKeyOperation, stringBuilder);
            }

            foreach (var addUniqueConstraintOperation in createTableOperation.UniqueConstraints)
            {
                stringBuilder.AppendLine(",");

                GenerateUniqueConstraint(addUniqueConstraintOperation, stringBuilder);
            }
        }

        public virtual void Generate([NotNull] DropTableOperation dropTableOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(dropTableOperation, "dropTableOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("DROP TABLE ")
                .Append(DelimitIdentifier(dropTableOperation.TableName));
        }

        public abstract void Generate([NotNull] RenameTableOperation renameTableOperation, [NotNull] IndentedStringBuilder stringBuilder);

        public abstract void Generate([NotNull] MoveTableOperation moveTableOperation, [NotNull] IndentedStringBuilder stringBuilder);

        public virtual void Generate([NotNull] AddColumnOperation addColumnOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(addColumnOperation, "addColumnOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("ALTER TABLE ")
                .Append(DelimitIdentifier(addColumnOperation.TableName))
                .Append(" ADD ");

            GenerateColumn(addColumnOperation.TableName, addColumnOperation.Column, stringBuilder);
        }

        public virtual void Generate([NotNull] DropColumnOperation dropColumnOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(dropColumnOperation, "dropColumnOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("ALTER TABLE ")
                .Append(DelimitIdentifier(dropColumnOperation.TableName))
                .Append(" DROP COLUMN ")
                .Append(DelimitIdentifier(dropColumnOperation.ColumnName));
        }

        public virtual void Generate([NotNull] AlterColumnOperation alterColumnOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(alterColumnOperation, "alterColumnOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            var newColumn = alterColumnOperation.NewColumn;

            stringBuilder
                .Append("ALTER TABLE ")
                .Append(DelimitIdentifier(alterColumnOperation.TableName))
                .Append(" ALTER COLUMN ")
                .Append(DelimitIdentifier(newColumn.Name))
                .Append(" ")
                .Append(GenerateDataType(alterColumnOperation.TableName, newColumn))
                .Append(newColumn.IsNullable ? " NULL" : " NOT NULL");
        }

        public virtual void Generate([NotNull] AddDefaultConstraintOperation addDefaultConstraintOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(addDefaultConstraintOperation, "addDefaultConstraintOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("ALTER TABLE ")
                .Append(DelimitIdentifier(addDefaultConstraintOperation.TableName))
                .Append(" ALTER COLUMN ")
                .Append(DelimitIdentifier(addDefaultConstraintOperation.ColumnName))
                .Append(" SET DEFAULT ");

            stringBuilder.Append(addDefaultConstraintOperation.DefaultSql ?? GenerateLiteral((dynamic)addDefaultConstraintOperation.DefaultValue));
        }

        public virtual void Generate([NotNull] DropDefaultConstraintOperation dropDefaultConstraintOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(dropDefaultConstraintOperation, "dropDefaultConstraintOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("ALTER TABLE ")
                .Append(DelimitIdentifier(dropDefaultConstraintOperation.TableName))
                .Append(" ALTER COLUMN ")
                .Append(DelimitIdentifier(dropDefaultConstraintOperation.ColumnName))
                .Append(" DROP DEFAULT");
        }

        public abstract void Generate([NotNull] RenameColumnOperation renameColumnOperation, [NotNull] IndentedStringBuilder stringBuilder);

        public virtual void Generate([NotNull] AddPrimaryKeyOperation addPrimaryKeyOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(addPrimaryKeyOperation, "addPrimaryKeyOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("ALTER TABLE ")
                .Append(DelimitIdentifier(addPrimaryKeyOperation.TableName))
                .Append(" ADD ");

            GeneratePrimaryKey(addPrimaryKeyOperation, stringBuilder);
        }

        public virtual void Generate([NotNull] DropPrimaryKeyOperation dropPrimaryKeyOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(dropPrimaryKeyOperation, "dropPrimaryKeyOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("ALTER TABLE ")
                .Append(DelimitIdentifier(dropPrimaryKeyOperation.TableName))
                .Append(" DROP CONSTRAINT ")
                .Append(DelimitIdentifier(dropPrimaryKeyOperation.PrimaryKeyName));
        }

        public virtual void Generate([NotNull] AddUniqueConstraintOperation addUniqueConstraintOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(addUniqueConstraintOperation, "addUniqueConstraintOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("ALTER TABLE ")
                .Append(DelimitIdentifier(addUniqueConstraintOperation.TableName))
                .Append(" ADD ");

            GenerateUniqueConstraint(addUniqueConstraintOperation, stringBuilder);
        }

        public virtual void Generate([NotNull] DropUniqueConstraintOperation dropUniqueConstraintOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(dropUniqueConstraintOperation, "dropUniqueConstraintOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("ALTER TABLE ")
                .Append(DelimitIdentifier(dropUniqueConstraintOperation.TableName))
                .Append(" DROP CONSTRAINT ")
                .Append(DelimitIdentifier(dropUniqueConstraintOperation.UniqueConstraintName));
        }

        public virtual void Generate([NotNull] AddForeignKeyOperation addForeignKeyOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(addForeignKeyOperation, "addForeignKeyOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("ALTER TABLE ")
                .Append(DelimitIdentifier(addForeignKeyOperation.TableName))
                .Append(" ADD ");

            GenerateForeignKey(addForeignKeyOperation, stringBuilder);
        }

        public virtual void Generate([NotNull] DropForeignKeyOperation dropForeignKeyOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(dropForeignKeyOperation, "dropForeignKeyOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("ALTER TABLE ")
                .Append(DelimitIdentifier(dropForeignKeyOperation.TableName))
                .Append(" DROP CONSTRAINT ")
                .Append(DelimitIdentifier(dropForeignKeyOperation.ForeignKeyName));
        }

        public virtual void Generate([NotNull] CreateIndexOperation createIndexOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(createIndexOperation, "createIndexOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder.Append("CREATE");

            if (createIndexOperation.IsUnique)
            {
                stringBuilder.Append(" UNIQUE");
            }

            // TODO: Move to SqlServer
            if (createIndexOperation.IsClustered)
            {
                stringBuilder.Append(" CLUSTERED");
            }

            stringBuilder
                .Append(" INDEX ")
                .Append(DelimitIdentifier(createIndexOperation.IndexName))
                .Append(" ON ")
                .Append(DelimitIdentifier(createIndexOperation.TableName))
                .Append(" (")
                .Append(createIndexOperation.ColumnNames.Select(DelimitIdentifier).Join())
                .Append(")");
        }

        public virtual void Generate([NotNull] DropIndexOperation dropIndexOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(dropIndexOperation, "dropIndexOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("DROP INDEX ")
                .Append(DelimitIdentifier(dropIndexOperation.IndexName));
        }

        public abstract void Generate([NotNull] RenameIndexOperation renameIndexOperation, [NotNull] IndentedStringBuilder stringBuilder);

        public virtual void Generate([NotNull] CopyDataOperation copyDataOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(copyDataOperation, "copyDataOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("INSERT INTO ")
                .Append(DelimitIdentifier(copyDataOperation.TargetTableName))
                .Append(" ( ")
                .Append(copyDataOperation.TargetColumnNames.Select(DelimitIdentifier).Join())
                .AppendLine(" )");

            using (stringBuilder.Indent())
            {
                stringBuilder
                    .Append("SELECT ")
                    .Append(copyDataOperation.SourceColumnNames.Select(DelimitIdentifier).Join())
                    .Append(" FROM ")
                    .Append(DelimitIdentifier(copyDataOperation.SourceTableName));
            }
        }

        public virtual void Generate([NotNull] SqlOperation sqlOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(sqlOperation, "sqlOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder.Append(sqlOperation.Sql);
        }

        public virtual string GenerateDataType(SchemaQualifiedName tableName, [NotNull] Column column)
        {
            Check.NotNull(column, "column");

            if (!string.IsNullOrEmpty(column.DataType))
            {
                return column.DataType;
            }

            var entityType = TargetModel.EntityTypes.Single(t => NameBuilder.SchemaQualifiedTableName(t) == tableName);
            var property = entityType.Properties.Single(p => NameBuilder.ColumnName(p) == column.Name);
            var isKey = property.IsKey() || property.IsForeignKey();

            return _typeMapper.GetTypeMapping(column.DataType, column.Name, column.ClrType, isKey, column.IsTimestamp).StoreTypeName;
        }

        public virtual string GenerateLiteral([NotNull] object value)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}", value);
        }

        public virtual string GenerateLiteral(bool value)
        {
            return value ? "1" : "0";
        }

        public virtual string GenerateLiteral([NotNull] string value)
        {
            Check.NotNull(value, "value");

            return "'" + value + "'";
        }

        public virtual string GenerateLiteral(Guid value)
        {
            return "'" + value + "'";
        }

        public virtual string GenerateLiteral(DateTime value)
        {
            return "'" + value.ToString(DateTimeFormat, CultureInfo.InvariantCulture) + "'";
        }

        public virtual string GenerateLiteral(DateTimeOffset value)
        {
            return "'" + value.ToString(DateTimeOffsetFormat, CultureInfo.InvariantCulture) + "'";
        }

        public virtual string GenerateLiteral(TimeSpan value)
        {
            return "'" + value + "'";
        }

        public virtual string GenerateLiteral([NotNull] byte[] value)
        {
            Check.NotNull(value, "value");

            var stringBuilder = new StringBuilder("0x");

            foreach (var @byte in value)
            {
                stringBuilder.Append(@byte.ToString("X2", CultureInfo.InvariantCulture));
            }

            return stringBuilder.ToString();
        }

        public virtual string DelimitIdentifier(SchemaQualifiedName schemaQualifiedName)
        {
            return
                (schemaQualifiedName.IsSchemaQualified
                    ? DelimitIdentifier(schemaQualifiedName.Schema) + "."
                    : string.Empty)
                + DelimitIdentifier(schemaQualifiedName.Name);
        }

        public virtual string DelimitIdentifier([NotNull] string identifier)
        {
            Check.NotEmpty(identifier, "identifier");

            return "\"" + EscapeIdentifier(identifier) + "\"";
        }

        public virtual string EscapeIdentifier([NotNull] string identifier)
        {
            Check.NotEmpty(identifier, "identifier");

            return identifier.Replace("\"", "\"\"");
        }

        public virtual string DelimitLiteral([NotNull] string literal)
        {
            Check.NotNull(literal, "literal");

            return "'" + EscapeLiteral(literal) + "'";
        }

        public virtual string EscapeLiteral([NotNull] string literal)
        {
            Check.NotNull(literal, "literal");

            return literal.Replace("'", "''");
        }

        protected virtual void GenerateColumns(
            [NotNull] CreateTableOperation createTableOperation, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(createTableOperation, "createTableOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            var columns = createTableOperation.Columns;
            if (columns.Count == 0)
            {
                return;
            }

            GenerateColumn(createTableOperation.TableName, columns[0], stringBuilder);

            for (var i = 1; i < columns.Count; i++)
            {
                stringBuilder.AppendLine(",");

                GenerateColumn(createTableOperation.TableName, columns[i], stringBuilder);
            }
        }

        protected virtual void GenerateColumn(
            SchemaQualifiedName tableName, [NotNull] Column column, [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(column, "column");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append(DelimitIdentifier(column.Name))
                .Append(" ");

            stringBuilder.Append(GenerateDataType(tableName, column));

            if (!column.IsNullable)
            {
                stringBuilder.Append(" NOT NULL");
            }

            GenerateColumnTraits(tableName, column, stringBuilder);

            if (column.DefaultSql != null)
            {
                stringBuilder
                    .Append(" DEFAULT ")
                    .Append(column.DefaultSql);
            }
            else if (column.DefaultValue != null)
            {
                stringBuilder
                    .Append(" DEFAULT ")
                    .Append(GenerateLiteral((dynamic)column.DefaultValue));
            }
        }

        protected virtual void GenerateColumnTraits(SchemaQualifiedName tableName, 
            [NotNull] Column column, [NotNull] IndentedStringBuilder stringBuilder)
        {
        }

        protected virtual void GeneratePrimaryKey(
            [NotNull] AddPrimaryKeyOperation primaryKeyOperation,
            [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(primaryKeyOperation, "primaryKeyOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("CONSTRAINT ")
                .Append(DelimitIdentifier(primaryKeyOperation.PrimaryKeyName))
                .Append(" PRIMARY KEY");

            GeneratePrimaryKeyTraits(primaryKeyOperation, stringBuilder);

            stringBuilder
                .Append(" (")
                .Append(primaryKeyOperation.ColumnNames.Select(DelimitIdentifier).Join())
                .Append(")");
        }

        protected virtual void GeneratePrimaryKeyTraits(
            [NotNull] AddPrimaryKeyOperation primaryKeyOperation,
            [NotNull] IndentedStringBuilder stringBuilder)
        {
        }

        protected virtual void GenerateUniqueConstraint(
            [NotNull] AddUniqueConstraintOperation uniqueConstraintOperation,
            [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(uniqueConstraintOperation, "uniqueConstraintOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("CONSTRAINT ")
                .Append(DelimitIdentifier(uniqueConstraintOperation.UniqueConstraintName))
                .Append(" UNIQUE (")
                .Append(uniqueConstraintOperation.ColumnNames.Select(DelimitIdentifier).Join())
                .Append(")");
        }

        protected virtual void GenerateForeignKey(
            [NotNull] AddForeignKeyOperation foreignKeyOperation,
            [NotNull] IndentedStringBuilder stringBuilder)
        {
            Check.NotNull(foreignKeyOperation, "foreignKeyOperation");
            Check.NotNull(stringBuilder, "stringBuilder");

            stringBuilder
                .Append("CONSTRAINT ")
                .Append(DelimitIdentifier(foreignKeyOperation.ForeignKeyName))
                .Append(" FOREIGN KEY (")
                .Append(foreignKeyOperation.ColumnNames.Select(DelimitIdentifier).Join())
                .Append(") REFERENCES ")
                .Append(DelimitIdentifier(foreignKeyOperation.ReferencedTableName))
                .Append(" (")
                .Append(foreignKeyOperation.ReferencedColumnNames.Select(DelimitIdentifier).Join())
                .Append(")");

            if (foreignKeyOperation.CascadeDelete)
            {
                stringBuilder.Append(" ON DELETE CASCADE");
            }
        }
    }
}
