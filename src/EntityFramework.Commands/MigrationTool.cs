﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Commands.Migrations;
using Microsoft.Data.Entity.Commands.Utilities;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Migrations;
using Microsoft.Data.Entity.Migrations.Infrastructure;
using Microsoft.Data.Entity.Relational;
using Microsoft.Data.Entity.Utilities;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.Logging;

namespace Microsoft.Data.Entity.Commands
{
    public class MigrationTool
    {
        private readonly ILoggerProvider _loggerProvider;
        private readonly LazyRef<ILogger> _logger;
        private readonly Assembly _assembly;

        public MigrationTool([NotNull] ILoggerProvider loggerProvider, [NotNull] Assembly assembly)
        {
            Check.NotNull(loggerProvider, "loggerProvider");
            Check.NotNull(assembly, "assembly");

            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(loggerProvider);

            _loggerProvider = loggerProvider;
            _logger = new LazyRef<ILogger>(() => loggerFactory.Create<MigrationTool>());
            _assembly = assembly;
        }

        public virtual ScaffoldedMigration AddMigration(
            [NotNull] string migrationName,
            [NotNull] string rootNamespace,
            [CanBeNull] string contextTypeName)
        {
            Check.NotEmpty(migrationName, "migrationName");
            Check.NotEmpty(rootNamespace, "rootNamespace");

            var contextType = GetContextType(contextTypeName);
            using (var context = CreateContext(contextType))
            {
                var scopedServiceProvider = ((IDbContextServices)context).ScopedServiceProvider;
                var options = scopedServiceProvider.GetRequiredService<LazyRef<IDbContextOptions>>();
                var model = scopedServiceProvider.GetRequiredService<LazyRef<IModel>>();

                var extension = RelationalOptionsExtension.Extract(options.Value);
                if (extension.MigrationNamespace == null)
                {
                    extension.MigrationNamespace = rootNamespace + ".Migrations";
                }

                var migrator = CreateMigrator(context);
                var scaffolder = new MigrationScaffolder(
                    context,
                    options.Value,
                    model.Value,
                    migrator.MigrationAssembly,
                    migrator.ModelDiffer,
                    new CSharpMigrationCodeGenerator(new CSharpModelCodeGenerator()));

                var migration = scaffolder.ScaffoldMigration(migrationName);

                // Derive default directory from namespace
                if (migration.Directory == null)
                {
                    var directory = migration.MigrationNamespace;
                    if (directory.StartsWith(rootNamespace + '.'))
                    {
                        directory = directory.Substring(rootNamespace.Length + 1);
                    }

                    migration.Directory = directory.Replace('.', Path.DirectorySeparatorChar);
                }

                return migration;
            }
        }

        public virtual IEnumerable<string> WriteMigration(
            [NotNull] string projectDir,
            [NotNull] ScaffoldedMigration migration)
        {
            Check.NotEmpty(projectDir, "projectDir");
            Check.NotNull(migration, "migration");

            var migrationDir = Path.Combine(projectDir, migration.Directory);
            Directory.CreateDirectory(migrationDir);

            // TODO: Get from migration (set in MigrationScaffolder)
            var extension = ".cs";

            var userCodeFile = Path.Combine(migrationDir, migration.MigrationId + extension);
            File.WriteAllText(userCodeFile, migration.MigrationCode);
            yield return userCodeFile;

            var designerCodeFile = Path.Combine(migrationDir, migration.MigrationId + ".Designer" + extension);
            File.WriteAllText(designerCodeFile, migration.MigrationMetadataCode);
            yield return designerCodeFile;

            var modelSnapshotFile = Path.Combine(migrationDir, migration.SnapshotModelClass + extension);
            File.WriteAllText(modelSnapshotFile, migration.SnapshotModelCode);
            yield return modelSnapshotFile;
        }

        public virtual IEnumerable<Migration> GetMigrations([CanBeNull] string contextTypeName)
        {
            var contextType = GetContextType(contextTypeName);

            return MigrationAssembly.LoadMigrations(GetMigrationTypes(), contextType);
        }

        public virtual string ScriptMigration(
            [CanBeNull] string fromMigrationName,
            [CanBeNull] string toMigrationName,
            bool idempotent,
            [CanBeNull] string contextTypeName)
        {
            var contextType = GetContextType(contextTypeName);
            using (var context = CreateContext(contextType))
            {
                var migrator = CreateMigrator(context);

                // TODO: Use fromMigrationName
                // TODO: Use idempotent
                var statements = string.IsNullOrEmpty(toMigrationName)
                    ? migrator.ScriptMigrations()
                    : migrator.ScriptMigrations(toMigrationName);

                // TODO: Use SuppressTransaction somehow?
                return string.Join(Environment.NewLine, statements.Select(s => s.Sql));
            }
        }

        public virtual void ApplyMigration([CanBeNull] string migrationName, [CanBeNull] string contextTypeName)
        {
            var contextType = GetContextType(contextTypeName);
            using (var context = CreateContext(contextType))
            {
                var migrator = CreateMigrator(context);

                if (string.IsNullOrEmpty(migrationName))
                {
                    migrator.ApplyMigrations();
                }
                else
                {
                    migrator.ApplyMigrations(migrationName);
                }
            }
        }

        public virtual Type GetContextType([CanBeNull] string name)
        {
            var contextType = ContextTool.SelectType(GetContextTypes(), name);
            _logger.Value.WriteVerbose(Strings.LogUseContext(contextType.Name));

            return contextType;
        }

        public virtual IEnumerable<Type> GetContextTypes()
        {
            return ContextTool.GetContextTypes(_assembly)
                .Concat(
                    GetMigrationTypes()
                        .Select(MigrationAssembly.TryGetContextType)
                        .Where(t => t != null))
                .Distinct();
        }

        private DbContext CreateContext(Type type)
        {
            var context = ContextTool.CreateContext(type);

            var scopedServiceProvider = ((IDbContextServices)context).ScopedServiceProvider;
            var options = scopedServiceProvider.GetRequiredService<LazyRef<IDbContextOptions>>();

            var loggerFactory = scopedServiceProvider.GetRequiredService<ILoggerFactory>();
            loggerFactory.AddProvider(_loggerProvider);

            var extension = RelationalOptionsExtension.Extract(options.Value);
            if (extension.MigrationAssembly == null)
            {
                extension.MigrationAssembly = _assembly;
            }

            return context;
        }

        private Migrator CreateMigrator(DbContext context)
        {
            return ((IDbContextServices)context).ScopedServiceProvider.GetRequiredService<LazyRef<Migrator>>().Value;
        }

        private IEnumerable<Type> GetMigrationTypes()
        {
            return MigrationAssembly.GetMigrationTypes(_assembly);
        }
    }
}
