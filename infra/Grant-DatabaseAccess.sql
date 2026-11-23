-- Run once per environment by the DBA, connected to the DEFRA.BankHolidays database as the Entra SQL administrator
-- (not as the runtime identity), from an approved private-network path. Replace the placeholder with the exact name
-- of the user-assigned managed identity resource (the 'managedIdentityName' output of the foundation deployment,
-- for example '<prefix>-<env>-id').
--
-- The runtime identity gets data access only: no DDL, no migration-history changes. The schema is released
-- separately (see README: 'DEFRA.BankHolidays.dll --script <file>' or '--migrate', run by the DBA or a pipeline
-- under an administrative identity).

DECLARE @identity sysname = N'<managed-identity-name>';

IF @identity = N'<managed-identity-name>'
    THROW 50000, 'Replace the placeholder with the managed identity name before running this script.', 1;

DECLARE @sql nvarchar(max);

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @identity)
BEGIN
    SET @sql = N'CREATE USER ' + QUOTENAME(@identity) + N' FROM EXTERNAL PROVIDER;';
    EXEC (@sql);
END

SET @sql = N'ALTER ROLE db_datareader ADD MEMBER ' + QUOTENAME(@identity) + N';';
EXEC (@sql);

SET @sql = N'ALTER ROLE db_datawriter ADD MEMBER ' + QUOTENAME(@identity) + N';';
EXEC (@sql);

-- The application reads and writes data only (the refresh replaces rows in one transaction). Do not add
-- db_owner or db_ddladmin. Readiness checks (SELECT 1) need no extra permission.

-- Verify: the identity is a member of exactly the two data roles.
SELECT r.name AS role_name
FROM sys.database_role_members m
JOIN sys.database_principals r ON r.principal_id = m.role_principal_id
JOIN sys.database_principals u ON u.principal_id = m.member_principal_id
WHERE u.name = @identity
ORDER BY r.name;
