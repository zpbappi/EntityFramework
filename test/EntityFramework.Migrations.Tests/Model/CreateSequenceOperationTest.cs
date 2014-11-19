// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Data.Entity.Migrations.Model;
using Microsoft.Data.Entity.Relational.Metadata;
using Microsoft.Data.Entity.Relational.Model;
using Microsoft.Data.Entity.Utilities;
using Moq;
using Xunit;
using Sequence = Microsoft.Data.Entity.Relational.Metadata.Sequence;

namespace Microsoft.Data.Entity.Migrations.Tests.Model
{
    public class CreateSequenceOperationTest
    {
        [Fact]
        public void Create_and_initialize_operation()
        {
            var createSequenceOperation = new CreateSequenceOperation(
                "dbo.MySequence", 13, 7, 3, 103, typeof(int));

            Assert.Equal("dbo.MySequence", createSequenceOperation.SequenceName);
            Assert.Equal(13, createSequenceOperation.StartValue);
            Assert.Equal(7, createSequenceOperation.IncrementBy);
            Assert.Equal(3, createSequenceOperation.MinValue);
            Assert.Equal(103, createSequenceOperation.MaxValue);
            Assert.Equal(typeof(int), createSequenceOperation.Type);
            Assert.False(createSequenceOperation.IsDestructiveChange);
        }

        [Fact]
        public void Create_and_initialize_operation_with_defaults()
        {
            var createSequenceOperation = new CreateSequenceOperation("dbo.MySequence");

            Assert.Equal("dbo.MySequence", createSequenceOperation.SequenceName);
            Assert.Equal(Sequence.DefaultStartValue, createSequenceOperation.StartValue);
            Assert.Equal(Sequence.DefaultIncrement, createSequenceOperation.IncrementBy);
            Assert.False(createSequenceOperation.MinValue.HasValue);
            Assert.False(createSequenceOperation.MaxValue.HasValue);
            Assert.Equal(typeof(long), createSequenceOperation.Type);
            Assert.False(createSequenceOperation.IsDestructiveChange);
        }

        [Fact]
        public void Dispatches_visitor()
        {
            var createSequenceOperation = new CreateSequenceOperation("dbo.MySequence");
            var mockVisitor = new Mock<MigrationOperationSqlGenerator>(new RelationalMetadataExtensionProvider(), new RelationalTypeMapper());
            var builder = new Mock<IndentedStringBuilder>();
            createSequenceOperation.GenerateSql(mockVisitor.Object, builder.Object);

            mockVisitor.Verify(g => g.Generate(createSequenceOperation, builder.Object), Times.Once());
        }
    }
}
