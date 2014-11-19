// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Entity.ChangeTracking;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Relational.Metadata;
using Microsoft.Data.Entity.Relational.Model;
using Microsoft.Data.Entity.Relational.Update;
using Microsoft.Data.Entity.Update;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.DependencyInjection.Fallback;
using Microsoft.Framework.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace Microsoft.Data.Entity.Relational.Tests.Update
{
    public class ReaderModificationCommandBatchTest
    {
        [Fact]
        public void AddCommand_adds_command_if_possible()
        {
            var command = new ModificationCommand(new SchemaQualifiedName("T1"), new ParameterNameGenerator(), p => p.Relational());

            var batch = new ModificationCommandBatchFake();
            batch.AddCommand(command);
            batch.ShouldAddCommand = true;
            batch.ShouldValidateSql = true;

            batch.AddCommand(command);

            Assert.Equal(2, batch.ModificationCommands.Count);
            Assert.Same(command, batch.ModificationCommands[0]);
            Assert.Equal("..", batch.CommandText);
        }

        [Fact]
        public void AddCommand_does_not_add_command_if_not_possible()
        {
            var command = new ModificationCommand(new SchemaQualifiedName("T1"), new ParameterNameGenerator(), p => p.Relational());

            var batch = new ModificationCommandBatchFake();
            batch.AddCommand(command);
            batch.ShouldAddCommand = false;
            batch.ShouldValidateSql = true;

            batch.AddCommand(command);

            Assert.Equal(1, batch.ModificationCommands.Count);
            Assert.Equal(".", batch.CommandText);
        }

        [Fact]
        public void AddCommand_does_not_add_command_if_resulting_sql_is_invalid()
        {
            var command = new ModificationCommand(new SchemaQualifiedName("T1"), new ParameterNameGenerator(), p => p.Relational());

            var batch = new ModificationCommandBatchFake();
            batch.AddCommand(command);
            batch.ShouldAddCommand = true;
            batch.ShouldValidateSql = false;

            batch.AddCommand(command);

            Assert.Equal(1, batch.ModificationCommands.Count);
            Assert.Equal(".", batch.CommandText);
        }

        [Fact]
        public void UpdateCommandText_compiles_inserts()
        {
            var stateEntry = CreateStateEntry(EntityState.Added);

            var command = new ModificationCommand(new SchemaQualifiedName("T1"), new ParameterNameGenerator(), p => p.Relational());
            command.AddStateEntry(stateEntry);

            var sqlGeneratorMock = new Mock<SqlGenerator>();
            var batch = new ModificationCommandBatchFake(sqlGeneratorMock.Object);
            batch.AddCommand(command);

            batch.UpdateCachedCommandTextBase(0);

            sqlGeneratorMock.Verify(g => g.AppendBatchHeader(It.IsAny<StringBuilder>()));
            sqlGeneratorMock.Verify(g => g.AppendInsertOperation(It.IsAny<StringBuilder>(), command));
        }

        [Fact]
        public void UpdateCommandText_compiles_updates()
        {
            var stateEntry = CreateStateEntry(EntityState.Modified, generateKeyValues: true);

            var command = new ModificationCommand(new SchemaQualifiedName("T1"), new ParameterNameGenerator(), p => p.Relational());
            command.AddStateEntry(stateEntry);

            var sqlGeneratorMock = new Mock<SqlGenerator>();
            var batch = new ModificationCommandBatchFake(sqlGeneratorMock.Object);
            batch.AddCommand(command);

            batch.UpdateCachedCommandTextBase(0);

            sqlGeneratorMock.Verify(g => g.AppendBatchHeader(It.IsAny<StringBuilder>()));
            sqlGeneratorMock.Verify(g => g.AppendUpdateOperation(It.IsAny<StringBuilder>(), command));
        }

        [Fact]
        public void UpdateCommandText_compiles_deletes()
        {
            var stateEntry = CreateStateEntry(EntityState.Deleted);

            var command = new ModificationCommand(new SchemaQualifiedName("T1"), new ParameterNameGenerator(), p => p.Relational());
            command.AddStateEntry(stateEntry);

            var sqlGeneratorMock = new Mock<SqlGenerator>();
            var batch = new ModificationCommandBatchFake(sqlGeneratorMock.Object);
            batch.AddCommand(command);

            batch.UpdateCachedCommandTextBase(0);

            sqlGeneratorMock.Verify(g => g.AppendBatchHeader(It.IsAny<StringBuilder>()));
            sqlGeneratorMock.Verify(g => g.AppendDeleteOperation(It.IsAny<StringBuilder>(), command));
        }

        [Fact]
        public void UpdateCommandText_compiles_multiple_commands()
        {
            var stateEntry = CreateStateEntry(EntityState.Added);

            var command = new ModificationCommand(new SchemaQualifiedName("T1"), new ParameterNameGenerator(), p => p.Relational());
            command.AddStateEntry(stateEntry);

            var fakeSqlGenerator = new FakeSqlGenerator();
            fakeSqlGenerator.AppendInsertOperationCallback = (sb, c) =>
                sb.Append(c.SchemaQualifiedName.ToString());
            var batch = new ModificationCommandBatchFake(fakeSqlGenerator);
            batch.AddCommand(command);
            batch.AddCommand(command);

            Assert.Equal("..", batch.CommandText);

            Assert.Equal(1, fakeSqlGenerator.AppendBatchHeaderCalls);
        }

        private class FakeSqlGenerator : SqlGenerator
        {
            // Workaround for Roslyn breaking Moq
            public Action<StringBuilder, ModificationCommand> AppendInsertOperationCallback { get; set; }

            public override void AppendInsertOperation(StringBuilder commandStringBuilder, ModificationCommand command)
            {
                if (AppendInsertOperationCallback != null)
                {
                    AppendInsertOperationCallback(commandStringBuilder, command);
                }
            }

            public int AppendBatchHeaderCalls { get; set; }

            public override void AppendBatchHeader(StringBuilder commandStringBuilder)
            {
                AppendBatchHeaderCalls++;
                base.AppendBatchHeader(commandStringBuilder);
            }

            public override void AppendSelectAffectedCountCommand(StringBuilder commandStringBuilder, SchemaQualifiedName schemaQualifiedName)
            {
            }

            protected override void AppendRowsAffectedWhereCondition(StringBuilder commandStringBuilder, int expectedRowsAffected)
            {
            }

            protected override void AppendIdentityWhereCondition(StringBuilder commandStringBuilder, ColumnModification columnModification)
            {
            }
        }

        [Fact]
        public async Task ExecuteAsync_executes_batch_commands_and_consumes_reader()
        {
            var stateEntry = CreateStateEntry(EntityState.Added);
            var command = new ModificationCommand(new SchemaQualifiedName("T1"), new ParameterNameGenerator(), p => p.Relational());
            command.AddStateEntry(stateEntry);

            var mockReader = CreateDataReaderMock();
            var batch = new ModificationCommandBatchFake(mockReader.Object);
            batch.AddCommand(command);

            await batch.ExecuteAsync(new Mock<RelationalTransaction>().Object, new RelationalTypeMapper(), new Mock<DbContext>().Object, new Mock<ILogger>().Object);

            mockReader.Verify(r => r.ReadAsync(It.IsAny<CancellationToken>()), Times.Once);
            mockReader.Verify(r => r.GetFieldValue<int>(0), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_saves_store_generated_values()
        {
            var stateEntry = CreateStateEntry(EntityState.Added, generateKeyValues: true);
            stateEntry.MarkAsTemporary(stateEntry.EntityType.GetPrimaryKey().Properties[0]);

            var command = new ModificationCommand(new SchemaQualifiedName("T1"), new ParameterNameGenerator(), p => p.Relational());
            command.AddStateEntry(stateEntry);

            var batch = new ModificationCommandBatchFake(CreateDataReaderMock(new[] { "Col1" }, new List<object[]> { new object[] { 42 } }).Object);
            batch.AddCommand(command);

            await batch.ExecuteAsync(new Mock<RelationalTransaction>().Object, new RelationalTypeMapper(), new Mock<DbContext>().Object, new Mock<ILogger>().Object);

            Assert.Equal(42, stateEntry[stateEntry.EntityType.GetProperty("Id")]);
            Assert.Equal("Test", stateEntry[stateEntry.EntityType.GetProperty("Name")]);
        }

        [Fact]
        public async Task ExecuteAsync_saves_store_generated_values_on_non_key_columns()
        {
            var stateEntry = CreateStateEntry(
                EntityState.Added, generateKeyValues: true, computeNonKeyValue: true);
            stateEntry.MarkAsTemporary(stateEntry.EntityType.GetPrimaryKey().Properties[0]);

            var command = new ModificationCommand(new SchemaQualifiedName("T1"), new ParameterNameGenerator(), p => p.Relational());
            command.AddStateEntry(stateEntry);

            var batch = new ModificationCommandBatchFake(CreateDataReaderMock(new[] { "Col1", "Col2" }, new List<object[]> { new object[] { 42, "FortyTwo" } }).Object);
            batch.AddCommand(command);

            await batch.ExecuteAsync(new Mock<RelationalTransaction>().Object, new RelationalTypeMapper(), new Mock<DbContext>().Object, new Mock<ILogger>().Object);

            Assert.Equal(42, stateEntry[stateEntry.EntityType.GetProperty("Id")]);
            Assert.Equal("FortyTwo", stateEntry[stateEntry.EntityType.GetProperty("Name")]);
        }

        [Fact]
        public async Task ExecuteAsync_saves_store_generated_values_when_updating()
        {
            var stateEntry = CreateStateEntry(
                EntityState.Modified, generateKeyValues: true, computeNonKeyValue: true);

            var command = new ModificationCommand(new SchemaQualifiedName("T1"), new ParameterNameGenerator(), p => p.Relational());
            command.AddStateEntry(stateEntry);

            var batch = new ModificationCommandBatchFake(CreateDataReaderMock(new[] { "Col2" }, new List<object[]> { new object[] { "FortyTwo" } }).Object);
            batch.AddCommand(command);

            await batch.ExecuteAsync(new Mock<RelationalTransaction>().Object, new RelationalTypeMapper(), new Mock<DbContext>().Object, new Mock<ILogger>().Object);

            Assert.Equal(1, stateEntry[stateEntry.EntityType.GetProperty("Id")]);
            Assert.Equal("FortyTwo", stateEntry[stateEntry.EntityType.GetProperty("Name")]);
        }

        [Fact]
        public async Task Exception_not_thrown_for_more_than_one_row_returned_for_single_command()
        {
            var stateEntry = CreateStateEntry(EntityState.Added, generateKeyValues: true);
            stateEntry.MarkAsTemporary(stateEntry.EntityType.GetPrimaryKey().Properties[0]);

            var command = new ModificationCommand(new SchemaQualifiedName("T1"), new ParameterNameGenerator(), p => p.Relational());
            command.AddStateEntry(stateEntry);

            var mockReader = CreateDataReaderMock(new[] { "Col1" }, new List<object[]>
                {
                    new object[] { 42 },
                    new object[] { 43 }
                });
            var batch = new ModificationCommandBatchFake(mockReader.Object);
            batch.AddCommand(command);

            await batch.ExecuteAsync(new Mock<RelationalTransaction>().Object, new RelationalTypeMapper(), new Mock<DbContext>().Object, new Mock<ILogger>().Object);

            Assert.Equal(42, stateEntry[stateEntry.EntityType.GetProperty("Id")]);
        }

        [Fact]
        public async Task Exception_thrown_if_rows_returned_for_command_without_store_generated_values_is_not_1()
        {
            var stateEntry = CreateStateEntry(EntityState.Added);

            var command = new ModificationCommand(new SchemaQualifiedName("T1"), new ParameterNameGenerator(), p => p.Relational());
            command.AddStateEntry(stateEntry);

            var batch = new ModificationCommandBatchFake(CreateDataReaderMock(new[] { "Col1" }, new List<object[]> { new object[] { 42 } }).Object);
            batch.AddCommand(command);

            Assert.Equal(Strings.UpdateConcurrencyException(1, 42),
                (await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
                    async () => await batch.ExecuteAsync(
                        new Mock<RelationalTransaction>().Object,
                        new RelationalTypeMapper(),
                        new Mock<DbContext>().Object,
                        new Mock<ILogger>().Object))).Message);
        }

        [Fact]
        public async Task Exception_thrown_if_no_rows_returned_for_command_with_store_generated_values()
        {
            var stateEntry = CreateStateEntry(EntityState.Added, generateKeyValues: true);
            stateEntry.MarkAsTemporary(stateEntry.EntityType.GetPrimaryKey().Properties[0]);

            var command = new ModificationCommand(new SchemaQualifiedName("T1"), new ParameterNameGenerator(), p => p.Relational());
            command.AddStateEntry(stateEntry);

            var batch = new ModificationCommandBatchFake(CreateDataReaderMock(new[] { "Col1" }, new List<object[]>()).Object);
            batch.AddCommand(command);

            Assert.Equal(Strings.UpdateConcurrencyException(1, 0),
                (await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
                    async () => await batch.ExecuteAsync(
                        new Mock<RelationalTransaction>().Object,
                        new RelationalTypeMapper(),
                        new Mock<DbContext>().Object,
                        new Mock<ILogger>().Object))).Message);
        }

        [Fact]
        public void CreateStoreCommand_creates_parameters_for_each_ModificationCommand()
        {
            var stateEntry = CreateStateEntry(EntityState.Added, generateKeyValues: true);
            var property = stateEntry.EntityType.GetProperty("Id");
            stateEntry.MarkAsTemporary(property);
            var batch = new ModificationCommandBatchFake();

            var commandMock1 = new Mock<ModificationCommand>();
            commandMock1.Setup(m => m.ColumnModifications).Returns(
                new List<ColumnModification>
                    {
                        new ColumnModification(
                            stateEntry,
                            property,
                            property.Relational(),
                            new ParameterNameGenerator(), false, true, false, false)
                    });
            batch.AddCommand(commandMock1.Object);

            var commandMock2 = new Mock<ModificationCommand>();
            commandMock2.Setup(m => m.ColumnModifications).Returns(
                new List<ColumnModification>
                    {
                        new ColumnModification(
                            stateEntry,
                            property,
                            property.Relational(),
                            new ParameterNameGenerator(), false, true, false, false)
                    });
            batch.AddCommand(commandMock2.Object);

            var transaction = CreateMockDbTransaction();

            var command = batch.CreateStoreCommandBase("foo", transaction, new RelationalTypeMapper());

            Assert.Equal(CommandType.Text, command.CommandType);
            Assert.Equal("foo", command.CommandText);
            Assert.Same(transaction, command.Transaction);
            Assert.Equal(2, batch.PopulateParameterCalls);
        }

        [Fact]
        public void PopulateParameters_creates_parameter_for_write_ModificationCommand()
        {
            var stateEntry = CreateStateEntry(EntityState.Added, generateKeyValues: true);
            var property = stateEntry.EntityType.GetProperty("Id");
            stateEntry.MarkAsTemporary(property);
            var batch = new ModificationCommandBatchFake();
            var dbCommandMock = CreateDbCommandMock();

            batch.PopulateParametersBase(dbCommandMock.Object,
                new ColumnModification(
                    stateEntry,
                    property,
                    property.Relational(),
                    new ParameterNameGenerator(), false, true, false, false),
                new RelationalTypeMapper());

            Assert.Equal(1, dbCommandMock.Object.Parameters.Count);
        }

        [Fact]
        public void PopulateParameters_creates_parameter_for_condition_ModificationCommand()
        {
            var stateEntry = CreateStateEntry(EntityState.Added, generateKeyValues: true);
            var property = stateEntry.EntityType.GetProperty("Id");
            stateEntry.MarkAsTemporary(property);
            var batch = new ModificationCommandBatchFake();
            var dbCommandMock = CreateDbCommandMock();

            batch.PopulateParametersBase(dbCommandMock.Object,
                new ColumnModification(
                    stateEntry,
                    property,
                    property.Relational(),
                    new ParameterNameGenerator(), false, false, false, true),
                new RelationalTypeMapper());

            Assert.Equal(1, dbCommandMock.Object.Parameters.Count);
        }

        [Fact]
        public void PopulateParameters_creates_parameter_for_write_and_condition_ModificationCommand()
        {
            var stateEntry = CreateStateEntry(EntityState.Added, generateKeyValues: true);
            var property = stateEntry.EntityType.GetProperty("Id");
            stateEntry.MarkAsTemporary(property);
            var batch = new ModificationCommandBatchFake();
            var dbCommandMock = CreateDbCommandMock();

            batch.PopulateParametersBase(dbCommandMock.Object,
                new ColumnModification(
                    stateEntry,
                    property,
                    property.Relational(),
                    new ParameterNameGenerator(), false, true, false, true),
                new RelationalTypeMapper());

            Assert.Equal(2, dbCommandMock.Object.Parameters.Count);
        }

        [Fact]
        public void PopulateParameters_does_not_create_parameter_for_read_ModificationCommand()
        {
            var stateEntry = CreateStateEntry(EntityState.Added, generateKeyValues: true);
            var property = stateEntry.EntityType.GetProperty("Id");
            stateEntry.MarkAsTemporary(property);
            var batch = new ModificationCommandBatchFake();
            var dbCommandMock = CreateDbCommandMock();

            batch.PopulateParametersBase(dbCommandMock.Object,
                new ColumnModification(
                    stateEntry,
                    property,
                    property.Relational(),
                    new ParameterNameGenerator(), true, false, false, false),
                new RelationalTypeMapper());

            Assert.Equal(0, dbCommandMock.Object.Parameters.Count);
        }

        private static Mock<DbConnection> CreateMockDbConnection(DbCommand dbCommand = null)
        {
            var mockConnection = new Mock<DbConnection>();
            mockConnection
                .Protected()
                .Setup<DbCommand>("CreateDbCommand")
                .Returns(dbCommand ?? CreateDbCommandMock().Object);
            return mockConnection;
        }

        private static Mock<DbCommand> CreateDbCommandMock(DbDataReader dataReader = null)
        {
            var dbCommandMock = new Mock<DbCommand>();
            dbCommandMock
                .Protected()
                .Setup<DbParameter>("CreateDbParameter")
                .Returns(() => CreateDbParameterMock().Object);
            dbCommandMock
                .Protected()
                .SetupGet<DbParameterCollection>("DbParameterCollection")
                .Returns(CreateDbParameterCollectionMock().Object);

            var tcs = new TaskCompletionSource<DbDataReader>();
            tcs.SetResult(dataReader ?? CreateDataReaderMock().Object);

            dbCommandMock
                .Protected()
                .Setup<Task<DbDataReader>>("ExecuteDbDataReaderAsync", ItExpr.IsAny<CommandBehavior>(), ItExpr.IsAny<CancellationToken>())
                .Returns(tcs.Task);

            string text = null;
            dbCommandMock.SetupSet(m => m.CommandText = It.IsAny<string>()).Callback<string>(t => { text = t; });
            dbCommandMock.Setup(m => m.CommandText).Returns(() => text);

            var type = default(CommandType);
            dbCommandMock.SetupSet(m => m.CommandType = It.IsAny<CommandType>()).Callback<CommandType>(t => { type = t; });
            dbCommandMock.Setup(m => m.CommandType).Returns(() => type);

            DbTransaction transaction = null;
            dbCommandMock.Protected().SetupSet<DbTransaction>("DbTransaction", ItExpr.IsAny<DbTransaction>()).Callback(t => { transaction = t; });
            dbCommandMock.Protected().Setup<DbTransaction>("DbTransaction").Returns(() => transaction);

            return dbCommandMock;
        }

        private static Mock<DbParameter> CreateDbParameterMock()
        {
            var dbParameterMock = new Mock<DbParameter>();
            string name = null;
            dbParameterMock
                .Setup(m => m.ParameterName)
                .Returns(() => name);
            dbParameterMock
                .SetupSet(m => m.ParameterName = It.IsAny<string>())
                .Callback<string>(n => name = n);
            return dbParameterMock;
        }

        private static Mock<DbParameterCollection> CreateDbParameterCollectionMock()
        {
            var parameters = new List<object>();
            var dbParameterCollectionMock = new Mock<DbParameterCollection>();
            dbParameterCollectionMock
                .Setup(m => m.Add(It.IsAny<object>()))
                .Returns<object>(p =>
                {
                    parameters.Add(p);
                    return parameters.Count - 1;
                });
            dbParameterCollectionMock
                .Protected()
                .Setup<DbParameter>("GetParameter", ItExpr.IsAny<int>())
                .Returns<int>(i => (DbParameter)parameters[i]);
            dbParameterCollectionMock.Setup(m => m.Count).Returns(() => parameters.Count);
            return dbParameterCollectionMock;
        }

        private static DbTransaction CreateMockDbTransaction(DbConnection dbConnection = null)
        {
            var dbTransactionMock = new Mock<DbTransaction>();
            dbTransactionMock
                .Protected()
                .Setup<DbConnection>("DbConnection")
                .Returns(dbConnection ?? CreateMockDbConnection().Object);

            return dbTransactionMock.Object;
        }

        private static Mock<DbDataReader> CreateDataReaderMock(string[] columnNames = null, IList<object[]> results = null)
        {
            results = results ?? new List<object[]> { new object[] { 1 } };
            columnNames = columnNames ?? new[] { "RowsAffected" };
            var rowIndex = 0;
            object[] currentRow = null;

            var mockDataReader = new Mock<DbDataReader>();

            mockDataReader.Setup(r => r.FieldCount).Returns(columnNames.Length);
            mockDataReader.Setup(r => r.GetName(It.IsAny<int>())).Returns((int columnIdx) => columnNames[columnIdx]);
            mockDataReader.Setup(r => r.GetValue(It.IsAny<int>())).Returns((int columnIdx) => currentRow[columnIdx]);
            mockDataReader.Setup(r => r.GetFieldValue<int>(It.IsAny<int>())).Returns((int columnIdx) => (int)currentRow[columnIdx]);
            mockDataReader.Setup(r => r.GetFieldValue<string>(It.IsAny<int>())).Returns((int columnIdx) => (string)currentRow[columnIdx]);
            mockDataReader.Setup(r => r.GetFieldValue<object>(It.IsAny<int>())).Returns((int columnIdx) => currentRow[columnIdx]);

            mockDataReader
                .Setup(r => r.ReadAsync(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    currentRow = rowIndex < results.Count ? results[rowIndex++] : null;
                    return Task.FromResult(currentRow != null);
                });

            return mockDataReader;
        }

        private class T1
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        private static IModel BuildModel(bool generateKeyValues, bool computeNonKeyValue)
        {
            var model = new Entity.Metadata.Model();

            var entityType = model.AddEntityType(typeof(T1));

            var key = entityType.GetOrAddProperty("Id", typeof(int));
            key.GenerateValueOnAdd = generateKeyValues;
            key.Relational().Column = "Col1";
            entityType.GetOrSetPrimaryKey(key);

            var nonKey = entityType.GetOrAddProperty("Name", typeof(string));
            nonKey.Relational().Column = "Col2";
            nonKey.IsStoreComputed = computeNonKeyValue;

            return model;
        }

        private static IServiceProvider CreateContextServices(IModel model)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddEntityFramework().AddInMemoryStore();
            return ((IDbContextServices)new DbContext(serviceCollection.BuildServiceProvider(),
                new DbContextOptions()
                    .UseInMemoryStore(false)
                    .UseModel(model)))
                .ScopedServiceProvider;
        }

        private static StateEntry CreateStateEntry(
            EntityState entityState,
            bool generateKeyValues = false,
            bool computeNonKeyValue = false)
        {
            var model = BuildModel(generateKeyValues, computeNonKeyValue);
            var stateEntry = CreateContextServices(model).GetRequiredService<StateManager>().GetOrCreateEntry(
                new T1 { Id = 1, Name = "Test" });
            stateEntry.EntityState = entityState;
            return stateEntry;
        }

        private class ModificationCommandBatchFake : ReaderModificationCommandBatch
        {
            private readonly DbDataReader _reader;

            public ModificationCommandBatchFake(SqlGenerator sqlGenerator = null)
                : base(sqlGenerator ?? new FakeSqlGenerator())
            {
                ShouldAddCommand = true;
                ShouldValidateSql = true;
            }

            public ModificationCommandBatchFake(DbDataReader reader, SqlGenerator sqlGenerator = null)
                : base(sqlGenerator ?? new FakeSqlGenerator())
            {
                _reader = reader;
                ShouldAddCommand = true;
                ShouldValidateSql = true;
            }

            public string CommandText
            {
                get { return base.GetCommandText(); }
            }

            public bool ShouldAddCommand { get; set; }

            protected override bool CanAddCommand(ModificationCommand modificationCommand)
            {
                return ShouldAddCommand;
            }

            public bool ShouldValidateSql { get; set; }

            protected override bool IsCommandTextValid()
            {
                return ShouldValidateSql;
            }

            protected override void UpdateCachedCommandText(int commandIndex)
            {
                CachedCommandText = CachedCommandText ?? new StringBuilder();
                CachedCommandText.Append(".");
            }

            public void UpdateCachedCommandTextBase(int commandIndex)
            {
                base.UpdateCachedCommandText(commandIndex);
            }

            protected override DbCommand CreateStoreCommand(string commandText, DbTransaction transaction, RelationalTypeMapper typeMapper)
            {
                return CreateDbCommandMock(_reader).Object;
            }

            public DbCommand CreateStoreCommandBase(string commandText, DbTransaction transaction, RelationalTypeMapper typeMapper)
            {
                return base.CreateStoreCommand(commandText, transaction, typeMapper);
            }

            public int PopulateParameterCalls { get; set; }

            protected override void PopulateParameters(DbCommand command, ColumnModification columnModification, RelationalTypeMapper typeMapper)
            {
                PopulateParameterCalls++;
            }

            public void PopulateParametersBase(DbCommand command, ColumnModification columnModification, RelationalTypeMapper typeMapper)
            {
                base.PopulateParameters(command, columnModification, typeMapper);
            }

            public override IRelationalPropertyExtensions GetPropertyExtensions(IProperty property)
            {
                return property.Relational();
            }
        }
    }
}
