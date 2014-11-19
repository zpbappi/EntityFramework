// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Data.Entity.Internal;
using Microsoft.Data.Entity.Metadata;
using Xunit;

namespace Microsoft.Data.Entity.Tests.ChangeTracking
{
    public class MixedStateEntryTest : StateEntryTest
    {
        [Fact]
        public void Can_get_entity()
        {
            var model = BuildModel();
            var configuration = TestHelpers.CreateContextServices(model);

            var entity = new SomeEntity();
            var entry = CreateStateEntry(configuration, model.GetEntityType(typeof(SomeEntity).FullName), entity);

            Assert.Same(entity, entry.Entity);
        }

        [Fact]
        public void Can_set_and_get_property_value_from_CLR_object()
        {
            var model = BuildModel();
            var entityType = model.GetEntityType(typeof(SomeEntity).FullName);
            var keyProperty = entityType.GetProperty("Id");
            var nonKeyProperty = entityType.GetProperty("Name");
            var configuration = TestHelpers.CreateContextServices(model);

            var entity = new SomeEntity { Id = 77, Name = "Magic Tree House" };
            var entry = CreateStateEntry(configuration, entityType, entity);

            Assert.Null(entry[keyProperty]); // In shadow
            Assert.Equal("Magic Tree House", entry[nonKeyProperty]);

            entry[keyProperty] = 78;
            entry[nonKeyProperty] = "Normal Tree House";

            Assert.Equal(77, entity.Id); // In shadow
            Assert.Equal("Normal Tree House", entity.Name);
        }

        [Fact]
        public void Asking_for_entity_instance_causes_it_to_be_materialized()
        {
            var model = BuildModel();
            var entityType = model.GetEntityType(typeof(SomeEntity).FullName);
            var configuration = TestHelpers.CreateContextServices(model);

            var entry = CreateStateEntry(configuration, entityType, new ObjectArrayValueReader(new object[] { 1, "Kool" }));

            var entity = (SomeEntity)entry.Entity;

            Assert.Equal("Kool", entity.Name);
        }

        [Fact]
        public void All_original_values_can_be_accessed_for_entity_that_does_no_notifiction()
        {
            var model = BuildModel();
            var entityType = model.GetEntityType(typeof(SomeEntity).FullName);

            AllOriginalValuesTest(model, entityType);
        }

        [Fact]
        public void All_original_values_can_be_accessed_for_entity_that_does_changed_only_notifictions_if_eager_values_on()
        {
            var model = BuildModel();
            var entityType = model.GetEntityType(typeof(ChangedOnlyEntity).FullName);
            entityType.UseLazyOriginalValues = false;

            AllOriginalValuesTest(model, entityType);
        }

        [Fact]
        public void Setting_CLR_property_with_snapshot_change_tracking_requires_DetectChanges()
        {
            SetPropertyClrTest<SomeEntity>(needsDetectChanges: true);
        }

        [Fact]
        public void Setting_CLR_property_with_changed_only_notifications_does_not_require_DetectChanges()
        {
            SetPropertyClrTest<ChangedOnlyEntity>(needsDetectChanges: false);
        }

        [Fact]
        public void Setting_CLR_property_with_full_notifications_does_not_require_DetectChanges()
        {
            SetPropertyClrTest<FullNotificationEntity>(needsDetectChanges: false);
        }

        [Fact]
        public void Original_values_are_not_tracked_unless_needed_by_default_for_properties_of_full_notifications_entity()
        {
            var model = BuildModel();
            var entityType = model.GetEntityType(typeof(FullNotificationEntity).FullName);
            var idProperty = entityType.GetProperty("Id");
            var configuration = TestHelpers.CreateContextServices(model);

            var entry = CreateStateEntry(configuration, entityType, new ObjectArrayValueReader(new object[] { 1, "Kool" }));

            Assert.Equal(
                Strings.OriginalValueNotTracked("Id", typeof(FullNotificationEntity).FullName),
                Assert.Throws<InvalidOperationException>(() => entry.OriginalValues[idProperty] = 1).Message);

            Assert.Equal(
                Strings.OriginalValueNotTracked("Id", typeof(FullNotificationEntity).FullName),
                Assert.Throws<InvalidOperationException>(() => entry.OriginalValues[idProperty]).Message);
        }

        protected override Model BuildModel()
        {
            var model = new Model();

            var entityType1 = model.AddEntityType(typeof(SomeEntity));
            var key1 = entityType1.GetOrAddProperty("Id", typeof(int), shadowProperty: true);
            key1.GenerateValueOnAdd = true;
            entityType1.GetOrSetPrimaryKey(key1);
            entityType1.GetOrAddProperty("Name", typeof(string));

            var entityType2 = model.AddEntityType(typeof(SomeDependentEntity));
            var key2a = entityType2.GetOrAddProperty("Id1", typeof(int));
            var key2b = entityType2.GetOrAddProperty("Id2", typeof(string));
            entityType2.GetOrSetPrimaryKey(new[] { key2a, key2b });
            var fk = entityType2.GetOrAddProperty("SomeEntityId", typeof(int), shadowProperty: true);
            entityType2.GetOrAddForeignKey(new[] { fk }, entityType1.GetPrimaryKey());
            var justAProperty = entityType2.GetOrAddProperty("JustAProperty", typeof(int));
            justAProperty.GenerateValueOnAdd = true;

            var entityType3 = model.AddEntityType(typeof(FullNotificationEntity));
            entityType3.GetOrSetPrimaryKey(entityType3.GetOrAddProperty("Id", typeof(int), shadowProperty: true));
            entityType3.GetOrAddProperty("Name", typeof(string)).IsConcurrencyToken = true;

            var entityType4 = model.AddEntityType(typeof(ChangedOnlyEntity));
            entityType4.GetOrSetPrimaryKey(entityType4.GetOrAddProperty("Id", typeof(int), shadowProperty: true));
            entityType4.GetOrAddProperty("Name", typeof(string)).IsConcurrencyToken = true;

            var entityType5 = model.AddEntityType(typeof(SomeMoreDependentEntity));
            var key5 = entityType5.GetOrAddProperty("Id", typeof(int));
            entityType5.GetOrSetPrimaryKey(key5);
            var fk5a = entityType5.GetOrAddProperty("Fk1", typeof(int));
            var fk5b = entityType5.GetOrAddProperty("Fk2", typeof(string));
            entityType5.GetOrAddForeignKey(new[] { fk5a, fk5b }, entityType2.GetPrimaryKey());

            return model;
        }
    }
}
