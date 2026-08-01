# MERGE with CASE Statement: 6 Scenarios

## Overview
CASE statements in MERGE allow conditional logic for complex calculations when updating `rent_total`.

---

## SCENARIO 1: Tier-Based Rent Calculation (ACTIVE)

**Use When:** Different discount tiers based on contract amount

**Logic:**
```
< $25,000        → Use 100% (no discount)
$25,000-$35,000  → Apply 95% factor
$35,001-$100,000 → Apply 90% factor
> $100,000       → Apply 85% factor
```

**SQL Pattern:**
```sql
arc.rent_total = CASE
  WHEN src.contract_amount < 25000 THEN src.contract_amount
  WHEN src.contract_amount >= 25000 AND src.contract_amount <= 35000
    THEN ROUND(src.contract_amount * 0.95, 2)
  WHEN src.contract_amount > 35000 AND src.contract_amount <= 100000
    THEN ROUND(src.contract_amount * 0.90, 2)
  WHEN src.contract_amount > 100000
    THEN ROUND(src.contract_amount * 0.85, 2)
  ELSE src.contract_amount
END
```

**Example:**
```
Invoice 900042: $33,880  → Tier 2 → $33,880 × 0.95 = $32,186.00
Invoice 900041: $440,000 → Tier 4 → $440,000 × 0.85 = $374,000.00
```

**Updates:**
- `rent_total`: Calculated value
- `contract_tier`: Tier classification (TIER_1_SMALL, TIER_2_MEDIUM, etc.)

---

## SCENARIO 2: Monthly vs Annual Billing

**Use When:** Different billing frequencies need different calculations

**Logic:**
```
ANNUAL   → Divide by 12 (convert to monthly)
MONTHLY  → Use as-is
```

**SQL Pattern:**
```sql
arc.rent_total = CASE
  WHEN src.billing_freq = 'ANNUAL'
    THEN ROUND(src.contract_amount / 12, 2)
  WHEN src.billing_freq = 'MONTHLY'
    THEN src.contract_amount
  ELSE src.contract_amount
END,
arc.annualized_amount = CASE
  WHEN src.billing_freq = 'MONTHLY'
    THEN ROUND(src.contract_amount * 12, 2)
  ELSE src.contract_amount
END
```

**Example:**
```
Invoice 900042: $33,880 (ANNUAL)  → $33,880 / 12 = $2,823.33/month
Invoice 900046: $25,000 (MONTHLY) → $25,000/month
```

**Updates:**
- `rent_total`: Monthly rent amount
- `billing_frequency`: ANNUAL or MONTHLY
- `annualized_amount`: Annualized equivalent

---

## SCENARIO 3: Revenue Recognition Rules

**Use When:** Different contract types have different recognition methods

**Logic:**
```
SERVICE      → Recognize 100% upfront
PRODUCT      → Recognize 70% upfront + 30% over 3 months
SUBSCRIPTION → Recognize 1/12 per month
```

**SQL Pattern:**
```sql
arc.rent_total = CASE
  WHEN src.contract_type = 'SERVICE'
    THEN src.contract_amount
  WHEN src.contract_type = 'PRODUCT'
    THEN ROUND(src.contract_amount * 0.70, 2)
  WHEN src.contract_type = 'SUBSCRIPTION'
    THEN ROUND(src.contract_amount / 12, 2)
  ELSE src.contract_amount
END,
arc.revenue_recognition_method = src.contract_type
```

**Example:**
```
Invoice 900042: $33,880 (SERVICE)      → $33,880.00
Invoice 900046: $25,000 (PRODUCT)      → $25,000 × 0.70 = $17,500.00
Invoice 900041: $440,000 (SUBSCRIPTION) → $440,000 / 12 = $36,666.67
```

**Updates:**
- `rent_total`: Amount to recognize
- `revenue_recognition_method`: Recognition method

---

## SCENARIO 4: Discounts and Adjustments

**Use When:** Apply tiered discounts and track discount amounts separately

**Logic:**
```
Based on discount_percent column:
- 0% discount  → Use 100%
- 5% discount  → Use 95%
- 10% discount → Use 90%
- 15% discount → Use 85%
```

**SQL Pattern:**
```sql
arc.rent_total = ROUND(src.contract_amount * (1 - src.discount_percent/100), 2),
arc.original_amount = src.contract_amount,
arc.discount_percentage = src.discount_percent,
arc.discount_amount = ROUND(src.contract_amount * src.discount_percent / 100, 2)
```

**Example:**
```
Invoice 900042: $33,880 (5% discount)  → $33,880 - $1,694 = $32,186.00
Invoice 900048: $25,000 (15% discount) → $25,000 - $3,750 = $21,250.00
```

**Updates:**
- `rent_total`: Discounted amount
- `original_amount`: Before discount
- `discount_percentage`: % discount
- `discount_amount`: $ amount discounted

---

## SCENARIO 5: Status-Based Logic

**Use When:** Contract status determines if revenue should be recognized

**Logic:**
```
ACTIVE       → Use 100% (recognized)
PENDING      → Use 50% (contingent)
INACTIVE     → Use 0% (no revenue)
CANCELLED    → Use 0% (no revenue)
```

**SQL Pattern:**
```sql
arc.rent_total = CASE
  WHEN src.contract_status = 'ACTIVE'
    THEN src.contract_amount
  WHEN src.contract_status = 'PENDING'
    THEN ROUND(src.contract_amount * 0.50, 2)
  WHEN src.contract_status IN ('INACTIVE', 'CANCELLED')
    THEN 0
  ELSE src.contract_amount
END,
arc.contract_status = src.contract_status,
arc.is_revenue_eligible = CASE
  WHEN src.contract_status IN ('ACTIVE', 'PENDING') THEN 1
  ELSE 0
END
```

**Example:**
```
Invoice 900042: $33,880 (ACTIVE)    → $33,880.00
Invoice 900046: $25,000 (PENDING)   → $25,000 × 0.50 = $12,500.00
Invoice 900038: $32,670 (CANCELLED) → $0.00
```

**Updates:**
- `rent_total`: Recognized amount
- `contract_status`: Current status
- `is_revenue_eligible`: 1 (eligible) or 0 (not eligible)

---

## SCENARIO 6: Complex Business Logic (RECOMMENDED)

**Use When:** Combine multiple factors (class, term, amount)

**Logic:**
```
ENTERPRISE class:
  - Always 15% discount

PREMIUM class:
  - 24 months: 10% discount
  - 12 months: 5% discount
  - 6 months: No discount

STANDARD class:
  - 24 months: 7% discount
  - 12 months: 3% discount
  - 6 months: No discount
```

**SQL Pattern:**
```sql
arc.rent_total = CASE
  WHEN src.contract_class = 'ENTERPRISE'
    THEN ROUND(src.contract_amount * 0.85, 2)
  WHEN src.contract_class = 'PREMIUM' AND src.contract_months = 24
    THEN ROUND(src.contract_amount * 0.90, 2)
  WHEN src.contract_class = 'PREMIUM' AND src.contract_months = 12
    THEN ROUND(src.contract_amount * 0.95, 2)
  -- ... more conditions ...
  ELSE src.contract_amount
END,
arc.monthly_rent = ROUND(src.contract_amount / src.contract_months, 2),
arc.contract_term_months = src.contract_months,
arc.contract_class = src.contract_class
```

**Example:**
```
Invoice 900045: $35,574 (PREMIUM, 12 months)
  → $35,574 × 0.95 = $33,795.30/year
  → $33,795.30 / 12 = $2,816.27/month

Invoice 900041: $440,000 (ENTERPRISE, 12 months)
  → $440,000 × 0.85 = $374,000/year
  → $374,000 / 12 = $31,166.67/month
```

**Updates:**
- `rent_total`: Calculated rent
- `monthly_rent`: Calculated monthly rate
- `contract_term_months`: Term length
- `contract_class`: Class type

---

## Comparison: Which Scenario to Use?

| Scenario | Best For | Complexity | Data Needed |
|----------|----------|-----------|------------|
| 1: Tier-Based | Volume-based discounts | Low | Contract amount |
| 2: Monthly/Annual | Different billing periods | Low | Billing frequency |
| 3: Revenue Recognition | ASC 606 compliance | Medium | Contract type |
| 4: Discounts | Fixed/tiered discounts | Low | Discount % |
| 5: Status-Based | Contingent revenue | Medium | Contract status |
| 6: Complex Logic | Multi-factor decisions | High | Amount + term + class |

---

## How to Choose Your Scenario

1. **Are contracts different sizes?**
   - YES → Scenario 1 or 6 (tier-based discounts)
   - NO → Scenario 2, 3, 4, or 5

2. **Do you bill monthly or annually?**
   - YES → Scenario 2 (monthly/annual conversion)

3. **Do you need revenue recognition rules?**
   - YES → Scenario 3 (ASC 606/revenue standard)

4. **Do you offer tiered discounts?**
   - YES → Scenario 4 (discount tracking)

5. **Can contracts be inactive/pending?**
   - YES → Scenario 5 (status-based)

6. **Multiple factors (class + term + amount)?**
   - YES → Scenario 6 (complex logic)

---

## Implementation Steps

### Step 1: Choose Your Scenario
Review above and pick the one matching your business logic.

### Step 2: Uncomment the Scenario
```sql
-- Open SQL_MERGE_WITH_CASE.sql
-- Find the scenario you want (SCENARIO 1 is active, others are commented)
-- Uncomment the WHEN/UPDATE block you need
```

### Step 3: Test in DEV
```sql
-- Run in development database first
-- Verify calculations are correct
-- Check all 12 invoices
```

### Step 4: Verify Results
```sql
SELECT trx_number, contract_amount, rent_total
FROM RR_AR_REVENUE_CONTRACT
WHERE trx_number IN ('900042', '900045', ...);
```

### Step 5: Deploy to PROD
```sql
-- Once verified, run in production
-- COMMIT to save changes
```

---

## Common CASE Patterns

### Pattern 1: Percentage-Based
```sql
ROUND(base_value * percentage_factor, 2)
```

### Pattern 2: Division-Based
```sql
ROUND(base_value / divisor, 2)
```

### Pattern 3: Conditional Multiplication
```sql
CASE WHEN condition THEN value * factor ELSE value END
```

### Pattern 4: Range-Based
```sql
CASE
  WHEN value < 10000 THEN factor_1
  WHEN value >= 10000 AND value < 50000 THEN factor_2
  WHEN value >= 50000 THEN factor_3
END
```

### Pattern 5: Text-Based
```sql
CASE
  WHEN status = 'ACTIVE' THEN amount
  WHEN status = 'PENDING' THEN amount * 0.5
  ELSE 0
END
```

---

## Troubleshooting

### Issue: Decimal precision problems
**Solution:** Always use `ROUND(value, 2)` for currency calculations

### Issue: NULL values in CASE
**Solution:** Add `ELSE` clause to handle NULLs

### Issue: Performance slow with many WHEN clauses
**Solution:** Order conditions from most frequent to least frequent

### Issue: Wrong calculations
**Solution:** Test with sample data first; verify with SELECT before MERGE

---

## Next Steps

1. Copy SQL_MERGE_WITH_CASE.sql
2. Choose your scenario (uncomment it)
3. Test in development
4. Run verification query
5. Deploy to production
