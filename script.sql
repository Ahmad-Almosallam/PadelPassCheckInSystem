UPDATE ci
SET EndUserSubscriptionId = (
    SELECT TOP 1 eus.Id
    FROM access.EndUserSubscriptions eus
    WHERE eus.EndUserId = ci.EndUserId
      AND DATEADD(HOUR, 3, ci.CheckInDateTime) >= eus.StartDate
      AND DATEADD(HOUR, 3, ci.CheckInDateTime) <= eus.EndDate
    ORDER BY
        eus.StartDate DESC, -- Prefer more recent subscriptions if overlapping
        eus.Id DESC -- If same start date, prefer higher ID
)
FROM access.CheckIns ci
WHERE EXISTS (
    SELECT 1
    FROM access.EndUserSubscriptions eus
    WHERE eus.EndUserId = ci.EndUserId
      AND DATEADD(HOUR, 3, ci.CheckInDateTime) >= eus.StartDate
      AND DATEADD(HOUR, 3, ci.CheckInDateTime) <= eus.EndDate
)
  AND ci.EndUserSubscriptionId IS NULL;


SELECT
    ci.Id as CheckInId,
    ci.EndUserId,
    DATEADD(HOUR, 3, ci.CheckInDateTime) AS CheckInDateTime,
    ci.EndUserSubscriptionId,
    eu.Name as EndUserName,
    eus.Id as SubscriptionId,
    eus.StartDate as SubStartDate,
    eus.EndDate as SubEndDate,
    eus.Status as SubStatus,
    eus.Name as SubName,
    eus.Code as SubCode,
    CASE
        WHEN DATEADD(HOUR, 3, ci.CheckInDateTime) BETWEEN eus.StartDate AND eus.EndDate
            THEN 'MATCH'
        ELSE 'INVALID MATCH - CHECK!'
        END as ValidationStatus
FROM access.CheckIns ci
         INNER JOIN access.EndUsers eu ON ci.EndUserId = eu.Id
         LEFT JOIN access.EndUserSubscriptions eus ON ci.EndUserSubscriptionId = eus.Id
ORDER BY ci.CheckInDateTime DESC;