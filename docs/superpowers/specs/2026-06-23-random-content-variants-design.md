# Random Content Variants — Design Spec

**Date**: 2026-06-23
**Owner**: thangtv
**Scope**: NotificationAPI / wwwroot/js/notification-client*.js

## Mục tiêu

Cho phép 1 notification chứa nhiều biến thể (variant) nội dung HTML. Mỗi lần client render, JS pick ngẫu nhiên 1 variant để hiển thị. Tránh người dùng thấy cùng 1 nội dung lặp đi lặp lại.

## Constraint do user đặt

- KHÔNG đổi backend code (`NotificationCB`, `NotificationConfig`, API response, Couchbase schema).
- KHÔNG đổi admin UI (vẫn 1 textarea `content` như cũ).
- KHÔNG đổi Redis cache layer hiện có.
- Chỉ thay đổi trong JS client.

## Convention input

Admin nhập vào textarea `content` như bình thường, các biến thể HTML ngăn nhau bằng marker:

```
<!--##VARIANT##-->
```

Ví dụ:

```html
<div class="promo">Sale 20% hôm nay!</div>
<!--##VARIANT##-->
<div class="promo">Giảm 30% cuối tuần!</div>
<!--##VARIANT##-->
<div class="promo">Mua 1 tặng 1 — chỉ 12h!</div>
```

Notification cũ không có marker → render nguyên content như cũ. Không cần migration.

## Code change

### 1. Helper function

Thêm vào `NotificationClient.prototype` (đặt cùng nhóm utility, gần đầu file):

```js
NotificationClient.prototype._pickVariant = function (content) {
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

### 2. Call sites — đổi `notification.content` thành `this._pickVariant(notification.content)`

Trong mỗi file `notification-client.js`, `notification-client-v1.js`, `notification-client-v2.js`, sửa 5 chỗ (số dòng ở đây tính theo `notification-client-v2.js`, file khác có thể lệch nhẹ):

| Line (v2) | Show type | Hiện tại |
|---|---|---|
| 1235 | HTML | `body.innerHTML = notification.content;` |
| 1504 | Modal | `var modalHtml = notification.content;` |
| 1564 | Popup body | `body.innerHTML = notification.content;` |
| 1701 | Inline HTML | `content.innerHTML = notification.content;` |
| 1757 | Link URL | `var url = notification.content;` |

**Lưu ý Link URL**: cùng marker `<!--##VARIANT##-->` cũng tách được URL (string split). Admin có thể nhập:
```
https://example.com/landing-a<!--##VARIANT##-->https://example.com/landing-b
```

## Tính chất

- **Pick mỗi lần render** → user reload page thì random lại. KHÔNG sticky per user.
- **Uniform random** giữa các variant (mỗi variant cơ hội 1/N).
- **Backward 100%** — content không có marker → trả nguyên content.
- **Frequency / MaxShow** vẫn áp dụng theo notification (không tách theo variant). Hệ quả của "chỉ JS": backend không biết variant nào đã shown.
- **Không tracking** variant nào được shown. Nếu sau này cần A/B metric thì là phase 2 (cần thêm client beacon hoặc chuyển random về server).

## Edge cases

| Input | Output |
|---|---|
| `null` / `undefined` / non-string | `''` |
| Content rỗng | `''` |
| Không có marker | nguyên content |
| Marker ở đầu/cuối | filter trim → ignore phần rỗng |
| Sau filter còn 1 variant | trả luôn, không random |
| N variants (N ≥ 2) | uniform random pick |
| Variant chỉ có whitespace | bị filter bỏ |

## Files modified

- `NotificationAPI/wwwroot/js/notification-client.js`
- `NotificationAPI/wwwroot/js/notification-client-v1.js`
- `NotificationAPI/wwwroot/js/notification-client-v2.js`

Không file nào khác (C#, view, csproj, appsettings) bị ảnh hưởng.

## Testing

Manual test qua `wwwroot/debug.html` hoặc 1 trang partner thật:

1. Tạo notification có content nhiều variant, reload trang 5-10 lần → quan sát thấy ít nhất 2-3 variant khác nhau.
2. Tạo notification content KHÔNG có marker → render đúng như cũ.
3. Tạo notification content có marker nhưng giữa 2 marker là whitespace → variant đó bị filter, không bao giờ shown.
4. Show type Link với 2 URL ngăn nhau bằng marker → click qua nhiều lần thấy redirect khác URL.

## Out of scope (có thể là phase sau)

- Weighted random per variant
- Sticky per user (A/B test)
- Tracking variant impression / click
- Admin UI chuyên biệt cho variant (repeater field thay vì textarea + marker)
- Server-side random
