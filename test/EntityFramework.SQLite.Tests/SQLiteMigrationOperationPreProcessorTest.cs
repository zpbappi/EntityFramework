﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Migrations;
using Microsoft.Data.Entity.Migrations.Model;
using Microsoft.Data.Entity.Relational;
using Microsoft.Data.Entity.Relational.Model;
using Microsoft.Data.Entity.Relational.Utilities;
using Xunit;
using ForeignKey = Microsoft.Data.Entity.Relational.Model.ForeignKey;
using Index = Microsoft.Data.Entity.Relational.Model.Index;

namespace Microsoft.Data.Entity.SQLite.Tests
{
    public class SQLiteMigrationOperationPreProcessorTest
    {
        [Fact]
        public void Visit_with_create_table_operation()
        {
            var modelBuilder = new BasicModelBuilder();
            var operation = new CreateTableOperation("T");
            operation.Columns.Add(new Column("Id", typeof(int)));

            var operationCollection = new MigrationOperationCollection();
            operationCollection.Add(operation);

            var operations = PreProcess(modelBuilder, operationCollection);

            Assert.Equal(1, operations.Count);
            Assert.IsType<CreateTableOperation>(operations[0]);

            var createTableOperation = (CreateTableOperation)operations[0];
            Assert.Equal("T", createTableOperation.TableName);
            Assert.Equal(new[] { "Id" }, createTableOperation.Columns.Select(c => c.Name));
            Assert.Equal(new[] { typeof(int) }, createTableOperation.Columns.Select(c => c.ClrType));
        }

        [Fact]
        public void Visit_with_create_table_operation_followed_by_add_foreign_key_operation()
        {
            var modelBuilder = new BasicModelBuilder();
            modelBuilder.Entity("T1",
                b =>
                    {
                        b.Property<int>("Id");
                        b.Key("Id");
                    });
            modelBuilder.Entity("T2",
                b =>
                    {
                        b.Property<int>("Id");
                        b.Property<int>("C");
                        b.Key("Id");
                        b.ForeignKey("T1", "C").ForRelational().Name("FK");
                    });

            var table = new SQLiteDatabaseBuilder(new SQLiteTypeMapper()).GetDatabase(modelBuilder.Model).GetTable("T2");
            var createTableOperation = new CreateTableOperation(table);
            var addForeignKeyOperation = createTableOperation.ForeignKeys[0];

            var operationCollection = new MigrationOperationCollection();
            operationCollection.Add(createTableOperation);
            operationCollection.Add(addForeignKeyOperation);

            var operations = PreProcess(modelBuilder, operationCollection);

            Assert.Equal(1, operations.Count);

            Assert.Same(createTableOperation, createTableOperation);
        }

        [Fact]
        public void Visit_with_create_table_operation_followed_by_create_index_operation()
        {
            var modelBuilder = new BasicModelBuilder();
            modelBuilder.Entity("T",
                b =>
                {
                    b.Property<int>("Id");
                    b.Property<int>("C");
                    b.Key("Id");
                    b.Index("C").ForRelational().Name("IX");
                });

            var table = new SQLiteDatabaseBuilder(new SQLiteTypeMapper()).GetDatabase(modelBuilder.Model).GetTable("T");
            var createTableOperation = new CreateTableOperation(table);
            var createIndexOperation
                = new CreateIndexOperation("T", "IX", new[] { "C" }, isUnique: true, isClustered: true);

            var operationCollection = new MigrationOperationCollection();
            operationCollection.Add(createTableOperation);
            operationCollection.Add(createIndexOperation);

            var operations = PreProcess(modelBuilder, operationCollection);

            Assert.Equal(2, operations.Count);

            Assert.Same(createTableOperation, operations[0]);
            Assert.Same(createIndexOperation, operations[1]);
        }

        [Fact]
        public void Visit_with_supported_table_subordinate_operation()
        {
            var modelBuilder = new BasicModelBuilder();
            modelBuilder.Entity("T",
                b =>
                    {
                        b.Property<int>("Id");
                        b.Key("Id");
                    });

            var operation = new AddColumnOperation("T", new Column("C", typeof(string)));

            var operationCollection = new MigrationOperationCollection();
            operationCollection.Add(operation);

            var operations = PreProcess(modelBuilder, operationCollection);

            Assert.Equal(1, operations.Count);
            Assert.Same(operation, operations[0]);
        }

        [Fact]
        public void Visit_with_rename_operation_followed_by_supported_table_subordinate_operation()
        {
            var modelBuilder = new BasicModelBuilder();
            modelBuilder.Entity("T",
                b =>
                    {
                        b.Property<int>("Id");
                        b.Key("Id");
                        // TODO: SQLite-specific. Issue #875
                        b.ForRelational().Table("T", "dbo");
                    });
            var moveTableOperation = new MoveTableOperation("dbo.T", "dbo2");
            var renameTableOperation = new RenameTableOperation("dbo2.T", "T2");
            var addColumnOperation = new AddColumnOperation("dbo2.T2", new Column("C", typeof(string)));

            var operationCollection = new MigrationOperationCollection();
            operationCollection.Add(moveTableOperation);
            operationCollection.Add(renameTableOperation);
            operationCollection.Add(addColumnOperation);

            var operations = PreProcess(modelBuilder, operationCollection);

            Assert.Equal(3, operations.Count);
            Assert.Same(moveTableOperation, operations[0]);
            Assert.Same(renameTableOperation, operations[1]);
            Assert.Same(addColumnOperation, operations[2]);
        }

        [Fact]
        public void Visit_with_unsupported_table_subordinate_operation()
        {
            var modelBuilder = new BasicModelBuilder();
            modelBuilder.Entity("T1",
                b =>
                    {
                        b.Property<int>("Id");
                        b.Key("Id");
                    });
            modelBuilder.Entity("T2",
                b =>
                    {
                        b.Property<int>("Id");
                        b.Property<int>("C");
                        b.Key("Id");
                    });

            var addForeignKeyOperation
                = new AddForeignKeyOperation("T2", "FK", new[] { "C" }, "T1", new[] { "Id" }, cascadeDelete: true);

            var operationCollection = new MigrationOperationCollection();
            operationCollection.Add(addForeignKeyOperation);

            var operations = PreProcess(modelBuilder, operationCollection);

            Assert.Equal(4, operations.Count);
            Assert.IsType<RenameTableOperation>(operations[0]);
            Assert.IsType<CreateTableOperation>(operations[1]);
            Assert.IsType<CopyDataOperation>(operations[2]);
            Assert.IsType<DropTableOperation>(operations[3]);

            var renameTableOperation = (RenameTableOperation)operations[0];

            Assert.Equal("T2", renameTableOperation.TableName);
            Assert.Equal("__mig_tmp__T2", renameTableOperation.NewTableName);

            var createTableOperation = (CreateTableOperation)operations[1];

            Assert.Equal("T2", createTableOperation.TableName);
            Assert.Equal(new[] { "Id", "C" }, createTableOperation.Columns.Select(c => c.Name));
            Assert.Equal(new[] { typeof(int), typeof(int) }, createTableOperation.Columns.Select(c => c.ClrType));
            Assert.Equal(1, createTableOperation.ForeignKeys.Count);
            Assert.Equal("FK", createTableOperation.ForeignKeys[0].ForeignKeyName);
            Assert.Equal("T1", createTableOperation.ForeignKeys[0].ReferencedTableName);
            Assert.Equal(new[] { "C" }, createTableOperation.ForeignKeys[0].ColumnNames);
            Assert.Equal(new[] { "Id" }, createTableOperation.ForeignKeys[0].ReferencedColumnNames);

            var copyDataOperation = (CopyDataOperation)operations[2];

            Assert.Equal("__mig_tmp__T2", copyDataOperation.SourceTableName);
            Assert.Equal(new[] { "Id", "C" }, copyDataOperation.SourceColumnNames);
            Assert.Equal("T2", copyDataOperation.TargetTableName);
            Assert.Equal(new[] { "Id", "C" }, copyDataOperation.TargetColumnNames);

            var dropTableOperation = (DropTableOperation)operations[3];

            Assert.Equal("__mig_tmp__T2", dropTableOperation.TableName);
        }

        [Fact]
        public void Visit_with_rename_operation_followed_by_unsupported_subordinate_operation()
        {
            var modelBuilder = new BasicModelBuilder();
            modelBuilder.Entity("T1",
                b =>
                    {
                        b.Property<int>("Id");
                        b.Key("Id");
                    });
            modelBuilder.Entity("T2",
                b =>
                    {
                        b.Property<int>("Id");
                        b.Property<int>("C");
                        b.Key("Id");
                        // TODO: SQLite-specific. Issue #875
                        b.ForRelational().Table("T", "dbo");
                    });

            var moveTableOperation = new MoveTableOperation("dbo.T", "dbo2");
            var renameTableOperation = new RenameTableOperation("dbo2.T", "T2");
            var addForeignKeyOperation
                = new AddForeignKeyOperation("dbo2.T2", "FK", new[] { "C" }, "T1", new[] { "Id" }, cascadeDelete: true);

            var operationCollection = new MigrationOperationCollection();
            operationCollection.Add(moveTableOperation);
            operationCollection.Add(renameTableOperation);
            operationCollection.Add(addForeignKeyOperation);

            var operations = PreProcess(modelBuilder, operationCollection);

            Assert.Equal(3, operations.Count);
            Assert.IsType<CreateTableOperation>(operations[0]);
            Assert.IsType<CopyDataOperation>(operations[1]);
            Assert.IsType<DropTableOperation>(operations[2]);

            var createTableOperation = (CreateTableOperation)operations[0];

            Assert.Equal("dbo2.T2", createTableOperation.TableName);
            Assert.Equal(new[] { "Id", "C" }, createTableOperation.Columns.Select(c => c.Name));
            Assert.Equal(new[] { typeof(int), typeof(int) }, createTableOperation.Columns.Select(c => c.ClrType));
            Assert.Equal(1, createTableOperation.ForeignKeys.Count);
            Assert.Equal("FK", createTableOperation.ForeignKeys[0].ForeignKeyName);
            Assert.Equal("T1", createTableOperation.ForeignKeys[0].ReferencedTableName);
            Assert.Equal(new[] { "C" }, createTableOperation.ForeignKeys[0].ColumnNames);
            Assert.Equal(new[] { "Id" }, createTableOperation.ForeignKeys[0].ReferencedColumnNames);

            var copyDataOperation = (CopyDataOperation)operations[1];

            Assert.Equal("dbo.T", copyDataOperation.SourceTableName);
            Assert.Equal(new[] { "Id", "C" }, copyDataOperation.SourceColumnNames);
            Assert.Equal("dbo2.T2", copyDataOperation.TargetTableName);
            Assert.Equal(new[] { "Id", "C" }, copyDataOperation.TargetColumnNames);

            var dropTableOperation = (DropTableOperation)operations[2];

            Assert.Equal("dbo.T", dropTableOperation.TableName);
        }

        [Fact]
        public void Visit_with_rename_index_operation()
        {
            var modelBuilder = new BasicModelBuilder();
            modelBuilder.Entity("T",
                b =>
                    {
                        b.Property<int>("Id");
                        b.Key("Id");
                        // TODO: SQLite-specific. Issue #875
                        b.Index("Id").IsUnique().ForRelational().Name("IX");
                    });

            var renameIndexOperation = new RenameIndexOperation("T", "IX", "IX2");

            var operationCollection = new MigrationOperationCollection();
            operationCollection.Add(renameIndexOperation);

            var operations = PreProcess(modelBuilder, operationCollection);

            Assert.Equal(2, operations.Count);
            Assert.IsType<DropIndexOperation>(operations[0]);
            Assert.IsType<CreateIndexOperation>(operations[1]);

            var dropIndexOperation = (DropIndexOperation)operations[0];

            Assert.Equal("T", dropIndexOperation.TableName);
            Assert.Equal("IX", dropIndexOperation.IndexName);

            var createIndexOperation = (CreateIndexOperation)operations[1];

            Assert.Equal("T", createIndexOperation.TableName);
            Assert.Equal("IX", createIndexOperation.IndexName);
            Assert.Equal(new[] { "Id" }, createIndexOperation.ColumnNames);
            Assert.True(createIndexOperation.IsUnique);
        }

        private static IReadOnlyList<MigrationOperation> PreProcess(BasicModelBuilder modelBuilder, MigrationOperationCollection operations)
        {
            return PreProcess(new SQLiteDatabaseBuilder(new SQLiteTypeMapper()).GetDatabase(modelBuilder.Model), operations);
        }

        private static IReadOnlyList<MigrationOperation> PreProcess(DatabaseModel sourceDatabase, MigrationOperationCollection operations)
        {
            var targetDatabase = sourceDatabase.Clone();
            new DatabaseModelModifier().Modify(targetDatabase, operations.GetAll());
            return new SQLiteMigrationOperationPreProcessor(new SQLiteTypeMapper()).Process(operations, sourceDatabase, targetDatabase).ToList();
        }

        public class DatabaseModelModifier : MigrationOperationVisitor<DatabaseModel>
        {
            public virtual void Modify(DatabaseModel databaseModel, IEnumerable<MigrationOperation> operations)
            {
                foreach (var operation in operations)
                {
                    operation.Accept(this, databaseModel);
                }
            }

            public override void Visit(CreateTableOperation operation, DatabaseModel databaseModel)
            {
                var cloneContext = new CloneContext();
                var table = new Table(operation.TableName, operation.Columns.Select(c => c.Clone(cloneContext)));

                databaseModel.AddTable(table);

                if (operation.PrimaryKey != null)
                {
                    Visit(operation.PrimaryKey, databaseModel);
                }

                foreach (var addUniqueConstraintOp in operation.UniqueConstraints)
                {
                    Visit(addUniqueConstraintOp, databaseModel);
                }

                foreach (var addForeignKeyOp in operation.ForeignKeys)
                {
                    Visit(addForeignKeyOp, databaseModel);
                }

                foreach (var createIndexOp in operation.Indexes)
                {
                    Visit(createIndexOp, databaseModel);
                }
            }

            public override void Visit(DropTableOperation operation, DatabaseModel databaseModel)
            {
                databaseModel.RemoveTable(operation.TableName);
            }

            public override void Visit(RenameTableOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                table.Name = new SchemaQualifiedName(operation.NewTableName, operation.TableName.Schema);
            }

            public override void Visit(MoveTableOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                table.Name = new SchemaQualifiedName(operation.TableName.Name, operation.NewSchema);
            }

            public override void Visit(AddColumnOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                table.AddColumn(operation.Column.Clone(new CloneContext()));
            }

            public override void Visit(DropColumnOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                table.RemoveColumn(operation.ColumnName);
            }

            public override void Visit(AlterColumnOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                var newColumn = operation.NewColumn;
                var column = table.GetColumn(newColumn.Name);
                column.Copy(newColumn);
            }

            public override void Visit(AddDefaultConstraintOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                var column = table.GetColumn(operation.ColumnName);
                column.DefaultValue = operation.DefaultValue;
                column.DefaultSql = operation.DefaultSql;
            }

            public override void Visit(DropDefaultConstraintOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                var column = table.GetColumn(operation.ColumnName);
                column.DefaultValue = null;
                column.DefaultSql = null;
            }

            public override void Visit(RenameColumnOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                var column = table.GetColumn(operation.ColumnName);
                column.Name = operation.NewColumnName;
            }

            public override void Visit(AddPrimaryKeyOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                table.PrimaryKey = new PrimaryKey(
                    operation.PrimaryKeyName,
                    operation.ColumnNames.Select(table.GetColumn).ToArray(),
                    operation.IsClustered);
            }

            public override void Visit(DropPrimaryKeyOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                table.PrimaryKey = null;
            }

            public override void Visit(AddUniqueConstraintOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                table.AddUniqueConstraint(
                    new UniqueConstraint(
                        operation.UniqueConstraintName,
                        operation.ColumnNames.Select(table.GetColumn).ToArray()));
            }

            public override void Visit(DropUniqueConstraintOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                table.RemoveUniqueConstraint(operation.UniqueConstraintName);
            }

            public override void Visit(AddForeignKeyOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                var referencedTable = databaseModel.GetTable(operation.ReferencedTableName);
                table.AddForeignKey(
                    new ForeignKey(
                        operation.ForeignKeyName,
                        operation.ColumnNames.Select(table.GetColumn).ToArray(),
                        operation.ReferencedColumnNames.Select(referencedTable.GetColumn).ToArray(),
                        operation.CascadeDelete));
            }

            public override void Visit(DropForeignKeyOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                table.RemoveForeignKey(operation.ForeignKeyName);
            }

            public override void Visit(CreateIndexOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                table.AddIndex(
                    new Index(
                        operation.IndexName,
                        operation.ColumnNames.Select(table.GetColumn).ToArray(),
                        operation.IsUnique,
                        operation.IsClustered));
            }

            public override void Visit(DropIndexOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                table.RemoveIndex(operation.IndexName);
            }

            public override void Visit(RenameIndexOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                var index = table.GetIndex(operation.IndexName);
                index.Name = operation.NewIndexName;
            }

            protected override void VisitDefault(MigrationOperation operation, DatabaseModel databaseModel)
            {
                throw new InvalidOperationException();
            }

            public override void Visit(DropColumnOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                table.RemoveColumn(operation.ColumnName);
            }

            public override void Visit(AlterColumnOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                var newColumn = operation.NewColumn;
                var column = table.GetColumn(newColumn.Name);
                column.Copy(newColumn);
            }

            public override void Visit(AddDefaultConstraintOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                var column = table.GetColumn(operation.ColumnName);
                column.DefaultValue = operation.DefaultValue;
                column.DefaultSql = operation.DefaultSql;
            }

            public override void Visit(DropDefaultConstraintOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                var column = table.GetColumn(operation.ColumnName);
                column.DefaultValue = null;
                column.DefaultSql = null;
            }

            public override void Visit(RenameColumnOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                var column = table.GetColumn(operation.ColumnName);
                column.Name = operation.NewColumnName;
            }

            public override void Visit(AddPrimaryKeyOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                table.PrimaryKey = new PrimaryKey(
                    operation.PrimaryKeyName,
                    operation.ColumnNames.Select(table.GetColumn).ToArray(),
                    operation.IsClustered);
            }

            public override void Visit(DropPrimaryKeyOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                table.PrimaryKey = null;
            }

            public override void Visit(AddUniqueConstraintOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                table.AddUniqueConstraint(
                    new UniqueConstraint(
                        operation.UniqueConstraintName,
                        operation.ColumnNames.Select(table.GetColumn).ToArray()));
            }

            public override void Visit(DropUniqueConstraintOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                table.RemoveUniqueConstraint(operation.UniqueConstraintName);
            }

            public override void Visit(AddForeignKeyOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                var referencedTable = databaseModel.GetTable(operation.ReferencedTableName);
                table.AddForeignKey(
                    new ForeignKey(
                        operation.ForeignKeyName,
                        operation.ColumnNames.Select(table.GetColumn).ToArray(),
                        operation.ReferencedColumnNames.Select(referencedTable.GetColumn).ToArray(),
                        operation.CascadeDelete));
            }

            public override void Visit(DropForeignKeyOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                table.RemoveForeignKey(operation.ForeignKeyName);
            }

            public override void Visit(CreateIndexOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                table.AddIndex(
                    new Index(
                        operation.IndexName,
                        operation.ColumnNames.Select(table.GetColumn).ToArray(),
                        operation.IsUnique,
                        operation.IsClustered));
            }

            public override void Visit(DropIndexOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                table.RemoveIndex(operation.IndexName);
            }

            public override void Visit(RenameIndexOperation operation, DatabaseModel databaseModel)
            {
                var table = databaseModel.GetTable(operation.TableName);
                var index = table.GetIndex(operation.IndexName);
                index.Name = operation.NewIndexName;
            }

            protected override void VisitDefault(MigrationOperation operation, DatabaseModel databaseModel)
            {
                throw new InvalidOperationException();
            }
        }
    }
}
