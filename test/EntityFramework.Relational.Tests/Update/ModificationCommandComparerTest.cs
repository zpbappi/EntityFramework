// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Data.Entity.ChangeTracking;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Relational.Update;
using Microsoft.Framework.DependencyInjection;
using Xunit;

namespace Microsoft.Data.Entity.Relational.Tests.Update
{
    public class ModificationCommandComparerTest
    {
        [Fact]
        public void Compare_returns_0_only_for_commands_that_are_equal()
        {
            var model = new Entity.Metadata.Model();
            var entityType = model.AddEntityType(typeof(object));

            var contextServices = ((IDbContextServices)new DbContext(
                new DbContextOptions()
                .UseModel(model)
                .UseInMemoryStore(persist: false))).ScopedServiceProvider;
            var stateManager = contextServices.GetRequiredService<StateManager>();

            var key = entityType.GetOrAddProperty("Id", typeof(int), shadowProperty: true);
            entityType.GetOrSetPrimaryKey(key);

            var stateEntry1 = stateManager.GetOrCreateEntry(new object());
            stateEntry1[key] = 0;
            stateEntry1.EntityState = EntityState.Added;
            var modificationCommandAdded = new ModificationCommand(new SchemaQualifiedName("A"), new ParameterNameGenerator(), p => p.Relational());
            modificationCommandAdded.AddStateEntry(stateEntry1);

            var stateEntry2 = stateManager.GetOrCreateEntry(new object());
            stateEntry2[key] = 1;
            stateEntry2.EntityState = EntityState.Modified;
            var modificationCommandModified = new ModificationCommand(new SchemaQualifiedName("A"), new ParameterNameGenerator(), p => p.Relational());
            modificationCommandModified.AddStateEntry(stateEntry2);

            var stateEntry3 = stateManager.GetOrCreateEntry(new object());
            stateEntry3[key] = 2;
            stateEntry3.EntityState = EntityState.Deleted;
            var modificationCommandDeleted = new ModificationCommand(new SchemaQualifiedName("A"), new ParameterNameGenerator(), p => p.Relational());
            modificationCommandDeleted.AddStateEntry(stateEntry3);

            var mCC = new ModificationCommandComparer();

            Assert.True(0 == mCC.Compare(modificationCommandAdded, modificationCommandAdded));
            Assert.True(0 == mCC.Compare(null, null));
            Assert.True(0 == mCC.Compare(
                new ModificationCommand(new SchemaQualifiedName("A", "dbo"), new ParameterNameGenerator(), p => p.Relational()),
                new ModificationCommand(new SchemaQualifiedName("A", "dbo"), new ParameterNameGenerator(), p => p.Relational())));

            Assert.True(0 > mCC.Compare(null, new ModificationCommand(new SchemaQualifiedName("A"), new ParameterNameGenerator(), p => p.Relational())));
            Assert.True(0 < mCC.Compare(new ModificationCommand(new SchemaQualifiedName("A"), new ParameterNameGenerator(), p => p.Relational()), null));

            Assert.True(0 > mCC.Compare(
                new ModificationCommand(new SchemaQualifiedName("A"), new ParameterNameGenerator(), p => p.Relational()),
                new ModificationCommand(new SchemaQualifiedName("A", "dbo"), new ParameterNameGenerator(), p => p.Relational())));
            Assert.True(0 < mCC.Compare(
                new ModificationCommand(new SchemaQualifiedName("A", "dbo"), new ParameterNameGenerator(), p => p.Relational()),
                new ModificationCommand(new SchemaQualifiedName("A"), new ParameterNameGenerator(), p => p.Relational())));

            Assert.True(0 > mCC.Compare(
                new ModificationCommand(new SchemaQualifiedName("A", "dbo"), new ParameterNameGenerator(), p => p.Relational()),
                new ModificationCommand(new SchemaQualifiedName("A", "foo"), new ParameterNameGenerator(), p => p.Relational())));
            Assert.True(0 < mCC.Compare(
                new ModificationCommand(new SchemaQualifiedName("A", "foo"), new ParameterNameGenerator(), p => p.Relational()),
                new ModificationCommand(new SchemaQualifiedName("A", "dbo"), new ParameterNameGenerator(), p => p.Relational())));

            Assert.True(0 > mCC.Compare(
                new ModificationCommand(new SchemaQualifiedName("A"), new ParameterNameGenerator(), p => p.Relational()),
                new ModificationCommand(new SchemaQualifiedName("B"), new ParameterNameGenerator(), p => p.Relational())));
            Assert.True(0 < mCC.Compare(
                new ModificationCommand(new SchemaQualifiedName("B"), new ParameterNameGenerator(), p => p.Relational()),
                new ModificationCommand(new SchemaQualifiedName("A"), new ParameterNameGenerator(), p => p.Relational())));

            Assert.True(0 > mCC.Compare(modificationCommandModified, modificationCommandAdded));
            Assert.True(0 < mCC.Compare(modificationCommandAdded, modificationCommandModified));

            Assert.True(0 > mCC.Compare(modificationCommandDeleted, modificationCommandAdded));
            Assert.True(0 < mCC.Compare(modificationCommandAdded, modificationCommandDeleted));

            Assert.True(0 > mCC.Compare(modificationCommandDeleted, modificationCommandModified));
            Assert.True(0 < mCC.Compare(modificationCommandModified, modificationCommandDeleted));
        }
    }
}
