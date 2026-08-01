-- =====================================================
-- MERGE Statement: Update rent_total in RR_AR_REVENUE_CONTRACT
-- Purpose: Match invoice numbers (trx_number) and update contract amounts
-- Source: Invoice/Contract Amount mapping
-- =====================================================

-- Option 1: MERGE with Inline Source Data (Most Common)
MERGE INTO RR_AR_REVENUE_CONTRACT arc
USING (
  SELECT '900042' AS invoice_num, 33880.00 AS contract_amount FROM DUAL UNION ALL
  SELECT '900045', 35574.00 FROM DUAL UNION ALL
  SELECT '900046', 25000.00 FROM DUAL UNION ALL
  SELECT '900040', 24200.00 FROM DUAL UNION ALL
  SELECT '900039', 25200.00 FROM DUAL UNION ALL
  SELECT '900036', 31500.00 FROM DUAL UNION ALL
  SELECT '900047', 33075.00 FROM DUAL UNION ALL
  SELECT '900044', 33880.00 FROM DUAL UNION ALL
  SELECT '900038', 32670.00 FROM DUAL UNION ALL
  SELECT '900037', 25000.00 FROM DUAL UNION ALL
  SELECT '900048', 25000.00 FROM DUAL UNION ALL
  SELECT '900041', 440000.00 FROM DUAL
) src
ON (arc.trx_number = src.invoice_num)
WHEN MATCHED THEN
  UPDATE SET
    arc.rent_total = src.contract_amount,
    arc.last_updated_date = SYSDATE,
    arc.last_updated_by = USER
WHEN NOT MATCHED THEN
  INSERT (trx_number, rent_total, creation_date, created_by)
  VALUES (src.invoice_num, src.contract_amount, SYSDATE, USER);

-- Commit the changes
COMMIT;

-- =====================================================
-- Verification Query
-- =====================================================
SELECT
  trx_number,
  rent_total,
  last_updated_date
FROM RR_AR_REVENUE_CONTRACT
WHERE trx_number IN ('900042', '900045', '900046', '900040', '900039', '900036',
                     '900047', '900044', '900038', '900037', '900048', '900041')
ORDER BY trx_number;

-- =====================================================
-- Alternative Option 2: If data is in a staging table
-- =====================================================
/*
MERGE INTO RR_AR_REVENUE_CONTRACT arc
USING TEMP_CONTRACT_AMOUNTS tca  -- Replace with your staging table name
ON (arc.trx_number = tca.invoice_num)
WHEN MATCHED THEN
  UPDATE SET
    arc.rent_total = tca.contract_amount,
    arc.last_updated_date = SYSDATE,
    arc.last_updated_by = USER
WHEN NOT MATCHED THEN
  INSERT (trx_number, rent_total, creation_date, created_by)
  VALUES (tca.invoice_num, tca.contract_amount, SYSDATE, USER);

COMMIT;
*/

-- =====================================================
-- Alternative Option 3: Simple UPDATE (if all records exist)
-- =====================================================
/*
UPDATE RR_AR_REVENUE_CONTRACT arc
SET rent_total = (
  SELECT contract_amount
  FROM (
    SELECT '900042' AS invoice_num, 33880.00 AS contract_amount FROM DUAL UNION ALL
    SELECT '900045', 35574.00 FROM DUAL UNION ALL
    SELECT '900046', 25000.00 FROM DUAL UNION ALL
    SELECT '900040', 24200.00 FROM DUAL UNION ALL
    SELECT '900039', 25200.00 FROM DUAL UNION ALL
    SELECT '900036', 31500.00 FROM DUAL UNION ALL
    SELECT '900047', 33075.00 FROM DUAL UNION ALL
    SELECT '900044', 33880.00 FROM DUAL UNION ALL
    SELECT '900038', 32670.00 FROM DUAL UNION ALL
    SELECT '900037', 25000.00 FROM DUAL UNION ALL
    SELECT '900048', 25000.00 FROM DUAL UNION ALL
    SELECT '900041', 440000.00 FROM DUAL
  ) src
  WHERE src.invoice_num = arc.trx_number
),
last_updated_date = SYSDATE,
last_updated_by = USER
WHERE arc.trx_number IN ('900042', '900045', '900046', '900040', '900039', '900036',
                         '900047', '900044', '900038', '900037', '900048', '900041');

COMMIT;
*/

-- =====================================================
-- Count of Updated Records
-- =====================================================
SELECT COUNT(*) AS updated_records
FROM RR_AR_REVENUE_CONTRACT
WHERE trx_number IN ('900042', '900045', '900046', '900040', '900039', '900036',
                     '900047', '900044', '900038', '900037', '900048', '900041')
AND rent_total > 0;
