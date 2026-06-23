# Random Content Variants Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cho phép 1 notification chứa nhiều biến thể HTML trong field `content`, ngăn cách bằng marker `<!--##VARIANT##-->`. Client JS pick uniform-random 1 variant mỗi lần render.

**Architecture:** JS-only. Trong mỗi file `notification-client*.js` thêm 1 private helper `_pickVariant(content)` (đặt trong IIFE scope để không bị ràng buộc `this`), rồi thay 5 chỗ gán `notification.content` ở các hàm render thành `_pickVariant(notification.content)`. Không đổi backend, schema, admin UI, hay Redis cache.

**Tech Stack:** Vanilla JavaScript (ES5, IIFE pattern, không có test framework). Test thủ công qua browser DevTools console hoặc `wwwroot/debug.html`.

**Spec source:** [docs/superpowers/specs/2026-06-23-random-content-variants-design.md](../specs/2026-06-23-random-content-variants-design.md)

---

## File Structure

3 file JS client trong `NotificationAPI/wwwroot/js/`, mỗi file:
- Thêm 1 helper `_pickVariant` ở đầu IIFE (sau `'use strict';`)
- Sửa 5 chỗ gán `notification.content` (4 chỗ unique + 1 chỗ trùng dùng `replace_all`)

| File | Helper insert sau | Site 1 line | Site 2 line | Site 3 line | Site 4 line | Site 5 line |
|---|---|---|---|---|---|---|
| `notification-client-v2.js` | line 7 (`'use strict';`) | 1235 | 1504 | 1564 | 1701 | 1757 |
| `notification-client-v1.js` | line 7 (`'use strict';`) | 1041 | 1310 | 1370 | 1507 | 1563 |
| `notification-client.js` | line 7 (`'use strict';`) | 1041 | 1310 | 1370 | 1507 | 1563 |

(Site 1+3 đều là `body.innerHTML = notification.content;` — xuất hiện 2 lần per file, dùng `replace_all` của Edit tool sẽ hit cả 2.)

Không file C# / view / config / csproj nào bị đụng.

---

## Task 1: Create feature branch

**Files:** none

- [ ] **Step 1: Verify clean working tree**

Run: `git status`
Expected: `nothing to commit, working tree clean`. Nếu dirty, stop và hỏi user trước khi tiếp tục.

- [ ] **Step 2: Verify on master**

Run: `git branch --show-current`
Expected: `master`

- [ ] **Step 3: Pull latest**

Run: `git pull origin master`
Expected: `Already up to date.` hoặc fast-forward.

- [ ] **Step 4: Create branch**

Run: `git checkout -b feat/random-content-variants`
Expected: `Switched to a new branch 'feat/random-content-variants'`

---

## Task 2: Update `notification-client-v2.js`

**Files:**
- Modify: `NotificationAPI/wwwroot/js/notification-client-v2.js`

- [ ] **Step 1: Add helper function**

Use Edit tool với `replace_all=false` (chỉ 1 chỗ):

`old_string`:
```
(function (window) {
    'use strict';

    // Đối tượng NotificationClient chính
```

`new_string`:
```
(function (window) {
    'use strict';

    // Helper: tách content theo marker <!--##VARIANT##--> và pick uniform random 1 variant.
    // Backward compat: content không có marker → trả nguyên content.
    var _pickVariant = function (content) {
        if (!content || typeof content !== 'string') return content || '';
        var MARKER = '<!--##VARIANT##-->';
        if (content.indexOf(MARKER) === -1) return content;
        var parts = content.split(MARKER)
            .map(function (s) { return s.replace(/^\s+|\s+$/g, ''); })
            .filter(function (s) { return s.length > 0; });
        if (parts.length === 0) return '';
        if (parts.length === 1) return parts[0];
        return parts[Math.floor(Math.random() * parts.length)];
    };

    // Đối tượng NotificationClient chính
```

- [ ] **Step 2: Replace `body.innerHTML = notification.content;` (xuất hiện 2 lần — site HTML và site Popup)**

Use Edit tool với `replace_all=true`:

`old_string`:
```
body.innerHTML = notification.content;
```

`new_string`:
```
body.innerHTML = _pickVariant(notification.content);
```

- [ ] **Step 3: Replace `var modalHtml = notification.content;` (site Modal)**

Use Edit tool với `replace_all=false`:

`old_string`:
```
            var modalHtml = notification.content;
```

`new_string`:
```
            var modalHtml = _pickVariant(notification.content);
```

- [ ] **Step 4: Replace `content.innerHTML = notification.content;` (site Inline HTML)**

Use Edit tool với `replace_all=false`:

`old_string`:
```
        content.innerHTML = notification.content;
```

`new_string`:
```
        content.innerHTML = _pickVariant(notification.content);
```

- [ ] **Step 5: Replace `var url = notification.content;` (site Link URL)**

Use Edit tool với `replace_all=false`:

`old_string`:
```
        var url = notification.content;
```

`new_string`:
```
        var url = _pickVariant(notification.content);
```

- [ ] **Step 6: Verify edits**

Run: `git diff NotificationAPI/wwwroot/js/notification-client-v2.js | grep -c "_pickVariant"`
Expected: `6` (1 declaration + 5 call sites, mỗi call site thêm `_pickVariant(` 1 lần)

Run: `git diff NotificationAPI/wwwroot/js/notification-client-v2.js | grep -E "^\+.*notification\.content" | grep -v "_pickVariant"`
Expected: empty (mọi `notification.content` mới thêm phải bọc trong `_pickVariant`)

- [ ] **Step 7: Commit**

```bash
git add NotificationAPI/wwwroot/js/notification-client-v2.js
git commit -m "feat(client-v2): random content variants per render

Marker <!--##VARIANT##--> ngăn các biến thể trong field content.
Helper _pickVariant() tách marker, filter trim, uniform random pick.
Backward compat: content không marker -> trả nguyên content.

5 call sites updated: HTML body, Modal, Popup body, Inline HTML, Link URL.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: Update `notification-client-v1.js`

**Files:**
- Modify: `NotificationAPI/wwwroot/js/notification-client-v1.js`

Cùng pattern Task 2 — code thay đổi GIỐNG HỆT. File `-v1.js` chỉ khác `-v2.js` ở chỗ ít features hơn (line numbers nhỏ hơn) nhưng 5 call sites cùng dạng code.

- [ ] **Step 1: Add helper function**

Use Edit tool với `replace_all=false`:

`old_string`:
```
(function (window) {
    'use strict';

    // Đối tượng NotificationClient chính
```

`new_string`:
```
(function (window) {
    'use strict';

    // Helper: tách content theo marker <!--##VARIANT##--> và pick uniform random 1 variant.
    // Backward compat: content không có marker → trả nguyên content.
    var _pickVariant = function (content) {
        if (!content || typeof content !== 'string') return content || '';
        var MARKER = '<!--##VARIANT##-->';
        if (content.indexOf(MARKER) === -1) return content;
        var parts = content.split(MARKER)
            .map(function (s) { return s.replace(/^\s+|\s+$/g, ''); })
            .filter(function (s) { return s.length > 0; });
        if (parts.length === 0) return '';
        if (parts.length === 1) return parts[0];
        return parts[Math.floor(Math.random() * parts.length)];
    };

    // Đối tượng NotificationClient chính
```

- [ ] **Step 2: Replace `body.innerHTML = notification.content;` (xuất hiện 2 lần)**

Use Edit tool với `replace_all=true`:

`old_string`:
```
body.innerHTML = notification.content;
```

`new_string`:
```
body.innerHTML = _pickVariant(notification.content);
```

- [ ] **Step 3: Replace `var modalHtml = notification.content;`**

Use Edit tool với `replace_all=false`:

`old_string`:
```
            var modalHtml = notification.content;
```

`new_string`:
```
            var modalHtml = _pickVariant(notification.content);
```

Nếu Edit báo `old_string` không khớp (indent khác), copy ra nguyên context từ Read trước khi Edit.

- [ ] **Step 4: Replace `content.innerHTML = notification.content;`**

Use Edit tool với `replace_all=false`:

`old_string`:
```
        content.innerHTML = notification.content;
```

`new_string`:
```
        content.innerHTML = _pickVariant(notification.content);
```

- [ ] **Step 5: Replace `var url = notification.content;`**

Use Edit tool với `replace_all=false`:

`old_string`:
```
        var url = notification.content;
```

`new_string`:
```
        var url = _pickVariant(notification.content);
```

- [ ] **Step 6: Verify edits**

Run: `git diff NotificationAPI/wwwroot/js/notification-client-v1.js | grep -c "_pickVariant"`
Expected: `6`

Run: `git diff NotificationAPI/wwwroot/js/notification-client-v1.js | grep -E "^\+.*notification\.content" | grep -v "_pickVariant"`
Expected: empty

- [ ] **Step 7: Commit**

```bash
git add NotificationAPI/wwwroot/js/notification-client-v1.js
git commit -m "feat(client-v1): random content variants per render

Cùng cơ chế như client-v2: marker <!--##VARIANT##--> + _pickVariant()
helper, uniform random pick mỗi lần render. Backward compat 100%.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Update `notification-client.js`

**Files:**
- Modify: `NotificationAPI/wwwroot/js/notification-client.js`

Lặp lại Task 3 cho file `notification-client.js`. File này gần giống `-v1.js` (chỉ khác 1 dòng dùng `this` vs `self` ở line 149 — không liên quan tới đoạn sửa).

- [ ] **Step 1: Add helper function**

Use Edit tool với `replace_all=false`:

`old_string`:
```
(function (window) {
    'use strict';

    // Đối tượng NotificationClient chính
```

`new_string`:
```
(function (window) {
    'use strict';

    // Helper: tách content theo marker <!--##VARIANT##--> và pick uniform random 1 variant.
    // Backward compat: content không có marker → trả nguyên content.
    var _pickVariant = function (content) {
        if (!content || typeof content !== 'string') return content || '';
        var MARKER = '<!--##VARIANT##-->';
        if (content.indexOf(MARKER) === -1) return content;
        var parts = content.split(MARKER)
            .map(function (s) { return s.replace(/^\s+|\s+$/g, ''); })
            .filter(function (s) { return s.length > 0; });
        if (parts.length === 0) return '';
        if (parts.length === 1) return parts[0];
        return parts[Math.floor(Math.random() * parts.length)];
    };

    // Đối tượng NotificationClient chính
```

- [ ] **Step 2: Replace `body.innerHTML = notification.content;` (xuất hiện 2 lần)**

Use Edit tool với `replace_all=true`:

`old_string`:
```
body.innerHTML = notification.content;
```

`new_string`:
```
body.innerHTML = _pickVariant(notification.content);
```

- [ ] **Step 3: Replace `var modalHtml = notification.content;`**

Use Edit tool với `replace_all=false`:

`old_string`:
```
            var modalHtml = notification.content;
```

`new_string`:
```
            var modalHtml = _pickVariant(notification.content);
```

- [ ] **Step 4: Replace `content.innerHTML = notification.content;`**

Use Edit tool với `replace_all=false`:

`old_string`:
```
        content.innerHTML = notification.content;
```

`new_string`:
```
        content.innerHTML = _pickVariant(notification.content);
```

- [ ] **Step 5: Replace `var url = notification.content;`**

Use Edit tool với `replace_all=false`:

`old_string`:
```
        var url = notification.content;
```

`new_string`:
```
        var url = _pickVariant(notification.content);
```

- [ ] **Step 6: Verify edits**

Run: `git diff NotificationAPI/wwwroot/js/notification-client.js | grep -c "_pickVariant"`
Expected: `6`

Run: `git diff NotificationAPI/wwwroot/js/notification-client.js | grep -E "^\+.*notification\.content" | grep -v "_pickVariant"`
Expected: empty

- [ ] **Step 7: Commit**

```bash
git add NotificationAPI/wwwroot/js/notification-client.js
git commit -m "feat(client): random content variants per render

Cùng cơ chế như client-v1/v2: marker <!--##VARIANT##--> + _pickVariant()
helper, uniform random pick mỗi lần render. Backward compat 100%.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: Smoke test trong browser console

**Files:** none (chỉ verify behavior)

Mục đích: xác nhận `_pickVariant` hoạt động đúng cho mọi edge case mà không cần dựng Couchbase + tạo notification thật.

- [ ] **Step 1: Start app**

Run: `dotnet run --project NotificationAPI/NotificationAPI.csproj`
Expected: app listens at `http://localhost:5201` (xem `Properties/launchSettings.json`). Đợi đến khi log hiện "Application started" hoặc Couchbase connected.

- [ ] **Step 2: Open browser console**

Mở Chrome/Edge → `http://localhost:5201/js/notification-client-v2.js` → DevTools (F12) → Console tab. Hoặc dễ hơn: mở `http://localhost:5201/debug.html` rồi F12.

- [ ] **Step 3: Inject helper for testing (vì `_pickVariant` private trong IIFE, không truy cập trực tiếp được)**

Trong console paste:

```js
// Copy-paste y nguyên hàm helper từ file để test isolated
var _pickVariant = function (content) {
    if (!content || typeof content !== 'string') return content || '';
    var MARKER = '<!--##VARIANT##-->';
    if (content.indexOf(MARKER) === -1) return content;
    var parts = content.split(MARKER)
        .map(function (s) { return s.replace(/^\s+|\s+$/g, ''); })
        .filter(function (s) { return s.length > 0; });
    if (parts.length === 0) return '';
    if (parts.length === 1) return parts[0];
    return parts[Math.floor(Math.random() * parts.length)];
};
```

- [ ] **Step 4: Test case 1 — null/undefined/empty**

Trong console paste:

```js
console.assert(_pickVariant(null) === '', 'null should return ""');
console.assert(_pickVariant(undefined) === '', 'undefined should return ""');
console.assert(_pickVariant('') === '', 'empty should return ""');
console.assert(_pickVariant(123) === '', 'non-string should return ""');
console.log('Test 1 PASS');
```

Expected: `Test 1 PASS` (không thấy assertion failed warning).

- [ ] **Step 5: Test case 2 — không có marker**

Trong console paste:

```js
console.assert(_pickVariant('<div>Hello</div>') === '<div>Hello</div>', 'no marker -> return as-is');
console.log('Test 2 PASS');
```

Expected: `Test 2 PASS`.

- [ ] **Step 6: Test case 3 — N variants, uniform distribution**

Trong console paste:

```js
var content = 'A<!--##VARIANT##-->B<!--##VARIANT##-->C';
var counts = {A: 0, B: 0, C: 0};
for (var i = 0; i < 3000; i++) {
    counts[_pickVariant(content)]++;
}
console.log('Distribution (1000 expected per variant):', counts);
// Mỗi count nên trong khoảng 900-1100 với 3000 samples
var ok = counts.A > 800 && counts.A < 1200 && counts.B > 800 && counts.B < 1200 && counts.C > 800 && counts.C < 1200;
console.assert(ok, 'distribution should be approximately uniform');
console.log(ok ? 'Test 3 PASS' : 'Test 3 FAIL');
```

Expected: `Test 3 PASS` và mỗi count trong khoảng 800-1200.

- [ ] **Step 7: Test case 4 — marker ở đầu/cuối + whitespace**

Trong console paste:

```js
// Marker ở đầu: phần đầu rỗng → bị filter
console.assert(_pickVariant('<!--##VARIANT##-->onlyone') === 'onlyone', 'marker at start -> 1 part');
// Marker ở cuối: phần cuối rỗng → bị filter
console.assert(_pickVariant('onlyone<!--##VARIANT##-->') === 'onlyone', 'marker at end -> 1 part');
// Toàn whitespace giữa marker
var r = _pickVariant('A<!--##VARIANT##-->   \n\t  <!--##VARIANT##-->B');
console.assert(r === 'A' || r === 'B', 'whitespace-only variant filtered, result must be A or B, got: ' + r);
console.log('Test 4 PASS');
```

Expected: `Test 4 PASS`.

- [ ] **Step 8: Test case 5 — content là URL (Link show type)**

Trong console paste:

```js
var url = 'https://a.com/x<!--##VARIANT##-->https://b.com/y';
var r = _pickVariant(url);
console.assert(r === 'https://a.com/x' || r === 'https://b.com/y', 'URL variant: ' + r);
console.log('Test 5 PASS');
```

Expected: `Test 5 PASS`.

- [ ] **Step 9: Stop app**

Ctrl+C trong terminal đang chạy `dotnet run`.

---

## Task 6: Merge & push

**Files:** none

- [ ] **Step 1: Verify all commits on branch**

Run: `git log master..HEAD --oneline`
Expected: 3 commit (Task 2, 3, 4).

- [ ] **Step 2: Push feature branch**

Run: `git push -u origin feat/random-content-variants`
Expected: branch created on origin.

- [ ] **Step 3: Hỏi user xác nhận trước khi merge vào master**

Hỏi: "3 commit feature đã push. Có merge --no-ff vào master và xoá branch không?"

Nếu user OK → Step 4. Nếu không → STOP, để user tự quyết.

- [ ] **Step 4: Checkout master, pull, merge --no-ff**

```bash
git checkout master
git pull origin master
git merge --no-ff feat/random-content-variants -m "Merge branch 'feat/random-content-variants' — random content variants

Cho phép 1 notification chứa nhiều biến thể HTML trong field content,
ngăn cách bằng marker <!--##VARIANT##-->. Mỗi lần render, JS client
pick uniform random 1 biến thể. Backward compat 100%, không đổi
backend / schema / admin UI.

Spec: docs/superpowers/specs/2026-06-23-random-content-variants-design.md
Plan: docs/superpowers/plans/2026-06-23-random-content-variants.md

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

Expected: Merge thành công, 3 file thay đổi.

- [ ] **Step 5: Push master + xoá branch**

```bash
git push origin master
git branch -d feat/random-content-variants
git push origin --delete feat/random-content-variants
```

Expected: master updated, branch xoá local + remote.

- [ ] **Step 6: Final verify**

Run: `git log --oneline -5`
Expected: thấy merge commit trên top, dưới là 3 feat commit.

Run: `git status`
Expected: `working tree clean`, `Your branch is up to date with 'origin/master'`.
