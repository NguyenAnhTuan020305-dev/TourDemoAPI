# Itinerary Form Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign section 2 from raw JSON textarea to a dynamic form with add/remove days and activities, dropdown location selection, and validation.

**Architecture:** Single file change (`wwwroot/index.html`). Vanilla JS with state array `itineraryDays`. Render functions rebuild DOM from state. Backend API unchanged.

**Tech Stack:** HTML, CSS (existing Bootstrap + custom), Vanilla JavaScript

---

## File Map

- **Modify:** `wwwroot/index.html` — all changes in this single file
  - Section 2 HTML: replace textarea with `<div id="itineraryContainer">`
  - Section 3 HTML: reposition test error buttons as secondary
  - CSS: add styles for day cards, activity rows, form controls
  - JS: add state management + render functions, update `buildPayload()`, update `fillDaLatSample()`

---

### Task 1: Add CSS styles for itinerary form components

**Files:**
- Modify: `wwwroot/index.html:12-328` (inside `<style>` block)

- [ ] **Step 1: Add new CSS classes after existing styles**

Add these styles inside the existing `<style>` block, before the closing `</style>` tag (after line 327):

```css
.itinerary-day {
    border: 1px solid var(--line);
    border-radius: 16px;
    margin-bottom: 16px;
    overflow: hidden;
}

.itinerary-day-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    background: #f7fbff;
    border-bottom: 1px solid var(--line);
    padding: 12px 16px;
}

.itinerary-day-header h3 {
    font-size: 0.95rem;
    font-weight: 800;
    margin: 0;
}

.itinerary-day-body {
    padding: 14px 16px;
}

.itinerary-activity {
    display: grid;
    grid-template-columns: 100px 1fr 1fr;
    gap: 10px;
    padding: 10px 0;
    border-bottom: 1px solid #edf2f7;
    align-items: start;
}

.itinerary-activity:last-child {
    border-bottom: 0;
}

.itinerary-activity .form-control,
.itinerary-activity .form-select {
    font-size: 0.85rem;
    padding: 7px 10px;
}

.itinerary-activity-label {
    color: var(--muted);
    font-size: 0.75rem;
    font-weight: 700;
    margin-bottom: 3px;
}

.itinerary-day-footer {
    padding: 10px 16px;
    border-top: 1px solid #edf2f7;
}

.btn-add {
    border: 1px dashed var(--blue);
    background: var(--soft-blue);
    color: var(--blue);
    font-size: 0.85rem;
    font-weight: 700;
    padding: 8px 16px;
    border-radius: 12px;
}

.btn-add:hover {
    background: #dde8ff;
    border-color: var(--blue);
    color: var(--blue);
}

.btn-remove {
    border: none;
    background: transparent;
    color: var(--red);
    font-size: 1.1rem;
    padding: 4px 8px;
    border-radius: 8px;
}

.btn-remove:hover {
    background: #fff1f2;
}

.btn-remove-day {
    border: none;
    background: transparent;
    color: var(--red);
    font-size: 0.82rem;
    font-weight: 600;
}

.btn-remove-day:hover {
    text-decoration: underline;
}

.activity-row-full {
    grid-column: 1 / -1;
}

.itinerary-empty {
    text-align: center;
    color: var(--muted);
    padding: 30px;
    border: 1px dashed #bdd0e6;
    border-radius: 14px;
}

.btn-test-group {
    display: flex;
    gap: 8px;
    flex-wrap: wrap;
}

.btn-outline-danger.btn-sm {
    font-size: 0.78rem;
    padding: 5px 12px;
    border-radius: 10px;
}
```

- [ ] **Step 2: Verify file is syntactically valid**

Read the file and confirm no syntax errors in the CSS.

---

### Task 2: Replace Section 2 HTML

**Files:**
- Modify: `wwwroot/index.html:420-435` (section 2 HTML)

- [ ] **Step 1: Replace the itinerary-form-panel content**

Replace lines 420-435 (the `#itinerary-form-panel` section) with:

```html
            <div class="col-xl-7">
                <section id="itinerary-form-panel" class="panel">
                    <div class="panel-header">
                        <div class="panel-icon green"><i class="bi bi-calendar2-week-fill"></i></div>
                        <div>
                            <h2 class="panel-title">2. Lịch trình chi tiết theo ngày</h2>
                            <p class="panel-subtitle">Thêm ngày và hoạt động. Địa danh phải thuộc danh sách chính thức ở section 1.</p>
                        </div>
                    </div>
                    <div class="panel-body">
                        <div id="itineraryContainer"></div>
                        <button type="button" class="btn btn-add mt-2" onclick="addDay()">
                            <i class="bi bi-plus-circle me-1"></i>Thêm ngày
                        </button>
                    </div>
                </section>
            </div>
```

- [ ] **Step 2: Verify HTML structure is correct**

---

### Task 3: Update Section 3 HTML - reposition test error buttons

**Files:**
- Modify: `wwwroot/index.html:437-456` (section 3 HTML)

- [ ] **Step 1: Replace the validation-panel content**

Replace lines 437-456 (the `#validation-panel` section) with:

```html
        <section id="validation-panel" class="panel">
            <div class="panel-header">
                <div class="panel-icon orange"><i class="bi bi-shield-check"></i></div>
                <div>
                    <h2 class="panel-title">3. Lưu tour đầy đủ và xem kết quả kiểm tra</h2>
                    <p class="panel-subtitle">POST /api/tours/full, hiển thị lỗi nếu thiếu địa danh hoặc dùng địa danh ngoài tour.</p>
                </div>
            </div>
            <div class="panel-body">
                <div class="d-flex flex-wrap gap-2 align-items-center mb-3">
                    <button type="button" id="saveTourBtn" class="btn btn-success" onclick="saveFullTour()">
                        <i class="bi bi-cloud-arrow-up-fill me-1"></i>Lưu tour đầy đủ
                    </button>
                    <div class="btn-test-group">
                        <button type="button" class="btn btn-outline-danger btn-sm" onclick="makeMissingLocationSample()">
                            <i class="bi bi-exclamation-triangle me-1"></i>Tạo lỗi: thiếu địa danh
                        </button>
                        <button type="button" class="btn btn-outline-danger btn-sm" onclick="makeWrongLocationSample()">
                            <i class="bi bi-signpost-split me-1"></i>Tạo lỗi: địa danh ngoài tour
                        </button>
                    </div>
                </div>
                <div id="validationResult" class="result-box">
                    Chưa gửi dữ liệu. Bấm <strong>Điền mẫu Đà Lạt 3N2Đ</strong>, sau đó bấm <strong>Lưu tour đầy đủ</strong> để kiểm tra.
                </div>
            </div>
        </section>
```

---

### Task 4: Update Section 1 HTML - remove duplicate test buttons

**Files:**
- Modify: `wwwroot/index.html:354-366` (inside section 1 panel-body)

- [ ] **Step 1: Replace the button group in section 1**

Replace lines 355-365 (the `<div class="d-flex flex-wrap gap-2 mb-3">` block with 3 buttons) with:

```html
                        <div class="d-flex flex-wrap gap-2 mb-3">
                            <button type="button" class="btn btn-teacher" onclick="fillDaLatSample()">
                                <i class="bi bi-magic me-1"></i>Điền mẫu Đà Lạt 3N2Đ
                            </button>
                        </div>
```

This removes the two test error buttons from section 1 (they are now in section 3).

---

### Task 5: Add JS state management and render functions

**Files:**
- Modify: `wwwroot/index.html:474+` (inside `<script>` block)

- [ ] **Step 1: Add state variable and helper functions**

Replace the entire `<script>` block content (lines 474-784) with the following code. This is a complete rewrite of the JS:

```javascript
        const API = '';
        let isSaving = false;
        let itineraryDays = [];

        const sampleLocationNames = {
            DD01: 'Đồi Cù',
            DD02: 'Hồ Tuyền Lâm',
            DD03: 'Vườn Hoa Thành Phố',
            DD04: 'Dinh Bảo Đại',
            DD05: 'Địa danh không thuộc tour'
        };

        const timeSlots = [
            '06:00','06:30','07:00','07:30','08:00','08:30','09:00','09:30',
            '10:00','10:30','11:00','11:30','12:00','12:30','13:00','13:30',
            '14:00','14:30','15:00','15:30','16:00','16:30','17:00','17:30',
            '18:00','18:30','19:00','19:30','20:00','20:30','21:00','21:30','22:00'
        ];

        const mealOptions = ['', 'Sáng', 'Trưa', 'Tối', 'Khác'];

        function getOfficialLocations() {
            return document.getElementById('officialLocations').value
                .split(',')
                .map(s => s.trim())
                .filter(Boolean);
        }

        function escapeAttr(value) {
            return String(value ?? '')
                .replaceAll('&', '&amp;')
                .replaceAll('"', '&quot;')
                .replaceAll('<', '&lt;')
                .replaceAll('>', '&gt;');
        }

        function escapeHtml(value) {
            return String(value ?? '')
                .replaceAll('&', '&amp;')
                .replaceAll('<', '&lt;')
                .replaceAll('>', '&gt;')
                .replaceAll('"', '&quot;')
                .replaceAll("'", '&#039;');
        }

        function makeTourCode() {
            return 'TDL' + Date.now().toString().slice(-9);
        }

        // ── Render Itinerary ──

        function renderItinerary() {
            const container = document.getElementById('itineraryContainer');
            const locations = getOfficialLocations();

            if (itineraryDays.length === 0) {
                container.innerHTML = '<div class="itinerary-empty"><i class="bi bi-calendar-x me-1"></i>Chưa có ngày nào. Bấm <strong>+ Thêm ngày</strong> để bắt đầu.</div>';
                return;
            }

            container.innerHTML = itineraryDays.map((day, dayIndex) => {
                const activitiesHtml = (day.activities || []).map((act, actIndex) => {
                    const locationOptions = locations.map(loc =>
                        `<option value="${escapeAttr(loc)}" ${act.maDiaDanh === loc ? 'selected' : ''}>${escapeAttr(loc)}${sampleLocationNames[loc] ? ' - ' + sampleLocationNames[loc] : ''}</option>`
                    ).join('');

                    const timeOptions = timeSlots.map(t =>
                        `<option value="${escapeAttr(t)}" ${act.khungGio === t ? 'selected' : ''}>${escapeAttr(t)}</option>`
                    ).join('');

                    const mealOptionsHtml = mealOptions.map(m =>
                        `<option value="${escapeAttr(m)}" ${act.buaAn === m ? 'selected' : ''}>${m || '-- Chọn --'}</option>`
                    ).join('');

                    return `
                        <div class="itinerary-activity">
                            <div>
                                <div class="itinerary-activity-label">Giờ</div>
                                <select class="form-select" onchange="updateActivity(${dayIndex},${actIndex},'khungGio',this.value)">
                                    <option value="">-- Chọn --</option>
                                    ${timeOptions}
                                </select>
                            </div>
                            <div>
                                <div class="itinerary-activity-label">Địa danh</div>
                                <select class="form-select" onchange="updateActivity(${dayIndex},${actIndex},'maDiaDanh',this.value)">
                                    <option value="">-- Không có --</option>
                                    ${locationOptions}
                                </select>
                            </div>
                            <div>
                                <div class="itinerary-activity-label">Bữa ăn</div>
                                <select class="form-select" onchange="updateActivity(${dayIndex},${actIndex},'buaAn',this.value)">
                                    ${mealOptionsHtml}
                                </select>
                            </div>
                            <div class="activity-row-full">
                                <div class="itinerary-activity-label">Nội dung hoạt động</div>
                                <div class="d-flex gap-2">
                                    <input type="text" class="form-control" value="${escapeAttr(act.noiDungHoatDong)}"
                                        placeholder="Mô tả hoạt động..."
                                        oninput="updateActivity(${dayIndex},${actIndex},'noiDungHoatDong',this.value)">
                                    <button type="button" class="btn-remove" onclick="removeActivity(${dayIndex},${actIndex})" title="Xóa hoạt động">
                                        <i class="bi bi-trash"></i>
                                    </button>
                                </div>
                            </div>
                        </div>
                    `;
                }).join('');

                return `
                    <div class="itinerary-day">
                        <div class="itinerary-day-header">
                            <h3><i class="bi bi-calendar-event me-1"></i>Ngày ${day.soThuTuNgay}</h3>
                            <button type="button" class="btn-remove-day" onclick="removeDay(${dayIndex})">
                                <i class="bi bi-x-lg me-1"></i>Xóa ngày
                            </button>
                        </div>
                        <div class="itinerary-day-body">
                            <div class="row g-2 mb-3">
                                <div class="col-md-8">
                                    <div class="itinerary-activity-label">Tiêu đề ngày</div>
                                    <input type="text" class="form-control" value="${escapeAttr(day.tieuDeNgay)}"
                                        placeholder="VD: Ngày 1: TP.HCM - Đà Lạt - Đồi Cù"
                                        oninput="updateDay(${dayIndex},'tieuDeNgay',this.value)">
                                </div>
                                <div class="col-md-4">
                                    <div class="itinerary-activity-label">Khách sạn</div>
                                    <input type="text" class="form-control" value="${escapeAttr(day.khachSan)}"
                                        placeholder="Tên khách sạn (tùy chọn)"
                                        oninput="updateDay(${dayIndex},'khachSan',this.value)">
                                </div>
                            </div>
                            ${activitiesHtml || '<div class="itinerary-empty" style="padding:16px"><i class="bi bi-list-check me-1"></i>Chưa có hoạt động nào.</div>'}
                        </div>
                        <div class="itinerary-day-footer">
                            <button type="button" class="btn btn-add btn-sm" onclick="addActivity(${dayIndex})">
                                <i class="bi bi-plus-circle me-1"></i>Thêm hoạt động
                            </button>
                        </div>
                    </div>
                `;
            }).join('');
        }

        // ── State Mutations ──

        function addDay() {
            const nextDay = itineraryDays.length + 1;
            itineraryDays.push({
                soThuTuNgay: nextDay,
                tieuDeNgay: '',
                khachSan: '',
                activities: []
            });
            renderItinerary();
        }

        function removeDay(dayIndex) {
            itineraryDays.splice(dayIndex, 1);
            itineraryDays.forEach((day, i) => day.soThuTuNgay = i + 1);
            renderItinerary();
        }

        function addActivity(dayIndex) {
            itineraryDays[dayIndex].activities.push({
                khungGio: '',
                maDiaDanh: '',
                noiDungHoatDong: '',
                buaAn: ''
            });
            renderItinerary();
        }

        function removeActivity(dayIndex, actIndex) {
            itineraryDays[dayIndex].activities.splice(actIndex, 1);
            renderItinerary();
        }

        function updateDay(dayIndex, field, value) {
            itineraryDays[dayIndex][field] = value;
        }

        function updateActivity(dayIndex, actIndex, field, value) {
            itineraryDays[dayIndex].activities[actIndex][field] = value;
        }

        // ── Sample Fillers ──

        function fillDaLatSample() {
            const maTour = makeTourCode();
            document.getElementById('tourCode').value = maTour;
            document.getElementById('tourName').value = 'Tour Đà Lạt 3N2Đ';
            document.getElementById('tourDuration').value = '3 ngày 2 đêm';
            document.getElementById('tourDays').value = '3';
            document.getElementById('tourNights').value = '2';
            document.getElementById('tourTransport').value = 'Xe du lịch 45 chỗ';
            document.getElementById('tourType').value = 'Tham quan - học tập thực tế';
            document.getElementById('tourDescription').value = 'Hành trình Đà Lạt 3 ngày 2 đêm dùng để minh họa ràng buộc giữa Tour, Địa danh và Lịch trình chi tiết. Mỗi địa danh chính thức của tour phải được sử dụng trong lịch trình và hoạt động không được tham chiếu địa danh ngoài tour.';
            document.getElementById('adultPrice').value = '3500000';
            document.getElementById('childPrice').value = '2500000';
            document.getElementById('officialLocations').value = 'DD01,DD02,DD03,DD04';

            itineraryDays = [
                {
                    soThuTuNgay: 1,
                    tieuDeNgay: 'Ngày 1: TP.HCM - Đà Lạt - Đồi Cù',
                    khachSan: 'Khách sạn trung tâm Đà Lạt',
                    activities: [
                        { khungGio: '06:00', maDiaDanh: '', noiDungHoatDong: 'Tập trung tại trường, điểm danh đoàn và khởi hành đi Đà Lạt.', buaAn: 'Sáng' },
                        { khungGio: '11:30', maDiaDanh: '', noiDungHoatDong: 'Dùng bữa trưa, nghe hướng dẫn viên giới thiệu tổng quan tuyến điểm.', buaAn: 'Trưa' },
                        { khungGio: '15:00', maDiaDanh: 'DD01', noiDungHoatDong: 'Tham quan Đồi Cù, ghi nhận vai trò cảnh quan xanh trong sản phẩm du lịch Đà Lạt.', buaAn: '' },
                        { khungGio: '18:30', maDiaDanh: '', noiDungHoatDong: 'Ăn tối và nhận phòng khách sạn.', buaAn: 'Tối' }
                    ]
                },
                {
                    soThuTuNgay: 2,
                    tieuDeNgay: 'Ngày 2: Hồ Tuyền Lâm - Vườn Hoa Thành Phố',
                    khachSan: 'Khách sạn trung tâm Đà Lạt',
                    activities: [
                        { khungGio: '07:00', maDiaDanh: '', noiDungHoatDong: 'Ăn sáng tại khách sạn và phổ biến mục tiêu học tập trong ngày.', buaAn: 'Sáng' },
                        { khungGio: '08:30', maDiaDanh: 'DD02', noiDungHoatDong: 'Tham quan Hồ Tuyền Lâm, quan sát cảnh quan mặt nước và cách tổ chức tuyến tham quan nghỉ dưỡng.', buaAn: '' },
                        { khungGio: '12:00', maDiaDanh: '', noiDungHoatDong: 'Dùng bữa trưa theo thực đơn địa phương.', buaAn: 'Trưa' },
                        { khungGio: '14:00', maDiaDanh: 'DD03', noiDungHoatDong: 'Khảo sát Vườn Hoa Thành Phố, phân tích cách khai thác tài nguyên hoa trong tour Đà Lạt.', buaAn: '' },
                        { khungGio: '18:30', maDiaDanh: '', noiDungHoatDong: 'Ăn tối, sinh hoạt nhóm và tổng kết ngày.', buaAn: 'Tối' }
                    ]
                },
                {
                    soThuTuNgay: 3,
                    tieuDeNgay: 'Ngày 3: Dinh Bảo Đại - Mua đặc sản - TP.HCM',
                    khachSan: '',
                    activities: [
                        { khungGio: '07:00', maDiaDanh: '', noiDungHoatDong: 'Ăn sáng, trả phòng khách sạn.', buaAn: 'Sáng' },
                        { khungGio: '08:30', maDiaDanh: 'DD04', noiDungHoatDong: 'Tham quan Dinh Bảo Đại, tìm hiểu giá trị lịch sử và cách đưa di sản vào lịch trình.', buaAn: '' },
                        { khungGio: '11:30', maDiaDanh: '', noiDungHoatDong: 'Dùng bữa trưa, mua đặc sản làm quà.', buaAn: 'Trưa' },
                        { khungGio: '13:30', maDiaDanh: '', noiDungHoatDong: 'Khởi hành về TP.HCM, kết thúc tour Đà Lạt 3 ngày 2 đêm.', buaAn: '' }
                    ]
                }
            ];

            renderItinerary();
            renderInfo('Đã điền mẫu hợp lệ. Lịch trình hiện dùng đủ DD01, DD02, DD03, DD04 ít nhất một lần.');
            document.getElementById('brochurePreview').className = 'brochure-empty';
            document.getElementById('brochurePreview').innerHTML = 'Brochure sẽ xuất hiện tại đây sau khi lưu tour thành công.';
        }

        function makeMissingLocationSample() {
            fillDaLatSample();
            itineraryDays.forEach(day => {
                day.activities = day.activities.filter(act => act.maDiaDanh !== 'DD04');
            });
            renderItinerary();
            renderInfo('Đã tạo mẫu lỗi: DD04 vẫn thuộc danh sách địa danh chính thức nhưng đã bị xóa khỏi lịch trình.');
        }

        function makeWrongLocationSample() {
            fillDaLatSample();
            const firstActivityWithLocation = itineraryDays[0].activities.find(act => act.maDiaDanh === 'DD01');
            if (firstActivityWithLocation) firstActivityWithLocation.maDiaDanh = 'DD05';
            renderItinerary();
            renderInfo('Đã tạo mẫu lỗi: một hoạt động dùng DD05, nhưng DD05 không thuộc danh sách địa danh chính thức của tour.');
        }

        // ── Build Payload ──

        function buildPayload() {
            const maTour = document.getElementById('tourCode').value.trim();
            const locations = document.getElementById('officialLocations').value
                .split(',')
                .map(item => item.trim())
                .filter(Boolean)
                .map((maDiaDanh, index) => ({ maDiaDanh, thuTuThamQuan: index + 1 }));

            return {
                maTour,
                tenTour: document.getElementById('tourName').value.trim(),
                thoiLuong: document.getElementById('tourDuration').value.trim(),
                soNgay: Number(document.getElementById('tourDays').value),
                soDem: Number(document.getElementById('tourNights').value),
                phuongTien: document.getElementById('tourTransport').value.trim() || null,
                loaiHinh: document.getElementById('tourType').value.trim() || null,
                moTa: document.getElementById('tourDescription').value.trim() || null,
                giaNguoiLon: Number(document.getElementById('adultPrice').value),
                giaTreEm: Number(document.getElementById('childPrice').value),
                diaDanh: locations,
                days: itineraryDays.map(day => ({
                    maLichTrinh: maTour + '_N' + day.soThuTuNgay,
                    soThuTuNgay: day.soThuTuNgay,
                    tieuDeNgay: day.tieuDeNgay,
                    khachSan: day.khachSan || null,
                    activities: day.activities.map(act => ({
                        khungGio: act.khungGio,
                        maDiaDanh: act.maDiaDanh || null,
                        noiDungHoatDong: act.noiDungHoatDong,
                        buaAn: act.buaAn || null
                    }))
                }))
            };
        }

        // ── Save & Load ──

        async function saveFullTour() {
            if (isSaving) return;

            let payload;
            try {
                payload = buildPayload();
            } catch (error) {
                renderErrors(['Lỗi xây dựng dữ liệu: ' + error.message]);
                return;
            }

            if (!payload.maTour || !payload.tenTour || !payload.thoiLuong) {
                renderErrors(['Vui lòng nhập mã tour, tên tour và thời lượng trước khi lưu.']);
                return;
            }

            if (itineraryDays.length === 0) {
                renderErrors(['Vui lòng thêm ít nhất một ngày vào lịch trình.']);
                return;
            }

            renderInfo('Đang gửi dữ liệu đến /api/tours/full ...');
            isSaving = true;
            setSaveButtonState(true);

            try {
                const response = await fetch(API + '/api/tours/full', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                });

                const data = await readJson(response);
                if (!response.ok) {
                    renderErrors(normalizeErrors(data));
                    return;
                }

                renderSuccess(`${data.message || 'Tạo tour đầy đủ thành công!'} Mã tour: ${payload.maTour}`);
                await loadBrochure(payload.maTour);
            } catch (error) {
                renderErrors(['Không gọi được API: ' + error.message]);
            } finally {
                isSaving = false;
                setSaveButtonState(false);
            }
        }

        async function loadBrochure(maTour) {
            const preview = document.getElementById('brochurePreview');
            preview.className = '';
            preview.innerHTML = '<div class="brochure-empty">Đang tải brochure từ /api/tours/' + escapeHtml(maTour) + '/brochure ...</div>';

            try {
                const response = await fetch(API + '/api/tours/' + encodeURIComponent(maTour) + '/brochure');
                const data = await readJson(response);
                if (!response.ok) {
                    preview.className = 'brochure-empty';
                    preview.innerHTML = 'Không tải được brochure cho tour ' + escapeHtml(maTour) + '.';
                    return;
                }

                preview.className = '';
                preview.innerHTML = renderBrochure(data);
            } catch (error) {
                preview.className = 'brochure-empty';
                preview.innerHTML = 'Không gọi được API brochure: ' + escapeHtml(error.message);
            }
        }

        function renderBrochure(data) {
            const tour = data.tour || {};
            const locations = data.locations || [];
            const days = data.days || [];
            const activities = data.activities || [];
            const price = formatCurrency(tour.giaNguoiLon) + ' / người lớn';
            const childPrice = formatCurrency(tour.giaTreEm) + ' / trẻ em';
            const locationHtml = locations.map(location => `
                <span class="location-badge">
                    <i class="bi bi-geo-alt-fill"></i>${escapeHtml(location.maDiaDanh)} - ${escapeHtml(location.tenDiaDanh || sampleLocationNames[location.maDiaDanh] || '')}
                </span>
            `).join('') || '<span class="text-muted">Chưa có địa danh.</span>';

            const dayHtml = days.map(day => {
                const dayActivities = activities.filter(activity => String(activity.maLichTrinh) === String(day.maLichTrinh));
                const activitiesHtml = dayActivities.map(activity => `
                    <div class="activity-row">
                        <div class="activity-time">${escapeHtml(activity.khungGio || '')}</div>
                        <div>
                            ${activity.tenDiaDanh ? `<span class="activity-place"><i class="bi bi-pin-map-fill me-1"></i>${escapeHtml(activity.tenDiaDanh)}</span>` : ''}
                            ${activity.buaAn ? `<span class="meal-badge"><i class="bi bi-cup-hot me-1"></i>${escapeHtml(activity.buaAn)}</span>` : ''}
                            <div class="mt-1">${escapeHtml(activity.noiDung || '')}</div>
                        </div>
                    </div>
                `).join('');

                return `
                    <article class="day-card">
                        <div class="day-header">
                            <h3>${escapeHtml(day.tieuDeNgay || 'Ngày ' + day.soThuTuNgay)}</h3>
                            ${day.khachSan ? `<div class="hotel-line"><i class="bi bi-building me-1"></i>Khách sạn: ${escapeHtml(day.khachSan)}</div>` : ''}
                        </div>
                        <div>${activitiesHtml || '<div class="p-3 text-muted">Chưa có hoạt động.</div>'}</div>
                    </article>
                `;
            }).join('');

            return `
                <div class="brochure-hero mb-4">
                    <div class="row g-3 align-items-end">
                        <div class="col-lg-7">
                            <div class="teacher-pill mb-3"><i class="bi bi-stars"></i> Brochure tour đã lưu</div>
                            <h2>${escapeHtml(tour.tenTour || '')}</h2>
                            <p class="mb-0">${escapeHtml(tour.moTa || '')}</p>
                        </div>
                        <div class="col-lg-5">
                            <div class="row g-2">
                                <div class="col-6"><div class="metric"><small>Giá</small><strong>${price}</strong><br><small>${childPrice}</small></div></div>
                                <div class="col-6"><div class="metric"><small>Thời lượng</small><strong>${escapeHtml(tour.thoiLuong || '')}</strong></div></div>
                                <div class="col-6"><div class="metric"><small>Phương tiện</small><strong>${escapeHtml(tour.phuongTien || '')}</strong></div></div>
                                <div class="col-6"><div class="metric"><small>Loại hình</small><strong>${escapeHtml(tour.loaiHinh || '')}</strong></div></div>
                            </div>
                        </div>
                    </div>
                </div>
                <h3 class="h6 fw-bold mb-2">Địa danh chính thức</h3>
                <div class="mb-3">${locationHtml}</div>
                <h3 class="h6 fw-bold mb-0">Lịch trình chi tiết</h3>
                ${dayHtml || '<div class="brochure-empty mt-3">Chưa có lịch trình.</div>'}
            `;
        }

        // ── Helpers ──

        async function readJson(response) {
            const text = await response.text();
            if (!text) return {};
            try { return JSON.parse(text); }
            catch { return { message: text }; }
        }

        function normalizeErrors(data) {
            if (Array.isArray(data?.errors)) return data.errors;
            if (data?.message) return [data.message];
            return ['API từ chối dữ liệu nhưng không trả về chi tiết lỗi.'];
        }

        function renderInfo(message) {
            const result = document.getElementById('validationResult');
            result.className = 'result-box';
            result.innerHTML = `<i class="bi bi-info-circle me-1"></i>${escapeHtml(message)}`;
        }

        function renderSuccess(message) {
            const result = document.getElementById('validationResult');
            result.className = 'result-box success';
            result.innerHTML = `<i class="bi bi-check-circle-fill me-1"></i><strong>Thành công:</strong> ${escapeHtml(message)}`;
        }

        function renderErrors(errors) {
            markInvalidBrochure();
            const result = document.getElementById('validationResult');
            result.className = 'result-box error';
            result.innerHTML = '<strong><i class="bi bi-x-circle-fill me-1"></i>Lỗi kiểm tra dữ liệu:</strong><ul class="mb-0 mt-2">' +
                errors.map(error => `<li>${escapeHtml(error)}</li>`).join('') +
                '</ul>';
        }

        function markInvalidBrochure() {
            const preview = document.getElementById('brochurePreview');
            preview.className = 'brochure-empty';
            preview.innerHTML = 'Chưa có brochure hợp lệ cho dữ liệu hiện tại.';
        }

        function setSaveButtonState(disabled) {
            const button = document.getElementById('saveTourBtn');
            if (!button) return;
            button.disabled = disabled;
            button.innerHTML = disabled
                ? '<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>Đang lưu...'
                : '<i class="bi bi-cloud-arrow-up-fill me-1"></i>Lưu tour đầy đủ';
        }

        function formatCurrency(value) {
            return Number(value || 0).toLocaleString('vi-VN') + ' VNĐ';
        }

        // ── Init ──

        document.getElementById('officialLocations').addEventListener('input', () => renderItinerary());
        fillDaLatSample();
```

- [ ] **Step 2: Verify the script tag is properly closed**

Ensure the `</script>` tag is present after the JS code.

---

### Task 6: Verify complete file integrity

**Files:**
- Read: `wwwroot/index.html`

- [ ] **Step 1: Read the full file and verify**

Check:
1. HTML structure is valid (proper nesting of tags)
2. CSS block has no syntax errors
3. JS block has no syntax errors
4. All IDs referenced in JS exist in HTML (`itineraryContainer`, `validationResult`, `brochurePreview`, `saveTourBtn`, `tourCode`, `tourName`, etc.)
5. All onclick handlers reference existing functions
6. The `</script>` and `</body>` and `</html>` tags are present

- [ ] **Step 2: Test in browser**

Open `http://localhost:5000` and verify:
1. Section 2 shows 3 day cards with activities (pre-filled from sample)
2. Click "+ Thêm ngày" adds a new day card
3. Click "+ Thêm hoạt động" adds a new activity row
4. Click × on activity removes it
5. Click "Xóa ngày" removes the day
6. Dropdown địa danh shows DD01-DD04 from section 1
7. Click "Lưu tour đầy đủ" → success → brochure renders
8. Click "Tạo lỗi: thiếu địa danh" → error message
9. Click "Tạo lỗi: địa danh ngoài tour" → error message
