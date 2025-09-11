DROP TABLE [test].[__EFMigrationsHistory];

DECLARE @sql NVARCHAR(MAX) = N'';

SELECT @sql += 'DROP TABLE [test].[' + t.name + '];'
FROM sys.tables t
         INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
WHERE s.name = 'test';

EXEC sp_executesql @sql;

drop schema test