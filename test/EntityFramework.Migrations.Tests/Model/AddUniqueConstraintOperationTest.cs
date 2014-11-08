// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Data.Entity.Migrations.Model;
using Microsoft.Data.Entity.Relational.Metadata;
using Microsoft.Data.Entity.Relational.Model;
using Microsoft.Data.Entity.Utilities;
using Moq;
using Xunit;

namespace Microsoft.Data.Entity.Migrations.Tests.Model
{
    public class DropUniqueConstraintOperationTest
    {
        [Fact]
        public void Create_and_initialize_operation()
        {
            var operation = new DropUniqueConstraintOperation("dbo.MyTable", "MyUC");

            Assert.Equal("dbo.MyTable", operation.TableName);
            Assert.Equal("MyUC", operation.UniqueConstraintName);
            Assert.True(operation.IsDestructiveChange);
        }

        [Fact]
        public void Dispatches_visitor()
        {
            var operation = new DropUniqueConstraintOperation("dbo.MyTable", "MyUC");

            var sqlGeneratorMock = new Mock<MigrationOperationSqlGenerator>(new RelationalMetadataExtensionProvider(), new RelationalTypeMapper());
            var builder = new IndentedStringBuilder();
            operation.GenerateSql(sqlGeneratorMock.Object, builder);

            sqlGeneratorMock.Verify(g => g.Generate(operation, builder), Times.Once());

            var codeGeneratorMock = new Mock<MigrationCodeGenerator>(new Mock<ModelCodeGenerator>().Object);
            builder = new IndentedStringBuilder();
            operation.GenerateCode(codeGeneratorMock.Object, builder);

            codeGeneratorMock.Verify(g => g.Generate(operation, builder), Times.Once());

            var visitorMock = new Mock<MigrationOperationVisitor<object>>();
            var context = new object();
            operation.Accept(visitorMock.Object, context);

            visitorMock.Verify(v => v.Visit(operation, context), Times.Once());
        }
    }
}
