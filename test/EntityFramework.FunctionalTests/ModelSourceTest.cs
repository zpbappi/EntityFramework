// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.DependencyInjection.Fallback;
using Xunit;

namespace Microsoft.Data.Entity.FunctionalTests
{
    public class ModelSourceTest
    {
        [Fact] // Issue #943
        public void Can_replace_ModelSource_without_access_to_internals()
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFramework()
                .AddInMemoryStore()
                .AddDbContext<JustSomeContext>()
                .ServiceCollection
                .AddSingleton<IModelSource, MyModelSource>()
                .BuildServiceProvider();

            using (var context = serviceProvider.GetRequiredService<JustSomeContext>())
            {
                var model = context.Model;

                Assert.Equal("Us!", model["AllYourModelAreBelongTo"]);
                Assert.Equal("Us!", model.EntityTypes.Single(e => e.SimpleName == "Base")["AllYourBaseAreBelongTo"]);
                Assert.Contains("Peak", model.EntityTypes.Select(e => e.SimpleName));
            }
        }

        private class MyModelSource : IModelSource
        {
            private readonly ThreadSafeDictionaryCache<Type, IModel> _models = new ThreadSafeDictionaryCache<Type, IModel>();

            private readonly DbSetFinder _setFinder;

            public MyModelSource(DbSetFinder setFinder)
            {
                _setFinder = setFinder;
            }

            public virtual IModel GetModel(DbContext context, IModelBuilderFactory modelBuilderFactory)
            {
                return _models.GetOrAdd(context.GetType(), k => CreateModel(context, modelBuilderFactory));
            }

            private IModel CreateModel(DbContext context, IModelBuilderFactory modelBuilderFactory)
            {
                var model = new Model();
                var modelBuilder = modelBuilderFactory.CreateConventionBuilder(model);

                foreach (var setInfo in _setFinder.FindSets(context))
                {
                    modelBuilder.Entity(setInfo.EntityType);
                }

                ModelSourceHelpers.OnModelCreating(context, modelBuilder);

                model["AllYourModelAreBelongTo"] = "Us!";

                return model;
            }
        }

        private class JustSomeContext : DbContext
        {
            public JustSomeContext(IServiceProvider serviceProvider)
                : base(serviceProvider)
            {
            }

            public DbSet<Peak> Peaks { get; set; }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Base>().Annotation("AllYourBaseAreBelongTo", "Us!");
            }
        }

        private class Base
        {
            public int Id { get; set; }
        }

        private class Peak
        {
            public int Id { get; set; }
        }
    }
}
