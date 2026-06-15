/**
 * NotificationClient - Thư viện JavaScript để hiển thị thông báo từ NotificationAPI
 * Version: 1.0.0
 */

(function (window) {
    'use strict';

    // Đối tượng NotificationClient chính
    var NotificationClient = function (config) {
        // Khởi tạo đối tượng với cấu hình mặc định
        this.init(config);
    };

    // Cấu hình mặc định
    NotificationClient.defaultConfig = {
        apiUrl: '/api/Notification',
        autoDetectDomain: true,
        autoDetectPage: true,
        autoDetectUserId: false,
        autoDetectDevice: true,
        domain: window.location.hostname,
        userId: '',
        device: 0, // Unknown by default
        timeOut: 1000, //3 giây
        storageKeyPrefix: 'notification_data', // Tiền tố cho khóa lưu trữ
        containerLink: '#container-link', // Container để gắn các link
        linkCountContainer: '#link-count', // Container để hiển thị số lượng link
        trackingLink: '.notification-track-click',
        defaultModalId: 'notification-default-modal', // ID của modal mặc định
        debug: false,
        resetTime: false, // Reset sau 24 giờ
        triggerActions: [] //Thuộc tính để thêm các hành động kích hoạt
    };

    // Ánh xạ tên fields ngắn gọn cho localStorage
    NotificationClient.fieldMapping = {
        // Thông báo
        id: 'i',
        domains: 'd',
        userId: 'u',
        pageShows: 'ps',
        pageExcludes: 'pe',
        showTypes: 'st',
        title: 't',
        content: 'c',
        infoMore: 'im',
        startDate: 'sd',
        endDate: 'ed',
        maxShow: 'ms',
        frequency: 'f',
        listTime: 'lt',
        order: 'o',
        attributes: 'a',
        status: 's',
        deviceTypes: 'dt',
        lastUpdated: 'lu',
        dateCreated: 'dc',
        triggerActions: 'ta',


        // Trạng thái
        notificationId: 'ni',
        shownCount: 'sc',
        lastShown: 'ls',
        lastClick: 'lc',
        remainingShows: 'rs',
        seen: 'sn',

        // InfoMore
        popupDismissable: 'pd',
        htmlDisplayLocation: 'hdl',
        isDirectLink: 'idl',
        delay: 'dl',

        // ConfigLink
        clt: 'cl',
        cltg: 'cg',
        value: 'v',

    };

    // Enum DeviceType
    NotificationClient.DeviceType = {
        Unknown: 0,
        All: 1,
        WebSite: 2,
        MobileWeb: 3,
        Mobile: 4
    };

    // Phương thức khởi tạo
    NotificationClient.prototype.init = function (config) {

        // Kết hợp cấu hình mặc định với cấu hình người dùng
        this.config = this._mergeConfig(NotificationClient.defaultConfig, config || {});

        // Đảm bảo userId không rỗng
        if (!this.config.userId) {
            this.config.userId = 'anonymous';
        }

        // Tạo khóa lưu trữ dựa trên userId
        this.storageKey = this._getStorageKey();

        // Khởi tạo các thuộc tính
        this.notifications = [];
        this.lastUpdated = this._getLastUpdated();
        this.currentPage = window.location.pathname;

        // Khởi tạo trạng thái
        this.status = this._getStatus();

        // Phát hiện thiết bị nếu cần
        if (this.config.autoDetectDevice) {
            this.config.device = this._detectDevice();
        }

        // Tạo CSS cho thông báo
        this._createStyles();

        // Log debug nếu được bật
        this._debug('NotificationClient initialized with config:', this.config);
        this._debug('Using storage key:', this.storageKey);
    };

    // Phương thức bắt đầu
    NotificationClient.prototype.start = function () {
        // Tải thông báo
        this.loadNotifications();

        return this;
    };

    // Tạo khóa lưu trữ dựa trên userId
    NotificationClient.prototype._getStorageKey = function () {
        var userId = this.config.userId;
        return this.config.storageKeyPrefix + '_' + userId;
    };

    // Phương thức tải dữ liệu thông báo từ API
    NotificationClient.prototype.loadNotifications = function (callback) {
        var self = this;

        // Xác định domain và userId
        var domain = this.config.domain;
        var userId = this.config.userId;

        if (self._isTriggerActionsChanged(self.config)) {
            this.lastUpdated = 0;
        }

        // Tạo URL API với các tham số
        var apiUrl = this.config.apiUrl + '?Domain=' + encodeURIComponent(domain) +
            '&UserId=' + encodeURIComponent(userId) +
            '&LastUpdated=' + this.lastUpdated +
            '&TriggerActionString=' + encodeURIComponent(this.config.triggerActions.join(',')) +
            '&Device=' + this.config.device;


        this._debug('Loading notifications from:', apiUrl);

        // Gọi API để lấy thông báo
        this._fetchData(apiUrl, function (error, responseData) {
            if (error) {
                self._debug('Error loading notifications:', error);
                if (callback) callback(error);
                return;
            }

            // Cập nhật danh sách thông báo
            if (responseData.notifications && responseData.notifications.length > 0) {

                let data = responseData.notifications;
                let dicStatus = responseData.notificationStatus || {};

                self._debug('Received', data.length, 'notifications');

                // Lưu thông báo vào bộ nhớ
                self.notifications = data;

                // Cập nhật trạng thái cho các thông báo mới từ dữ liệu trả về
                self._updateStatusFromResponse(dicStatus);



                // Cập nhật thời gian cập nhật cuối cùng
                if (responseData.isSetData) {
                    self.lastUpdated = Math.floor(Date.now() / 1000);
                }
                // Lưu vào localStorage
                self._saveData();

                // Kiểm tra và xóa các localStorage có storageKeyPrefix khác với hiện tại
                self._cleanupOldStorageKeyPrefix();
            } else {
                // self._updateLinkCount("1");
                self._debug('No new notifications');
            }
            if (callback) callback(null, responseData);
        });
    };





    // Đếm số lượng link trong containerLink và hiển thị vào linkCountContainer
    NotificationClient.prototype._updateLinkCount = function (sourceId) {

        console.log(sourceId);
        // Kiểm tra xem có container để hiển thị số lượng link không
        var linkCountContainer = document.querySelector(this.config.linkCountContainer);
        if (!linkCountContainer) {
            this._debug('Link count container not found:', this.config.linkCountContainer);
            return;
        }
        console.log(this.config.containerLink);
        // Kiểm tra xem có container chứa các link không
        var containerLink = document.querySelector(this.config.containerLink);
        if (!containerLink) {
            this._debug('Container link not found:', this.config.containerLink);
            return;
        }

        // Đếm số lượng link trong container
        var links = containerLink.querySelectorAll('a');
        var linkCount = links.length;

        // Cập nhật số lượng vào container
        linkCountContainer.textContent = linkCount;

        // Xử lý trường hợp không có link
        if (linkCount === 0) {
            // Xóa nội dung hiện tại của container
            containerLink.innerHTML = '';

            // Tạo thông báo "Không có thông báo nào"
            var emptyMessage = document.createElement('div');
            emptyMessage.className = 'notification-link-empty';
            emptyMessage.textContent = 'Không có thông báo nào';

            // Thêm vào container
            containerLink.appendChild(emptyMessage);

            // Ẩn container số lượng
            // linkCountContainer.style.display = 'none';
        } else {
            // Xóa thông báo "Không có thông báo nào" nếu có
            var emptyMessage = containerLink.querySelector('.notification-link-empty');
            if (emptyMessage) {
                containerLink.removeChild(emptyMessage);
            }

            // Hiển thị container số lượng
            // linkCountContainer.style.display = '';
        }

        this._debug('Updated link count:', linkCount);
    };

    // Cập nhật trạng thái từ dữ liệu trả về của API
    NotificationClient.prototype._updateStatusFromResponse = function (dicStatus) {
        var self = this;

        // Kiểm tra xem có thông báo nào không
        if (!this.notifications || this.notifications.length === 0) {
            return;
        }

        // Đảm bảo rằng trạng thái chỉ chứa các thông báo hiện tại
        var newStatus = {};

        // dicStatus có thể là undefined nếu không được truyền vào
        dicStatus = dicStatus || {};

        // Cập nhật trạng thái từ dữ liệu trả về
        this.notifications.forEach(function (notification) {
            var id = notification.id;

            // Tạo hoặc cập nhật trạng thái
            if (self.status[id]) {
                // Giữ lại trạng thái cũ nếu đã có
                newStatus[id] = self.status[id];
            } else {
                // Tạo trạng thái mới nếu chưa có
                newStatus[id] = {
                    notificationId: id,
                    shownCount: 0,
                    lastShown: 0,
                    lastClick: 0,
                    remainingShows: 0
                };
            }

            // Cập nhật trạng thái từ dicStatus nếu có
            if (dicStatus[id]) {
                var status = dicStatus[id];

                // Cập nhật lastClick
                if (status.lastClick) {
                    newStatus[id].lastClick = status.lastClick;
                }

                // Cập nhật lastShown
                if (status.lastShown) {
                    newStatus[id].lastShown = status.lastShown;
                }

                // Cập nhật remainingShows
                if (status.remainingShows !== undefined) {
                    newStatus[id].remainingShows = status.remainingShows;
                    // Đồng bộ shownCount với remainingShows
                    newStatus[id].shownCount = status.remainingShows;
                }
                // Nếu có shownCount nhưng không có remainingShows
                else if (status.shownCount !== undefined) {
                    newStatus[id].shownCount = status.shownCount;
                    // Đồng bộ remainingShows với shownCount
                    newStatus[id].remainingShows = status.shownCount;
                }

                self._debug('Updated status from dicStatus for notification:', id,
                    'lastClick:', newStatus[id].lastClick,
                    'remainingShows:', newStatus[id].remainingShows);
            }
        });

        // Cập nhật trạng thái
        this.status = newStatus;
    };

    // Phương thức gọi API
    NotificationClient.prototype._fetchData = function (url, dataOrCallback, callback) {
        var xhr = new XMLHttpRequest();
        var method = 'GET';
        var data = null;
        var cb = callback;

        // Kiểm tra xem tham số thứ hai có phải là callback không
        if (typeof dataOrCallback === 'function') {
            cb = dataOrCallback;
        } else {
            data = dataOrCallback;
            method = 'POST';
        }

        // Kiểm tra xem callback có tồn tại không
        if (!cb) {
            cb = function () { }; // Hàm rỗng nếu không có callback
        }

        // Log URL để debug trong _debug

        xhr.open(method, url, true);
        xhr.setRequestHeader('Content-Type', 'application/json');

        xhr.onreadystatechange = function () {
            if (xhr.readyState === 4) {
                if (xhr.status === 200) {
                    try {
                        var responseData = JSON.parse(xhr.responseText);

                        cb(null, responseData);
                    } catch (e) {
                        console.error('JSON parse error:', e.message);
                        cb('Invalid JSON response: ' + e.message);
                    }
                } else {
                    console.error('API error:', xhr.status, xhr.responseText);
                    cb('Request failed. Status: ' + xhr.status);
                }
            }
        };

        xhr.onerror = function () {
            console.error('Network error');
            cb('Network error');
        };

        if (method === 'POST' && data) {
            xhr.send(JSON.stringify(data));
        } else {
            xhr.send();
        }
    };

    // Phương thức lưu dữ liệu vào localStorage
    NotificationClient.prototype._saveData = function () {
        try {
            const self = this;

            // Xử lý trước khi lưu để tránh quá tải
            // this._cleanupOldData();
            var data = {
                notifications: {
                    items: self.notifications,
                    status: self.status,
                    lastUpdated: self.lastUpdated
                }
            };
            // Nén dữ liệu bằng cách sử dụng tên fields ngắn gọn
            var compressedData = self._compressData(data);

            // Kiểm tra kích thước dữ liệu trước khi lưu
            var jsonData = JSON.stringify(compressedData);
            var dataSize = self._getStringSizeInKB(jsonData);

            // Hiển thị thông tin về kích thước trước và sau khi nén
            var originalData = JSON.stringify(data);
            var originalSize = self._getStringSizeInKB(originalData);
            self._debug('Data size before compression:', originalSize, 'KB, after compression:', dataSize, 'KB, saved:', Math.round((originalSize - dataSize) / originalSize * 100) + '%');
            self._debug('Data size to save:', dataSize, 'KB');

            // Lưu vào localStorage với xử lý lỗi QuotaExceededError
            try {
                localStorage.setItem(self.storageKey, jsonData);
                self._debug('Data saved to localStorage with key:', self.storageKey);
            } catch (storageError) {
                if (storageError.name === 'QuotaExceededError' ||
                    storageError.code === 22 || // Chrome
                    storageError.code === 1014) { // Firefox

                    self._logAlways('localStorage quota exceeded, performing emergency cleanup');

                    //Giữ lại 20 ghi mới nhất
                    self.notifications = self.notifications.slice(0, 20);
                    self._saveData();
                }
                else {
                    // Lỗi khác
                    self._debug('Error saving to localStorage:', storageError.message);
                }
            }
        } catch (e) {
            this._debug('Error preparing data for localStorage:', e.message);
        }
    };

    // Phương thức tải dữ liệu từ localStorage
    NotificationClient.prototype._loadDataFromStorage = function () {
        // Lấy dữ liệu từ các phương thức hỗ trợ
        this.notifications = this._getItems();
        this.status = this._getStatus();
        this.lastUpdated = this._getLastUpdated();

        if (this.notifications.length > 0) {
            this._debug('Loaded', this.notifications.length, 'notifications from localStorage');
            return true;
        }

        return false;
    };

    // Phương thức lọc thông báo phù hợp với trang hiện tại
    NotificationClient.prototype.getMatchingNotifications = function () {
        var self = this;
        var currentTime = Math.floor(Date.now() / 1000);
        var currentPage = this.currentPage;
        var matchingNotifications = [];

        // Kiểm tra xem có thông báo nào không
        if (!this.notifications || this.notifications.length === 0) {
            this._debug('No notifications available');
            return [];
        }

        this._debug('Filtering notifications for page:', currentPage);

        // Lọc thông báo phù hợp với trang hiện tại
        // Xử lý trường hợp PageShows hoặc PageExcludes là null hoặc undefined
        this.notifications.forEach(function (notification) {
            // Kiểm tra trạng thái hoạt động
            if (notification.status !== 1) { // 1 = Active
                self._debug('Notification', notification.id, 'is not active');
                return;
            }

            // Kiểm tra thời gian hiển thị
            if (notification.startDate > currentTime || notification.endDate < currentTime) {
                self._debug('Notification', notification.id, 'is outside time range');
                return;
            }

            // Kiểm tra trang hiển thị
            var showOnPage = self._shouldShowOnPage(notification, currentPage);
            if (!showOnPage) {
                self._debug('Notification', notification.id, 'should not show on this page');
                return;
            }

            // Kiểm tra số lần hiển thị và tần suất
            if (!self._checkShowFrequency(notification)) {
                self._debug('Notification', notification.id, 'frequency or max show limit reached');
                return;
            }

            // Kiểm tra xem thông báo đã được đánh dấu là "đã xem" chưa (Attribute 3)
            if (notification.attributes && notification.attributes.indexOf(3) !== -1) {
                var status = self.status[notification.id];
                if (status && status.lastClick && status.lastClick > 0) {
                    self._debug('Notification', notification.id, 'has been clicked/interacted with and will not be shown again');
                    return;
                }
            }


            // Thêm vào danh sách thông báo phù hợp
            matchingNotifications.push(notification);
        });

        // Sắp xếp theo thứ tự ưu tiên (giá trị order thấp hơn được hiển thị trước)
        matchingNotifications.sort(function (a, b) {
            return a.order - b.order;
        });

        this._debug('Found', matchingNotifications.length, 'matching notifications');

        return matchingNotifications;
    };

    // Kiểm tra xem thông báo có nên hiển thị trên trang hiện tại không
    NotificationClient.prototype._shouldShowOnPage = function (notification, currentPage) {
        this._debug('Checking if notification should show on page:', currentPage);
        this._debug('Notification ID:', notification.id);

        // Đảm bảo các trường pageShows và pageExcludes tồn tại
        var pageShows = notification.pageShows || [];
        var pageExcludes = notification.pageExcludes || [];

        this._debug('Page show patterns:', pageShows);
        this._debug('Page exclude patterns:', pageExcludes);

        // Kiểm tra danh sách trang loại trừ
        if (pageExcludes && pageExcludes.length > 0) {
            for (var i = 0; i < pageExcludes.length; i++) {
                var excludeConfig = pageExcludes[i];
                var isExcluded = this._matchPagePattern(excludeConfig, currentPage);
                this._debug('Checking exclude pattern:', excludeConfig, 'Result:', isExcluded);

                if (isExcluded) {
                    this._debug('Page is excluded, notification will not show');
                    return false;
                }
            }
        }

        // Kiểm tra danh sách trang hiển thị
        if (pageShows && pageShows.length > 0) {
            // Kiểm tra từng trang trong danh sách
            for (var j = 0; j < pageShows.length; j++) {
                var showConfig = pageShows[j];

                // Kiểm tra nếu có 'all' trong value
                if (showConfig.value === 'all') {
                    this._debug('Pattern "all" found, notification will show');
                    return true;
                }

                var shouldShow = this._matchPagePattern(showConfig, currentPage);
                this._debug('Checking show pattern:', showConfig, 'Result:', shouldShow);

                if (shouldShow) {
                    this._debug('Pattern matched, notification will show');
                    return true;
                }
            }

            // Nếu không có trang nào phù hợp, không hiển thị
            this._debug('No patterns matched, notification will not show');
            return false;
        }

        // Mặc định hiển thị nếu không có cấu hình trang
        // this._debug('No page patterns specified, notification will show by default');
        return false;
    };

    // Kiểm tra mẫu trang có khớp với trang hiện tại không
    NotificationClient.prototype._matchPagePattern = function (configLink, currentPage) {
        // Kiểm tra nếu configLink là chuỗi (cho tương thích ngược)
        if (typeof configLink === 'string') {
            this._debug('Legacy string pattern detected:', configLink);
            return this._matchLegacyPattern(configLink, currentPage);
        }

        // Đảm bảo configLink có cấu trúc đúng
        if (!configLink || typeof configLink !== 'object' || !('value' in configLink)) {
            this._debug('Invalid configLink object:', configLink);
            return false;
        }

        var pattern = configLink.value;
        var clt = configLink.clt || 0; // 0 = Path, 1 = Link (Path + Query)
        var cltg = configLink.cltg || 0; // 0 = Same, 1 = Regex

        this._debug('Matching configLink:', configLink, 'with current page:', currentPage);
        this._debug('Pattern value:', pattern, 'CLT:', clt, 'CLTG:', cltg);

        // Nếu là 'all', luôn khớp
        if (pattern === 'all') {
            this._debug('Pattern is "all", always matches');
            return true;
        }

        // Xử lý URL hiện tại dựa trên CLT (ConfigLinkType)
        var currentUrl = window.location.pathname;
        if (clt === 1) { // Link (Path + Query)
            currentUrl = window.location.pathname + window.location.search;
        }

        // Xử lý dựa trên CLTG (ConfigLinkTypeGet)
        if (cltg === 0) { // Same (so sánh chính xác)
            // Kiểm tra xem currentUrl có bằng chính xác với pattern không
            if (currentUrl === pattern) {
                this._debug('Exact match found');
                return true;
            }

            // Trường hợp đặc biệt cho các pattern như '/profile'
            // Phải chính xác là '/profile' hoặc bắt đầu bằng '/profile/'
            var exactMatch = currentUrl === pattern || currentUrl === pattern + '/';
            this._debug('Checking match - Exact or trailing slash:', exactMatch);
            return exactMatch;
        } else { // Regex (so sánh theo biểu thức chính quy)
            try {
                var regex = new RegExp(pattern);
                var regexResult = regex.test(currentUrl);
                this._debug('Regex pattern:', pattern, 'Result:', regexResult);
                return regexResult;
            } catch (error) {
                this._debug('Invalid regex pattern:', pattern, 'Error:', error.message);
                return false;
            }
        }
    };

    // Hàm xử lý mẫu cũ (chuỗi) cho tương thích ngược
    NotificationClient.prototype._matchLegacyPattern = function (pattern, currentPage) {
        this._debug('Matching legacy pattern:', pattern, 'with current page:', currentPage);

        // Nếu là 'all', luôn khớp
        if (pattern === 'all') {
            this._debug('Pattern is "all", always matches');
            return true;
        }

        // Nếu là URL trực tiếp, so sánh chuỗi
        if (pattern.indexOf('*') === -1 && pattern.indexOf('?') === -1) {
            // Kiểm tra xem currentPage có bằng chính xác với pattern không
            if (currentPage === pattern) {
                this._debug('Exact match found');
                return true;
            }

            // Trường hợp đặc biệt cho các pattern như '/profile'
            // Phải chính xác là '/profile' hoặc bắt đầu bằng '/profile/'
            var exactMatch = currentPage === pattern;
            var pathMatch = currentPage.startsWith(pattern + '/');
            this._debug('Checking special case - Exact match:', exactMatch, 'Path match:', pathMatch);
            return exactMatch || pathMatch;
        }

        // Nếu là mẫu, chuyển thành regex
        var regexPattern = pattern
            .replace(/\./g, '\\.')
            .replace(/\*/g, '.*')
            .replace(/\?/g, '.');

        var regex = new RegExp('^' + regexPattern + '$');
        var regexResult = regex.test(currentPage);
        this._debug('Regex pattern:', regexPattern, 'Result:', regexResult);
        return regexResult;
    };

    // Kiểm tra số lần hiển thị và tần suất
    NotificationClient.prototype._checkShowFrequency = function (notification) {
        var notificationId = notification.id;
        var status = this.status[notificationId];
        var currentTime = Math.floor(Date.now() / 1000);

        this._debug('Checking show frequency for notification:', notificationId);
        this._debug('Notification status:', status);
        this._debug('Notification maxShow:', notification.maxShow);
        this._debug('Notification frequency:', notification.frequency);
        this._debug('Notification listTime:', notification.listTime);

        // Nếu chưa có trạng thái, tạo mới
        if (!status) {
            this._debug('No status found, allowing display');
            return true;
        }

        // Kiểm tra số lần hiển thị (remainingShows) so với maxShow
        if (notification.maxShow > 0) {
            if (status.remainingShows >= notification.maxShow) {
                this._debug('Notification', notificationId, 'has reached max show limit:', notification.maxShow);
                return false;
            }
        }

        // Kiểm tra ListTime nếu có
        if (notification.listTime && notification.listTime.length > 0) {
            try {
                var lastShown = status.lastShown || 0;

                // Nếu lastShown = 0, tức là chưa hiển thị lần nào, hiển thị ngay lập tức
                if (lastShown === 0) {
                    this._debug('First time showing notification', notificationId, 'showing immediately');
                    return true;
                }

                // Đã hiển thị ít nhất 1 lần, áp dụng thời gian chờ
                var shownCount = status.remainingShows || 0;
                // Trừ 1 vì lần đầu tiên đã hiển thị ngay lập tức, các lần sau mới áp dụng thời gian chờ
                var timeIndex = Math.min(shownCount > 0 ? shownCount - 1 : 0, notification.listTime.length - 1);
                var timeInMinutes = notification.listTime[timeIndex];
                var timeInSeconds = timeInMinutes * 60; // Chuyển phút thành giây
                var timeSinceLastShown = currentTime - lastShown;

                this._debug('Using ListTime for notification', notificationId, 'at index', timeIndex, 'value:', timeInMinutes, 'minutes');

                if (timeSinceLastShown < timeInSeconds) {
                    this._debug('Notification', notificationId, 'ListTime limit not reached yet. Need to wait',
                        Math.round((timeInSeconds - timeSinceLastShown) / 60), 'more minutes');
                    return false;
                }
            }
            catch (e) {
                this._debug('Error parsing ListTime:', e.message);
                return false;
            }

        }
        // Nếu không có ListTime, sử dụng Frequency
        else if (notification.frequency > 0) {
            var lastShown = status.lastShown || 0;
            var frequencyInSeconds = notification.frequency * 3600; // Chuyển giờ thành giây
            var timeSinceLastShown = currentTime - lastShown;

            if (timeSinceLastShown < frequencyInSeconds) {
                this._debug('Notification', notificationId, 'frequency limit not reached yet');
                return false;
            }
        }

        return true;
    };

    // Phương thức hiển thị thông báo
    NotificationClient.prototype.showNotifications = function () {
        var self = this;

        var matchingNotifications = this.getMatchingNotifications();

        if (matchingNotifications.length === 0) {
            this._debug('No notifications to show');
            return;
        }

        // Phân loại thông báo theo kiểu hiển thị
        var popupNotifications = [];
        var modalNotifications = [];
        var otherNotifications = [];

        matchingNotifications.forEach(function (notification) {
            // Kiểm tra xem thông báo có kiểu Popup không
            if (notification.showTypes && notification.showTypes.indexOf(1) !== -1) {
                popupNotifications.push(notification);
            }
            // Kiểm tra xem thông báo có kiểu Modal không
            else if (notification.showTypes && notification.showTypes.indexOf(4) !== -1) {
                if (notification.infoMore) {
                    // Check if htmlDisplayLocation exists and the element exists in the DOM
                    if (!notification.infoMore.htmlDisplayLocation ||
                        document.querySelector(notification.infoMore.htmlDisplayLocation)) {
                        modalNotifications.push(notification);


                    }
                }
            } else {
                otherNotifications.push(notification);
            }
        });

        // Sắp xếp các thông báo theo thứ tự ưu tiên (order thấp hơn hiển thị trước)
        popupNotifications.sort(function (a, b) { return a.order - b.order; });
        modalNotifications.sort(function (a, b) { return a.order - b.order; });

        // Chỉ lấy thông báo đầu tiên cho popup và modal (theo thứ tự ưu tiên)
        var selectedPopup = popupNotifications.length > 0 ? [popupNotifications[0]] : [];
        var selectedModal = modalNotifications.length > 0 ? [modalNotifications[0]] : [];

        // Kiểm tra xem có hiển thị popup không (nếu có modal thì không hiển thị popup)
        var isShowPopupNotifications = selectedModal.length === 0;

        // Chỉ tải nội dung cho các thông báo sẽ được hiển thị
        var notificationsToDisplay = otherNotifications.slice();

        if (selectedModal.length > 0) {
            notificationsToDisplay = notificationsToDisplay.concat(selectedModal);
            this._debug('Selected 1 modal notification with highest priority (order: ' + selectedModal[0].order + ')');
        }

        if (isShowPopupNotifications && selectedPopup.length > 0) {
            notificationsToDisplay = notificationsToDisplay.concat(selectedPopup);
            this._debug('Selected 1 popup notification with highest priority (order: ' + selectedPopup[0].order + ')');
        }

        // Tải nội dung chỉ cho các thông báo sẽ hiển thị
        this._loadContentsForNotifications(notificationsToDisplay, function (updatedNotifications) {
            // Hiển thị tất cả các thông báo không phải popup và modal
            otherNotifications.forEach(function (notification) {
                self._renderNotification(notification);
            });

            // Hiển thị modal nếu có
            if (selectedModal.length > 0) {
                self._debug('Showing the selected modal notification');
                self._renderNotification(selectedModal[0]);
            }

            // Hiển thị popup nếu không có modal và có popup
            if (isShowPopupNotifications && selectedPopup.length > 0) {
                self._debug('Showing the selected popup notification');
                self._renderNotification(selectedPopup[0]);
            }

            setTimeout(function () {
                // Cập nhật số lượng thông báo loại link sau khi hiển thị
                self._updateLinkCount("2");
            }, 2000);
        });
    };

    // Tải nội dung cho nhiều thông báo cùng lúc
    //NotificationClient.prototype._loadContentsForNotifications = function (notifications, callback) {
    //    var self = this;
    //    var notificationsNeedingContent = [];

    //    // Lọc ra các thông báo cần tải nội dung
    //    notifications.forEach(function (notification) {
    //        if (notification.attributes && notification.attributes.indexOf(1) !== -1 && !notification.content) {
    //            notificationsNeedingContent.push(notification);
    //        }
    //    });

    //    // Nếu không có thông báo nào cần tải nội dung, gọi callback ngay
    //    if (notificationsNeedingContent.length === 0) {
    //        if (callback) callback(notifications);
    //        return;
    //    }

    //    // Tạo danh sách ID cần tải
    //    var ids = notificationsNeedingContent.map(function (notification) {
    //        return notification.id;
    //    });

    //    // Gọi API để lấy nội dung cho tất cả các thông báo cần tải
    //    this._fetchData(this.config.apiUrl + '/by-ids?strIds=' + ids.join(','), function (error, data) {
    //        if (!error && data && data.length > 0) {
    //            // Tạo map từ ID đến nội dung
    //            var contentMap = {};
    //            data.forEach(function (item) {
    //                contentMap[item.id] = item.content;
    //            });

    //            // Cập nhật nội dung cho các thông báo
    //            notificationsNeedingContent.forEach(function (notification) {
    //                if (contentMap[notification.id]) {
    //                    notification.content = contentMap[notification.id];
    //                }
    //            });
    //        }

    //        // Gọi callback với danh sách thông báo đã cập nhật
    //        if (callback) callback(notifications);
    //    });
    //};

    NotificationClient.prototype._fetchContentsByIds = function (ids, callback) {
        var self = this;
        this._fetchData(this.config.apiUrl + '/by-ids?strIds=' + ids.join(','), function (error, data) {
            if (!error && data && data.length > 0) {
                var contentMap = {};
                data.forEach(function (item) {
                    contentMap[item.id] = item.content;
                });
                callback(null, contentMap);
            } else {
                callback(error || new Error('No data received'), null);
            }
        });
    };

    NotificationClient.prototype._loadContentsForNotifications = function (notifications, callback) {
        var self = this;
        var notificationsNeedingContent = [];

        // Lọc ra các thông báo cần tải nội dung
        notifications.forEach(function (notification) {
            if (notification.attributes && notification.attributes.indexOf(1) !== -1 && !notification.content) {
                notificationsNeedingContent.push(notification);
            }
        });

        // Nếu không có thông báo nào cần tải nội dung, gọi callback ngay
        if (notificationsNeedingContent.length === 0) {
            if (callback) callback(notifications);
            return;
        }

        // Tạo danh sách ID cần tải
        var ids = notificationsNeedingContent.map(function (notification) {
            return notification.id;
        });

        // Gọi API để lấy nội dung
        this._fetchContentsByIds(ids, function (error, contentMap) {
            if (!error && contentMap) {
                // Cập nhật nội dung cho các thông báo
                notificationsNeedingContent.forEach(function (notification) {
                    if (contentMap[notification.id]) {
                        notification.content = contentMap[notification.id];
                    }
                });
            }

            // Gọi callback với danh sách thông báo đã cập nhật
            if (callback) callback(notifications);
        });
    };


    // Hiển thị một thông báo
    NotificationClient.prototype._showNotification = function (notification) {
        var self = this;

        // Kiểm tra nếu cần tải nội dung
        if (notification.attributes && notification.attributes.indexOf(1) !== -1 && !notification.content) {
            // Sử dụng phương thức tải hàng loạt, nhưng chỉ với một thông báo
            this._loadContentsForNotifications([notification], function (updatedNotifications) {
                self._renderNotification(updatedNotifications[0]);
            });
        } else {
            // Nếu đã có nội dung, hiển thị ngay
            this._renderNotification(notification);
        }
    };

    // Render thông báo theo kiểu hiển thị
    NotificationClient.prototype._renderNotification = function (notification) {
        var self = this;

        // Kiểm tra các kiểu hiển thị
        if (!notification.showTypes || notification.showTypes.length === 0) {
            this._debug('Notification', notification.id, 'has no show type');
            return;
        }

        // Lấy thời gian trễ hiển thị (nếu có)
        var delay = 0;
        if (notification.infoMore && notification.infoMore.delay && notification.infoMore.delay > 0) {
            delay = notification.infoMore.delay * 1000; // Chuyển từ giây sang mili giây
            this._debug('Notification', notification.id, 'will be displayed after', delay, 'ms delay');
        }

        // Hiển thị thông báo sau khoảng thời gian trễ
        setTimeout(function () {
            // Hiển thị theo từng kiểu
            notification.showTypes.forEach(function (type) {
                switch (type) {
                    case 1: // Popup
                        self._showPopup(notification);
                        break;
                    case 2: // HTML
                        self._showHtml(notification);
                        break;
                    case 3: // Link
                        self._showLink(notification);
                        break;
                    case 4: // Modal
                        self._showModal(notification);
                        break;
                    default:
                        self._debug('Unknown show type:', type);
                }
            });
        }, delay);
    };

    // Hiển thị thông báo dạng Popup
    NotificationClient.prototype._showPopup = function (notification) {
        var self = this;
        var notificationId = notification.id;


        // Kiểm tra xem người dùng đã tương tác với thông báo này chưa
        if (this._hasUserInteractedWithNotification(notificationId)) {

            this._debug('User has already interacted with notification, skipping popup:', notificationId);
            return;
        }

        // Kiểm tra Attribute 3 (Không làm phiền người dùng) - chỉ để log
        if (notification.attributes && notification.attributes.indexOf(3) !== -1) {
            this._debug('Notification has Attribute 3 (Do not disturb user)');
        }

        // Tạo phần tử popup
        var popupId = 'notification-popup-' + notificationId;

        // Kiểm tra xem popup đã tồn tại chưa
        if (document.getElementById(popupId)) {
            this._debug('Popup already exists:', popupId);
            return;
        }
        var popup = document.createElement('div');
        popup.id = popupId;
        popup.className = 'notification-popup';

        popup.setAttribute("data-notification-id", notificationId);
        popup.classList.add("user-" + this.config.userId);
        popup.classList.add("nof-" + notificationId);
        // Tạo nội dung popup
        var content = document.createElement('div');
        content.className = 'notification-content';

        // Thêm tiêu đề
        // if (notification.title) {
        //     var title = document.createElement('h3');
        //     title.className = 'notification-title';
        //     title.textContent = notification.title;
        //     content.appendChild(title);
        // }

        // Thêm nội dung
        var body = document.createElement('div');
        body.className = 'notification-body';
        body.innerHTML = notification.content;
        content.appendChild(body);

        // Thêm class tracking vào tất cả các link và button trong nội dung


        // Thêm nút đóng nếu cần
        if (notification.infoMore && notification.infoMore.popupDismissable) {
            var closeButton = document.createElement('button');
            closeButton.className = 'notification-close';
            closeButton.textContent = '×';
            closeButton.addEventListener('click', function () {
                // Xóa popup
                var popupElement = document.getElementById(popupId);
                if (popupElement) {
                    // Thêm class hide để tạo hiệu ứng ẩn
                    popupElement.classList.add('hide');

                    // Kiểm tra Attribute 3 (Không làm phiền người dùng)
                    // if (notification.attributes && notification.attributes.indexOf(3) !== -1) {
                    //     // Đánh dấu thông báo là "đã xem" và gửi về server
                    //     self._markNotificationAsSeen(notificationId);
                    // }

                    // Xóa sau khi hoàn tất hiệu ứng
                    setTimeout(function () {
                        if (popupElement.parentNode) {
                            popupElement.parentNode.removeChild(popupElement);
                        }
                        // Hiển thị popup tiếp theo nếu có
                        // self._showNextPendingPopup();
                    }, 300);
                }
            });
            popup.appendChild(closeButton);
        }

        // Thêm nội dung vào popup
        popup.appendChild(content);
        this._addTrackingClassesToContent(popup, notificationId);
        // Thêm sự kiện click cho các phần tử có class 'notification-track-click'
        var trackableElements = popup.querySelectorAll(this.config.trackingLink || '.notification-track-click');
        if (trackableElements.length > 0) {
            self._debug('Found', trackableElements.length, 'trackable elements in popup:', notificationId);
            trackableElements.forEach(function (element) {
                element.addEventListener('click', function () {
                    try {
                        // Đóng popup
                        var popupElement = document.getElementById(popupId);
                        if (popupElement) {
                            popupElement.classList.add('hide');
                        }
                    } catch (error) {
                        self._debug('Error hiding modal:', error.message);
                    }
                    // Kiểm tra Attribute 3 (Không làm phiền người dùng)
                    if (notification.attributes && notification.attributes.indexOf(3) !== -1) {
                        self._markNotificationAsSeen(notificationId);
                        self._debug('Notification marked as seen on trackable element click in popup:', notificationId);
                    }


                });

            });
        }

        // Thêm popup vào trang
        document.body.appendChild(popup);

        // Hiển thị popup sau 500ms và cập nhật trạng thái
        setTimeout(function () {
            popup.classList.add('show');

            // Chỉ cập nhật trạng thái khi popup đã hiển thị
            self._updateNotificationStatus(notificationId);
            self._debug('Popup displayed and status updated for:', notificationId);
        }, 500);
    };


    // Key lưu riêng triggerActions theo user (độc lập với this.storageKey)
    NotificationClient.prototype._getTriggerActionsStorageKey = function () {
        try {
            var prefix = (this.config && this.config.storageKeyPrefix) ? this.config.storageKeyPrefix : 'notification_data';
            var userId = (this.config && this.config.userId) ? this.config.userId : 'anonymous';
            return prefix + '_' + userId + '_triggerActions';
        } catch (e) {
            // fallback cực an toàn
            return 'notification_data_anonymous_triggerActions';
        }
    };

    // Lấy triggerActions đã lưu từ localStorage
    NotificationClient.prototype._getStoredTriggerActions = function () {
        try {
            var key = this._getTriggerActionsStorageKey();

            // localStorage có thể bị block (Safari private, policy, etc.)
            if (!window.localStorage) return [];

            var raw = localStorage.getItem(key);
            if (!raw) return [];

            var parsed = JSON.parse(raw);

            // Chỉ nhận array
            if (!Array.isArray(parsed)) return [];

            // đảm bảo item là string, tránh object lạ
            return parsed
                .filter(function (x) { return x !== null && x !== undefined; })
                .map(function (x) { return String(x); });
        } catch (e) {
            try { this._debug('Error reading stored triggerActions:', e && e.message ? e.message : e); } catch (_) { }
            return [];
        }
    };

    // Lưu triggerActions hiện tại vào localStorage
    NotificationClient.prototype._saveTriggerActions = function () {
        try {
            var key = this._getTriggerActionsStorageKey();
            if (!window.localStorage) return this.config.triggerActions || [];

            var actions = (this.config && this.config.triggerActions) ? this.config.triggerActions : [];

            // Chuẩn hóa: string + trim + bỏ rỗng + unique + sort
            var normalized = actions
                .filter(function (x) { return x !== null && x !== undefined; })
                .map(function (x) { return String(x).trim(); })
                .filter(function (x) { return x.length > 0; });

            // unique
            var seen = {};
            normalized = normalized.filter(function (x) {
                if (seen[x]) return false;
                seen[x] = true;
                return true;
            });

            normalized.sort();

            localStorage.setItem(key, JSON.stringify(normalized));

            // sync ngược lại config để dữ liệu nhất quán
            if (this.config) this.config.triggerActions = normalized;

            try { this._debug('Saved triggerActions to localStorage:', key, normalized); } catch (_) { }

            return normalized;
        } catch (e) {
            try { this._debug('Error saving triggerActions:', e && e.message ? e.message : e); } catch (_) { }
            return (this.config && this.config.triggerActions) ? this.config.triggerActions : [];
        }
    };

    // ✅ Check triggerActions có thay đổi so với localStorage không
    // options:
    // - autoUpdate: true => nếu khác thì tự lưu lại localStorage luôn (default true)
    // - ignoreOrder: true => coi đổi thứ tự là không đổi (default true)
    NotificationClient.prototype._isTriggerActionsChanged = function (options) {
        try {
            options = options || {};
            var autoUpdate = options.autoUpdate !== false;   // default true
            var ignoreOrder = options.ignoreOrder !== false; // default true

            var current = (this.config && this.config.triggerActions) ? this.config.triggerActions : [];
            var stored = this._getStoredTriggerActions();

            function normalize(arr) {
                try {
                    arr = (arr || [])
                        .filter(function (x) { return x !== null && x !== undefined; })
                        .map(function (x) { return String(x).trim(); })
                        .filter(function (x) { return x.length > 0; });

                    var seen = {};
                    arr = arr.filter(function (x) {
                        if (seen[x]) return false;
                        seen[x] = true;
                        return true;
                    });

                    if (ignoreOrder) arr.sort();
                    return arr;
                } catch (e) {
                    // nếu normalize lỗi, trả về array rỗng để không crash
                    return [];
                }
            }

            var nCur = normalize(current);
            var nSto = normalize(stored);

            var changed = JSON.stringify(nCur) !== JSON.stringify(nSto);

            if (changed && autoUpdate) {
                // đảm bảo config đồng bộ theo normalized
                if (this.config) this.config.triggerActions = nCur;

                // lưu lại localStorage
                this._saveTriggerActions();
            }

            return changed;
        } catch (e) {
            try { this._debug('Error checking triggerActions change:', e && e.message ? e.message : e); } catch (_) { }
            // nếu có lỗi thì coi như "không thay đổi" để tránh side-effect
            return false;
        }
    };




    // Trạng thái hiển thị modal
    NotificationClient.prototype.isModalShowing = false;

    // Kiểm tra xem modal có đang hiển thị không
    NotificationClient.prototype._isAnyModalVisible = function () {
        // Kiểm tra trạng thái của biến isModalShowing
        if (this.isModalShowing) {
            return true;
        }

        // Kiểm tra trực tiếp trên DOM
        if (document.body.classList.contains('modal-open')) {
            return true;
        }

        // Kiểm tra các modal có class 'show'
        var visibleModals = document.querySelectorAll('.modal.show');
        if (visibleModals.length > 0) {
            return true;
        }

        // Kiểm tra các modal có style display='block'
        var modals = document.querySelectorAll('.modal');
        for (var i = 0; i < modals.length; i++) {
            if (modals[i].style.display === 'block') {
                return true;
            }
        }

        return false;
    };

    NotificationClient.prototype._showModal = function (notification) {
        var self = this;
        var notificationId = notification.id;

        // Kiểm tra xem người dùng đã tương tác với thông báo này chưa
        if (this._hasUserInteractedWithNotification(notificationId)) {
            this._debug('User has already interacted with notification, skipping modal:', notificationId);
            return;
        }

        // Xác định modalSelector
        var modalSelector;
        let isProcessNoiDung = false;
        if (!notification.infoMore || !notification.infoMore.htmlDisplayLocation) {
            // Nếu không có htmlDisplayLocation và useDefaultModal = true

            this._debug('Using default modal for notification:', notificationId);

            // Sử dụng selector mặc định
            modalSelector = '#' + this.config.defaultModalId;
            // Chèn nội dung vào body
            var modalHtml = notification.content;
            document.body.insertAdjacentHTML('beforeend', modalHtml);



            this._debug('Added content to body');

        } else {
            modalSelector = notification.infoMore.htmlDisplayLocation;
            isProcessNoiDung = true;
        }

        // Kiểm tra xem đã có modal nào đang hiển thị không
        if (this._isAnyModalVisible()) {
            this._debug('A modal is already showing, skipping:', notificationId);
            return;
        }

        // Đánh dấu đang hiển thị modal
        this.isModalShowing = true;

        // Tìm phần tử modal dựa vào selector
        var modalElement = document.querySelector(modalSelector);

        if (!modalElement) {
            this._debug('Modal element not found with selector:', modalSelector);
            this.isModalShowing = false; // Reset trạng thái
            return;
        }
        if (isProcessNoiDung) {
            // Tìm phần tử nội dung trong modal
            var contentSelector = '.modal-body';
            var contentElement = modalElement.querySelector(contentSelector);

            if (!contentElement) {
                this._debug('Modal content element not found with selector:', contentSelector);
                this.isModalShowing = false; // Reset trạng thái
                return;
            }

            // Tạo ID duy nhất cho nội dung thông báo
            var modalContentId = 'notification-modal-content-' + notificationId;
            // Tạo phần tử chứa nội dung
            var container = document.createElement('div');
            container.id = modalContentId;
            container.className = 'notification-modal-content';

            // Thêm tiêu đề nếu có
            if (notification.title) {
                var titleSelector = '.modal-title';
                var titleElement = modalElement.querySelector(titleSelector);

                if (titleElement) {
                    titleElement.textContent = notification.title;
                }
            }

            // Thêm nội dung
            var body = document.createElement('div');
            body.className = 'notification-body';
            body.innerHTML = notification.content;
            container.appendChild(body);

            // Xóa nội dung cũ trong phần tử nội dung modal (nếu có)
            while (contentElement.firstChild) {
                contentElement.removeChild(contentElement.firstChild);
            }
            contentElement.appendChild(container);
        }



        // Áp dụng các thuộc tính và class cho modal
        if (notification.infoMore && !notification.infoMore.popupDismissable) {
            modalElement.setAttribute("data-bs-backdrop", "static");
            modalElement.setAttribute("data-backdrop", "static"); // Dành cho lib cũ nếu cần
            modalElement.classList.add("notification-no-dismiss-modal");
        } else {
            modalElement.removeAttribute("data-bs-backdrop");
            modalElement.removeAttribute("data-backdrop");
            modalElement.classList.remove("notification-no-dismiss-modal");
        }

        // Thêm các thuộc tính và class tracking
        modalElement.setAttribute("data-notification-id", notificationId);
        modalElement.classList.add("user-" + this.config.userId);
        modalElement.classList.add("nof-" + notificationId);



        this._addTrackingClassesToContent(modalElement, notificationId);
        // Thêm sự kiện click cho các phần tử có class 'notification-track-click'
        var trackableElements = modalElement.querySelectorAll(this.config.trackingLink || '.notification-track-click');
        if (trackableElements.length > 0) {
            self._debug('Found', trackableElements.length, 'trackable elements in modal:', notificationId);
            trackableElements.forEach(function (element) {
                element.addEventListener('click', function () {
                    if (notification.attributes && notification.attributes.indexOf(3) !== -1) {
                        self._markNotificationAsSeen(notificationId);
                        self._debug('Notification marked as seen on trackable element click in modal:', notificationId);
                    }
                    // try {
                    //     if (window.bootstrap && typeof bootstrap.Modal !== 'undefined') {
                    //         modalHandler(modalElement, "hide");

                    //         //var bootstrapModal = $(modalElement).data('bs.modal') || new bootstrap.Modal(modalElement);
                    //         //bootstrapModal.hide();
                    //     }
                    // } catch (error) {
                    //     self._debug('Error hiding modal:', error.message);
                    // }
                });
            });
        }
        // Hiển thị modal sử dụng Bootstrap
        try {
            if (window.bootstrap && typeof bootstrap.Modal !== 'undefined') {
                // Khởi tạo và hiển thị Bootstrap Modal
                //var bootstrapModal = $(modalElement).data('bs.modal') || new bootstrap.Modal(modalElement);
                //bootstrapModal.show();
                modalHandler(modalElement, "show");
                // Đảm bảo modal được dispose sau khi ẩn để tránh memory leak
                //modalElement.addEventListener('hidden.bs.modal', function handler() {
                //   /* bootstrapModal.dispose();*/
                //    modalElement.removeEventListener('hidden.bs.modal', handler);

                //    // Reset trạng thái và hiển thị modal tiếp theo nếu có
                //    self.isModalShowing = false;
                //    self._showNextPendingModal();
                //});
            } else {
                this._debug('Bootstrap Modal is not available');
                this.isModalShowing = false; // Reset trạng thái
                /*this._showNextPendingModal(); // Thử hiển thị modal tiếp theo*/
                return;
            }

            setTimeout(function () {
                // Cập nhật trạng thái thông báo
                self._updateNotificationStatus(notificationId);
                self._debug('Modal displayed and status updated for:', notificationId);
            }, 500);
        } catch (error) {
            this._debug('Error showing modal:', error.message);
            this.isModalShowing = false; // Reset trạng thái
        }
    };

    // Các phương thức xử lý hàng đợi đã bị loại bỏ để đơn giản hóa logic

    // Hiển thị thông báo dạng HTML
    NotificationClient.prototype._showHtml = function (notification) {
        var self = this;
        var notificationId = notification.id;

        // Kiểm tra vị trí hiển thị HTML
        if (!notification.infoMore || !notification.infoMore.htmlDisplayLocation) {
            this._debug('Notification', notificationId, 'has no HTML display location');
            return;
        }

        // Tìm phần tử để chèn HTML
        var container = document.querySelector(notification.infoMore.htmlDisplayLocation);
        if (!container) {
            this._debug('HTML container not found:', notification.infoMore.htmlDisplayLocation);
            return;
        }

        // Kiểm tra xem HTML đã tồn tại chưa
        var htmlId = 'notification-html-' + notificationId;
        if (document.getElementById(htmlId)) {
            this._debug('HTML notification already exists:', htmlId);
            return;
        }

        // Tạo phần tử HTML
        var htmlElement = document.createElement('div');
        htmlElement.className = 'notification-html';
        htmlElement.id = htmlId;


        // Thêm nội dung HTML

        htmlElement.setAttribute("data-notification-id", notificationId);
        htmlElement.classList.add("user-" + this.config.userId);
        htmlElement.classList.add("nof-" + notificationId);
        // Thêm tiêu đề nếu có
        // if (notification.title) {
        //     var title = document.createElement('h3');
        //     title.className = 'notification-title';
        //     title.textContent = notification.title;
        //     htmlElement.appendChild(title);
        // }

        // Thêm nội dung
        var content = document.createElement('div');
        content.className = 'notification-content';
        content.innerHTML = notification.content;
        htmlElement.appendChild(content);

        // Thêm class tracking vào tất cả các link và button trong nội dung
        this._addTrackingClassesToContent(content, notificationId);

        // Thêm sự kiện click cho bất kỳ phần tử nào có class 'notification-track-click' trong nội dung HTML
        // Có thể là thẻ a, button hoặc bất kỳ phần tử nào khác
        var trackableElements = content.querySelectorAll(this.config.trackingLink || '.notification-track-click');
        if (trackableElements.length > 0) {
            self._debug('Found', trackableElements.length, 'trackable elements in notification:', notificationId);
            trackableElements.forEach(function (element) {
                element.addEventListener('click', function () {
                    // Kiểm tra Attribute 3 (Không làm phiền người dùng)
                    if (notification.attributes && notification.attributes.indexOf(3) !== -1) {
                        // Đánh dấu thông báo là "đã xem" và gửi về server
                        self._markNotificationAsSeen(notificationId);
                        self._debug('Notification marked as seen on trackable element click:', notificationId);
                    }
                });
            });
        } else {
            self._debug('No trackable elements found in notification:', notificationId);
        }

        // Chèn vào container
        container.appendChild(htmlElement);

        // Sử dụng Intersection Observer để kiểm tra khi HTML hiển thị trong viewport
        if ('IntersectionObserver' in window) {
            var observer = new IntersectionObserver(function (entries) {
                entries.forEach(function (entry) {
                    if (entry.isIntersecting) {
                        // HTML đã hiển thị trong viewport, cập nhật trạng thái
                        self._updateNotificationStatus(notificationId);
                        self._debug('HTML notification visible and status updated for:', notificationId);

                        // Ngừng theo dõi sau khi đã hiển thị
                        observer.disconnect();
                    }
                });
            }, { threshold: 0.5 }); // Hiển thị ít nhất 50% mới tính

            observer.observe(htmlElement);
        } else {
            // Fallback cho trình duyệt không hỗ trợ Intersection Observer
            self._updateNotificationStatus(notificationId);
        }
    };

    // Hiển thị thông báo dạng Link
    NotificationClient.prototype._showLink = function (notification) {
        var self = this;
        var notificationId = notification.id;

        // Sử dụng content làm URL
        var url = notification.content;

        // Tạo ID cho link
        var linkId = 'notification-link-' + notificationId;

        // Kiểm tra xem link đã tồn tại chưa
        if (document.getElementById(linkId)) {
            this._debug('Link notification already exists:', linkId);
            return;
        }

        // Kiểm tra xem có phải là direct link không
        if (notification.infoMore && notification.infoMore.isDirectLink) {
            // Tạo một phần tử ẩn chứa link
            var hiddenContainer = document.createElement('div');
            hiddenContainer.id = linkId;
            hiddenContainer.style.display = 'none';
            document.body.appendChild(hiddenContainer);

            // Cập nhật trạng thái khi link được hiển thị
            self._updateNotificationStatus(notificationId);

            // Chuyển hướng ngay
            window.location.href = url;
            return;
        }

        // Nếu không phải direct link, hiển thị trong container
        var containerSelector = this.config.containerLink;
        var container = document.querySelector(containerSelector);

        if (!container) {
            this._debug('Link container not found:', containerSelector);
            this._debug('Creating container for links');

            return;

        }

        // Tạo phần tử link
        var linkElement = document.createElement('a');
        linkElement.id = linkId;
        linkElement.className = 'notification-link';
        linkElement.href = url;
        linkElement.textContent = notification.title || 'Thông báo';
        linkElement.target = '_blank'; // Mở trong tab mới

        // Thêm class tracking
        linkElement.classList.add("user-" + this.config.userId);
        linkElement.classList.add("nof-" + notificationId);

        let classTrackingLink = "notification-track-click";
        if (this.config.trackingLink) {
            classTrackingLink = this.config.trackingLink.replace('.', '');
        }

        if (notification.attributes && notification.attributes.indexOf(3) !== -1) {
            linkElement.classList.add(classTrackingLink);
        }


        // Thêm sự kiện click để kiểm tra Attribute 3 và đánh dấu đã xem
        linkElement.addEventListener('click', function () {
            // Kiểm tra Attribute 3 (Không làm phiền người dùng)
            if (linkElement.classList.contains(classTrackingLink)) {
                // Đánh dấu thông báo là "đã xem" và gửi về server
                self._markNotificationAsSeen(notificationId);
                self._debug('Notification marked as seen on link click:', notificationId);
            }
        });

        // Thêm vào container
        container.appendChild(linkElement);

        // Sử dụng Intersection Observer để kiểm tra khi link hiển thị trong viewport
        if ('IntersectionObserver' in window) {
            var observer = new IntersectionObserver(function (entries) {
                entries.forEach(function (entry) {
                    if (entry.isIntersecting) {
                        // Link đã hiển thị trong viewport, cập nhật trạng thái
                        self._updateNotificationStatus(notificationId);
                        self._debug('Link notification visible and status updated for:', notificationId);

                        // Ngừng theo dõi sau khi đã hiển thị
                        observer.disconnect();
                    }
                });
            }, { threshold: 0.5 }); // Hiển thị ít nhất 50% mới tính

            observer.observe(linkElement);
        } else {
            // Fallback cho trình duyệt không hỗ trợ Intersection Observer
            self._updateNotificationStatus(notificationId);
        }
    };

    // Cập nhật trạng thái hiển thị của thông báo
    NotificationClient.prototype._updateNotificationStatus = function (notificationId) {
        var currentTime = Math.floor(Date.now() / 1000);
        var notification = this._getNotificationById(notificationId);

        if (!notification) {
            this._debug('Notification not found:', notificationId);
            return;
        }

        // Lấy hoặc tạo trạng thái mới
        if (!this.status[notificationId]) {
            this.status[notificationId] = {
                notificationId: notificationId,
                shownCount: 0,
                lastShown: 0,
                lastClick: 0,
                remainingShows: 0
            };
        }

        // Cập nhật trạng thái
        this.status[notificationId].lastShown = currentTime;

        // Tăng số lần hiển thị
        this.status[notificationId].remainingShows++;
        // Đồng bộ shownCount với remainingShows
        this.status[notificationId].shownCount = this.status[notificationId].remainingShows;

        this._debug('Increased show count for notification:', notificationId,
            'New value:', this.status[notificationId].remainingShows);

        // Lưu trạng thái vào localStorage
        this._saveData();

        // Kiểm tra xem có cần cập nhật trạng thái về server không (Attribute 2)
        if (notification.attributes && notification.attributes.indexOf(2) !== -1) {
            this._debug('Sending status update to server for notification:', notificationId);
            this._sendStatusToServer(notificationId);
        }
    };

    // Tìm thông báo theo ID
    NotificationClient.prototype._getNotificationById = function (notificationId) {
        for (var i = 0; i < this.notifications.length; i++) {
            if (this.notifications[i].id === notificationId) {
                return this.notifications[i];
            }
        }
        return null;
    };

    // Gửi trạng thái hiển thị về server
    NotificationClient.prototype._sendStatusToServer = function (notificationId) {
        var self = this;
        var status = this.status[notificationId];

        if (!status) {
            this._debug('No status to send for notification:', notificationId);
            return;
        }

        // Tạo URL API
        var apiUrl = this.config.apiUrl + '/update-status';

        // Đảm bảo remainingShows và shownCount đồng bộ trước khi gửi
        if (status.remainingShows !== undefined && status.shownCount !== status.remainingShows) {
            status.shownCount = status.remainingShows;
        }

        // Tạo dữ liệu gửi đi
        var data = {
            notificationId: notificationId,
            userId: this.config.userId,
            domain: this.config.domain,
            device: this.config.device,
            remainingShows: 1 // Đảm bảo remainingShows = shownCount
        };
        // Gửi dữ liệu lên server sử dụng phương thức _fetchData
        this._fetchData(apiUrl, data, function (error, response) {
            if (error) {
                self._debug('Failed to send status update for notification:', notificationId, 'Error:', error);
            } else {
                self._debug('Status update sent successfully for notification:', notificationId);
            }
        });
    };

    // Đánh dấu thông báo là "đã xem" và gửi về server
    NotificationClient.prototype._markNotificationAsSeen = function (notificationId) {
        var self = this;
        var notification = this._getNotificationById(notificationId);

        if (!notification) {
            this._debug('Notification not found:', notificationId);
            return;
        }

        this._debug('Marking notification as seen:', notificationId);


        // Cập nhật trạng thái trong localStorage
        var currentTime = Math.floor(Date.now() / 1000);
        if (!this.status[notificationId]) {
            this.status[notificationId] = {
                notificationId: notificationId,
                shownCount: 1,
                lastShown: currentTime,
                lastClick: currentTime,
                remainingShows: 1, // Đồng bộ với shownCount
                seen: true
            };
        } else {
            this.status[notificationId].lastClick = currentTime;
            this.status[notificationId].seen = true;

            // Đảm bảo remainingShows và shownCount đồng bộ
            if (this.status[notificationId].remainingShows === undefined) {
                this.status[notificationId].remainingShows = this.status[notificationId].shownCount || 1;
            } else if (this.status[notificationId].shownCount !== undefined &&
                this.status[notificationId].shownCount !== this.status[notificationId].remainingShows) {
                this.status[notificationId].shownCount = this.status[notificationId].remainingShows;
            }
        }

        // Lưu trạng thái vào localStorage
        this._saveData();

        // Kiểm tra xem có cần gửi trạng thái về server không (Attribute 2)
        var notification = this._getNotificationById(notificationId);
        if (notification && notification.attributes && notification.attributes.indexOf(2) !== -1) {
            // Gửi trạng thái về server
            var apiUrl = this.config.apiUrl + '/mark-as-seen';

            // Tạo dữ liệu gửi đi
            var data = {
                notificationId: notificationId,
                userId: this.config.userId,
                domain: this.config.domain,
                device: this.config.device,
                lastClick: currentTime
            };

            // Gửi dữ liệu lên server sử dụng phương thức _fetchData
            this._fetchData(apiUrl, data, function (error, response) {
                if (error) {
                    self._debug('Failed to mark notification as seen:', notificationId, 'Error:', error);
                } else {
                    self._debug('Notification marked as seen successfully and sent to server:', notificationId);
                }
            });
        } else {
            self._debug('Notification marked as seen locally only (no Attribute 2):', notificationId);
        }
    };

    // Kiểm tra xem người dùng đã tương tác với thông báo chưa
    NotificationClient.prototype._hasUserInteractedWithNotification = function (notificationId) {
        // Kiểm tra xem thông báo đã được đánh dấu là đã xem chưa
        if (this.status[notificationId] && this.status[notificationId].lastClick) {
            this._debug('User has already interacted with notification:', notificationId);
            this._debug('Last click time:', new Date(this.status[notificationId].lastClick * 1000).toLocaleString());
            return true;
        }

        return false;
    };





    // Phương thức khởi tạo và chạy thông báo
    NotificationClient.prototype.run = function () {
        var self = this;

        // Kiểm tra xem có dữ liệu trong localStorage không
        var hasStoredData = this._loadDataFromStorage();

        // Luôn gọi API để kiểm tra có thông báo mới không
        this._debug('Loading notifications from API');

        setTimeout(function () {
            self.loadNotifications(function (error) {
                if (!error) {
                    self.showNotifications();
                } else if (hasStoredData) {
                    // Nếu có lỗi khi gọi API nhưng có dữ liệu đã lưu, vẫn hiển thị thông báo
                    self._debug('Using cached notifications due to API error');
                    self.showNotifications();
                } else {
                    self._updateLinkCount("3");
                }
            });

        }, this.config.timeOut);


        // Thiết lập kiểm tra định kỳ
        // if (this.config.checkInterval > 0) {
        //     setInterval(function() {
        //         self.loadNotifications(function(error) {
        //             if (!error) {
        //                 self.showNotifications();
        //             }
        //         });
        //     }, this.config.checkInterval);
        // }
    };

    // Tạo CSS cho thông báo
    NotificationClient.prototype._createStyles = function () {
        if (document.getElementById('notification-client-styles')) {
            return;
        }

        // Tạo CSS inline để đảm bảo popup hiển thị được
        var style = document.createElement('style');
        style.id = 'notification-client-styles';
        style.textContent = `
            .notification-popup {
                position: fixed;
                bottom: 20px;
                right: 20px;
                width: 300px;
                background-color: white;
                border-radius: 5px;
                box-shadow: 0 2px 10px rgba(0, 0, 0, 0.2);
                padding: 15px;
                z-index: 9999;
                opacity: 0;
                transform: translateY(20px);
                transition: opacity 0.3s, transform 0.3s;
            }
            .notification-popup.show {
                opacity: 1;
                transform: translateY(0);
            }
            .notification-popup.hide {
                opacity: 0;
                transform: translateY(20px);
            }
            .notification-content{
                padding-top: 15px;
            }

            .notification-title {
                margin-top: 0;
                margin-bottom: 10px;
                font-size: 18px;
                font-weight: bold;
            }
            .notification-body {
                margin-bottom: 10px;
            }
            .notification-close {
                position: absolute;
                top: 5px;
                right: 10px;
                background: none;
                border: none;
                font-size: 20px;
                cursor: pointer;
                color: #999;
            }
            .notification-close:hover {
                color: #333;
            }

        `;
        document.head.appendChild(style);




        this._debug('CSS styles created for notifications');
    };

    // Phương thức hỗ trợ để gộp cấu hình
    NotificationClient.prototype._mergeConfig = function (defaultConfig, userConfig) {
        var result = {};
        for (var key in defaultConfig) {
            result[key] = userConfig.hasOwnProperty(key) ? userConfig[key] : defaultConfig[key];
        }
        return result;
    };

    // Phát hiện loại thiết bị
    NotificationClient.prototype._detectDevice = function () {
        var userAgent = navigator.userAgent || navigator.vendor || window.opera;

        // Kiểm tra xem có phải là thiết bị di động không
        if (/android|webos|iphone|ipad|ipod|blackberry|iemobile|opera mini/i.test(userAgent.toLowerCase())) {
            // Kiểm tra xem có phải là trình duyệt di động không
            if (window.innerWidth <= 800) {
                this._debug('Device detected: Mobile/MobileWeb');
                return NotificationClient.DeviceType.MobileWeb; // Mobile hoặc MobileWeb
            } else {
                this._debug('Device detected: Tablet (treated as Website)');
                return NotificationClient.DeviceType.WebSite; // Tablet, coi như Website
            }
        } else {
            this._debug('Device detected: Website');
            return NotificationClient.DeviceType.WebSite; // Desktop/Website
        }
    };

    // Phương thức debug
    NotificationClient.prototype._debug = function () {
        if (this.config.debug && window.console) {
            console.log.apply(console, ['[NotificationClient]'].concat(Array.prototype.slice.call(arguments)));
        }
    };
    // Phương thức debug luôn ghi log (dùng cho các trường hợp quan trọng)
    NotificationClient.prototype._logAlways = function () {
        if (this.config.debug && window.console) {
            console.log.apply(console, ['[NotificationClient]'].concat(Array.prototype.slice.call(arguments)));
        }
    };

    // Thêm class tracking vào tất cả các link và button trong nội dung HTML
    NotificationClient.prototype._addTrackingClassesToContent = function (contentElement, notificationId) {
        if (!contentElement) return;

        var userId = this.config.userId;
        var links = contentElement.querySelectorAll('a, button');

        // Thêm class tracking vào tất cả các link và button
        for (var i = 0; i < links.length; i++) {
            var element = links[i];
            // Thêm class tracking
            element.classList.add("user-" + userId);
            element.classList.add("nof-" + notificationId);
        }

        this._debug('Added tracking classes to', links.length, 'links and buttons in notification:', notificationId);
    };
    // Phương thức lấy dữ liệu thông báo từ localStorage
    NotificationClient.prototype._getStoredData = function () {
        var stored = localStorage.getItem(this.storageKey);
        if (stored) {
            try {
                var parsedData = JSON.parse(stored);

                // Giải nén dữ liệu nếu cần
                var data = this._decompressData(parsedData);

                if (data && data.notifications) {
                    // Đã có cấu trúc mới
                    return data;
                } else {
                    // Cấu trúc cũ, chuyển đổi sang cấu trúc mới
                    return {
                        notifications: {
                            items: data && data.notifications ? data.notifications : [],
                            status: data && data.status ? data.status : {},
                            lastUpdated: data && data.lastUpdated ? data.lastUpdated : 0
                        }
                    };
                }
            } catch (e) {
                this._debug('Error parsing localStorage data:', e.message);
                return this._getDefaultData();
            }
        }
        return this._getDefaultData();
    };

    // Nén dữ liệu bằng cách sử dụng tên fields ngắn gọn
    NotificationClient.prototype._compressData = function (data) {
        var mapping = NotificationClient.fieldMapping;

        // Hàm để nén một đối tượng
        function compressObject(obj) {
            if (!obj || typeof obj !== 'object') return obj;

            // Nếu là mảng
            if (Array.isArray(obj)) {
                return obj.map(function (item) {
                    return compressObject(item);
                });
            }

            // Nếu là đối tượng
            var result = {};
            for (var key in obj) {
                if (obj.hasOwnProperty(key)) {
                    var shortKey = mapping[key] || key;
                    result[shortKey] = compressObject(obj[key]);
                }
            }
            return result;
        }

        return compressObject(data);
    };

    // Giải nén dữ liệu bằng cách chuyển đổi từ tên fields ngắn gọn sang tên đầy đủ
    NotificationClient.prototype._decompressData = function (data) {
        var mapping = NotificationClient.fieldMapping;

        // Tạo bảng ánh xạ ngược
        var reverseMapping = {};
        for (var key in mapping) {
            if (mapping.hasOwnProperty(key)) {
                reverseMapping[mapping[key]] = key;
            }
        }

        // Hàm để giải nén một đối tượng
        function decompressObject(obj) {
            if (!obj || typeof obj !== 'object') return obj;

            // Nếu là mảng
            if (Array.isArray(obj)) {
                return obj.map(function (item) {
                    return decompressObject(item);
                });
            }

            // Nếu là đối tượng
            var result = {};
            for (var key in obj) {
                if (obj.hasOwnProperty(key)) {
                    var longKey = reverseMapping[key] || key;
                    result[longKey] = decompressObject(obj[key]);
                }
            }
            return result;
        }

        return decompressObject(data);
    };

    // Tạo cấu trúc dữ liệu mặc định
    NotificationClient.prototype._getDefaultData = function () {
        return {
            notifications: {
                items: [],
                status: {},
                lastUpdated: 0
            }
        };
    };

    NotificationClient.prototype._getLastUpdated = function () {
        var data = this._getStoredData();

        var lastUpdated = 0;

        // Lấy lastUpdated từ thuộc tính của notifications nếu có
        if (data.notifications && data.notifications.lastUpdated) {
            lastUpdated = data.notifications.lastUpdated;
        }
        // Nếu có danh sách thông báo, tìm thông báo có lastUpdated lớn nhất
        //else if (data.notifications && data.notifications.items && data.notifications.items.length > 0) {
        //    var items = data.notifications.items;
        //    for (var i = 0; i < items.length; i++) {
        //        if (items[i].lastUpdated && items[i].lastUpdated > lastUpdated) {
        //            lastUpdated = items[i].lastUpdated;
        //        }
        //    }
        //}
        /*   lastUpdated = data.lastUpdated;*/
        // Luôn luôn lấy - 1 phút để tránh miss data
        //if (lastUpdated > 0) {
        //    lastUpdated -= 5; // trừ 1 phút (tính theo giây)
        //}
        if (lastUpdated < 0) {
            lastUpdated = 0;
        }
        return lastUpdated;
    };


    // Phương thức lấy trạng thái thông báo từ localStorage
    NotificationClient.prototype._getStatus = function () {
        var data = this._getStoredData();
        return data.notifications && data.notifications.status ? data.notifications.status : {};
    };

    // Phương thức lấy danh sách thông báo từ localStorage
    NotificationClient.prototype._getItems = function () {
        var data = this._getStoredData();
        return data.notifications && data.notifications.items ? data.notifications.items : [];
    };

    // Tính kích thước chuỗi theo KB
    NotificationClient.prototype._getStringSizeInKB = function (str) {
        // Mỗi ký tự trong JavaScript chiếm 2 byte (UTF-16)
        return Math.round((str.length * 2) / 1024);
    };

    // Dọ n dẹp dữ liệu cũ để giảm kích thước lưu trữ
    NotificationClient.prototype._cleanupOldData = function () {
        // Xóa các trạng thái cũ hơn 30 ngày
        var thirtyDaysAgo = Math.floor(Date.now() / 1000) - (30 * 24 * 60 * 60);
        var newStatus = {};

        for (var id in this.status) {
            if (this.status.hasOwnProperty(id)) {
                // Kiểm tra xem trạng thái có cũ quá không
                var status = this.status[id];
                if (status.lastClick > thirtyDaysAgo || status.lastShown > thirtyDaysAgo) {
                    newStatus[id] = status;
                } else {
                    this._debug('Removing old status for notification:', id);
                }
            }
        }

        // Cập nhật trạng thái
        this.status = newStatus;

        // Giớ i hạn số lượng thông báo được lưu trữ (giữ 50 thông báo mới nhất)
        if (this.notifications.length > 50) {
            this._debug('Limiting stored notifications from', this.notifications.length, 'to 50');
            this.notifications = this.notifications.slice(0, 50);
        }
    };

    NotificationClient.prototype._cleanupOldStorageKeyPrefix = function () {
        const self = this;
        const prefix = self.config.storageKeyPrefix;
        const currentKey = self.storageKey;
        const now = Date.now();

        for (let i = 0; i < localStorage.length; i++) {
            const key = localStorage.key(i);
            if (key.startsWith(prefix) && key !== currentKey) {
                try {

                    let strValue = localStorage.getItem(key);
                    var item = JSON.parse(strValue);
                    var decompressData = self._decompressData(item);
                    var data = {
                        notifications: {
                            items: [],
                            status: decompressData.notifications.status,
                            lastUpdated: 0
                        }
                    };
                    var compressedData = self._compressData(data);
                    localStorage.setItem(key, JSON.stringify(compressedData));
                } catch (e) {
                    // Nếu không phải JSON hợp lệ thì vẫn xóa
                    // localStorage.removeItem(key);
                }
            }
        }
    };


    // Dọ n dẹp mạnh hơn khi dữ liệu quá lớn
    NotificationClient.prototype._aggressiveCleanup = function () {
        // Xóa các trạng thái cũ hơn 7 ngày
        var sevenDaysAgo = Math.floor(Date.now() / 1000) - (7 * 24 * 60 * 60);
        var newStatus = {};

        for (var id in this.status) {
            if (this.status.hasOwnProperty(id)) {
                var status = this.status[id];
                if (status.lastClick > sevenDaysAgo || status.lastShown > sevenDaysAgo) {
                    newStatus[id] = status;
                }
            }
        }

        // Cập nhật trạng thái
        this.status = newStatus;

        // Giớ i hạn số lượng thông báo được lưu trữ (giữ 20 thông báo mới nhất)
        if (this.notifications.length > 20) {
            this.notifications = this.notifications.slice(0, 20);
        }
    };

    // Dọ n dẹp khẩn cấp khi localStorage đầy bằng cách lấy lại dữ liệu từ server
    NotificationClient.prototype._emergencyCleanup = function (callback) {
        var self = this;
        this._logAlways('Emergency cleanup: fetching fresh data from server');

        // Xóa các trạng thái cũ hơn 30 ngày để giảm bớt dữ liệu ngay lập tức
        var thirtyDaysAgo = Math.floor(Date.now() / 1000) - (30 * 24 * 60 * 60);
        var cleanedStatus = {};

        for (var id in this.status) {
            if (this.status.hasOwnProperty(id)) {
                var status = this.status[id];
                if (status.lastClick > thirtyDaysAgo || status.lastShown > thirtyDaysAgo) {
                    cleanedStatus[id] = status;
                }
            }
        }

        // Cập nhật trạng thái đã lọc
        this.status = cleanedStatus;

        // Gọi API để lấy dữ liệu mới nhất
        var domain = this.config.domain;
        var userId = this.config.userId;
        var apiUrl = this.config.apiUrl + '?Domain=' + encodeURIComponent(domain) +
            '&UserId=' + encodeURIComponent(userId) +
            '&Device=' + this.config.device;

        // Reset lastUpdated để lấy tất cả thông báo mới nhất
        this.lastUpdated = 0;
        apiUrl += '&LastUpdated=' + this.lastUpdated;

        this._fetchData(apiUrl, function (error, responseData) {
            if (error) {
                self._logAlways('Emergency cleanup: failed to fetch fresh data:', error);

                // Nếu không lấy được dữ liệu mới, giảm thiểu dữ liệu hiện tại
                self.notifications = self.notifications.slice(0, 3); // Chỉ giữ 3 thông báo mới nhất

                // Chỉ giữ trạng thái của 3 thông báo này
                var minimalStatus = {};
                for (var j = 0; j < self.notifications.length; j++) {
                    var notificationId = self.notifications[j].id;
                    if (self.status[notificationId]) {
                        minimalStatus[notificationId] = self.status[notificationId];
                    }
                }
                self.status = minimalStatus;
            } else {
                // Cập nhật dữ liệu mới từ server
                if (responseData && responseData.notifications) {
                    // Cập nhật danh sách thông báo
                    self.notifications = responseData.notifications.items || [];

                    // Lọc trạng thái, chỉ giữ lại trạng thái của các thông báo hiện tại
                    var currentStatus = {};
                    var notificationIds = self.notifications.map(function (notification) {
                        return notification.id;
                    });

                    for (var i = 0; i < notificationIds.length; i++) {
                        var id = notificationIds[i];
                        if (self.status[id]) {
                            currentStatus[id] = self.status[id];
                        }
                    }

                    self.status = currentStatus;
                    self.lastUpdated = responseData.notifications.lastUpdated || Math.floor(Date.now() / 1000);

                    self._logAlways('Emergency cleanup: updated with fresh data,', self.notifications.length, 'notifications');
                }
            }

            if (callback) callback();
        });
    };

    // Gán đối tượng NotificationClient vào window
    window.NotificationClient = NotificationClient;

})(window);



function modalHandler(element, action = 'show') {
    // Kiểm tra xem element có tồn tại
    if (!element) {
        console.error('Element not found');
        return;
    }

    // Kiểm tra xem jQuery có tồn tại (Bootstrap 4 hoặc Bootstrap 5 với jQuery)
    if (typeof jQuery !== 'undefined' && jQuery.fn && jQuery.fn.modal) {
        // Sử dụng jQuery để show hoặc hide modal
        if (action === 'show' || action === 'hide') {
            jQuery(element).modal(action);
        } else {
            console.error('Invalid action. Use "show" or "hide".');
        }
    } else if (window.bootstrap && window.bootstrap.Modal) {
        // Sử dụng Bootstrap 5 vanilla JavaScript
        const modal = bootstrap.Modal.getInstance(element) || new bootstrap.Modal(element);
        if (action === 'show') {
            modal.show();
        } else if (action === 'hide') {
            modal.hide();
        } else {
            console.error('Invalid action. Use "show" or "hide".');
        }
    } else {
        console.error('Bootstrap Modal or jQuery not found');
    }
}