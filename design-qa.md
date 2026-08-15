# VS-06 Evidence — Golden UI Design QA

**Source visual truth**

- `product-design/final-ui/DR-08_Add_Evidence.png`
- `product-design/final-ui/DR-09_Evidence_Detail.png`
- `product-design/final-ui/DR-10_Add_Human_Confirmation.png`

**Rendered implementation evidence**

- `artifacts/VS06/implementation-add-evidence-1671x941.png`
- `artifacts/VS06/implementation-evidence-detail-1671x941.png`
- `artifacts/VS06/implementation-human-confirmation-1671x941.png`
- Combined same-input comparison: `artifacts/VS06/qa-comparison-golden-vs-implementation.png`

**Normalization**

- Source pixels: `1671 × 941` for each Golden.
- Implementation pixels / CSS viewport: `1671 × 941`, desktop light theme, density 1.
- No scaling or density conversion was applied in the comparison pairs.
- Source Goldens show the Unknown Item investigation context. VS-06 intentionally enters from the already implemented Business Function Detail, so the comparison treats the right-side Evidence Drawer as the fidelity target and the underlying route content as an allowed context difference.

**State and interaction coverage**

- Add ordinary `CodeReference` Evidence with a fixed real `BusinessFunction` Subject.
- Open the resulting Evidence Detail and inspect source, structured locator, support reason, provider snapshot, and read-only knowledge impact.
- Correct mutable Evidence fields without changing EvidenceType or Subject.
- Add a full Human Confirmation snapshot and return to Evidence Detail.
- Verified the Business Function Evidence count refreshed and Knowledge Status remained `Inferred` throughout.
- Browser console errors checked: none.

## Findings

- No actionable P0, P1, or P2 mismatch remains.
- The implementation preserves the Golden hierarchy: technical drawer header, explicit Subject context, compact source fields, first-class support reason, person snapshot, read-only knowledge impact, and sticky actions.
- The current implementation intentionally omits Knowledge Status advancement controls shown in historical Goldens because VS-06 does not implement C22/C26. It clearly states that saving Evidence or Human Confirmation does not change status.

## Required Fidelity Surfaces

- Fonts and typography: uses the frozen System Knowledge Hub sans/technical font tokens; technical identifiers use the existing monospace treatment. Drawer title, section heading, field-label, and helper-copy hierarchy remain consistent with the baseline.
- Spacing and layout rhythm: 500px drawer at the Golden viewport, compact section separators, dense two-column technical inputs, and sticky footer match the Golden proportions. At 1440px the existing responsive token reduces the drawer to 440px and hides/replaces the Context Rail as frozen.
- Colors and visual tokens: light shell, calm indigo primary, subtle borders, neutral surfaces, and existing Knowledge Status colors are reused. No new visual system was introduced.
- Image quality and asset fidelity: these drawers contain no product imagery. Existing Element Plus and project icon components are used; no placeholder, handcrafted SVG, CSS art, or raster substitute was introduced.
- Copy and content: formal UI is Simplified Chinese; technical identifiers remain original. The product shell uses `系统知识中心 / System Knowledge Hub`, intentionally replacing historical screenshot naming.

## Focused Region Comparison

The right-side drawer region was inspected at readable scale in all three paired rows of `qa-comparison-golden-vs-implementation.png`. The Add Evidence type/source area, Evidence Detail source/support/provider area, and Human Confirmation person/statement/impact area were all compared separately within that combined input.

## Comparison History

1. P2 — Drawer replacement retained the previous drawer scroll offset, hiding the next drawer header.
   - Fix: the single DrawerHost now resets its body scroll after the replacement content renders.
   - Post-fix evidence: all final `1671 × 941` captures begin with the expected drawer header and hierarchy.
2. P2 — Evidence type controls initially rendered as unresolved custom elements and were too vertically loose.
   - Fix: registered `ElRadioGroup` / `ElRadioButton` locally and changed the control to compact wrapped pills using baseline colors.
   - Post-fix evidence: `implementation-add-evidence-1671x941.png` shows interactive selected and unselected states in two compact rows.
3. P2 — Code Reference locator initially appeared as raw JSON in Evidence Detail.
   - Fix: Code Reference locator fields now render as compact structured technical facts; non-code locator JSON remains a bounded fallback.
   - Post-fix evidence: `implementation-evidence-detail-1671x941.png` shows Repository, File, Start Line, and End Line as scan-friendly rows.

## Open Questions

- None blocking VS-06. Subject search and Unknown Item investigation-specific binding remain deferred to their frozen future slices.

## Follow-up Polish

- P3: a later slice may add richer type-specific previews (for example a code excerpt) when the frozen source-access use case is implemented; Q16 correctly does not probe external sources in VS-06.

## Implementation Checklist

- [x] Golden drawer hierarchy retained.
- [x] Single DrawerHost; no stacked drawers.
- [x] 1440 and Golden-size desktop states checked.
- [x] Add / detail / update / Human Confirmation interactions exercised.
- [x] No status-changing control exposed in this Slice.
- [x] Browser console checked.

final result: passed
