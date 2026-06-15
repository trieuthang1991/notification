# Hướng dẫn tích hợp hệ thống thông báo cho hệ thống bên ngoài

## Giới thiệu

Tài liệu này hướng dẫn cách tích hợp hệ thống thông báo (Notification API) vào các hệ thống bên ngoài. Hệ thống thông báo cho phép hiển thị các thông báo dưới nhiều hình thức khác nhau (popup, HTML, link) trên các trang web.

## Các loại thông báo

Hệ thống hỗ trợ 3 loại thông báo:

1. **Popup**: Hiển thị thông báo dạng popup trên trang
2. **HTML**: Hiển thị nội dung HTML trong một vùng được chỉ định trên trang
3. **Link**: Hiển thị liên kết trong một container được chỉ định

## API Endpoints

### 1. Tạo một thông báo đơn lẻ

**Endpoint**: `POST /api/Notification/external`

**Headers**:
```
Content-Type: application/json
```

**Request Body**:
```json
{
  "apiKey": "api_key_1",
  "domains": ["example.com"],
  "userId": "all",
  "pageShows": ["all"],
  "pageExcludes": [],
  "showTypes": [3],
  "title": "Tiêu đề thông báo",
  "content": "https://example.com/promotion",
  "htmlDisplayLocation": "",
  "isDirectLink": false,
  "popupDismissable": true,
  "startDate": 1672531200,
  "endDate": 1675209600,
  "maxShow": 10,
  "frequency": 0,
  "order": 1,
  "attributes": [2, 3],
  "deviceTypes": [1],
  "triggerActions": ["all"]
}
```

**Giải thích các trường**:

- `apiKey`: Khóa API để xác thực (được cấu hình trong appsettings.json)
- `domains`: Danh sách các tên miền mà thông báo sẽ hiển thị
- `userId`: ID người dùng hoặc "all" cho tất cả người dùng
- `pageShows`: Danh sách các trang sẽ hiển thị thông báo (có thể dùng "all" cho tất cả các trang)
- `pageExcludes`: Danh sách các trang sẽ không hiển thị thông báo
- `showTypes`: Kiểu hiển thị (1: Popup, 2: HTML, 3: Link)
- `title`: Tiêu đề thông báo
- `content`: Nội dung thông báo (đối với Link, đây là URL)
- `htmlDisplayLocation`: Vị trí hiển thị HTML (ví dụ: "#notification-container")
- `isDirectLink`: Có chuyển hướng trực tiếp đến link không
- `popupDismissable`: Có cho phép đóng popup không
- `startDate`: Thời gian bắt đầu hiển thị (Unix timestamp)
- `endDate`: Thời gian kết thúc hiển thị (Unix timestamp)
- `maxShow`: Số lần tối đa hiển thị cho mỗi người dùng (0 = không giới hạn)
- `frequency`: Tần suất hiển thị (tính theo giờ, 0 = không giới hạn)
- `order`: Thứ tự ưu tiên khi có nhiều thông báo
- `attributes`: Các thuộc tính đặc biệt (2: Gửi dữ liệu lastClick lên server, 3: Không làm phiền người dùng)
- `deviceTypes`: Loại thiết bị (0: Unknown, 1: All, 2: Website, 3: Mobile)
- `triggerActions`: Các hành động kích hoạt thông báo

**Response**:
```json
{
  "id": "notification_id",
  "message": "Đã tạo thông báo từ hệ thống ngoài thành công"
}
```

### 2. Tạo nhiều thông báo cùng lúc

**Endpoint**: `POST /api/Notification/external/batch`

**Headers**:
```
Content-Type: application/json
```

**Request Body**:
```json
{
  "apiKey": "api_key_1",
  "notifications": [
    {
      "domains": ["example.com"],
      "userId": "all",
      "pageShows": ["all"],
      "showTypes": [3],
      "title": "Thông báo 1",
      "content": "https://example.com/promo1",
      "startDate": 1672531200,
      "endDate": 1675209600,
      "attributes": [2, 3]
    },
    {
      "domains": ["example.com"],
      "userId": "all",
      "pageShows": ["/products", "/services"],
      "showTypes": [3],
      "title": "Thông báo 2",
      "content": "https://example.com/promo2",
      "startDate": 1672531200,
      "endDate": 1675209600,
      "attributes": [2, 3]
    }
  ]
}
```

## Hướng dẫn tạo thông báo dạng Link

Để tạo thông báo dạng Link, cần lưu ý các điểm sau:

1. Đặt `showTypes` là `[3]` (Link)
2. Đặt `content` là URL đích của link
3. Đặt `title` là văn bản hiển thị cho link
4. Nếu muốn chuyển hướng trực tiếp đến link, đặt `isDirectLink` là `true`
5. Nếu muốn hiển thị link trong container, đảm bảo trang web đã có phần tử với selector tương ứng với `containerLink` trong cấu hình client

### Ví dụ tạo thông báo dạng Link

```json
{
  "apiKey": "api_key_1",
  "domains": ["example.com"],
  "userId": "all",
  "pageShows": ["all"],
  "pageExcludes": [],
  "showTypes": [3],
  "title": "Khuyến mãi đặc biệt",
  "content": "https://example.com/special-promotion",
  "isDirectLink": false,
  "startDate": 1672531200,
  "endDate": 1675209600,
  "maxShow": 5,
  "order": 1,
  "attributes": [2, 3],
  "deviceTypes": [1],
  "triggerActions": ["all"]
}
```

## Thuộc tính đặc biệt

Hệ thống hỗ trợ các thuộc tính đặc biệt thông qua trường `attributes`:

- **Attribute 2**: Gửi dữ liệu lastClick lên server khi người dùng tương tác
- **Attribute 3**: Không làm phiền người dùng (đánh dấu thông báo là "đã xem" khi người dùng tương tác)

## Tích hợp JavaScript Client

Để hiển thị thông báo trên trang web, cần nhúng JavaScript client vào trang:

```html
<script src="https://your-notification-api-domain/js/notification-client.js"></script>
<script>
document.addEventListener('DOMContentLoaded', function() {
    var notificationClient = new NotificationClient({
        apiUrl: 'https://your-notification-api-domain/api/Notification',
        domain: window.location.hostname,
        userId: 'user123', // Hoặc 'all' cho tất cả người dùng
        containerLink: '#container-link', // Selector cho container chứa các link
        linkCountContainer: '#link-count', // Selector cho phần tử hiển thị số lượng link
        debug: false
    });

    notificationClient.run();
});
</script>
```

Đảm bảo trang web có các phần tử HTML tương ứng:

```html
<!-- Container cho các link thông báo -->
<div id="container-link"></div>

<!-- Hiển thị số lượng link (tùy chọn) -->
<span id="link-count"></span>
```

## Xử lý lỗi và gỡ rối

Để bật chế độ gỡ rối, đặt `debug: true` trong cấu hình client:

```javascript
var notificationClient = new NotificationClient({
    // Các cấu hình khác
    debug: true
});
```

Khi gặp vấn đề, kiểm tra:
1. Console của trình duyệt để xem các thông báo lỗi
2. Network tab để kiểm tra các request API
3. Đảm bảo API key hợp lệ
4. Kiểm tra các selector HTML đã chính xác

## Bảo mật

- Bảo vệ API key của bạn, không chia sẻ hoặc nhúng trực tiếp vào mã nguồn client-side
- Sử dụng HTTPS cho tất cả các request API
- Giới hạn các domain được phép gọi API thông qua cấu hình CORS nếu cần
