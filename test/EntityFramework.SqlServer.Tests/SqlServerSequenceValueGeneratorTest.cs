// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Relational;
using Microsoft.Data.Entity.Storage;
using Microsoft.Data.Entity.Tests;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.DependencyInjection.Fallback;
using Microsoft.Framework.Logging;
using Xunit;

namespace Microsoft.Data.Entity.SqlServer.Tests
{
    public class SqlServerSequenceValueGeneratorTest
    {
        private static readonly Model _model = TestHelpers.BuildModelFor<AnEntity>();

        [Fact]
        public void Generates_sequential_values()
        {
            var storeServices = CreateStoreServices();
            var entityType = _model.GetEntityType(typeof(AnEntity));

            var intProperty = entityType.GetProperty("Id");
            var longProperty = entityType.GetProperty("Long");
            var shortProperty = entityType.GetProperty("Short");
            var byteProperty = entityType.GetProperty("Byte");
            var nullableIntProperty = entityType.GetProperty("NullableId");
            var nullableLongProperty = entityType.GetProperty("NullableLong");
            var nullableShortProperty = entityType.GetProperty("NullableShort");
            var nullableByteProperty = entityType.GetProperty("NullableByte");

            var executor = new FakeSqlStatementExecutor(10);
            var generator = new SqlServerSequenceValueGenerator(executor, "Foo", 10);

            for (var i = 0; i < 15; i++)
            {
                var generatedValue = generator.Next(intProperty, storeServices);

                Assert.Equal(i, generatedValue.Value);
                Assert.False(generatedValue.IsTemporary);
            }

            for (var i = 15; i < 30; i++)
            {
                var generatedValue = generator.Next(longProperty, storeServices);

                Assert.Equal((long)i, generatedValue.Value);
                Assert.False(generatedValue.IsTemporary);
            }

            for (var i = 30; i < 45; i++)
            {
                var generatedValue = generator.Next(shortProperty, storeServices);

                Assert.Equal((short)i, generatedValue.Value);
                Assert.False(generatedValue.IsTemporary);
            }

            for (var i = 45; i < 60; i++)
            {
                var generatedValue = generator.Next(byteProperty, storeServices);

                Assert.Equal((byte)i, generatedValue.Value);
                Assert.False(generatedValue.IsTemporary);
            }

            for (var i = 60; i < 75; i++)
            {
                var generatedValue = generator.Next(nullableIntProperty, storeServices);

                Assert.Equal((int?)i, generatedValue.Value);
                Assert.False(generatedValue.IsTemporary);
            }

            for (var i = 75; i < 90; i++)
            {
                var generatedValue = generator.Next(nullableLongProperty, storeServices);

                Assert.Equal((long?)i, generatedValue.Value);
                Assert.False(generatedValue.IsTemporary);
            }

            for (var i = 90; i < 105; i++)
            {
                var generatedValue = generator.Next(nullableShortProperty, storeServices);

                Assert.Equal((short?)i, generatedValue.Value);
                Assert.False(generatedValue.IsTemporary);
            }

            for (var i = 105; i < 120; i++)
            {
                var generatedValue = generator.Next(nullableByteProperty, storeServices);

                Assert.Equal((byte?)i, generatedValue.Value);
                Assert.False(generatedValue.IsTemporary);
            }
        }

        [Fact]
        public async Task Generates_sequential_values_async()
        {
            var storeServices = CreateStoreServices();
            var entityType = _model.GetEntityType(typeof(AnEntity));

            var intProperty = entityType.GetProperty("Id");
            var longProperty = entityType.GetProperty("Long");
            var shortProperty = entityType.GetProperty("Short");
            var byteProperty = entityType.GetProperty("Byte");
            var nullableIntProperty = entityType.GetProperty("NullableId");
            var nullableLongProperty = entityType.GetProperty("NullableLong");
            var nullableShortProperty = entityType.GetProperty("NullableShort");
            var nullableByteProperty = entityType.GetProperty("NullableByte");

            var executor = new FakeSqlStatementExecutor(10);
            var generator = new SqlServerSequenceValueGenerator(executor, "Foo", 10);

            for (var i = 0; i < 15; i++)
            {
                var generatedValue = await generator.NextAsync(intProperty, storeServices);

                Assert.Equal(i, generatedValue.Value);
                Assert.False(generatedValue.IsTemporary);
            }

            for (var i = 15; i < 30; i++)
            {
                var generatedValue = await generator.NextAsync(longProperty, storeServices);

                Assert.Equal((long)i, generatedValue.Value);
                Assert.False(generatedValue.IsTemporary);
            }

            for (var i = 30; i < 45; i++)
            {
                var generatedValue = await generator.NextAsync(shortProperty, storeServices);

                Assert.Equal((short)i, generatedValue.Value);
                Assert.False(generatedValue.IsTemporary);
            }

            for (var i = 45; i < 60; i++)
            {
                var generatedValue = await generator.NextAsync(byteProperty, storeServices);

                Assert.Equal((byte)i, generatedValue.Value);
                Assert.False(generatedValue.IsTemporary);
            }

            for (var i = 60; i < 75; i++)
            {
                var generatedValue = await generator.NextAsync(nullableIntProperty, storeServices);

                Assert.Equal((int?)i, generatedValue.Value);
                Assert.False(generatedValue.IsTemporary);
            }

            for (var i = 75; i < 90; i++)
            {
                var generatedValue = await generator.NextAsync(nullableLongProperty, storeServices);

                Assert.Equal((long?)i, generatedValue.Value);
                Assert.False(generatedValue.IsTemporary);
            }

            for (var i = 90; i < 105; i++)
            {
                var generatedValue = await generator.NextAsync(nullableShortProperty, storeServices);

                Assert.Equal((short?)i, generatedValue.Value);
                Assert.False(generatedValue.IsTemporary);
            }

            for (var i = 105; i < 120; i++)
            {
                var generatedValue = await generator.NextAsync(nullableByteProperty, storeServices);

                Assert.Equal((byte?)i, generatedValue.Value);
                Assert.False(generatedValue.IsTemporary);
            }
        }

        [Fact]
        public void Multiple_threads_can_use_the_same_generator()
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFramework()
                .AddSqlServer().ServiceCollection
                .BuildServiceProvider();

            var property = _model.GetEntityType(typeof(AnEntity)).GetProperty("Long");

            var executor = new FakeSqlStatementExecutor(10);
            var generator = new SqlServerSequenceValueGenerator(executor, "Foo", 10);

            const int threadCount = 50;
            const int valueCount = 35;

            var tests = new Action[threadCount];
            var generatedValues = new List<long>[threadCount];
            for (var i = 0; i < tests.Length; i++)
            {
                var testNumber = i;
                generatedValues[testNumber] = new List<long>();
                tests[testNumber] = () =>
                {
                    for (var j = 0; j < valueCount; j++)
                    {
                        var storeServices = CreateStoreServices(serviceProvider);

                        var generatedValue = generator.Next(property, storeServices);

                        generatedValues[testNumber].Add((long)generatedValue.Value);
                    }
                };
            }

            Parallel.Invoke(tests);

            // Check that each value was generated once and only once
            var checks = new bool[threadCount * valueCount];
            foreach (var values in generatedValues)
            {
                Assert.Equal(valueCount, values.Count);
                foreach (var value in values)
                {
                    checks[value] = true;
                }
            }

            Assert.True(checks.All(c => c));
        }

        [Fact]
        public async Task Multiple_threads_can_use_the_same_generator_async()
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFramework()
                .AddSqlServer().ServiceCollection
                .BuildServiceProvider();

            var property = _model.GetEntityType(typeof(AnEntity)).GetProperty("Long");

            var executor = new FakeSqlStatementExecutor(10);
            var generator = new SqlServerSequenceValueGenerator(executor, "Foo", 10);

            const int threadCount = 50;
            const int valueCount = 35;

            var tests = new Func<Task>[threadCount];
            var generatedValues = new List<long>[threadCount];
            for (var i = 0; i < tests.Length; i++)
            {
                var testNumber = i;
                generatedValues[testNumber] = new List<long>();
                tests[testNumber] = async () =>
                {
                    for (var j = 0; j < valueCount; j++)
                    {
                        var storeServices = CreateStoreServices(serviceProvider);

                        var generatedValue = await generator.NextAsync(property, storeServices);

                        generatedValues[testNumber].Add((long)generatedValue.Value);
                    }
                };
            }

            var tasks = tests.Select(Task.Run).ToArray();

            foreach (var t in tasks)
            {
                await t;
            }

            // Check that each value was generated once and only once
            var checks = new bool[threadCount * valueCount];
            foreach (var values in generatedValues)
            {
                Assert.Equal(valueCount, values.Count);
                foreach (var value in values)
                {
                    checks[value] = true;
                }
            }

            Assert.True(checks.All(c => c));
        }

        private LazyRef<DataStoreServices> CreateStoreServices(IServiceProvider serviceProvider = null)
        {
            serviceProvider = serviceProvider ?? new ServiceCollection()
                .AddEntityFramework()
                .AddSqlServer().ServiceCollection
                .BuildServiceProvider();

            var contextServices = ((IDbContextServices)new DbContext(
                serviceProvider,
                new DbContextOptions()
                    .UseModel(_model)
                    .UseSqlServer(new SqlConnection()))).ScopedServiceProvider;

            return contextServices.GetRequiredService<LazyRef<DataStoreServices>>();
        }

        private class FakeSqlStatementExecutor : SqlStatementExecutor
        {
            private readonly int _blockSize;
            private long _current;

            public FakeSqlStatementExecutor(int blockSize)
                : base(new LoggerFactory())
            {
                _blockSize = blockSize;
                _current = -blockSize;
            }

            public override object ExecuteScalar(DbConnection connection, DbTransaction transaction, SqlStatement statement)
            {
                return Interlocked.Add(ref _current, _blockSize);
            }

            public override Task<object> ExecuteScalarAsync(
                DbConnection connection, DbTransaction transaction, SqlStatement statement, CancellationToken cancellationToken = new CancellationToken())
            {
                return Task.FromResult<object>(Interlocked.Add(ref _current, _blockSize));
            }
        }

        private class AnEntity
        {
            public int Id { get; set; }
            public long Long { get; set; }
            public short Short { get; set; }
            public byte Byte { get; set; }
            public int? NullableId { get; set; }
            public long? NullableLong { get; set; }
            public short? NullableShort { get; set; }
            public byte? NullableByte { get; set; }
        }
    }
}
