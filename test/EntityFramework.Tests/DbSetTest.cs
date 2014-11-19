// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace Microsoft.Data.Entity.Tests
{
    public class DbSetTest
    {
        [Fact]
        public void Can_add_new_entities_to_context()
        {
            var contextMock = CreateContextMock();
            contextMock.Setup(m => m.Add(It.IsAny<Random>())).Returns<Random>(e => e);

            var entity = new Random();
            Assert.Same(entity, new DbSet<Random>(contextMock.Object).Add(entity));

            contextMock.Verify(m => m.Add(entity));
        }

        [Fact]
        public void Can_add_new_entities_to_context_async()
        {
            var contextMock = CreateContextMock();
            contextMock.Setup(m => m.AddAsync(It.IsAny<Random>(), It.IsAny<CancellationToken>()))
                .Returns<Random, CancellationToken>((e, c) => Task.FromResult(e));

            var entity = new Random();
            Assert.Same(entity, new DbSet<Random>(contextMock.Object).AddAsync(entity).Result);

            contextMock.Verify(m => m.AddAsync(entity, CancellationToken.None));
        }

        [Fact]
        public void Can_add_new_entities_to_context_async_with_token()
        {
            var contextMock = CreateContextMock();
            contextMock.Setup(m => m.AddAsync(It.IsAny<Random>(), It.IsAny<CancellationToken>()))
                .Returns<Random, CancellationToken>((e, c) => Task.FromResult(e));

            var entity = new Random();
            var cancellationToken = new CancellationToken();
            Assert.Same(entity, new DbSet<Random>(contextMock.Object).AddAsync(entity, cancellationToken).Result);

            contextMock.Verify(m => m.AddAsync(entity, cancellationToken));
        }

        [Fact]
        public void Can_add_existing_entities_for_update_to_context()
        {
            var contextMock = CreateContextMock();
            contextMock.Setup(m => m.Update(It.IsAny<Random>())).Returns<Random>(e => e);

            var entity = new Random();
            Assert.Same(entity, new DbSet<Random>(contextMock.Object).Update(entity));

            contextMock.Verify(m => m.Update(entity));
        }

        [Fact]
        public void Can_add_existing_entities_for_update_to_context_async()
        {
            var contextMock = CreateContextMock();
            contextMock.Setup(m => m.UpdateAsync(It.IsAny<Random>(), It.IsAny<CancellationToken>()))
                .Returns<Random, CancellationToken>((e, c) => Task.FromResult(e));

            var entity = new Random();
            Assert.Same(entity, new DbSet<Random>(contextMock.Object).UpdateAsync(entity).Result);

            contextMock.Verify(m => m.UpdateAsync(entity, CancellationToken.None));
        }

        [Fact]
        public void Can_add_existing_entities_for_update_to_context_async_with_token()
        {
            var contextMock = CreateContextMock();
            contextMock.Setup(m => m.UpdateAsync(It.IsAny<Random>(), It.IsAny<CancellationToken>()))
                .Returns<Random, CancellationToken>((e, c) => Task.FromResult(e));

            var entity = new Random();
            var cancellationToken = new CancellationToken();
            Assert.Same(entity, new DbSet<Random>(contextMock.Object).UpdateAsync(entity, cancellationToken).Result);

            contextMock.Verify(m => m.UpdateAsync(entity, cancellationToken));
        }

        private static Mock<DbContext> CreateContextMock()
        {
            var contextMock = new Mock<DbContext>();
            return contextMock;
        }
    }
}
