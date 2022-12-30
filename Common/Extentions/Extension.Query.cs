using CEF.Common.Primitives;
using EFCore.Sharding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.SqlClient;

namespace CEF.Common.Extentions
{
    /// <summary>
    /// Extension.Query
    /// </summary>
    public static partial class Extension
    {
        //public static IQueryable<TEntity> CreateDateQuery<TEntity>(this IConfiguration configuration,
        //    DateTime date)
        //{
        //    var databaseOptions = configuration.GetSection("Database:TransactionDb")
        //        .Get<DatabaseOptions[]>();

        //    string connectionText = databaseOptions.FirstOrDefault()?.ConnectionString;
        //    var queryProvider = new CustomQueryProvider(connectionText, date);
        //    return new CustomQuery<TEntity>(queryProvider);
        //}

        public static IDbConnection GetTransactionDb(this IConfiguration configuration, ILogger logger = null)
        {
            var databaseOptions = configuration.GetSection("Database:TransactionDb")
                .Get<DatabaseOptions[]>();
            string? connectionText = databaseOptions?
                .Where(p => p.ReadWriteType == ReadWriteType.Read)
                .FirstOrDefault()?.ConnectionString;
            //logger.LogInformation("连接字符串:{connectionText}", connectionText);
            return new SqlConnection(connectionText);
        }
    }
}
