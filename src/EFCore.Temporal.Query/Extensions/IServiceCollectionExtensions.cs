using System.Collections.Generic;
using EntityFrameworkCore.TemporalTables.Query;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EntityFrameworkCore.TemporalTables.Extensions
{
    public static class IServiceCollectionExtensions
    {
        /// <summary>
        /// Register temporal table services for the specified <see cref="DbContext"/>.
        /// </summary>
        public static IServiceCollection RegisterTemporalQueriesForDatabase<TContext>(
            this IServiceCollection services)
            where TContext : DbContext
        {
            // replace the service responsible for generating SQL strings
            services.AddSingleton<IQuerySqlGeneratorFactory, AsOfQuerySqlGeneratorFactory>();
            // replace the service responsible for traversing the Linq AST (a.k.a Query Methods)
            services.AddSingleton<IQueryableMethodTranslatingExpressionVisitorFactory, AsOfQueryableMethodTranslatingExpressionVisitorFactory>();
            // replace the service responsible for providing instances of SqlExpressions
            services.AddSingleton<ISqlExpressionFactory, AsOfSqlExpressionFactory>();

            // replace the service responsible for checking SqlNullability
            // https://github.com/Adam-Langley/efcore-temporal-query/issues/7
            services.AddSingleton<IRelationalParameterBasedSqlProcessorFactory, TemporalRelationalParameterBasedSqlProcessorFactory>();

            return services;
        }
    }

    // https://github.com/Adam-Langley/efcore-temporal-query/issues/7
    public class TemporalRelationalParameterBasedSqlProcessorFactory : IRelationalParameterBasedSqlProcessorFactory
    {
        private readonly RelationalParameterBasedSqlProcessorDependencies _dependencies;

        /// <summary>
        ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
        ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
        ///     any release. You should only use it directly in your code with extreme caution and knowing that
        ///     doing so can result in application failures when updating to a new Entity Framework Core release.
        /// </summary>
        public TemporalRelationalParameterBasedSqlProcessorFactory(RelationalParameterBasedSqlProcessorDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        public RelationalParameterBasedSqlProcessor Create(bool useRelationalNulls)
        {
            return new TemporalSqlServerParameterBasedSqlProcessor(_dependencies, useRelationalNulls);
        }
    }

    public class TemporalSqlServerParameterBasedSqlProcessor : SqlServerParameterBasedSqlProcessor
    {
        public TemporalSqlServerParameterBasedSqlProcessor(RelationalParameterBasedSqlProcessorDependencies dependencies, bool useRelationalNulls)
            : base(dependencies, useRelationalNulls)
        {
        }

        protected override SelectExpression ProcessSqlNullability(SelectExpression selectExpression, IReadOnlyDictionary<string, object> parametersValues, out bool canCache)
        {
            return new TemporalSqlNullabilityProcessor(Dependencies, UseRelationalNulls).Process(selectExpression, parametersValues, out canCache);
        }
    }

    public class TemporalSqlNullabilityProcessor : SqlNullabilityProcessor
    {
        public TemporalSqlNullabilityProcessor(RelationalParameterBasedSqlProcessorDependencies dependencies, bool useRelationalNulls)
            : base(dependencies, useRelationalNulls)
        {
        }

        protected override TableExpressionBase Visit(TableExpressionBase tableExpressionBase)
        {
            if (tableExpressionBase is AsOfTableExpression)
                return tableExpressionBase;
            return base.Visit(tableExpressionBase);
        }
    }
}