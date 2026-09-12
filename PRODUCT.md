# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

Existing codebase: ASP.NET Core MVC (Razor views), server-rendered, Bootstrap 5 + Bootstrap Icons + Tom Select loaded from CDN, vanilla JS (no SPA framework), session-based auth. Redesign stays on this stack (Views + wwwroot/css/wwwroot/js only) — user confirmed no backend/framework change; delegated the technical judgment to Claude, who recommends keeping Razor/Bootstrap to avoid touching Controllers/Models.

## Users

Primary user: **Văn thư** (registry/clerical staff) at Trường Đại học Kiên Giang (Kiên Giang University) — non-technical office staff whose job is logging every incoming and outgoing official document (công văn) into the system, assigning it to the right department/leadership for handling, tracking due dates, and confirming completion. They are the heaviest daily users and the group this redesign is explicitly for.

Secondary users (same app, fewer/different permissions):
- **Lãnh đạo / Ban Giám Hiệu (BGH)** — university leadership who get documents routed to them for direction.
- **Cán bộ phòng/khoa/ban** (department staff) — receive assigned documents, log processing notes, mark completion.
- **Admin** (quyền 0/99, same role tier as Văn thư in this app) — also manages system config: staff accounts, permissions, departments, issuing-agency and document-type lookup lists, email notification settings.

## Product Purpose

A internal document-tracking register (sổ công văn điện tử) replacing a paper logbook. It exists to: record every incoming/outgoing official document with full metadata; route incoming documents to the responsible department and/or leadership with a deadline; track processing status (new → in progress → overdue → completed) end-to-end; store attachments; and let any department look up documents relevant to them. Success = Văn thư can log a document in under a minute with near-zero data-entry errors, and anyone can find the status of any document without asking Văn thư directly.

## Positioning

Not a generic CRM/ticketing tool — it is purpose-built around Vietnamese public-sector document register conventions (sổ công văn đến/đi, STT, trích yếu, cơ quan ban hành, hạn xử lý, xác nhận hoàn thành) and the specific multi-step approval/routing chain (Văn thư nhận → giao đơn vị chủ trì + đơn vị phối hợp → BGH chỉ đạo → đơn vị xử lý → Văn thư xác nhận hoàn thành).

## Operating Context

- Document arrives on paper or by email; Văn thư transcribes it into the "công văn đến" form (issuing agency, document number, date, subject/trích yếu, document type) and assigns a handling department, optionally a directing BGH member, optional co-handling departments, a due date, and attachments.
- Outgoing documents ("công văn đi") are logged similarly when the university issues something.
- Department staff view documents assigned to them, add processing notes, and mark their own completion; Văn thư gives the final official completion confirmation (xác nhận hoàn thành).
- Everyone regularly needs to find one document again by number/keyword/date and check whether it's overdue.
- Currently the "công văn đến" list is split across 6 separate menu destinations that are really just different filters of the same table (Danh sách / Đang xử lý / Xử lý trễ / Hoàn thành đúng hạn / Hoàn thành trễ / Chưa hoàn thành) — confirmed by the user as one of the most confusing/error-prone parts of the current UI.
- Runs on desktop browsers in an office setting; a lightweight mobile/tablet fallback matters less than desktop clarity for this audience.

## Capabilities and Constraints

- Existing Controllers/Models/data access are out of scope for this redesign — only Views, wwwroot/css, wwwroot/js change. Route names, form field names (`asp-for`), and posted parameter names must not change, since Controllers bind to them.
- Existing endpoints this UI must keep working: CongVanDen (Index/filtered variants/Nhap/ChiTiet/Xoa/ThemCoQuan/XoaFile/GetNextSTT/XuatExcel/TaiFile/ThemXuLy/XacNhanHoanThanh/HuyXacNhanHT/BoHoanThanh/CapNhatTrangThai), CongVanDi (Index/Nhap/ChiTiet/Xoa/XuatExcel), DonVi (Index/TraCuuVanBan/Them/Sua/GiaiThe/KhoiPhuc), Admin (multi-tab: staff, permissions, department leads + email, BGH, departments incl. merge, issuing agencies, document types, SMTP email config), Account (Login/DoiMatKhau/Logout), Notification (GetBadge polling).
- Confirmed pain points to fix (user selected "all of the above"):
  1. Sidebar navigation over-fragments one list into many near-duplicate destinations (6 items for công văn đến alone).
  2. Data tables are dense (up to 13 columns), small text, horizontal scrolling required.
  3. Data-entry forms (Nhập công văn đến/đi) have many fields, dropdowns, and a checkbox grid (đơn vị phối hợp) with no progressive disclosure, inviting missed/incorrect fields.
- Roles: quyền list of ints per session (0 and 99 = admin/Văn thư tier with full access incl. entry forms and admin panel; other values = limited/department view with notification bell for new assignments).
- Vietnamese-only UI (no i18n requirement stated).

## Brand Commitments

- Product name in use: "Quản lý Văn bản" / "Văn bản VNKGU", footer/login credit "Trường Đại học Kiên Giang". Keep Vietnamese-language, university-internal-tool framing. No fixed color/logo mandate was given — current blue (#2563eb-family) is incumbent, not a binding brand commitment.

## Evidence on Hand

- Full existing Razor views, CSS design tokens, and Controllers/Models were read directly from the codebase (Views/, wwwroot/css/site.css, Controllers/, Models/) — this is the incumbent implementation, treated as evidence/anti-reference per the redesign, not as user-approved. No mockups, brand guidelines, or user-research documents exist beyond this.

## Product Principles

1. One task, one screen. Filtered views of the same data (status variants of công văn đến) become filters/tabs on one page, not separate nav destinations.
2. Optimize for error prevention over density. Clerical data entry is the highest-stakes, highest-frequency task — favor larger touch targets, clear required-field marking, inline validation, and grouped/progressive form sections over cramming every field on screen at once.
3. Scannable status at a glance. Overdue/completed/in-progress must be readable from color+icon+position without reading every cell — this audience is non-technical and older on average.
4. Don't touch the backend contract. Every `asp-for`, route, and posted field name must survive the redesign unchanged.
5. Desktop-first, mobile-workable. Office desktop is the primary context; the layout must not break on a laptop but does not need app-grade mobile polish.

## Accessibility & Inclusion

Primary users are non-technical clerical staff, likely including older employees less comfortable with dense software UI — favor legible font sizes (avoid sub-12px body text as default), high-contrast status colors, generous click targets, and forgiving/obvious interaction patterns over compact information density.
