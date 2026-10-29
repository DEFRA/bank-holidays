using DEFRA.BankHolidays.Migrations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DEFRA.BankHolidays.Tests.Migrations
{
    [TestClass]
    public class MigrationCommandsTest
    {
        [TestMethod]
        public void Returns_null_when_no_command_is_given()
        {
            Assert.IsNull(MigrationCommands.Parse(Array.Empty<string>()));
            Assert.IsNull(MigrationCommands.Parse(new[] { "--urls", "http://localhost:5000" }));
        }

        [TestMethod]
        [DataRow("--migrate")]
        [DataRow("--MIGRATE")]
        public void Parses_the_migrate_command(string argument)
        {
            var command = MigrationCommands.Parse(new[] { argument });

            Assert.IsNotNull(command);
            Assert.AreEqual(MigrationAction.Migrate, command.Action);
            Assert.IsNull(command.ScriptPath);
        }

        [TestMethod]
        public void Parses_the_script_command_with_its_path()
        {
            var command = MigrationCommands.Parse(new[] { "--script", "schema.sql" });

            Assert.IsNotNull(command);
            Assert.AreEqual(MigrationAction.Script, command.Action);
            Assert.AreEqual("schema.sql", command.ScriptPath);
        }

        [TestMethod]
        public void Rejects_the_script_command_without_a_path()
        {
            Assert.ThrowsExactly<ArgumentException>(() => MigrationCommands.Parse(new[] { "--script" }));
            Assert.ThrowsExactly<ArgumentException>(() => MigrationCommands.Parse(new[] { "--script", "--migrate" }));
        }
    }
}
