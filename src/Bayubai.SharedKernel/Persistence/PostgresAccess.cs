using System.Data.Common;
using System.Text.RegularExpressions;

namespace Bayubai.SharedKernel.Persistence;

// The API signs in as a login role that can only read and write rows, so a flaw in it cannot change or drop the schema.
public static partial class PostgresAccess
{
    public static async Task EnsureLoginRoleAsync(DbConnection connection, string role, string password, CancellationToken cancellationToken)
    {
        CheckIdentifier(role);
        ArgumentException.ThrowIfNullOrEmpty(password);

        await using var lookup = connection.CreateCommand();
        lookup.CommandText = "SELECT count(*) FROM pg_roles WHERE rolname = @role";
        var parameter = lookup.CreateParameter();
        parameter.ParameterName = "role";
        parameter.Value = role;
        lookup.Parameters.Add(parameter);
        var exists = Convert.ToInt64(await lookup.ExecuteScalarAsync(cancellationToken)) > 0;

        // Role statements take no bind parameters; ALTER keeps the password in step with the secret after a rotation.
        var verb = exists ? "ALTER" : "CREATE";
        await ExecuteAsync(connection, $"{verb} ROLE {role} WITH LOGIN PASSWORD {Literal(password)}", cancellationToken);
    }

    public static Task GrantDataAccessAsync(DbConnection connection, string schema, string role, CancellationToken cancellationToken)
    {
        CheckIdentifier(schema);
        CheckIdentifier(role);
        return ExecuteAsync(connection, $"""
            GRANT USAGE ON SCHEMA {schema} TO {role};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {schema} TO {role};
            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA {schema} TO {role};
            """, cancellationToken);
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void CheckIdentifier(string name)
    {
        if (!Identifier().IsMatch(name))
        {
            throw new ArgumentException($"'{name}' is not a plain lower-case PostgreSQL identifier.", nameof(name));
        }
    }

    private static string Literal(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    [GeneratedRegex("^[a-z_][a-z0-9_]{0,62}$")]
    private static partial Regex Identifier();
}
