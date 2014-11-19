// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.Data.Entity.Migrations.Infrastructure;
using Microsoft.Data.Entity.Relational;
using Microsoft.Data.Entity.Relational.Update;
using Microsoft.Data.Entity.SqlServer.Metadata;
using Microsoft.Data.Entity.SqlServer.Update;
using Microsoft.Data.Entity.Storage;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.DependencyInjection.Fallback;
using Xunit;

namespace Microsoft.Data.Entity.SqlServer.Tests
{
    public class SqlServerEntityServicesBuilderExtensionsTest
    {
        [Fact]
        public void Can_get_default_services()
        {
            var services = new ServiceCollection();
            services
                .AddEntityFramework()
                .AddSqlServer();

            // Relational
            Assert.True(services.Any(sd => sd.ServiceType == typeof(RelationalObjectArrayValueReaderFactory)));
            Assert.True(services.Any(sd => sd.ServiceType == typeof(RelationalTypedValueReaderFactory)));
            Assert.True(services.Any(sd => sd.ServiceType == typeof(ModificationCommandComparer)));
            Assert.True(services.Any(sd => sd.ServiceType == typeof(GraphFactory)));

            // SQL Server dingletones
            Assert.True(services.Any(sd => sd.ServiceType == typeof(DataStoreSource)));
            Assert.True(services.Any(sd => sd.ServiceType == typeof(SqlServerSqlGenerator)));
            Assert.True(services.Any(sd => sd.ServiceType == typeof(SqlStatementExecutor)));
            Assert.True(services.Any(sd => sd.ServiceType == typeof(SqlServerTypeMapper)));
            Assert.True(services.Any(sd => sd.ServiceType == typeof(SqlServerBatchExecutor)));
            Assert.True(services.Any(sd => sd.ServiceType == typeof(SqlServerModificationCommandBatchFactory)));
            Assert.True(services.Any(sd => sd.ServiceType == typeof(SqlServerCommandBatchPreparer)));
            Assert.True(services.Any(sd => sd.ServiceType == typeof(SqlServerMetadataExtensionProvider)));
            Assert.True(services.Any(sd => sd.ServiceType == typeof(SqlServerMigrationOperationFactory)));

            // SQL Server scoped
            Assert.True(services.Any(sd => sd.ServiceType == typeof(SqlServerDataStore)));
            Assert.True(services.Any(sd => sd.ServiceType == typeof(SqlServerConnection)));
            Assert.True(services.Any(sd => sd.ServiceType == typeof(SqlServerMigrationOperationProcessor)));
            Assert.True(services.Any(sd => sd.ServiceType == typeof(SqlServerModelDiffer)));
            Assert.True(services.Any(sd => sd.ServiceType == typeof(SqlServerMigrationOperationSqlGeneratorFactory)));
            Assert.True(services.Any(sd => sd.ServiceType == typeof(SqlServerDataStoreCreator)));
            Assert.True(services.Any(sd => sd.ServiceType == typeof(MigrationAssembly)));
            Assert.True(services.Any(sd => sd.ServiceType == typeof(HistoryRepository)));
            Assert.True(services.Any(sd => sd.ServiceType == typeof(SqlServerMigrator)));
        }

        [Fact]
        public void Services_wire_up_correctly()
        {
            var services = new ServiceCollection();
            services
                .AddEntityFramework()
                .AddSqlServer();

            var serviceProvider = services.BuildServiceProvider();

            var context = new DbContext(
                serviceProvider,
                new DbContextOptions().UseSqlServer("goo=boo"));

            var scopedProvider = context.Configuration.Services.ServiceProvider;

            var arrayReaderFactory = scopedProvider.GetService<RelationalObjectArrayValueReaderFactory>();
            var typedReaderFactory = scopedProvider.GetService<RelationalTypedValueReaderFactory>();
            var batchPreparer = scopedProvider.GetService<SqlServerCommandBatchPreparer>();
            var modificationCommandComparer = scopedProvider.GetService<ModificationCommandComparer>();
            var graphFactory = scopedProvider.GetService<GraphFactory>();

            var sqlServerDataStoreSource = scopedProvider.GetService<DataStoreSource>() as SqlServerDataStoreSource;
            var sqlServerSqlGenerator = scopedProvider.GetService<SqlServerSqlGenerator>();
            var sqlStatementExecutor = scopedProvider.GetService<SqlStatementExecutor>();
            var sqlTypeMapper = scopedProvider.GetService<SqlServerTypeMapper>();
            var sqlServerBatchExecutor = scopedProvider.GetService<SqlServerBatchExecutor>();
            var sqlServerModificationCommandBatchFactory = scopedProvider.GetService<SqlServerModificationCommandBatchFactory>();
            var sqlServerMetadataExtensionProvider = scopedProvider.GetService<SqlServerMetadataExtensionProvider>();
            var sqlServerMigrationOperationFactory = scopedProvider.GetService<SqlServerMigrationOperationFactory>();

            var sqlServerDataStore = scopedProvider.GetService<SqlServerDataStore>();
            var sqlServerConnection = scopedProvider.GetService<SqlServerConnection>();
            var sqlServerMigrationOperationProcessor = scopedProvider.GetService<SqlServerMigrationOperationProcessor>();
            var modelDiffer = scopedProvider.GetService<SqlServerModelDiffer>();
            var serverMigrationOperationSqlGeneratorFactory = scopedProvider.GetService<SqlServerMigrationOperationSqlGeneratorFactory>();
            var sqlServerDataStoreCreator = scopedProvider.GetService<SqlServerDataStoreCreator>();
            var migrationAssembly = scopedProvider.GetService<MigrationAssembly>();
            var historyRepository = scopedProvider.GetService<HistoryRepository>();
            var sqlServerMigrator = scopedProvider.GetService<SqlServerMigrator>();

            Assert.NotNull(arrayReaderFactory);
            Assert.NotNull(typedReaderFactory);
            Assert.NotNull(batchPreparer);
            Assert.NotNull(modificationCommandComparer);
            Assert.NotNull(graphFactory);

            Assert.NotNull(sqlServerDataStoreSource);
            Assert.NotNull(sqlServerSqlGenerator);
            Assert.NotNull(sqlStatementExecutor);
            Assert.NotNull(sqlTypeMapper);
            Assert.NotNull(sqlServerBatchExecutor);
            Assert.NotNull(sqlServerModificationCommandBatchFactory);
            Assert.NotNull(sqlServerMetadataExtensionProvider);
            Assert.NotNull(sqlServerMigrationOperationFactory);

            Assert.NotNull(sqlServerDataStore);
            Assert.NotNull(sqlServerConnection);
            Assert.NotNull(sqlServerMigrationOperationProcessor);
            Assert.NotNull(modelDiffer);
            Assert.NotNull(serverMigrationOperationSqlGeneratorFactory);
            Assert.NotNull(sqlServerDataStoreCreator);
            Assert.NotNull(migrationAssembly);
            Assert.NotNull(historyRepository);
            Assert.NotNull(sqlServerMigrator);

            context.Dispose();

            context = new DbContext(
                serviceProvider,
                new DbContextOptions().UseSqlServer("goo=boo"));

            scopedProvider = context.Configuration.Services.ServiceProvider;

            // Dingletons
            Assert.Same(arrayReaderFactory, scopedProvider.GetService<RelationalObjectArrayValueReaderFactory>());
            Assert.Same(typedReaderFactory, scopedProvider.GetService<RelationalTypedValueReaderFactory>());
            Assert.Same(modificationCommandComparer, scopedProvider.GetService<ModificationCommandComparer>());
            Assert.Same(graphFactory, scopedProvider.GetService<GraphFactory>());

            Assert.Same(sqlServerSqlGenerator, scopedProvider.GetService<SqlServerSqlGenerator>());
            Assert.Same(sqlStatementExecutor, scopedProvider.GetService<SqlStatementExecutor>());
            Assert.Same(sqlTypeMapper, scopedProvider.GetService<SqlServerTypeMapper>());
            Assert.Same(batchPreparer, scopedProvider.GetService<SqlServerCommandBatchPreparer>());
            Assert.Same(sqlServerModificationCommandBatchFactory, scopedProvider.GetService<SqlServerModificationCommandBatchFactory>());
            Assert.Same(sqlServerMetadataExtensionProvider, scopedProvider.GetService<SqlServerMetadataExtensionProvider>());
            Assert.Same(sqlServerMigrationOperationFactory, scopedProvider.GetService<SqlServerMigrationOperationFactory>());

            // Scoped
            Assert.NotSame(sqlServerBatchExecutor, scopedProvider.GetService<SqlServerBatchExecutor>());
            Assert.NotSame(sqlServerDataStoreSource, scopedProvider.GetService<DataStoreSource>());
            Assert.NotSame(sqlServerDataStore, scopedProvider.GetService<SqlServerDataStore>());
            Assert.NotSame(sqlServerConnection, scopedProvider.GetService<SqlServerConnection>());
            Assert.NotSame(sqlServerMigrationOperationProcessor, scopedProvider.GetService<SqlServerMigrationOperationProcessor>());
            Assert.NotSame(modelDiffer, scopedProvider.GetService<SqlServerModelDiffer>());
            Assert.NotSame(serverMigrationOperationSqlGeneratorFactory, scopedProvider.GetService<SqlServerMigrationOperationSqlGeneratorFactory>());
            Assert.NotSame(sqlServerDataStoreCreator, scopedProvider.GetService<SqlServerDataStoreCreator>());
            Assert.NotSame(migrationAssembly, scopedProvider.GetService<MigrationAssembly>());
            Assert.NotSame(historyRepository, scopedProvider.GetService<HistoryRepository>());
            Assert.NotSame(sqlServerMigrator, scopedProvider.GetService<SqlServerMigrator>());

            context.Dispose();
        }
    }
}
