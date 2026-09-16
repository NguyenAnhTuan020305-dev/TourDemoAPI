# Design: Redesign Section 2 - Lịch trình chi tiết theo ngày

## Summary

Redesign the itinerary section (section 2) from a raw JSON textarea to a dynamic form-based UI. Users can add days, add activities per day, select locations from dropdowns, and save with validation.

## Current State

- **Backend**: `/api/tours/full` already validates: missing locations, wrong locations, day count mismatch
- **Frontend**: Section 2 uses a raw JSON textarea - not user-friendly
- **Backend stays unchanged** - only frontend work

## Approach

Vanilla JS (no new dependencies). Dynamic DOM rendering via `renderItinerary()`.

## UI Layout

```
Section 2: Lịch trình chi tiết theo ngày
├── Card Ngày 1
│   ├── Input: Tiêu đề ngày
│   ├── Input: Khách sạn
│   ├── Card Hoạt động 1
│   │   ├── Select: Giờ (06:00-22:00)
│   │   ├── Select: Địa danh (from section 1 official list)
│   │   ├── Select: Bữa ăn (Sáng/Trưa/Tối/Khác)
│   │   ├── Input: Nội dung hoạt động
│   │   └── Button: × Xóa hoạt động
│   ├── Card Hoạt động 2...
│   ├── Button: + Thêm hoạt động
│   └── Button: × Xóa ngày
├── Card Ngày 2...
├── Button: + Thêm ngày
```

## State Management

```js
let itineraryDays = [
  {
    soThuTuNgay: 1,
    tieuDeNgay: '',
    khachSan: '',
    activities: [
      { khungGio: '07:00', maDiaDanh: '', noiDungHoatDong: '', buaAn: '' }
    ]
  }
];
```

## Key Functions

| Function | Purpose |
|----------|---------|
| `renderItinerary()` | Re-render all day cards from `itineraryDays` state |
| `addDay()` | Push new day to state, re-render |
| `removeDay(index)` | Remove day by index, re-render |
| `addActivity(dayIndex)` | Push new activity to day, re-render |
| `removeActivity(dayIndex, actIndex)` | Remove activity, re-render |
| `getOfficialLocations()` | Read section 1 input, return array of location codes |
| `buildPayload()` | Merge section 1 + section 2 data into API payload |
| `saveFullTour()` | POST to API, render errors or success + brochure |

## Data Flow

1. User fills section 1 (tour info + official locations: DD01,DD02,DD03,DD04)
2. `getOfficialLocations()` parses the comma-separated input
3. Dropdown locations in section 2 show only codes from section 1
4. User fills days and activities via form
5. `buildPayload()` reads all form data, builds JSON
6. POST `/api/tours/full`
7. Backend validates, returns success or errors
8. On success: load brochure. On error: render error list.

## Validation

### Frontend (before API call)
- Number of day cards must match `soNgay` from section 1
- Each activity must have: khungGio, noiDungHoatDong
- If maDiaDanh selected, must be in official locations list

### Backend (existing `/api/tours/full`)
- Tour has 4 locations but itinerary uses 3 → "thiếu địa danh"
- Itinerary uses location not in tour list → "địa danh ngoài tour"
- Day count mismatch → "thiếu ngày"

## Changes to Existing Code

### Section 2 (itinerary-form-panel)
- **Before**: `<textarea id="itineraryJson">`
- **After**: `<div id="itineraryContainer">` rendered by JS

### Section 3 (validation-panel)
- Nút "Tạo lỗi thiếu địa danh" and "Tạo lỗi địa danh ngoài tour" become smaller secondary buttons (`btn-outline-danger btn-sm`)
- Placed in a row with the main save button

### Section 1 (tour-form-panel)
- When user types in "Địa danh chính thức" input, section 2 dropdowns auto-update
- Add `oninput` event to `officialLocations` input

## File Changes

- `wwwroot/index.html`: Only file modified
  - Update section 2 HTML
  - Update section 3 button layout
  - Replace JS: remove `fillDaLatSample` JSON logic, add form rendering functions
  - Keep: `buildPayload()` structure, `saveFullTour()`, `loadBrochure()`, `renderBrochure()`

## Testing

1. Fill section 1 with sample data (DD01-DD04)
2. Section 2 shows 3 day cards with activity forms
3. Add/remove days and activities
4. Click "Lưu tour đầy đủ" → success → brochure renders
5. Remove a location from activities → error "thiếu địa danh"
6. Add wrong location → error "địa danh ngoài tour"
7. Change soNgay but don't add day → error "thiếu ngày"
