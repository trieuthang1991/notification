/**
 * Xử lý form đăng xuất
 */
document.addEventListener('DOMContentLoaded', function() {
    // Tìm tất cả các form đăng xuất
    var logoutForms = document.querySelectorAll('form[action*="Logout"]');
    
    // Thêm sự kiện submit cho mỗi form
    logoutForms.forEach(function(form) {
        form.addEventListener('submit', function(e) {
            e.preventDefault(); // Ngăn chặn hành vi mặc định
            
            // Lấy token chống giả mạo
            var token = form.querySelector('input[name="__RequestVerificationToken"]').value;
            
            // Tạo request
            fetch('/Account/Logout', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                    'RequestVerificationToken': token
                },
                body: '__RequestVerificationToken=' + encodeURIComponent(token)
            })
            .then(function(response) {
                if (response.ok) {
                    // Chuyển hướng đến trang đăng nhập
                    window.location.href = '/Account/Login';
                } else {
                    console.error('Lỗi khi đăng xuất:', response.status);
                    alert('Đã xảy ra lỗi khi đăng xuất. Vui lòng thử lại.');
                }
            })
            .catch(function(error) {
                console.error('Lỗi khi đăng xuất:', error);
                alert('Đã xảy ra lỗi khi đăng xuất. Vui lòng thử lại.');
            });
        });
    });
});
