using System.Data.Common;
using Microsoft.Extensions.Configuration;

namespace ECommerce.SharedKernel.Persistence;

public static class ReadReplicaSettings
{
    public static string GetReadConnectionString(IConfiguration configuration)
    {
        var primary = configuration.GetConnectionString("MainDb")
            ?? throw new InvalidOperationException("Connection string 'MainDb' is not configured.");

        if (!configuration.GetValue<bool>("ReadReplica:Enabled"))
            return primary;

        var replica = configuration.GetConnectionString("MainDbReplica");
        if (string.IsNullOrWhiteSpace(replica))
            throw new InvalidOperationException(
                "ReadReplica:Enabled is true but connection string 'MainDbReplica' is not configured.");

        var builder = new DbConnectionStringBuilder { ConnectionString = replica };
        builder.Remove("Application Intent");
        builder["ApplicationIntent"] = "ReadOnly";
        return builder.ConnectionString;
    }
}
