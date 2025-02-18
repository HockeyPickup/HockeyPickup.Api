-- Find users with v2 password hashes
SELECT Id, UserName, Email, PasswordHash 
FROM AspNetUsers
WHERE PasswordHash LIKE 'AQAAAAEAACcQ%';

-- Find users with v3 password hashes
SELECT Id, UserName, Email, PasswordHash 
FROM AspNetUsers
WHERE PasswordHash LIKE 'AQAAAAIAAYag%';
