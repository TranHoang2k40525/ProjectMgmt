# STITCH DESIGN SYSTEM (Project ID: 3489937127396055955)
Source of Truth: Google Stitch Project `projects/3489937127396055955` (Scrum AI HUCE / HUCE Jira-Agile Intelligence)

---

## 1. Color System (Material 3 & Jira Agile Spectrum)
- **Primary Base**: `#003d9b`
- **Primary Container**: `#0052cc`
- **On Primary**: `#ffffff`
- **On Primary Container**: `#c4d2ff`
- **Primary Fixed / Dim**: `#dae2ff` / `#b2c5ff`
- **On Primary Fixed**: `#001848`
- **Secondary Base**: `#4648d4`
- **Secondary Container**: `#6063ee`
- **Secondary Fixed Dim**: `#c0c1ff`
- **Tertiary (AI & Special Accent)**: `#6100af`
- **Tertiary Container**: `#7c2ccd`
- **Tertiary Fixed**: `#efdbff`
- **On Tertiary Fixed**: `#2b0052`
- **Error**: `#ba1a1a`
- **Error Container**: `#ffdad6`
- **On Error**: `#ffffff`
- **Outline / Border**: `#737685`
- **Outline Variant**: `#c3c6d6`

---

## 2. Background & Surface Hierarchy
- **Background / Main Canvas**: `#faf9ff`
- **Surface**: `#faf9ff`
- **Surface Container Lowest (Pure White Card)**: `#ffffff`
- **Surface Container Low**: `#f1f3ff`
- **Surface Container**: `#e9edff`
- **Surface Container High**: `#e1e8ff`
- **Surface Container Highest**: `#d8e2ff`
- **Surface Dim / Bright**: `#ccdaff` / `#faf9ff`
- **On Surface**: `#051a3e`
- **On Surface Variant**: `#434654`

---

## 3. Typography & Heading Scale (Inter Font Family)
- **Display Large**: `40px` / `48px`, weight `700`, tracking `-0.02em`
- **Display Large Mobile**: `30px` / `38px`, weight `700`, tracking `-0.015em`
- **Headline Large**: `28px` / `36px`, weight `600`, tracking `-0.015em`
- **Headline Large Mobile**: `24px` / `32px`, weight `600`, tracking `-0.01em`
- **Headline Medium**: `20px` / `28px`, weight `600`, tracking `-0.01em`
- **Headline Small**: `16px` / `24px`, weight `600`, tracking `-0.005em`
- **Body Large**: `16px` / `24px`, weight `400`, tracking `0em`
- **Body Medium**: `14px` / `20px`, weight `400`, tracking `0em`
- **Body Small**: `12px` / `16px`, weight `400`, tracking `0.01em`
- **Label Large**: `14px` / `20px`, weight `500`, tracking `0.005em`
- **Label Medium**: `12px` / `16px`, weight `600`, tracking `0.02em`
- **Label Small**: `11px` / `14px`, weight `700`, tracking `0.04em`

---

## 4. Border Radii & Elevation
- **Border Radius**:
  - Small (Badge, tag): `4px` (`rounded`)
  - Medium (Input, button, small card): `8px` (`rounded-lg` / `rounded-md`)
  - Large (Card, dialog, sheet): `12px` (`rounded-xl`)
  - Extra Large (Hero panel, container): `16px` (`rounded-2xl`)
  - Full (Pill, avatar): `9999px` (`rounded-full`)
- **Shadows**:
  - Header / Subtle: `shadow-[0_1px_8px_rgba(0,0,0,0.04)]`
  - Container / Card: `shadow-sm` or `shadow-[0_1px_4px_rgba(0,61,155,0.2)]`
  - AI Gradient Glow: `shadow-[0_0_12px_rgba(99,102,241,0.25)]`

---

## 5. Component Styles
- **Primary Button**: `bg-primary-container hover:bg-primary text-on-primary font-label-md rounded-lg shadow-sm transition-all h-9 px-4`
- **Secondary Button**: `bg-surface-container hover:bg-surface-container-high text-on-surface font-label-md rounded-lg transition-all h-9 px-4`
- **Ghost Button**: `bg-transparent hover:bg-surface-container-high text-on-surface-variant hover:text-on-surface rounded-lg transition-all`
- **AI Action Button**: `bg-gradient-to-r from-secondary-container to-tertiary-container hover:brightness-110 text-on-secondary-container font-label-md rounded-lg shadow-[0_0_12px_rgba(99,102,241,0.25)] h-9 px-4`
- **Input Field**: `bg-surface-container-low text-on-surface placeholder:text-outline font-body-sm rounded-lg border-0 focus:outline-none focus:ring-2 focus:ring-primary-container focus:bg-surface-container-lowest transition-all h-10 px-3.5`
- **Card**: `bg-surface-container-lowest rounded-xl border border-outline-variant/30 shadow-sm p-4 sm:p-6`
- **Glass Panel**: `bg-surface-container-lowest/85 backdrop-blur-xl border border-outline-variant/20 shadow-sm`

---

## 6. Layout & Spacing Token Scale
- `space-xs`: `0.25rem` (4px)
- `space-sm`: `0.5rem` (8px)
- `space-md`: `1.0rem` (16px)
- `space-lg`: `1.5rem` (24px)
- `space-xl`: `2.0rem` (32px)
- `gutter`: `1.0rem` (16px)
- `gutter-compact`: `0.5rem` (8px)
- `margin`: `1.5rem` (24px)
- `margin-mobile`: `1.0rem` (16px)
