-- Find users with v2 password hashes
SELECT Id, UserName, Email, PasswordHash 
FROM AspNetUsers
WHERE PasswordHash NOT LIKE 'AQAAAAIAAYag%';

-- Find users with v3 password hashes
SELECT Id, UserName, Email, PasswordHash 
FROM AspNetUsers
WHERE PasswordHash LIKE 'AQAAAAIAAYag%';

SELECT 
    SUM(CASE WHEN PasswordHash NOT LIKE 'AQAAAAIAAYag%' THEN 1 ELSE 0 END) as V2PasswordCount,
    SUM(CASE WHEN PasswordHash LIKE 'AQAAAAIAAYag%' THEN 1 ELSE 0 END) as V3PasswordCount,
    COUNT(*) as TotalUsers
FROM AspNetUsers;