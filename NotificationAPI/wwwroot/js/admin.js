// Admin area JavaScript

// Xử lý chuyển đổi giữa Unix timestamp và datetime cho datepicker
$(document).ready(function() {
    // Initialize Select2
    $('.select2').select2({
        theme: 'bootstrap-5'
    });

    // Initialize Flatpickr for date inputs
    $(".datepicker").flatpickr({
        enableTime: true,
        dateFormat: "Y-m-d H:i",
        time_24hr: true
    });

    // Xử lý chuyển đổi giữa Unix timestamp và datetime cho datepicker
    $('.datepicker').each(function() {
        var $input = $(this);
        var timestamp = parseInt($input.val());
        
        if (!isNaN(timestamp)) {
            var date = new Date(timestamp * 1000);
            $input.attr('data-original-value', timestamp);
            $input.val(date.toISOString().slice(0, 16).replace('T', ' '));
        }
    });

    $('.datepicker').flatpickr({
        enableTime: true,
        dateFormat: "Y-m-d H:i",
        time_24hr: true,
        onChange: function(selectedDates, dateStr, instance) {
            var $input = $(instance.element);
            if ($input.attr('data-unix') === 'true') {
                var timestamp = Math.floor(selectedDates[0].getTime() / 1000);
                $input.attr('data-original-value', timestamp);
            }
        }
    });

    // Xử lý form submit để chuyển đổi lại thành Unix timestamp
    $('form').on('submit', function() {
        $('.datepicker[data-unix="true"]').each(function() {
            var $input = $(this);
            var dateStr = $input.val();
            
            if (dateStr) {
                var date = new Date(dateStr);
                var timestamp = Math.floor(date.getTime() / 1000);
                $input.val(timestamp);
            }
        });
    });

    // Xử lý sự kiện khi nhấn nút toggle trạng thái
    $('.toggle-status').on('click', function() {
        var button = $(this);
        var id = button.data('id');
        var status = button.data('status');
        
        // Gửi yêu cầu AJAX để cập nhật trạng thái
        $.ajax({
            url: '/Admin/Notification/UpdateStatus',
            type: 'POST',
            data: {
                id: id,
                status: status
            },
            success: function(response) {
                if (response.success) {
                    // Hiển thị thông báo thành công
                    alert(response.message);
                    // Tải lại trang để cập nhật dữ liệu
                    location.reload();
                } else {
                    // Hiển thị thông báo lỗi
                    alert(response.message);
                }
            },
            error: function() {
                alert('Đã xảy ra lỗi khi cập nhật trạng thái.');
            }
        });
    });
});
