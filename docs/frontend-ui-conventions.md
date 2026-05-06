# Frontend UI Conventions

## 1. Design Principles

- Consistency > flexibility.
- Reuse > duplication.
- Simple > clever.
- PrimeNG is the primary component library.
- PrimeFlex and Tailwind utilities are preferred for layout composition.

## 2. Design Token Sources

All visual values must come from token files under `src/styles/tokens`.

- `colors.scss`: semantic colors (primary, secondary, success, warning, danger, info, text, border, surfaces).
- `spacing.scss`: spacing scale and stack helpers.
- `radius.scss`: border radius scale.
- `typography.scss`: font size, weight, line-height scale.
- `shadow.scss`: shadow levels.

Do not hardcode colors, spacing, radius, or shadows in feature pages unless there is a one-off visual exception with clear justification.

## 2.1 Government Formal Token Baseline

This is the default visual baseline for internal government-facing screens.

- Color:
  - `--app-primary-900: #0f2747`
  - `--app-primary-600: #1a3b63`
  - `--app-primary-500: #2a5a8a`
  - `--app-secondary-700: #425466`
  - `--app-secondary-500: #6b7c8f`
  - `--app-bg: #fafbfc`
  - `--app-surface: #ffffff`
  - `--app-border: #d7dee6`
  - `--app-text: #1a2533`
  - `--app-text-muted: #4a5a6a`
- Typography:
  - `--app-font-family-base: Noto Sans`
  - `--app-font-size-title: 1.75rem`
  - `--app-font-size-section: 1.375rem`
  - `--app-font-size-body: 0.9375rem`
  - `--app-font-size-label: 0.875rem`
- Spacing:
  - 8px grid via `--app-space-*`
  - section gap: `--app-layout-section-gap`
  - card padding: `--app-layout-card-padding`
- Radius:
  - control: `--app-radius-control: 6px`
  - card/table: `--app-radius-card/table: 8px`
- Shadow:
  - subtle only, prefer border-led separation (`--app-shadow-1..3`)

## 3. Component Usage Guidelines

### 3.1 PageHeader

Use for page-level title and subtitle region. Keep action buttons in the action slot area.

### 3.2 SectionCard

Use as the default content container for each logical block. Do not create custom page shells for each module.

### 3.3 FormActionBar

Use for all form-level actions (save, submit, cancel, delete). Keep button order predictable:

1. Primary action first.
2. Secondary action next.
3. Destructive action last.

### 3.4 EmptyState

Use when list/table/form sections have no data. Include a clear message and an optional recovery action (refresh, create, retry).

### 3.5 Confirm Dialog

Never use `window.confirm`. Use `ConfirmDialogWrapperService` for all destructive or irreversible actions.

## 4. Layout Rules

- Page structure:
  1. `PageHeader` at top.
  2. Main content split into one or more `SectionCard` blocks.
  3. Optional filter strip via `FilterBar` before list/table.
- Spacing:
  - Use token spacing (`--app-space-*`) and helper classes (`app-stack-*`, `app-form-grid`).
- Grouping:
  - Keep one business intent per section.
  - Avoid deeply nested cards.

## 5. Form Rules

- Field spacing uses `app-field` and `app-form`/`app-form-grid`.
- Labels stay above controls.
- Required fields use `.required` to show `*` indicator.
- Validation errors use `.app-validation-error` under the field.
- Disabled state follows Prime component disabled visuals and tokenized opacity.
- Submit flow:
  1. Validate locally.
  2. Show loading on primary action.
  3. Show toast for success/error.

### 5.1 Form Specs (Executable)

- Control height: `--app-form-control-height: 2.375rem`.
- Border: `1px solid var(--app-border-strong)`.
- Radius: `var(--app-radius-control)`.
- Focus: border `var(--app-primary-500)` + subtle focus ring.
- Label: `--app-font-size-label`, semi-bold.
- Error: `.app-validation-error` directly under field.

### 5.2 Select Variants (Standard)

- Use `app-select-form` + `app-select-form-panel` for normal form fields.
- Use `app-select-compact` + `app-select-compact-panel` for table filters and inline action rows.
- `app-select-duty` is kept as a compatibility alias and should not be used for new screens.

## 6. Button Conventions

- Primary: submit, save, commit.
- Secondary: non-destructive supporting actions.
- Danger: delete or irreversible operations.
- Text: low-emphasis contextual action.

Never style action semantics with random colors in feature pages.

## 7. Table/List Conventions

- Use PrimeNG `p-table`.
- Wrap in `.app-table-wrap` when table needs bounded surface style.
- Keep filter controls in `FilterBar` above table.
- Use consistent empty state with `EmptyState` component.
- Pagination should use Prime paginator defaults unless product requires custom page sizes.

### 7.1 Table Specs (Executable)

- Header background: `var(--app-surface-soft)`.
- Header typography: `--app-font-size-label` + semibold.
- Row minimum height: `2.5rem`.
- Cell padding: `var(--app-space-3)`.
- Row hover: `var(--app-bg-soft)`.
- Table boundary: `1px` border + `--app-radius-table`.

## 8. Status & Badge Rules

Standard semantic mapping:

- Draft -> warning
- Submitted -> info
- Locked -> success
- Error/Rejected -> danger

Use `StatusBadge` to render statuses. Avoid hand-crafted tag styles in page templates.

## 9. Loading and Error Feedback

- Page-level loading: `LoadingOverlay`.
- Inline loading: Prime spinner/skeleton where appropriate.
- Empty data: `EmptyState`.
- Error notification: toast via `NotificationService`.

## 10. Do / Don't

### Do

- Use shared UI components.
- Use design tokens from `src/styles/tokens`.
- Keep component APIs reusable and explicit.
- Keep page styles focused on composition, not visual primitives.

### Don't

- Hardcode color/spacing/radius values in page files.
- Use `window.confirm`.
- Create isolated custom widgets when shared components already cover the use case.
- Break business flow or API contracts for visual changes.
