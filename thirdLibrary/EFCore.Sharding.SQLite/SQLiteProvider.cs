using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Sqlite.Infrastructure.Internal;
using Microsoft.Extensions.Options;
using System.Data.Common;

namespace EFCore.Sharding.SQLite
{
    internal class SQLiteProvider : AbstractProvider
    {
        public override DbProviderFactory DbProviderFactory => SqliteFactory.Instance;

        public override ModelBuilder GetModelBuilder() => new ModelBuilder(SqliteConventionSetBuilder.Build());

        public override IDbAccessor GetDbAccessor(GenericDbContext baseDbContext) => new SQLiteDbAccessor(baseDbContext);

        public override void UseDatabase(DbContextOptionsBuilder dbContextOptionsBuilder, DbConnection dbConnection)
        {
            dbContextOptionsBuilder.UseSqlite(dbConnection, x =>
            {
                x.UseNetTopologySuite();
                var infrastructure = (IDbContextOptionsBuilderInfrastructure)dbContextOptionsBuilder;
#pragma warning disable EF1001
                var sqliteExtension = dbContextOptionsBuilder.Options.FindExtension<SqliteOptionsExtension>() ?? new SqliteOptionsExtension();

                //We need to disable LoadSpatialite but it's not provided as an option externally we need to dig into the internals...
                infrastructure.AddOrUpdateExtension(sqliteExtension.WithLoadSpatialite(false));
#pragma warning restore EF1001
            });
            dbContextOptionsBuilder.ReplaceService<IMigrationsSqlGenerator, ShardingSQLiteMigrationsSqlGenerator>();
        }
    }
}
