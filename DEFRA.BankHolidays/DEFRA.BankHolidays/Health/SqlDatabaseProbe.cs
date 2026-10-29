using Microsoft.Data.SqlClient;

namespace DEFRA.BankHolidays.Health
{
    /// <summary>Checks that the database can be reached.</summary>
    public interface IDatabaseProbe
    {
        Task CheckAsync(CancellationToken cancellationToken);
    }

    public sealed class SqlDatabaseProbe : IDatabaseProbe
    {
        private readonly string connectionString;

        public SqlDatabaseProbe(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task CheckAsync(CancellationToken cancellationToken)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = 5;
            await command.ExecuteScalarAsync(cancellationToken);
        }
    }
}
