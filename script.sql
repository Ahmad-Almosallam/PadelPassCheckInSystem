UPDATE ci
SET EndUserSubscriptionId = (
    SELECT TOP 1 eus.Id
    FROM test.EndUserSubscriptions eus
    WHERE eus.EndUserId = ci.EndUserId
      AND CAST(DATEADD(HOUR, 3, ci.CheckInDateTime) AS DATE) >= eus.StartDate
      AND CAST(DATEADD(HOUR, 3, ci.CheckInDateTime) AS DATE) <= eus.EndDate
    ORDER BY
        eus.StartDate DESC, -- Prefer more recent subscriptions if overlapping
        eus.Id DESC -- If same start date, prefer higher ID
)
FROM test.CheckIns ci
WHERE EXISTS (
    SELECT 1
    FROM test.EndUserSubscriptions eus
    WHERE eus.EndUserId = ci.EndUserId
      AND CAST(DATEADD(HOUR, 3, ci.CheckInDateTime) AS DATE) >= eus.StartDate
      AND CAST(DATEADD(HOUR, 3, ci.CheckInDateTime) AS DATE) <= eus.EndDate
)
  AND ci.EndUserSubscriptionId IS NULL;


SELECT
    ci.Id as CheckInId,
    ci.EndUserId,
    DATEADD(HOUR, 3, ci.CheckInDateTime) AS CheckInDateTime,
    ci.EndUserSubscriptionId,
    eu.Name as EndUserName,
    eu.PhoneNumber as EndUserPhoneNumber,
    eus.Id as SubscriptionId,
    eus.StartDate as SubStartDate,
    eus.EndDate as SubEndDate,
    eus.Status as SubStatus,
    eus.Name as SubName,
    eus.Code as SubCode,
    CASE
        WHEN CAST(DATEADD(HOUR, 3, ci.CheckInDateTime) AS DATE) BETWEEN eus.StartDate AND eus.EndDate
            THEN 'MATCH'
        ELSE 'INVALID MATCH - CHECK!'
        END as ValidationStatus
FROM test.CheckIns ci
         INNER JOIN test.EndUsers eu ON ci.EndUserId = eu.Id
         LEFT JOIN test.EndUserSubscriptions eus ON ci.EndUserSubscriptionId = eus.Id
ORDER BY ci.CheckInDateTime DESC;


