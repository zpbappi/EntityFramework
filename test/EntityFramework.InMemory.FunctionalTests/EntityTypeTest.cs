// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Entity.Metadata;
using Xunit;

namespace Microsoft.Data.Entity.InMemory.FunctionalTests
{
    public class EntityTypeTest : IClassFixture<InMemoryFixture>
    {
        [Fact]
        public void Can_use_different_entity_types_end_to_end()
        {
            Can_add_update_delete_end_to_end<Private>();
            Can_add_update_delete_end_to_end<object>();
            Can_add_update_delete_end_to_end<List<Private>>();
        }

        private class Private
        {
        }

        private void Can_add_update_delete_end_to_end<T>()
            where T : class
        {
            var type = typeof(T);
            var model = new Model();

            var entityType = model.AddEntityType(type);
            var idProperty = entityType.GetOrAddProperty("Id", typeof(int), shadowProperty: true);
            var nameProperty = entityType.GetOrAddProperty("Name", typeof(string), shadowProperty: true);
            entityType.GetOrSetPrimaryKey(idProperty);

            var options = new DbContextOptions()
                .UseModel(model)
                .UseInMemoryStore();

            T entity;
            using (var context = new DbContext(_fixture.ServiceProvider, options))
            {
                var stateEntry = context.ChangeTracker.StateManager.CreateNewEntry(entityType);
                entity = (T)stateEntry.Entity;
                
                stateEntry[idProperty] = 42;
                stateEntry[nameProperty] = "The";

                stateEntry.EntityState = EntityState.Added;

                context.SaveChanges();
            }

            using (var context = new DbContext(_fixture.ServiceProvider, options))
            {
                var entityFromStore = context.Set<T>().Single();
                var entityEntry = context.ChangeTracker.Entry(entityFromStore);

                Assert.NotSame(entity, entityFromStore);
                Assert.Equal(42, entityEntry.Property(idProperty.Name).CurrentValue);
                Assert.Equal("The", entityEntry.Property(nameProperty.Name).CurrentValue);

                entityEntry.StateEntry[nameProperty] = "A";

                context.Update(entityFromStore);

                context.SaveChanges();
            }

            using (var context = new DbContext(_fixture.ServiceProvider, options))
            {
                var entityFromStore = context.Set<T>().Single();
                var entry = context.ChangeTracker.Entry(entityFromStore);

                Assert.Equal("A", entry.Property(nameProperty.Name).CurrentValue);

                context.Delete(entityFromStore);

                context.SaveChanges();
            }

            using (var context = new DbContext(_fixture.ServiceProvider, options))
            {
                Assert.Equal(0, context.Set<T>().Count());
            }
        }

        private readonly InMemoryFixture _fixture;

        public EntityTypeTest(InMemoryFixture fixture)
        {
            _fixture = fixture;
        }
    }
}
