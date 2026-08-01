-- =====================================================
-- SIMPLE UPDATE: Rent Total with CASE Statement
-- For all 12 invoices at once
-- =====================================================

UPDATE RR_AR_REVENUE_CONTRACT
SET rent_total = CASE trx_number
  WHEN '900042' THEN 33880.00
  WHEN '900045' THEN 35574.00
  WHEN '900046' THEN 25000.00
  WHEN '900040' THEN 24200.00
  WHEN '900039' THEN 25200.00
  WHEN '900036' THEN 31500.00
  WHEN '900047' THEN 33075.00
  WHEN '900044' THEN 33880.00
  WHEN '900038' THEN 32670.00
  WHEN '900037' THEN 25000.00
  WHEN '900048' THEN 25000.00
  WHEN '900041' THEN 440000.00
  ELSE rent_total  -- Keep existing value if not in list
END,
last_updated_date = SYSDATE,
last_updated_by = USER
WHERE trx_number IN ('900042', '900045', '900046', '900040', '900039', '900036',
                     '900047', '900044', '900038', '900037', '900048', '900041');

COMMIT;

-- =====================================================
-- VERIFY: Check updated records
-- =====================================================
SELECT
  trx_number,
  rent_total,
  last_updated_date
FROM RR_AR_REVENUE_CONTRACT
WHERE trx_number IN ('900042', '900045', '900046', '900040', '900039', '900036',
                     '900047', '900044', '900038', '900037', '900048', '900041')
ORDER BY trx_number;
