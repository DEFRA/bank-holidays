using System.Data.Entity.Migrations;
using System.Data.Entity.Migrations.Infrastructure;

namespace DEFRA.BankHolidays.Migrations
{
    internal enum MigrationAction
    {
        Migrate,
        Script
    }

    internal sealed record MigrationCommand(MigrationAction Action, string ScriptPath);

    /// <summary>
    /// Command-line utilities for pipelines and jobs: <c>--migrate</c> applies pending migrations and exits,
    /// <c>--script &lt;path&gt;</c> writes the T-SQL for all migrations to a file and exits.
    /// </summary>
    internal static class MigrationCommands
    {
        public static MigrationCommand Parse(string[] args)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--migrate", StringComparison.OrdinalIgnoreCase))
                {
                    return new MigrationCommand(MigrationAction.Migrate, null);
                }

                if (string.Equals(args[i], "--script", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                    {
                        throw new ArgumentException("'--script' requires an output file path.");
                    }

                    return new MigrationCommand(MigrationAction.Script, args[i + 1]);
                }
            }

            return null;
        }

        public static int Run(MigrationCommand command, string connectionString)
        {
            var migrator = new DbMigrator(new Configuration(connectionString));

            if (command.Action == MigrationAction.Migrate)
            {
                migrator.Update();
                Console.WriteLine("Migrations applied.");
                return 0;
            }

            var script = new MigratorScriptingDecorator(migrator).ScriptUpdate(null, null);
            File.WriteAllText(command.ScriptPath, script);
            Console.WriteLine($"Migration script written to {command.ScriptPath}.");
            return 0;
        }
    }
}
