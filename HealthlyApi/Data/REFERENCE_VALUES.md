# Reference values — guide for medical advisors

> ⚠️ **STATUS: DRAFT — NOT MEDICALLY REVIEWED.**
> Every row in `reference-values.csv` was prepared by the development team from commonly published adult reference
> intervals and guidelines (the `Source` column). **None of them has been checked by a physician yet** — the
> `ReviewedBy` column is empty everywhere. Healthly uses these values to mark lab results as normal, out of range or
> critical, so each row must be reviewed and signed off by a qualified medical advisor before the platform is used
> with real patients. The API logs a warning on every start while draft rows remain.

## What this file is

`reference-values.csv` is the single source of the reference ranges Healthly uses. When the API starts, it loads the
file into the database; the code never contains medical numbers. To change a norm, edit the file — no programming
needed. Every change is visible in git history (who changed which value, when), and can be reviewed in a pull request.

## How a value is evaluated

1. The AI only **reads** the values from the lab report. It never decides whether a value is normal.
2. The code finds the row for the test (by `Metric` or one of its `Aliases`) that matches the patient's **sex and age**.
3. The lab's unit is converted to the row's `Unit` (for example g/dL → g/L, mg/dL → mmol/L, /mm³ → 10^9/L).
4. Outside `Min`–`Max` → **out of range**; beyond `CriticalLow`/`CriticalHigh` → **critical** (red warning, the
   patient is told to contact a doctor right away).
5. If no row matches (for example, a child — the file has adult rows only), the range printed on the lab report is used.

## Columns

| Column | Meaning | Example |
|---|---|---|
| `Metric` | Name of the test as shown to the user | `Hemoglobin` |
| `Aliases` | Other names on lab reports, separated by `\|` | `Haemoglobin\|HGB\|Hb` |
| `Sex` | `Any`, `Female` or `Male` | `Female` |
| `AgeFrom`, `AgeTo` | Age in full years, both inclusive; empty = no limit | `18`, *(empty)* |
| `Min`, `Max` | Normal range; one of them may be empty (`Max` only = "below X") | `120`, `155` |
| `CriticalLow`, `CriticalHigh` | Life-threatening limits; empty = none. Must lie outside the normal range | `70`, `200` |
| `Unit` | Unit of all numbers in the row (the same for all rows of a test) | `g/L` |
| `Category` | Group of tests | `Complete blood count` |
| `Specialty` | Doctor to suggest when the value is out of range | `Hematologist` |
| `Source` | Where the values come from (guideline, textbook, lab) | `WHO 2011/2024 …` |
| `ReviewedBy` | Name of the advisor who checked the row — **fill in when you approve it** | `Dr. A. Yılmaz` |
| `ReviewedAt` | Date of the review, `YYYY-MM-DD` | `2026-11-15` |

Rows of one test must not overlap: for any person (sex + age) at most one row may apply. `Any` overlaps with
`Female` and `Male`, so a test has either one `Any` row or separate `Female` and `Male` rows for the same ages.

## How to review and edit

- **Easiest:** open the file on GitHub (it is shown as a table), or import it into Google Sheets / Excel via
  *Data → From Text/CSV* (UTF-8, comma-separated).
- Excel with Turkish or Russian regional settings may save the file with `;` and decimal commas (`3,9`) — this is
  supported, nothing needs to be converted back.
- Please review especially: the **critical limits** (they trigger urgent warnings), **sex-specific** rows, and the
  rows marked `verify` in `Source`.
- When a row is approved, fill in `ReviewedBy` and `ReviewedAt`. To change a value, update it and the `Source`.
- Send the file back to the development team (or open a pull request). If the file contains a mistake (for example
  `Min` greater than `Max`, overlapping rows), the API refuses to start and reports the line number and the problem.

## Known limitations of the draft

- Adult rows only (`AgeFrom` = 18). Children and adolescents need their own rows.
- Pregnancy-specific ranges are not supported yet.
- Ranges depend on the laboratory method; partner laboratories may provide their own intervals.
- Glucose assumes a fasting sample.
