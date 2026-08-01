# SQL MERGE: Update rent_total in RR_AR_REVENUE_CONTRACT

## Overview
Update the `rent_total` column in `RR_AR_REVENUE_CONTRACT` table by matching invoice numbers (as `trx_number`) with contract amounts.

## Data Mapping
| Invoice (trx_number) | Contract Amount | Existing rent_total |
|---|---|---|
| 900042 | 33,880.00 | ? |
| 900045 | 35,574.00 | ? |
| 900046 | 25,000.00 | ? |
| 900040 | 24,200.00 | ? |
| 900039 | 25,200.00 | ? |
| 900036 | 31,500.00 | ? |
| 900047 | 33,075.00 | ? |
| 900044 | 33,880.00 | ? |
| 900038 | 32,670.00 | ? |
| 900037 | 25,000.00 | ? |
| 900048 | 25,000.00 | ? |
| 900041 | 440,000.00 | ? |

## MERGE Statement Explained

### Option 1: MERGE with Inline Data (Recommended)
```sql
MERGE INTO RR_AR_REVENUE_CONTRACT arc
USING (
  -- Source: Invoice to Contract Amount mapping
  SELECT '900042' AS invoice_num, 33880.00 AS contract_amount FROM DUAL UNION ALL
  SELECT '900045', 35574.00 FROM DUAL UNION ALL
  -- ... more invoices ...
) src
ON (arc.trx_number = src.invoice_num)  -- JOIN condition
WHEN MATCHED THEN
  UPDATE SET
    arc.rent_total = src.contract_amount,
    arc.last_updated_date = SYSDATE,
    arc.last_updated_by = USER
WHEN NOT MATCHED THEN
  INSERT (trx_number, rent_total, creation_date, created_by)
  VALUES (src.invoice_num, src.contract_amount, SYSDATE, USER);

COMMIT;
```

**How it works:**
1. **USING clause**: Creates temporary source table with invoice → amount mapping
2. **ON clause**: Joins table on `trx_number = invoice_num`
3. **WHEN MATCHED**: If trx_number exists, update `rent_total`
4. **WHEN NOT MATCHED**: If trx_number doesn't exist, insert new row (optional)

### Option 2: From Staging Table
If you have the data in a table already:
```sql
MERGE INTO RR_AR_REVENUE_CONTRACT arc
USING TEMP_CONTRACT_AMOUNTS tca
ON (arc.trx_number = tca.invoice_num)
WHEN MATCHED THEN
  UPDATE SET
    arc.rent_total = tca.contract_amount,
    arc.last_updated_date = SYSDATE,
    arc.last_updated_by = USER;

COMMIT;
```

### Option 3: Simple UPDATE (if all records exist)
If you're certain all trx_number values exist:
```sql
UPDATE RR_AR_REVENUE_CONTRACT arc
SET rent_total = (
  SELECT contract_amount
  FROM (
    SELECT '900042' AS invoice_num, 33880.00 AS contract_amount FROM DUAL UNION ALL
    -- ... more invoices ...
  ) src
  WHERE src.invoice_num = arc.trx_number
),
last_updated_date = SYSDATE,
last_updated_by = USER
WHERE arc.trx_number IN ('900042', '900045', '900046', '900040', '900039', '900036',
                         '900047', '900044', '900038', '900037', '900048', '900041');

COMMIT;
```

## Verification Steps

### 1. Check records BEFORE update
```sql
SELECT
  trx_number,
  rent_total,
  creation_date,
  last_updated_date
FROM RR_AR_REVENUE_CONTRACT
WHERE trx_number IN ('900042', '900045', '900046', '900040', '900039', '900036',
                     '900047', '900044', '900038', '900037', '900048', '900041')
ORDER BY trx_number;
```

### 2. Run MERGE statement
Execute the MERGE statement from the SQL file.

### 3. Check records AFTER update
```sql
SELECT
  trx_number,
  rent_total,
  last_updated_date,
  last_updated_by
FROM RR_AR_REVENUE_CONTRACT
WHERE trx_number IN ('900042', '900045', '900046', '900040', '900039', '900036',
                     '900047', '900044', '900038', '900037', '900048', '900041')
ORDER BY trx_number;
```

### 4. Verify data integrity
```sql
-- Count updated records
SELECT COUNT(*) AS updated_records
FROM RR_AR_REVENUE_CONTRACT
WHERE trx_number IN ('900042', '900045', '900046', '900040', '900039', '900036',
                     '900047', '900044', '900038', '900037', '900048', '900041')
AND rent_total > 0;

-- Should return: 12 (or the number of records that should have been updated)
```

## Important Notes

### 1. Data Type Considerations
- Ensure `rent_total` column can hold the values (especially 440,000.00)
- Check if `trx_number` is VARCHAR2 or NUMBER
  - If NUMBER, convert: `SELECT 900042` (not '900042')
  - If VARCHAR2, use: `SELECT '900042'`

### 2. WHEN NOT MATCHED Clause
- **REMOVE** if you only want to update existing records
- **KEEP** if new trx_numbers should be inserted
- Current statement will insert if trx_number doesn't exist

### 3. Audit Columns
The statement updates:
- `rent_total`: The contract amount
- `last_updated_date`: Current system date
- `last_updated_by`: Current database user

Adjust these if your table has different audit column names:
- `updated_date` vs `last_updated_date`
- `updated_by` vs `last_updated_by`
- `modified_date` vs `last_updated_date`

### 4. Transaction Control
- **COMMIT**: Saves all changes
- **ROLLBACK**: Reverts all changes (if needed before COMMIT)

To test without committing:
```sql
-- Run MERGE statement
-- Verify results
-- If OK: COMMIT;
-- If NOT OK: ROLLBACK;
```

## Safety: Test in DEV First
```sql
-- 1. Test in DEV environment
-- 2. Verify all 12 records updated correctly
-- 3. Compare rent_total values with source data
-- 4. Once confirmed, run in PROD
```

## Rollback Plan
If something goes wrong:
```sql
-- Immediately run after bad merge:
ROLLBACK;

-- Or if already committed, restore from backup/previous state
-- Contact Oracle DBA for point-in-time recovery
```

## Summary
- **Best choice**: Option 1 (MERGE with inline data) - Most flexible
- **Alternative**: Option 2 (from staging table) - If data volume is large
- **Simple case**: Option 3 (UPDATE) - If all records exist

All options produce the same result. Choose based on your data source and comfort level.
