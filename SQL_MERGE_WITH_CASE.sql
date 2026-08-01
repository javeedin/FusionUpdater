-- =====================================================
-- MERGE with CASE Statement: Update rent_total
-- Multiple scenarios for conditional updates
-- =====================================================

-- =====================================================
-- SCENARIO 1: CASE for Rent Calculation Based on Amount Tiers
-- =====================================================
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
    arc.rent_total = CASE
      -- Tier 1: Less than 25,000 - Use as-is
      WHEN src.contract_amount < 25000 THEN src.contract_amount
      -- Tier 2: 25,000 to 35,000 - Apply 95% factor
      WHEN src.contract_amount >= 25000 AND src.contract_amount <= 35000
        THEN ROUND(src.contract_amount * 0.95, 2)
      -- Tier 3: 35,001 to 100,000 - Apply 90% factor
      WHEN src.contract_amount > 35000 AND src.contract_amount <= 100000
        THEN ROUND(src.contract_amount * 0.90, 2)
      -- Tier 4: Greater than 100,000 - Apply 85% factor
      WHEN src.contract_amount > 100000
        THEN ROUND(src.contract_amount * 0.85, 2)
      -- Default: Use original amount
      ELSE src.contract_amount
    END,
    arc.contract_tier = CASE
      WHEN src.contract_amount < 25000 THEN 'TIER_1_SMALL'
      WHEN src.contract_amount >= 25000 AND src.contract_amount <= 35000 THEN 'TIER_2_MEDIUM'
      WHEN src.contract_amount > 35000 AND src.contract_amount <= 100000 THEN 'TIER_3_LARGE'
      WHEN src.contract_amount > 100000 THEN 'TIER_4_ENTERPRISE'
      ELSE 'UNCLASSIFIED'
    END,
    arc.last_updated_date = SYSDATE,
    arc.last_updated_by = USER;

COMMIT;

-- =====================================================
-- SCENARIO 2: CASE for Monthly vs Annual Rent Calculation
-- =====================================================
/*
MERGE INTO RR_AR_REVENUE_CONTRACT arc
USING (
  SELECT '900042' AS invoice_num, 33880.00 AS contract_amount, 'ANNUAL' AS billing_freq FROM DUAL UNION ALL
  SELECT '900045', 35574.00, 'ANNUAL' FROM DUAL UNION ALL
  SELECT '900046', 25000.00, 'MONTHLY' FROM DUAL UNION ALL
  SELECT '900040', 24200.00, 'MONTHLY' FROM DUAL UNION ALL
  SELECT '900039', 25200.00, 'ANNUAL' FROM DUAL UNION ALL
  SELECT '900036', 31500.00, 'ANNUAL' FROM DUAL UNION ALL
  SELECT '900047', 33075.00, 'MONTHLY' FROM DUAL UNION ALL
  SELECT '900044', 33880.00, 'ANNUAL' FROM DUAL UNION ALL
  SELECT '900038', 32670.00, 'MONTHLY' FROM DUAL UNION ALL
  SELECT '900037', 25000.00, 'ANNUAL' FROM DUAL UNION ALL
  SELECT '900048', 25000.00, 'MONTHLY' FROM DUAL UNION ALL
  SELECT '900041', 440000.00, 'ANNUAL' FROM DUAL
) src
ON (arc.trx_number = src.invoice_num)
WHEN MATCHED THEN
  UPDATE SET
    arc.rent_total = CASE
      -- If ANNUAL: divide by 12 for monthly rent
      WHEN src.billing_freq = 'ANNUAL'
        THEN ROUND(src.contract_amount / 12, 2)
      -- If MONTHLY: use as-is
      WHEN src.billing_freq = 'MONTHLY'
        THEN src.contract_amount
      -- Default: use original amount
      ELSE src.contract_amount
    END,
    arc.billing_frequency = src.billing_freq,
    arc.annualized_amount = CASE
      WHEN src.billing_freq = 'MONTHLY'
        THEN ROUND(src.contract_amount * 12, 2)
      ELSE src.contract_amount
    END,
    arc.last_updated_date = SYSDATE,
    arc.last_updated_by = USER;

COMMIT;
*/

-- =====================================================
-- SCENARIO 3: CASE for Revenue Recognition Rules
-- =====================================================
/*
MERGE INTO RR_AR_REVENUE_CONTRACT arc
USING (
  SELECT '900042' AS invoice_num, 33880.00 AS contract_amount, 'SERVICE' AS contract_type FROM DUAL UNION ALL
  SELECT '900045', 35574.00, 'PRODUCT' FROM DUAL UNION ALL
  SELECT '900046', 25000.00, 'SUBSCRIPTION' FROM DUAL UNION ALL
  SELECT '900040', 24200.00, 'SERVICE' FROM DUAL UNION ALL
  SELECT '900039', 25200.00, 'PRODUCT' FROM DUAL UNION ALL
  SELECT '900036', 31500.00, 'SUBSCRIPTION' FROM DUAL UNION ALL
  SELECT '900047', 33075.00, 'SERVICE' FROM DUAL UNION ALL
  SELECT '900044', 33880.00, 'PRODUCT' FROM DUAL UNION ALL
  SELECT '900038', 32670.00, 'SUBSCRIPTION' FROM DUAL UNION ALL
  SELECT '900037', 25000.00, 'SERVICE' FROM DUAL UNION ALL
  SELECT '900048', 25000.00, 'PRODUCT' FROM DUAL UNION ALL
  SELECT '900041', 440000.00, 'SUBSCRIPTION' FROM DUAL
) src
ON (arc.trx_number = src.invoice_num)
WHEN MATCHED THEN
  UPDATE SET
    arc.rent_total = CASE
      -- SERVICE: Recognize 100% upfront
      WHEN src.contract_type = 'SERVICE'
        THEN src.contract_amount
      -- PRODUCT: Recognize 70% upfront, 30% over 3 months
      WHEN src.contract_type = 'PRODUCT'
        THEN ROUND(src.contract_amount * 0.70, 2)
      -- SUBSCRIPTION: Recognize monthly (1/12 per month)
      WHEN src.contract_type = 'SUBSCRIPTION'
        THEN ROUND(src.contract_amount / 12, 2)
      ELSE src.contract_amount
    END,
    arc.revenue_recognition_method = src.contract_type,
    arc.last_updated_date = SYSDATE,
    arc.last_updated_by = USER;

COMMIT;
*/

-- =====================================================
-- SCENARIO 4: CASE for Discounts and Adjustments
-- =====================================================
/*
MERGE INTO RR_AR_REVENUE_CONTRACT arc
USING (
  SELECT '900042' AS invoice_num, 33880.00 AS contract_amount, 5 AS discount_percent FROM DUAL UNION ALL
  SELECT '900045', 35574.00, 0 FROM DUAL UNION ALL
  SELECT '900046', 25000.00, 10 FROM DUAL UNION ALL
  SELECT '900040', 24200.00, 5 FROM DUAL UNION ALL
  SELECT '900039', 25200.00, 0 FROM DUAL UNION ALL
  SELECT '900036', 31500.00, 7 FROM DUAL UNION ALL
  SELECT '900047', 33075.00, 0 FROM DUAL UNION ALL
  SELECT '900044', 33880.00, 5 FROM DUAL UNION ALL
  SELECT '900038', 32670.00, 10 FROM DUAL UNION ALL
  SELECT '900037', 25000.00, 0 FROM DUAL UNION ALL
  SELECT '900048', 25000.00, 15 FROM DUAL UNION ALL
  SELECT '900041', 440000.00, 0 FROM DUAL
) src
ON (arc.trx_number = src.invoice_num)
WHEN MATCHED THEN
  UPDATE SET
    arc.rent_total = ROUND(src.contract_amount * (1 - src.discount_percent/100), 2),
    arc.original_amount = src.contract_amount,
    arc.discount_percentage = src.discount_percent,
    arc.discount_amount = ROUND(src.contract_amount * src.discount_percent / 100, 2),
    arc.last_updated_date = SYSDATE,
    arc.last_updated_by = USER;

COMMIT;
*/

-- =====================================================
-- SCENARIO 5: CASE with Status-Based Logic
-- =====================================================
/*
MERGE INTO RR_AR_REVENUE_CONTRACT arc
USING (
  SELECT '900042' AS invoice_num, 33880.00 AS contract_amount, 'ACTIVE' AS contract_status FROM DUAL UNION ALL
  SELECT '900045', 35574.00, 'ACTIVE' FROM DUAL UNION ALL
  SELECT '900046', 25000.00, 'PENDING' FROM DUAL UNION ALL
  SELECT '900040', 24200.00, 'ACTIVE' FROM DUAL UNION ALL
  SELECT '900039', 25200.00, 'INACTIVE' FROM DUAL UNION ALL
  SELECT '900036', 31500.00, 'ACTIVE' FROM DUAL UNION ALL
  SELECT '900047', 33075.00, 'PENDING' FROM DUAL UNION ALL
  SELECT '900044', 33880.00, 'ACTIVE' FROM DUAL UNION ALL
  SELECT '900038', 32670.00, 'CANCELLED' FROM DUAL UNION ALL
  SELECT '900037', 25000.00, 'ACTIVE' FROM DUAL UNION ALL
  SELECT '900048', 25000.00, 'PENDING' FROM DUAL UNION ALL
  SELECT '900041', 440000.00, 'ACTIVE' FROM DUAL
) src
ON (arc.trx_number = src.invoice_num)
WHEN MATCHED THEN
  UPDATE SET
    arc.rent_total = CASE
      -- ACTIVE: Use full amount
      WHEN src.contract_status = 'ACTIVE'
        THEN src.contract_amount
      -- PENDING: Use 50% (contingent)
      WHEN src.contract_status = 'PENDING'
        THEN ROUND(src.contract_amount * 0.50, 2)
      -- INACTIVE: Use 0 (no revenue)
      WHEN src.contract_status = 'INACTIVE'
        THEN 0
      -- CANCELLED: Use 0 (no revenue)
      WHEN src.contract_status = 'CANCELLED'
        THEN 0
      ELSE src.contract_amount
    END,
    arc.contract_status = src.contract_status,
    arc.is_revenue_eligible = CASE
      WHEN src.contract_status IN ('ACTIVE', 'PENDING') THEN 1
      ELSE 0
    END,
    arc.last_updated_date = SYSDATE,
    arc.last_updated_by = USER;

COMMIT;
*/

-- =====================================================
-- SCENARIO 6: CASE with Complex Business Logic
-- =====================================================
/*
MERGE INTO RR_AR_REVENUE_CONTRACT arc
USING (
  SELECT '900042' AS invoice_num, 33880.00 AS contract_amount, 12 AS contract_months, 'STANDARD' AS contract_class FROM DUAL UNION ALL
  SELECT '900045', 35574.00, 12, 'PREMIUM' FROM DUAL UNION ALL
  SELECT '900046', 25000.00, 6, 'STANDARD' FROM DUAL UNION ALL
  SELECT '900040', 24200.00, 12, 'STANDARD' FROM DUAL UNION ALL
  SELECT '900039', 25200.00, 24, 'PREMIUM' FROM DUAL UNION ALL
  SELECT '900036', 31500.00, 12, 'STANDARD' FROM DUAL UNION ALL
  SELECT '900047', 33075.00, 6, 'STANDARD' FROM DUAL UNION ALL
  SELECT '900044', 33880.00, 12, 'PREMIUM' FROM DUAL UNION ALL
  SELECT '900038', 32670.00, 12, 'STANDARD' FROM DUAL UNION ALL
  SELECT '900037', 25000.00, 24, 'PREMIUM' FROM DUAL UNION ALL
  SELECT '900048', 25000.00, 6, 'STANDARD' FROM DUAL UNION ALL
  SELECT '900041', 440000.00, 12, 'ENTERPRISE' FROM DUAL
) src
ON (arc.trx_number = src.invoice_num)
WHEN MATCHED THEN
  UPDATE SET
    arc.rent_total = CASE
      -- ENTERPRISE: Always apply 15% discount
      WHEN src.contract_class = 'ENTERPRISE'
        THEN ROUND(src.contract_amount * 0.85, 2)
      -- PREMIUM + 24 months: Apply 10% discount
      WHEN src.contract_class = 'PREMIUM' AND src.contract_months = 24
        THEN ROUND(src.contract_amount * 0.90, 2)
      -- PREMIUM + 12 months: Apply 5% discount
      WHEN src.contract_class = 'PREMIUM' AND src.contract_months = 12
        THEN ROUND(src.contract_amount * 0.95, 2)
      -- PREMIUM + 6 months: No discount
      WHEN src.contract_class = 'PREMIUM' AND src.contract_months = 6
        THEN src.contract_amount
      -- STANDARD + 24 months: Apply 7% discount
      WHEN src.contract_class = 'STANDARD' AND src.contract_months = 24
        THEN ROUND(src.contract_amount * 0.93, 2)
      -- STANDARD + 12 months: Apply 3% discount
      WHEN src.contract_class = 'STANDARD' AND src.contract_months = 12
        THEN ROUND(src.contract_amount * 0.97, 2)
      -- STANDARD + 6 months: No discount
      WHEN src.contract_class = 'STANDARD' AND src.contract_months = 6
        THEN src.contract_amount
      -- Default
      ELSE src.contract_amount
    END,
    arc.monthly_rent = ROUND(src.contract_amount / src.contract_months, 2),
    arc.contract_term_months = src.contract_months,
    arc.contract_class = src.contract_class,
    arc.last_updated_date = SYSDATE,
    arc.last_updated_by = USER;

COMMIT;
*/

-- =====================================================
-- VERIFICATION: View updated records with CASE calculations
-- =====================================================
SELECT
  arc.trx_number,
  arc.contract_amount,
  arc.rent_total,
  arc.contract_tier,
  CASE
    WHEN arc.rent_total < 25000 THEN 'LOW_VALUE'
    WHEN arc.rent_total >= 25000 AND arc.rent_total <= 100000 THEN 'MID_VALUE'
    WHEN arc.rent_total > 100000 THEN 'HIGH_VALUE'
  END AS rent_category,
  arc.last_updated_date
FROM RR_AR_REVENUE_CONTRACT arc
WHERE arc.trx_number IN ('900042', '900045', '900046', '900040', '900039', '900036',
                         '900047', '900044', '900038', '900037', '900048', '900041')
ORDER BY arc.trx_number;

-- =====================================================
-- SUMMARY: What Each SCENARIO Does
-- =====================================================
/*
SCENARIO 1 (ACTIVE): Tier-based rent calculation
  - Tier 1 (<25K): 100%
  - Tier 2 (25-35K): 95%
  - Tier 3 (35-100K): 90%
  - Tier 4 (>100K): 85%

SCENARIO 2 (COMMENTED): Monthly vs Annual rent
  - ANNUAL: Divide by 12
  - MONTHLY: Use as-is

SCENARIO 3 (COMMENTED): Revenue recognition rules
  - SERVICE: 100% upfront
  - PRODUCT: 70% upfront + 30% over 3 months
  - SUBSCRIPTION: 1/12 monthly

SCENARIO 4 (COMMENTED): Discounts and adjustments
  - Apply tiered discounts (0-15%)
  - Calculate discount amounts

SCENARIO 5 (COMMENTED): Status-based logic
  - ACTIVE: 100%
  - PENDING: 50%
  - INACTIVE/CANCELLED: 0%

SCENARIO 6 (COMMENTED): Complex business logic
  - Class-based + Contract term-based discounts
  - Calculate monthly rent
*/
