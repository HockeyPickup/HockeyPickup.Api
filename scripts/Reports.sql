/* Tune Indexes */
EXEC sp_updatestats;

/* Recent Activities, shows SessionDate column for context */
SELECT TOP 100 FirstName + ' ' + LastName AS Name, ActivityLogs.CreateDateTime AT TIME ZONE 'UTC' AT TIME ZONE 'Pacific Standard Time' as 'CreateDateTime PST', Activity, DATENAME(WEEKDAY, SessionDate) AS SessionDay, ActivityLogs.SessionId, SessionDate, ActivityLogs.UserId from ActivityLogs
INNER JOIN Sessions ON ActivityLogs.SessionId = sessions.SessionId
INNER JOIN AspNetUsers ON AspNetUsers.Id = UserId
ORDER BY ActivityLogs.CreateDateTime DESC

/* List potential buyers based on how recently they played */
EXEC [GetPotentialBuyers] 2889

/* Integrity check on SessionRosters. Should never be 0 */
select FirstName, LastName, * from SessionRosters 
inner join AspNetUsers on Id = UserId
where TeamAssignment = 0
order by SessionId desc

--exec [PopulateHistoricalSessionRosters]
EXEC [GetUserStats] 'fdbfe74a-a5c5-4ff0-8edb-c98a5df9d85a'

/* Players that have set their jersey number or shoots (Left / Right) */
select firstname, lastname, shoots, jerseynumber, PositionPreference from AspNetUsers where active = 1 and shoots <> 0 or jerseynumber <> '0' order by JerseyNumber
select firstname, lastname, shoots, jerseynumber, PositionPreference from AspNetUsers where active = 1 and NotificationPreference = 1

/* Trigger list */
SELECT 
    t.name AS TriggerName,
    s.name AS SchemaName,
    o.name AS TableName,
    t.is_instead_of_trigger,
    t.create_date,
    t.modify_date
FROM sys.triggers t
JOIN sys.objects o ON t.parent_id = o.object_id
JOIN sys.schemas s ON o.schema_id = s.schema_id
WHERE o.type = 'U'  -- User-created tables
AND t.is_ms_shipped = 0  -- Exclude system triggers
AND t.name NOT LIKE '%dss%'  -- Exclude replication triggers
ORDER BY s.name, o.name, t.name;

/* Sproc list */
SELECT 
    p.name AS ProcedureName,
    s.name AS SchemaName,
    p.create_date,
    p.modify_date
FROM sys.procedures p
JOIN sys.schemas s ON p.schema_id = s.schema_id
WHERE s.name = 'dbo'
ORDER BY s.name, p.name;


/* Emergency Info */
SELECT FirstName, LastName, EmergencyName, EmergencyPhone FROM AspNetUsers WHERE EmergencyName IS NOT NULL OR EmergencyPhone IS NOT NULL

/* Number of sessions by day of week */
select datename(weekday, sessiondate) as Weekday, count(sessionid) as '# of Sessions' from Sessions
where note not like '%cancelled%'
group by datename(weekday, sessiondate)
order by count(sessionid) desc

/* Number of sessions by month */
select datename(month, sessiondate) as Month, count(sessionid) as '# of Sessions' from Sessions
where note not like '%cancelled%'
group by datename(month, sessiondate), month(sessiondate)
order by count(sessionid) desc

/* Number of sessions by month and year */
select datename(month, sessiondate) as Month, datename(year, sessiondate) as Year, count(sessionid) as '# of Sessions' from Sessions
where note not like '%cancelled%' -- and datename(month, sessiondate) = 'August'
group by datename(month, sessiondate), month(sessiondate), datename(year, sessiondate), year(sessiondate)
order by year(sessiondate), month(sessiondate)

/* Number of sessions by year */
select year(sessiondate) as Year, count(sessionid) as '# of Sessions' from Sessions
where note not like '%cancelled%'
group by year(sessiondate)
order by year(sessiondate)

/* Top Sellers - All time */
SELECT FirstName, LastName, COUNT(SellerUserId) AS SellerCount, MAX(SessionDate) As LastSessionSold, MIN(SessionDate) As FirstSessionSold from BuySells
INNER JOIN AspNetUsers on SellerUserId = AspNetUsers.id
INNER JOIN Sessions on Sessions.SessionId = BuySells.SessionId
WHERE BuyerUserId != SellerUserId
GROUP BY SellerUserId, FirstName, LastName
ORDER BY COUNT(SellerUserId) DESC

/* Top Sellers - Adjust year and day variables */
DECLARE @SellerYear INT
SET @SellerYear = 2024
DECLARE @SellerWeekday nvarchar(10)
SET @SellerWeekday = 'Wednesday'

SELECT T1.FirstName, T1.LastName, T1.SellerCount, T2.SessionCount, CAST((CAST(T1.SellerCount AS decimal) / CAST(T2.SessionCount AS decimal)) AS decimal(18,4)) * 100 AS SellingPercentage, T2.Year, T2.Weekday FROM
(SELECT SellerUserId, FirstName, LastName, COUNT(SellerUserId) AS SellerCount from BuySells
INNER JOIN AspNetUsers on SellerUserId = AspNetUsers.id
INNER JOIN Sessions on BuySells.SessionId = Sessions.SessionId
LEFT OUTER JOIN SessionsByDate on YEAR(Sessions.SessionDate) = YEAR(SessionsByDate.Year)
WHERE YEAR(Sessions.SessionDate) = @SellerYear AND DATENAME(WeekDay, Sessions.SessionDate) = @SellerWeekday AND Sessions.SessionId != 2771
AND BuyerUserId != SellerUserId
GROUP BY SellerUserId, FirstName, LastName
) AS T1,
(SELECT * from SessionsByDate WHERE YEAR = @SellerYear AND SessionsByDate.Weekday = @SellerWeekday) AS T2
ORDER BY T1.SellerCount DESC

/* Top Sellers - All time */
SELECT 
    T1.FirstName, 
    T1.LastName, 
    T1.SellerCount, 
    T1.FirstSale,
    T1.LastSale,
    T2.SessionCount, 
    CAST((CAST(T1.SellerCount AS decimal) / CAST(T2.SessionCount AS decimal)) AS decimal(18,4)) * 100 AS SellingPercentage
FROM
(SELECT 
    SellerUserId, 
    FirstName, 
    LastName, 
    COUNT(DISTINCT BuySells.SessionId) AS SellerCount,
    MIN(Sessions.SessionDate) AS FirstSale,
    MAX(Sessions.SessionDate) AS LastSale
 FROM BuySells
 INNER JOIN AspNetUsers ON SellerUserId = AspNetUsers.id
 INNER JOIN Sessions ON BuySells.SessionId = Sessions.SessionId
 WHERE Sessions.SessionId != 2771 AND BuyerUserId != SellerUserId
 GROUP BY SellerUserId, FirstName, LastName
) AS T1
CROSS APPLY
(SELECT COUNT(*) AS SessionCount 
 FROM Sessions 
 WHERE SessionId != 2771
 AND SessionDate BETWEEN T1.FirstSale AND T1.LastSale
) AS T2
ORDER BY T1.SellerCount DESC

/* Top Buyers - All time */
SELECT FirstName, LastName, COUNT(BuyerUserId) AS BuyerCount, MAX(SessionDate) As LastSessionBought, MIN(SessionDate) As FirstSessionBought from BuySells
INNER JOIN AspNetUsers on BuyerUserId = AspNetUsers.id
INNER JOIN Sessions on Sessions.SessionId = BuySells.SessionId
WHERE SellerUserId != BuyerUserId
GROUP BY BuyerUserId, FirstName, LastName
ORDER BY COUNT(BuyerUserId) DESC

/* Active users that have never bought */
SELECT Id, FirstName, LastName FROM AspNetUsers
WHERE Id NOT IN (SELECT BuyerUserId from BuySells WHERE BuyerUserId IS NOT NULL)
AND Id NOT IN (SELECT DISTINCT UserId from Regulars)
AND Active = 1
ORDER BY FirstName

/* Subs last bought by date */
SELECT Name, SessionDate, BuySellsSessionId, BuyerUserId FROM BuySellsByBuyer
WHERE SellerUserId IS NOT NULL
  AND SessionDate = (
    SELECT MAX(SessionDate) FROM BuySellsByBuyer bb
    WHERE bb.BuyerUserId = BuySellsByBuyer.BuyerUserId
      AND bb.SellerUserId IS NOT NULL
      AND NOT EXISTS (
        SELECT 1 FROM BuySellsByBuyer bb2
        WHERE bb2.SessionId = bb.SessionId 
          AND bb2.SellerUserId = bb.BuyerUserId
      )
  )
ORDER BY SessionDate DESC

/* Subs first bought by date */
SELECT Name, SessionDate, BuySellsSessionId, BuyerUserId FROM BuySellsByBuyer
WHERE SellerUserId IS NOT NULL
  AND SessionDate = (
    SELECT MIN(SessionDate) FROM BuySellsByBuyer bb
    WHERE bb.BuyerUserId = BuySellsByBuyer.BuyerUserId
      AND bb.SellerUserId IS NOT NULL
      AND NOT EXISTS (
        SELECT 1 FROM BuySellsByBuyer bb2
        WHERE bb2.SessionId = bb.SessionId 
          AND bb2.SellerUserId = bb.BuyerUserId
      )
  )
ORDER BY SessionDate DESC

/* Times from bought to payment sent */
WITH RankedTimes AS (
    SELECT
        b.FirstName,
        b.LastName,
        DATEDIFF(SECOND, b.CreateDateTime, s.CreateDateTime) AS TimeDifference,
        ROW_NUMBER() OVER (PARTITION BY b.UserId ORDER BY DATEDIFF(SECOND, b.CreateDateTime, s.CreateDateTime) ASC) AS FastestRank,
        ROW_NUMBER() OVER (PARTITION BY b.UserId ORDER BY DATEDIFF(SECOND, b.CreateDateTime, s.CreateDateTime) DESC) AS SlowestRank,
        b.SessionId,
        b.UserId
    FROM
        BoughtAndSentView AS b
    JOIN
        BoughtAndSentView AS s ON b.SessionId = s.SessionId AND b.UserId = s.UserId
    JOIN
        Sessions AS ss ON b.SessionId = ss.SessionId
    WHERE
        b.ActivityType = 'BOUGHT' 
        AND s.ActivityType = 'SENT'
        AND ss.SessionDate >= '2023-11-01'
)
SELECT
    FirstName,
    LastName,
    MIN(TimeDifference) AS FastestTimeS,
    dbo.FormatDateTimeFromSeconds(MIN(TimeDifference)) AS FastestTime,
    MAX(TimeDifference) AS SlowestTimeS,
    dbo.FormatDateTimeFromSeconds(MAX(TimeDifference)) AS SlowestTime,
    MIN(SessionId) AS FastestSessionID,
    MAX(SessionId) AS SlowestSessionID,
    AVG(TimeDifference) AS AverageTimeS,
    dbo.FormatDateTimeFromSeconds(AVG(TimeDifference)) AS AverageTime,
    COUNT(DISTINCT SessionId) AS SessionCount,
    UserId
FROM
    RankedTimes
WHERE
    FastestRank > 2  -- Exclude top 2 fastest times
    AND SlowestRank > 2  -- Exclude bottom 2 slowest times
    AND UserId IN (
        SELECT UserId
        FROM BoughtAndSentView
        GROUP BY UserId
    )
GROUP BY
    UserId, FirstName, LastName
ORDER BY
    AverageTimeS;

/* Number of buyers / session totals */
SELECT BuySells.SessionId, Sessions.SessionDate, COUNT(DISTINCT CASE WHEN BuyerUserId IS NOT NULL AND SellerUserId IS NOT NULL THEN BuySellId END) AS [Sold Count]
FROM dbo.BuySells
INNER JOIN Sessions ON BuySells.SessionId = Sessions.SessionId
WHERE Sessions.Note NOT LIKE '%cancelled%' AND SessionDate < GETDATE()
GROUP BY BuySells.SessionId, Sessions.SessionDate
ORDER BY [Sold Count] ASC

SELECT [Sold Count], COUNT(*) AS [Session Count]
FROM (
    SELECT BuySells.SessionId, COUNT(DISTINCT CASE WHEN BuyerUserId IS NOT NULL AND SellerUserId IS NOT NULL THEN BuySellId END) AS [Sold Count]
    FROM dbo.BuySells
	INNER JOIN Sessions ON BuySells.SessionId = Sessions.SessionId
	WHERE Sessions.Note NOT LIKE '%cancelled%' AND SessionDate < GETDATE()
    GROUP BY BuySells.SessionId
) AS Subquery
GROUP BY [Sold Count]
ORDER BY [Sold Count] ASC;

/* Skaters that have never bought or sold */
SELECT TOP (2000) Id, Email, EmailConfirmed, PasswordHash, SecurityStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEndDateUtc, LockoutEnabled, AccessFailedCount, UserName, FirstName, LastName, NotificationPreference, PayPalEmail, Active, Preferred, 
             VenmoAccount, MobileLast4, Rating, PreferredPlus, EmergencyName, EmergencyPhone, LockerRoom13
FROM   dbo.AspNetUsers
WHERE (Id NOT IN
                 (SELECT DISTINCT BuyerUserId
                 FROM    dbo.BuySells
                 WHERE (BuyerUserId IS NOT NULL))) AND (Email NOT LIKE '%brettmorrison%') AND (Id NOT IN
                 (SELECT DISTINCT SellerUserId
                 FROM    dbo.BuySells AS BuySells_1
                 WHERE (SellerUserId IS NOT NULL))) AND (Email NOT LIKE '%brettmorrison%') AND (Id NOT IN
                 (SELECT DISTINCT UserId
                 FROM    dbo.ActivityLogs))

/* List of sells by player by year */
DECLARE @UserId NVARCHAR(128) = '7a06eb4d-d110-421c-9f4d-b3c965acb5a7';
WITH SellingActivity AS (
    -- Get all sessions where Alex either went through selling queue OR sold directly
    SELECT DISTINCT
        s.SessionId,
        s.SessionDate,
        seller.FirstName AS FirstName,
        seller.LastName AS LastName,
        -- Find when they were added to selling queue (if they were)
        (SELECT MIN(al.CreateDateTime) 
         FROM ActivityLogs al 
         WHERE al.SessionId = s.SessionId 
         AND al.Activity LIKE seller.FirstName + ' ' + seller.LastName + '%added to SELLING queue'
        ) AS TimestampListed,
        -- Find when they sold
        (SELECT MIN(al.CreateDateTime) 
         FROM ActivityLogs al 
         WHERE al.SessionId = s.SessionId 
         AND (
             al.Activity LIKE seller.FirstName + ' ' + seller.LastName + '%SOLD%'
             OR 
             al.Activity LIKE '%BOUGHT%from seller: ' + seller.FirstName + ' ' + seller.LastName + '%'
         )
        ) AS TimestampSold
    FROM Sessions s
        INNER JOIN AspNetUsers seller ON seller.Id = @UserId
        INNER JOIN Regulars r ON r.UserId = seller.Id
        INNER JOIN RegularSets rs ON r.RegularSetId = rs.RegularSetId
    WHERE YEAR(s.SessionDate) = 2025
        AND s.Note NOT LIKE '%cancelled%'
        AND (
            -- Either they were in selling queue
            EXISTS (
                SELECT 1 FROM ActivityLogs al 
                WHERE al.SessionId = s.SessionId 
                AND al.Activity LIKE seller.FirstName + ' ' + seller.LastName + '%added to SELLING queue'
            )
            OR
            -- Or they sold without queue activity
            EXISTS (
                SELECT 1 FROM ActivityLogs al 
                WHERE al.SessionId = s.SessionId 
                AND (
                    al.Activity LIKE seller.FirstName + ' ' + seller.LastName + '%SOLD%'
                    OR 
                    al.Activity LIKE '%BOUGHT%from seller: ' + seller.FirstName + ' ' + seller.LastName + '%'
                )
            )
        )
)
SELECT 
    SessionId,
    SessionDate,
    FirstName,
    LastName,
    COALESCE(TimestampListed, TimestampSold) AS TimestampListed,  -- Use sale time if no listing time
    TimestampSold,
    CASE 
        WHEN TimestampSold IS NULL THEN 'Listed and not sold'
        WHEN TimestampListed IS NULL THEN 'Sold when listed'  -- No queue activity, sold immediately
        ELSE 'Sold when buyer entered queue'  -- Went through selling queue process
    END AS Status
FROM SellingActivity
WHERE (COALESCE(TimestampListed, TimestampSold) < SessionDate)  -- Either listing or sale before session
    AND (TimestampSold IS NULL OR TimestampSold < SessionDate)
ORDER BY SessionDate DESC, COALESCE(TimestampListed, TimestampSold) DESC;

/* Top players by attendance */
DECLARE @Year INT = 2025;

SELECT 
    u.FirstName,
    u.LastName,
    COUNT(DISTINCT sr.SessionId) AS GamesPlayed
FROM 
    SessionRosters sr
    INNER JOIN AspNetUsers u ON sr.UserId = u.Id
    INNER JOIN Sessions s ON sr.SessionId = s.SessionId
WHERE 
    sr.IsPlaying = 1  -- Only count players who actually played
    AND YEAR(s.SessionDate) = @Year  -- Filter by the specified year
    AND s.SessionDate <= GETDATE()  -- Only count past games, not future scheduled ones
    AND (s.Note IS NULL OR s.Note NOT LIKE '%cancelled%')  -- Exclude cancelled sessions
GROUP BY 
    u.FirstName, 
    u.LastName,
    u.Id  -- Include Id in GROUP BY to handle players with same name
ORDER BY 
    GamesPlayed DESC,  -- Primary sort by games played (descending)
    u.LastName ASC,    -- Secondary sort by last name
    u.FirstName ASC;   -- Tertiary sort by first name

DECLARE @GoalieYear INT = 2024;
-- Goalie Games Played Report (Fixed for comma-separated format)
-- Parses goalie names from session notes in format: "Goalies: FirstName LastName, FirstName LastName"
-- Returns: FirstName, LastName, GamesPlayed

WITH SessionGoalieText AS (
    -- Extract the text after "Goalies: " from session notes
    SELECT 
        SessionId,
        SessionDate,
        Note,
        -- Extract everything after "Goalies: " until line break or end
        LTRIM(RTRIM(
            SUBSTRING(
                Note, 
                CHARINDEX('Goalies: ', Note) + 9, -- Skip "Goalies: " (9 chars)
                CASE 
                    -- Find the end point (newline or end of string)
                    WHEN CHARINDEX(CHAR(13), Note, CHARINDEX('Goalies: ', Note) + 9) > 0
                    THEN CHARINDEX(CHAR(13), Note, CHARINDEX('Goalies: ', Note) + 9) - CHARINDEX('Goalies: ', Note) - 9
                    WHEN CHARINDEX(CHAR(10), Note, CHARINDEX('Goalies: ', Note) + 9) > 0
                    THEN CHARINDEX(CHAR(10), Note, CHARINDEX('Goalies: ', Note) + 9) - CHARINDEX('Goalies: ', Note) - 9
                    ELSE LEN(Note)
                END
            )
        )) AS GoalieListText
    FROM Sessions
    WHERE 
        YEAR(SessionDate) = @GoalieYear
        AND SessionDate <= GETDATE()
        AND Note IS NOT NULL 
        AND Note LIKE '%Goalies: %'
        AND (Note NOT LIKE '%cancelled%')
),
-- Parse individual goalies from comma-separated list
ParsedGoalies AS (
    SELECT 
        SessionId,
        SessionDate,
        GoalieListText,
        -- Goalie 1 (before first comma, or entire string if no comma)
        LTRIM(RTRIM(
            CASE 
                WHEN CHARINDEX(',', GoalieListText) > 0 
                THEN LEFT(GoalieListText, CHARINDEX(',', GoalieListText) - 1)
                ELSE GoalieListText
            END
        )) AS Goalie1,
        -- Goalie 2 (after first comma)
        CASE 
            WHEN CHARINDEX(',', GoalieListText) > 0 
            THEN LTRIM(RTRIM(
                CASE 
                    WHEN CHARINDEX(',', GoalieListText, CHARINDEX(',', GoalieListText) + 1) > 0
                    THEN SUBSTRING(GoalieListText, 
                                  CHARINDEX(',', GoalieListText) + 1, 
                                  CHARINDEX(',', GoalieListText, CHARINDEX(',', GoalieListText) + 1) - CHARINDEX(',', GoalieListText) - 1)
                    ELSE SUBSTRING(GoalieListText, CHARINDEX(',', GoalieListText) + 1, LEN(GoalieListText))
                END
            ))
            ELSE NULL
        END AS Goalie2,
        -- Goalie 3 (after second comma)
        CASE 
            WHEN LEN(GoalieListText) - LEN(REPLACE(GoalieListText, ',', '')) >= 2
            THEN LTRIM(RTRIM(
                SUBSTRING(GoalieListText, 
                         CHARINDEX(',', GoalieListText, CHARINDEX(',', GoalieListText) + 1) + 1, 
                         LEN(GoalieListText))
            ))
            ELSE NULL
        END AS Goalie3
    FROM SessionGoalieText
    WHERE GoalieListText IS NOT NULL AND GoalieListText != ''
),
-- Union all goalies into individual rows
AllGoalieNames AS (
    SELECT SessionId, SessionDate, Goalie1 AS FullName 
    FROM ParsedGoalies 
    WHERE Goalie1 IS NOT NULL AND Goalie1 != '' AND LEN(Goalie1) > 5
    
    UNION ALL
    
    SELECT SessionId, SessionDate, Goalie2 AS FullName 
    FROM ParsedGoalies 
    WHERE Goalie2 IS NOT NULL AND Goalie2 != '' AND LEN(Goalie2) > 5
    
    UNION ALL
    
    SELECT SessionId, SessionDate, Goalie3 AS FullName 
    FROM ParsedGoalies 
    WHERE Goalie3 IS NOT NULL AND Goalie3 != '' AND LEN(Goalie3) > 5
),
-- Extract first and last names, then create normalized keys
NormalizedGoalies AS (
    SELECT 
        SessionId,
        SessionDate,
        FullName,
        -- Extract first name (text before first space)
        CASE 
            WHEN CHARINDEX(' ', LTRIM(RTRIM(FullName))) > 0
            THEN LEFT(LTRIM(RTRIM(FullName)), CHARINDEX(' ', LTRIM(RTRIM(FullName))) - 1)
            ELSE ''
        END AS FirstName,
        -- Extract last name (text after first space)
        CASE 
            WHEN CHARINDEX(' ', LTRIM(RTRIM(FullName))) > 0
            THEN LTRIM(SUBSTRING(LTRIM(RTRIM(FullName)), 
                                 CHARINDEX(' ', LTRIM(RTRIM(FullName))) + 1, 
                                 LEN(FullName)))
            ELSE ''
        END AS LastName,
        -- Create normalized key (first 3 chars of first + first 3 chars of last)
        LOWER(LEFT(
            CASE 
                WHEN CHARINDEX(' ', LTRIM(RTRIM(FullName))) > 0
                THEN LEFT(LTRIM(RTRIM(FullName)), CHARINDEX(' ', LTRIM(RTRIM(FullName))) - 1)
                ELSE ''
            END, 3)) + 
        LOWER(LEFT(
            CASE 
                WHEN CHARINDEX(' ', LTRIM(RTRIM(FullName))) > 0
                THEN LTRIM(SUBSTRING(LTRIM(RTRIM(FullName)), 
                                     CHARINDEX(' ', LTRIM(RTRIM(FullName))) + 1, 
                                     LEN(FullName)))
                ELSE ''
            END, 3)) AS NormalizedKey
    FROM AllGoalieNames
    WHERE CHARINDEX(' ', LTRIM(RTRIM(FullName))) > 0 -- Must have both first and last name
),
-- Group by normalized key to combine variations of the same goalie
GroupedGoalies AS (
    SELECT 
        NormalizedKey,
        -- Take the most common version of the name
        (SELECT TOP 1 FirstName FROM NormalizedGoalies n2 
         WHERE n2.NormalizedKey = n1.NormalizedKey 
         GROUP BY FirstName 
         ORDER BY COUNT(*) DESC) AS FirstName,
        (SELECT TOP 1 LastName FROM NormalizedGoalies n2 
         WHERE n2.NormalizedKey = n1.NormalizedKey 
         GROUP BY LastName 
         ORDER BY COUNT(*) DESC) AS LastName,
        COUNT(DISTINCT SessionId) AS GamesPlayed
    FROM NormalizedGoalies n1
    WHERE 
        LEN(FirstName) > 1 
        AND LEN(LastName) > 1
        AND FirstName NOT LIKE '%[0-9]%'
        AND LastName NOT LIKE '%[0-9]%'
    GROUP BY NormalizedKey
)
-- Final result
SELECT 
    FirstName,
    LastName,
    GamesPlayed
FROM GroupedGoalies
WHERE NormalizedKey != '' -- Filter out any empty keys
ORDER BY 
    GamesPlayed DESC,
    LastName ASC,
    FirstName ASC;

/* 
Note: This query assumes goalie names are mentioned in the session notes
in patterns like:
- "Goalies: John Smith and Jane Doe"
- "Goalie: Mike Johnson"
- "Goalkeepers: Bob Wilson, Tom Brown"

If your data uses different patterns, the parsing logic may need adjustment.
*/
