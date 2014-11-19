﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Migrations.Model;
using Microsoft.Data.Entity.Migrations.Utilities;
using Microsoft.Data.Entity.Relational.Metadata;
using Microsoft.Data.Entity.Relational.Model;

namespace Microsoft.Data.Entity.Migrations
{
    public class MigrationOperationProcessor
    {
        private readonly IRelationalMetadataExtensionProvider _extensionProvider;
        private readonly RelationalTypeMapper _typeMapper;
        private readonly MigrationOperationFactory _operationFactory;

        public MigrationOperationProcessor(
            [NotNull] IRelationalMetadataExtensionProvider extensionProvider,
            [NotNull] RelationalTypeMapper typeMapper,
            [NotNull] MigrationOperationFactory operationFactory)
        {
            Check.NotNull(extensionProvider, "extensionProvider");
            Check.NotNull(typeMapper, "typeMapper");
            Check.NotNull(operationFactory, "operationFactory");

            _extensionProvider = extensionProvider;
            _typeMapper = typeMapper;
            _operationFactory = operationFactory;
        }

        public virtual IRelationalMetadataExtensionProvider ExtensionProvider
        {
            get { return _extensionProvider; }
        }

        protected virtual RelationalNameBuilder NameBuilder
        {
            get { return ExtensionProvider.NameBuilder; }
        }

        public virtual RelationalTypeMapper TypeMapper
        {
            get { return _typeMapper; }
        }

        public virtual MigrationOperationFactory OperationFactory
        {
            get { return _operationFactory; }
        }

        public virtual IReadOnlyList<MigrationOperation> Process(
            [NotNull] MigrationOperationCollection operations,
            [NotNull] IModel sourceModel,
            [NotNull] IModel targetModel)
        {
            return operations.GetAll();
        }
    }
}
