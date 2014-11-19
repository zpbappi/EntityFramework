// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Entity.ChangeTracking;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Tests;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.DependencyInjection.Fallback;
using Microsoft.Framework.Logging;
using Moq;
using Xunit;

namespace Microsoft.Data.Entity.InMemory.Tests
{
    public class InMemoryDataStoreTest
    {
        [Fact]
        public void Uses_persistent_database_by_default()
        {
            var serviceProvider = TestHelpers.CreateServiceProvider();

            var store1 = CreateScopedServices(CreateModel(), serviceProvider).GetRequiredService<InMemoryDataStore>();
            var store2 = CreateScopedServices(CreateModel(), serviceProvider).GetRequiredService<InMemoryDataStore>();

            Assert.Same(store1.Database, store2.Database);
        }

        [Fact]
        public void Uses_persistent_database_if_configured_as_persistent()
        {
            var serviceProvider = TestHelpers.CreateServiceProvider();

            Assert.Same(
                CreateStore(serviceProvider, persist: true).Database,
                CreateStore(serviceProvider, persist: true).Database);
        }

        [Fact]
        public void Uses_transient_database_if_not_configured_as_persistent()
        {
            var serviceProvider = TestHelpers.CreateServiceProvider();

            Assert.NotSame(
                CreateStore(serviceProvider, persist: false).Database,
                CreateStore(serviceProvider, persist: false).Database);
        }

        [Fact]
        public void EnsureDatabaseCreated_returns_true_for_first_use_of_persistent_database_and_false_thereafter()
        {
            var serviceProvider = TestHelpers.CreateServiceProvider();
            var model = CreateModel();
            var store = CreateStore(serviceProvider, persist: true);

            Assert.True(store.EnsureDatabaseCreated(model));
            Assert.False(store.EnsureDatabaseCreated(model));
            Assert.False(store.EnsureDatabaseCreated(model));

            store = CreateStore(serviceProvider, persist: true);

            Assert.False(store.EnsureDatabaseCreated(model));
        }

        [Fact]
        public void EnsureDatabaseCreated_returns_true_for_first_use_of_non_persistent_database_and_false_thereafter()
        {
            var serviceProvider = TestHelpers.CreateServiceProvider();
            var model = CreateModel();
            var store = CreateStore(serviceProvider, persist: false);

            Assert.True(store.EnsureDatabaseCreated(model));
            Assert.False(store.EnsureDatabaseCreated(model));
            Assert.False(store.EnsureDatabaseCreated(model));

            store = CreateStore(serviceProvider, persist: false);

            Assert.True(store.EnsureDatabaseCreated(model));
            Assert.False(store.EnsureDatabaseCreated(model));
            Assert.False(store.EnsureDatabaseCreated(model));
        }

        private static InMemoryDataStore CreateStore(IServiceProvider serviceProvider, bool persist)
        {
            var contextServices = ((IDbContextServices)new DbContext(serviceProvider, new DbContextOptions().UseInMemoryStore(persist: persist))).ScopedServiceProvider;

            return contextServices.GetRequiredService<InMemoryDataStore>();
        }

        [Fact]
        public async Task Save_changes_adds_new_objects_to_store()
        {
            var serviceProvider = CreateScopedServices(CreateModel());
            var customer = new Customer { Id = 42, Name = "Unikorn" };
            var entityEntry = serviceProvider.GetRequiredService<StateManager>().GetOrCreateEntry(customer);
            await entityEntry.SetEntityStateAsync(EntityState.Added);

            var inMemoryDataStore = serviceProvider.GetRequiredService<InMemoryDataStore>();

            await inMemoryDataStore.SaveChangesAsync(new[] { entityEntry });

            Assert.Equal(1, inMemoryDataStore.Database.SelectMany(t => t).Count());
            Assert.Equal(new object[] { 42, "Unikorn" }, inMemoryDataStore.Database.Single().Single());
        }

        [Fact]
        public async Task Save_changes_updates_changed_objects_in_store()
        {
            var serviceProvider = CreateScopedServices(CreateModel());

            var customer = new Customer { Id = 42, Name = "Unikorn" };
            var entityEntry = serviceProvider.GetRequiredService<StateManager>().GetOrCreateEntry(customer);
            await entityEntry.SetEntityStateAsync(EntityState.Added);

            var inMemoryDataStore = serviceProvider.GetRequiredService<InMemoryDataStore>();

            await inMemoryDataStore.SaveChangesAsync(new[] { entityEntry });

            customer.Name = "Unikorn, The Return";
            await entityEntry.SetEntityStateAsync(EntityState.Modified);

            await inMemoryDataStore.SaveChangesAsync(new[] { entityEntry });

            Assert.Equal(1, inMemoryDataStore.Database.SelectMany(t => t).Count());
            Assert.Equal(new object[] { 42, "Unikorn, The Return" }, inMemoryDataStore.Database.Single().Single());
        }

        [Fact]
        public async Task Save_changes_removes_deleted_objects_from_store()
        {
            var serviceProvider = CreateScopedServices(CreateModel());

            var customer = new Customer { Id = 42, Name = "Unikorn" };
            var entityEntry = serviceProvider.GetRequiredService<StateManager>().GetOrCreateEntry(customer);
            await entityEntry.SetEntityStateAsync(EntityState.Added);

            var inMemoryDataStore = serviceProvider.GetRequiredService<InMemoryDataStore>();

            await inMemoryDataStore.SaveChangesAsync(new[] { entityEntry });

            // Because the data store is being used directly the entity state must be manually changed after saving.
            await entityEntry.SetEntityStateAsync(EntityState.Unchanged);

            customer.Name = "Unikorn, The Return";
            await entityEntry.SetEntityStateAsync(EntityState.Deleted);

            await inMemoryDataStore.SaveChangesAsync(new[] { entityEntry });

            Assert.Equal(0, inMemoryDataStore.Database.SelectMany(t => t).Count());
        }

        private static IServiceProvider CreateScopedServices(IModel model, IServiceProvider serviceProvider = null)
        {
            return ((IDbContextServices)new DbContext(
                serviceProvider ?? TestHelpers.CreateServiceProvider(),
                new DbContextOptions().UseModel(model).UseInMemoryStore())).ScopedServiceProvider;
        }

        [Fact]
        public async Task Should_log_writes()
        {
            var mockLogger = new Mock<ILogger>();
            mockLogger.Setup(l => l.IsEnabled(LogLevel.Information)).Returns(true);

            var mockFactory = new Mock<ILoggerFactory>();
            mockFactory.Setup(m => m.Create(It.IsAny<string>())).Returns(mockLogger.Object);

            var serviceProvider = new ServiceCollection()
                .AddEntityFramework()
                .AddInMemoryStore()
                .ServiceCollection
                .AddInstance(mockFactory.Object)
                .BuildServiceProvider();

            var scopedServices = CreateScopedServices(CreateModel(), serviceProvider);

            var customer = new Customer { Id = 42, Name = "Unikorn" };
            var entityEntry = scopedServices.GetRequiredService<StateManager>().GetOrCreateEntry(customer);
            await entityEntry.SetEntityStateAsync(EntityState.Added);

            var inMemoryDataStore = scopedServices.GetRequiredService<InMemoryDataStore>();

            await inMemoryDataStore.SaveChangesAsync(new[] { entityEntry });

            mockLogger.Verify(
                l => l.Write(
                    LogLevel.Information,
                    0,
                    It.IsAny<string>(),
                    null,
                    It.IsAny<Func<object, Exception, string>>()),
                Times.Once);
        }

        private static IModel CreateModel()
        {
            var model = new Model();
            var modelBuilder = new BasicModelBuilder(model);

            modelBuilder.Entity<Customer>(b =>
            {
                b.Key(c => c.Id);
                b.Property(c => c.Name);
            });

            return model;
        }

        private class Customer
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}
