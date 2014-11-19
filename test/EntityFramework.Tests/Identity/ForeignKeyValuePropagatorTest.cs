// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Data.Entity.ChangeTracking;
using Microsoft.Data.Entity.Identity;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Framework.DependencyInjection;
using Xunit;

namespace Microsoft.Data.Entity.Tests.Identity
{
    public class ForeignKeyValuePropagatorTest
    {
        [Fact]
        public void Foreign_key_value_is_obtained_from_reference_to_principal()
        {
            var model = BuildModel();

            var principal = new Category { Id = 11 };
            var dependent = new Product { Id = 21, Category = principal };

            var dependentEntry = CreateContextServices(model).GetRequiredService<StateManager>().GetOrCreateEntry(dependent);
            var property = model.GetEntityType(typeof(Product)).GetProperty("CategoryId");

            CreateValueGenerator().PropagateValue(dependentEntry, property);

            Assert.Equal(11, dependentEntry[property]);
        }

        [Fact]
        public void Foreign_key_value_is_obtained_from_tracked_principal_with_populated_collection()
        {
            var model = BuildModel();
            var manager = CreateContextServices(model).GetRequiredService<StateManager>();

            var principal = new Category { Id = 11 };
            var dependent = new Product { Id = 21 };
            principal.Products.Add(dependent);

            manager.StartTracking(manager.GetOrCreateEntry(principal));
            var dependentEntry = manager.GetOrCreateEntry(dependent);
            var property = model.GetEntityType(typeof(Product)).GetProperty("CategoryId");

            CreateValueGenerator().PropagateValue(dependentEntry, property);

            Assert.Equal(11, dependentEntry[property]);
        }

        [Fact]
        public void One_to_one_foreign_key_value_is_obtained_from_reference_to_principal()
        {
            var model = BuildModel();

            var principal = new Product { Id = 21 };
            var dependent = new ProductDetail { Product = principal };

            var dependentEntry = CreateContextServices(model).GetRequiredService<StateManager>().GetOrCreateEntry(dependent);
            var property = model.GetEntityType(typeof(ProductDetail)).GetProperty("Id");

            CreateValueGenerator().PropagateValue(dependentEntry, property);

            Assert.Equal(21, dependentEntry[property]);
        }

        [Fact]
        public void One_to_one_foreign_key_value_is_obtained_from_tracked_principal()
        {
            var model = BuildModel();
            var manager = CreateContextServices(model).GetRequiredService<StateManager>();

            var dependent = new ProductDetail();
            var principal = new Product { Id = 21, Detail = dependent };

            manager.StartTracking(manager.GetOrCreateEntry(principal));
            var dependentEntry = manager.GetOrCreateEntry(dependent);
            var property = model.GetEntityType(typeof(ProductDetail)).GetProperty("Id");

            CreateValueGenerator().PropagateValue(dependentEntry, property);

            Assert.Equal(21, dependentEntry[property]);
        }

        [Fact]
        public void Composite_foreign_key_value_is_obtained_from_reference_to_principal()
        {
            var model = BuildModel();

            var principal = new OrderLine { OrderId = 11, ProductId = 21 };
            var dependent = new OrderLineDetail { OrderLine = principal };

            var dependentEntry = CreateContextServices(model).GetRequiredService<StateManager>().GetOrCreateEntry(dependent);
            var property1 = model.GetEntityType(typeof(OrderLineDetail)).GetProperty("OrderId");
            var property2 = model.GetEntityType(typeof(OrderLineDetail)).GetProperty("ProductId");

            CreateValueGenerator().PropagateValue(dependentEntry, property1);
            CreateValueGenerator().PropagateValue(dependentEntry, property2);

            Assert.Equal(11, dependentEntry[property1]);
            Assert.Equal(21, dependentEntry[property2]);
        }

        [Fact]
        public void Composite_foreign_key_value_is_obtained_from_tracked_principal()
        {
            var model = BuildModel();
            var manager = CreateContextServices(model).GetRequiredService<StateManager>();

            var dependent = new OrderLineDetail();
            var principal = new OrderLine { OrderId = 11, ProductId = 21, Detail = dependent };

            manager.StartTracking(manager.GetOrCreateEntry(principal));
            var dependentEntry = manager.GetOrCreateEntry(dependent);
            var property1 = model.GetEntityType(typeof(OrderLineDetail)).GetProperty("OrderId");
            var property2 = model.GetEntityType(typeof(OrderLineDetail)).GetProperty("ProductId");

            CreateValueGenerator().PropagateValue(dependentEntry, property1);
            CreateValueGenerator().PropagateValue(dependentEntry, property2);

            Assert.Equal(11, dependentEntry[property1]);
            Assert.Equal(21, dependentEntry[property2]);
        }

        private static IServiceProvider CreateContextServices(IModel model = null)
        {
            return TestHelpers.CreateContextServices(model ?? BuildModel());
        }

        private class Category
        {
            private readonly ICollection<Product> _products = new List<Product>();

            public int Id { get; set; }

            public ICollection<Product> Products
            {
                get { return _products; }
            }
        }

        private class Product
        {
            private readonly ICollection<OrderLine> _orderLines = new List<OrderLine>();

            public int Id { get; set; }

            public int CategoryId { get; set; }
            public Category Category { get; set; }

            public ProductDetail Detail { get; set; }

            public ICollection<OrderLine> OrderLines
            {
                get { return _orderLines; }
            }
        }

        private class ProductDetail
        {
            public int Id { get; set; }

            public Product Product { get; set; }
        }

        private class Order
        {
            private readonly ICollection<OrderLine> _orderLines = new List<OrderLine>();

            public int Id { get; set; }

            public ICollection<OrderLine> OrderLines
            {
                get { return _orderLines; }
            }
        }

        private class OrderLine
        {
            public int OrderId { get; set; }
            public int ProductId { get; set; }

            public virtual Order Order { get; set; }
            public virtual Product Product { get; set; }

            public virtual OrderLineDetail Detail { get; set; }
        }

        private class OrderLineDetail
        {
            public int OrderId { get; set; }
            public int ProductId { get; set; }

            public virtual OrderLine OrderLine { get; set; }
        }

        private static IModel BuildModel()
        {
            var model = new Model();
            var builder = new ModelBuilder(model);

            builder.Entity<Product>(b =>
            {
                b.OneToMany(e => e.OrderLines, e => e.Product);
                b.OneToOne(e => e.Detail, e => e.Product);
            });

            builder.Entity<Category>().OneToMany(e => e.Products, e => e.Category);

            builder.Entity<ProductDetail>();

            builder.Entity<Order>().OneToMany(e => e.OrderLines, e => e.Order);

            builder.Entity<OrderLineDetail>().Key(e => new { e.OrderId, e.ProductId });

            builder.Entity<OrderLine>(b =>
            {
                b.Key(e => new { e.OrderId, e.ProductId });
                b.OneToOne(e => e.Detail, e => e.OrderLine);
            });

            return model;
        }

        private static ForeignKeyValuePropagator CreateValueGenerator()
        {
            return new ForeignKeyValuePropagator(new ClrPropertyGetterSource(), new ClrCollectionAccessorSource(new CollectionTypeFactory()));
        }
    }
}
