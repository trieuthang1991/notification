# Hướng dẫn kỹ thuật thêm dữ liệu thông báo qua API

## 1. Tổng quan

Hệ thống NotificationAPI cung cấp các API endpoint cho phép thêm thông báo mới vào hệ thống từ các ứng dụng bên ngoài. Hướng dẫn này mô tả chi tiết cách sử dụng các API này.

## 2. Các API endpoint

### 2.1. Thêm một thông báo

- **URL**: `/api/Notification/external`
- **Method**: POST
- **Content-Type**: application/json

### 2.2. Thêm nhiều thông báo cùng lúc

- **URL**: `/api/Notification/external/batch`
- **Method**: POST
- **Content-Type**: application/json

## 3. Xác thực

Tất cả các API đều yêu cầu xác thực bằng API key. API key phải được gửi trong body của request.

```json
{
  "apiKey": "api_key_1"
}
```

## 4. Cấu trúc dữ liệu

### 4.1. Thêm một thông báo

```json
{
  "apiKey": "api_key_1",
  "domains": ["example.com"],
  "userId": "all",
  "pageShows": [
    {
      "clt": 0,//Path
      "cltg": 0,//Same (So sánh bằng)
      "value": "all"
    }
  ],
  "pageExcludes": [],
  "showTypes": [1], //Popup,
  "title": "Chính sách thay đổi chính sách  ",
  "content": "Nội dung thông báo",
  "moreInfo": {
    "popupDismissable": true,
    "htmlDisplayLocation": "",
    "isDirectLink": false
  },
  "startDate": 1712851200,
  "endDate": 1715443200,
  "maxShow": 10,
  "frequency": 0,
  "order": 1,
  "attributes": [3],
  "deviceTypes": [1]
}
```

### 4.2. Thêm nhiều thông báo cùng lúc

```json
{
  "apiKey": "api_key_1",
  "notifications": [
    {
      "domains": ["example.com"],
      "userId": "all",
      "pageShows": [
        {
          "clt": 0,
          "cltg": 0,
          "value": "all"
        }
      ],
      "showTypes": [1],
      "title": "Thông báo 1",
      "content": "Nội dung thông báo 1",
      "delay": 2,
      "startDate": 1712851200,
      "endDate": 1715443200,
      "maxShow": 10,
      "order": 1,
      "attributes": [3],
      "deviceTypes": [1]
    },
    {
      "domains": ["example.com"],
      "userId": "user123",
      "pageShows": [
        {
          "clt": 0,
          "cltg": 0,
          "value": "/trang-chu"
        }
      ],
      "showTypes": [2],
      "title": "Thông báo 2",
      "content": "<p>Nội dung HTML</p>",
      "htmlDisplayLocation": "#notification-container",
      "delay": 5,
      "startDate": 1712851200,
      "endDate": 1715443200,
      "maxShow": 5,
      "order": 2,
      "attributes": [2, 3],
      "deviceTypes": [1]
    }
  ]
}
```

## 5. Mô tả các trường dữ liệu

### 5.1. Thông tin cơ bản

| Trường | Kiểu dữ liệu | Bắt buộc | Mô tả |
|--------|--------------|----------|-------|
| apiKey | string | Có | API key để xác thực |
| domains | array of string | Có | Danh sách các tên miền áp dụng thông báo |
| userId | string | Không | ID người dùng nhận thông báo, "all" cho tất cả người dùng |
| title | string | Có | Tiêu đề thông báo |
| content | string | Có | Nội dung thông báo, có thể chứa HTML |

### 5.2. Phạm vi hiển thị

| Trường | Kiểu dữ liệu | Bắt buộc | Mô tả |
|--------|--------------|----------|-------|
| pageShows | array of ConfigLink | Không | Danh sách các trang hiển thị thông báo |
| pageExcludes | array of ConfigLink | Không | Danh sách các trang loại trừ không hiển thị thông báo |
| deviceTypes | array of int | Không | Các loại thiết bị hiển thị thông báo |

### 5.3. Kiểu hiển thị và thuộc tính

| Trường | Kiểu dữ liệu | Bắt buộc | Mô tả |
|--------|--------------|----------|-------|
| showTypes | array of int | Có | Kiểu hiển thị thông báo |
| attributes | array of int | Không | Các thuộc tính đặc biệt của thông báo |
| htmlDisplayLocation | string | Không | Vị trí hiển thị HTML (selector CSS) |
| popupDismissable | boolean | Không | Cho phép đóng popup (mặc định: true) |
| isDirectLink | boolean | Không | Nội dung là đường dẫn trực tiếp (mặc định: false) |
| delay | int | Không | Thời gian trễ hiển thị (giây) (mặc định: 0) |

### 5.4. Thời gian và tần suất

| Trường | Kiểu dữ liệu | Bắt buộc | Mô tả |
|--------|--------------|----------|-------|
| startDate | long | Không | Thời gian bắt đầu hiển thị (Unix timestamp) |
| endDate | long | Không | Thời gian kết thúc hiển thị (Unix timestamp) |
| maxShow | int | Không | Số lần hiển thị tối đa cho mỗi người dùng |
| frequency | int | Không | Tần suất hiển thị (giây) |
| order | int | Không | Thứ tự ưu tiên hiển thị (giá trị thấp hơn được hiển thị trước) |

## 6. Các giá trị enum

### 6.1. ShowTypeNotification (Kiểu hiển thị)

| Giá trị | Mô tả |
|---------|-------|
| 0 | All |
| 1 | Popup |
| 2 | HTML |
| 3 | Link |
| 4 | Modal |

### 6.2. DeviceType (Loại thiết bị)

| Giá trị | Mô tả |
|---------|-------|
| 0 | Unknown |
| 1 | All |
| 2 | Website |
| 3 | MobileWeb |
| 4 | Mobile |

### 6.3. Attributes (Thuộc tính đặc biệt)

| Giá trị | Mô tả |
|---------|-------|
| 1 | Lấy lại nội dung khi load trang |
| 2 | Gửi dữ liệu click về server |
| 3 | Không làm phiền người dùng (đánh dấu đã xem khi click) |

### 6.4. ConfigLinkType (CLT - Loại đường dẫn)

| Giá trị | Mô tả |
|---------|-------|
| 0 | Path (chỉ lấy đường dẫn) |
| 1 | Link (lấy cả đường dẫn và query) |

### 6.5. ConfigLinkTypeGet (CLTG - Cách so sánh)

| Giá trị | Mô tả |
|---------|-------|
| 0 | Same (so sánh chính xác) |
| 1 | Regex (so sánh theo biểu thức chính quy) |

## 7. Ví dụ

### 7.1. Thêm một thông báo popup

```json
{
  "apiKey": "api_key_1",
  "domains": ["example.com"],
  "userId": "all",
  "pageShows": [
    {
      "clt": 0,
      "cltg": 0,
      "value": "all"
    }
  ],
  "showTypes": [1],
  "title": "Chào mừng đến với website của chúng tôi!",
  "content": "<p>Chúng tôi rất vui mừng được chào đón bạn.</p><p>Hãy khám phá các tính năng mới nhất của chúng tôi!</p>",
  "popupDismissable": true,
  "delay": 3,
  "startDate": 1712851200,
  "endDate": 1715443200,
  "maxShow": 3,
  "frequency": 86400,
  "order": 1,
  "attributes": [3],
  "deviceTypes": [1]
}
```

### 7.2. Thêm một thông báo HTML

```json
{
  "apiKey": "api_key_1",
  "domains": ["example.com"],
  "userId": "all",
  "pageShows": [
    {
      "clt": 0,
      "cltg": 0,
      "value": "/trang-chu"
    }
  ],
  "showTypes": [2],
  "title": "Thông báo quan trọng",
  "content": "<div class='alert alert-info'>Chúng tôi sẽ bảo trì hệ thống vào ngày 15/05/2023. Xin lỗi vì sự bất tiện này.</div>",
  "htmlDisplayLocation": "#notification-container",
  "delay": 2,
  "startDate": 1712851200,
  "endDate": 1715443200,
  "maxShow": 5,
  "order": 2,
  "attributes": [2, 3],
  "deviceTypes": [1]
}
```

### 7.3. Thêm một thông báo link

```json
{
  "apiKey": "api_key_1",
  "domains": ["example.com"],
  "userId": "all",
  "pageShows": [
    {
      "clt": 0,
      "cltg": 0,
      "value": "all"
    }
  ],
  "showTypes": [3],
  "title": "Khuyến mãi đặc biệt",
  "content": "/khuyen-mai",
  "isDirectLink": false,
  "startDate": 1712851200,
  "endDate": 1715443200,
  "maxShow": 10,
  "order": 3,
  "attributes": [3],
  "deviceTypes": [1]
}
```

### 7.4. Thêm một thông báo modal

```json
{
  "apiKey": "api_key_1",
  "domains": ["example.com"],
  "userId": "all",
  "pageShows": [
    {
      "clt": 0,
      "cltg": 0,
      "value": "/san-pham"
    }
  ],
  "showTypes": [4],
  "title": "Đăng ký nhận thông báo",
  "content": "<div class='text-center p-3'><p>Đăng ký để nhận thông báo về các sản phẩm mới!</p><form><div class='mb-3'><input type='email' class='form-control' placeholder='Email của bạn'></div><button type='button' class='btn btn-primary notification-track-click'>Đăng ký</button></form></div>",
  "htmlDisplayLocation": "#notification-modal",
  "popupDismissable": true,
  "delay": 5,
  "startDate": 1712851200,
  "endDate": 1715443200,
  "maxShow": 2,
  "order": 4,
  "attributes": [2, 3],
  "deviceTypes": [1]
}
```

## 8. Phản hồi API

### 8.1. Thành công

```json
{
  "id": "notification_id_123",
  "message": "Đã tạo thông báo từ hệ thống ngoài thành công"
}
```

### 8.2. Lỗi

```json
{
  "message": "API key không hợp lệ"
}
```

## 9. Mã lỗi

| Mã lỗi | Mô tả |
|--------|-------|
| 400 | Bad Request - Dữ liệu không hợp lệ |
| 401 | Unauthorized - API key không hợp lệ |
| 500 | Internal Server Error - Lỗi server |

## 10. Lưu ý quan trọng

1. **Thời gian**: Tất cả các trường thời gian (startDate, endDate) phải ở định dạng Unix timestamp (số giây từ 1/1/1970).
2. **HTML Content**: Khi sử dụng nội dung HTML, hãy đảm bảo rằng HTML hợp lệ và an toàn.
3. **Tracking Link**: Để theo dõi tương tác của người dùng, thêm class `notification-track-click` vào các phần tử có thể click.
4. **API Key**: Bảo vệ API key của bạn và không chia sẻ nó với bất kỳ ai.
5. **Tối ưu hóa**: Sử dụng API batch để thêm nhiều thông báo cùng lúc thay vì gọi API riêng lẻ nhiều lần.
6. **Delay**: Trường `delay` cho phép điều chỉnh thời gian trễ hiển thị thông báo (tính bằng giây). Đặc biệt hữu ích cho các loại thông báo HTML. Lưu ý: đối với popup và modal, trường delay không ảnh hưởng đến thứ tự hiển thị, chúng luôn tuân theo thứ tự ưu tiên (order).
7. **Thứ tự ưu tiên**: Thứ tự ưu tiên (order) chỉ được áp dụng sau khi thông báo đã thỏa mãn tất cả các điều kiện hiển thị (trạng thái hoạt động, thời gian hiển thị, trang hiển thị, số lần hiển thị và tần suất). Thông báo kiểu popup và modal luôn được hiển thị theo thứ tự ưu tiên (order), không bị ảnh hưởng bởi trường delay. Thông báo có order thấp hơn sẽ được hiển thị trước.

## 11. Ví dụ code

### 11.1. JavaScript (Fetch API)

```javascript
// Thêm một thông báo
async function createNotification() {
  const notification = {
    apiKey: "api_key_1",
    domains: ["example.com"],
    userId: "all",
    pageShows: [{ clt: 0, cltg: 0, value: "all" }],
    showTypes: [1],
    title: "Thông báo mới",
    content: "<p>Nội dung thông báo</p>",
    startDate: Math.floor(Date.now() / 1000),
    endDate: Math.floor(Date.now() / 1000) + 30 * 24 * 60 * 60, // 30 ngày
    maxShow: 10,
    order: 1,
    attributes: [3],
    deviceTypes: [1]
  };

  try {
    const response = await fetch('https://your-api-domain.com/api/Notification/external', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(notification)
    });

    const data = await response.json();
    console.log('Thông báo đã được tạo:', data);
  } catch (error) {
    console.error('Lỗi khi tạo thông báo:', error);
  }
}
```

### 11.2. C# (HttpClient)

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class NotificationApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;
    private readonly string _apiKey;

    public NotificationApiClient(string apiUrl, string apiKey)
    {
        _httpClient = new HttpClient();
        _apiUrl = apiUrl;
        _apiKey = apiKey;
    }

    public async Task<string> CreateNotificationAsync()
    {
        var notification = new
        {
            apiKey = _apiKey,
            domains = new[] { "example.com" },
            userId = "all",
            pageShows = new[] { new { clt = 0, cltg = 0, value = "all" } },
            showTypes = new[] { 1 },
            title = "Thông báo mới",
            content = "<p>Nội dung thông báo</p>",
            startDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            endDate = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds(),
            maxShow = 10,
            order = 1,
            attributes = new[] { 3 },
            deviceTypes = new[] { 1 }
        };

        var json = JsonSerializer.Serialize(notification);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_apiUrl}/api/Notification/external", content);
        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadAsStringAsync();
        return responseContent;
    }
}
```

### 11.3. PHP (cURL)

```php
<?php
function createNotification() {
    $apiUrl = 'https://your-api-domain.com/api/Notification/external';
    $apiKey = 'api_key_1';

    $notification = [
        'apiKey' => $apiKey,
        'domains' => ['example.com'],
        'userId' => 'all',
        'pageShows' => [['clt' => 0, 'cltg' => 0, 'value' => 'all']],
        'showTypes' => [1],
        'title' => 'Thông báo mới',
        'content' => '<p>Nội dung thông báo</p>',
        'startDate' => time(),
        'endDate' => time() + (30 * 24 * 60 * 60), // 30 ngày
        'maxShow' => 10,
        'order' => 1,
        'attributes' => [3],
        'deviceTypes' => [1]
    ];

    $ch = curl_init($apiUrl);
    curl_setopt($ch, CURLOPT_RETURNTRANSFER, true);
    curl_setopt($ch, CURLOPT_POST, true);
    curl_setopt($ch, CURLOPT_POSTFIELDS, json_encode($notification));
    curl_setopt($ch, CURLOPT_HTTPHEADER, [
        'Content-Type: application/json'
    ]);

    $response = curl_exec($ch);
    $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
    curl_close($ch);

    if ($httpCode == 200) {
        return json_decode($response, true);
    } else {
        throw new Exception("API error: " . $response);
    }
}
?>
```
