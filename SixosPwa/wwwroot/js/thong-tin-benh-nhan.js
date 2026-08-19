    function toggleMenu() {
        var menu = document.getElementById("dropdownMenu");
        menu.classList.toggle("open");
    }

    // Auto-close menu when mouse leaves menu area
    document.addEventListener("DOMContentLoaded", function() {
        var mainSlider = document.getElementById('mainSlider');
        if (mainSlider && typeof bootstrap !== 'undefined') {
            new bootstrap.Carousel(mainSlider, {
                interval: 3000,
                ride: 'carousel'
            });
        }

        var menuBtn = document.getElementById("menuBtn");
        if (menuBtn) {
            menuBtn.addEventListener("mouseleave", function() {
                var menu = document.getElementById("dropdownMenu");
                if (menu) menu.classList.remove("open");
            });
        }

        var serviceItems = document.querySelectorAll('.ytv-company-service');
        if (serviceItems.length > 1) {
            var activeServiceIndex = 0;
            window.setInterval(function() {
                serviceItems[activeServiceIndex].classList.remove('is-active');
                serviceItems[activeServiceIndex].setAttribute('aria-hidden', 'true');
                serviceItems[activeServiceIndex].setAttribute('tabindex', '-1');
                activeServiceIndex = (activeServiceIndex + 1) % serviceItems.length;
                serviceItems[activeServiceIndex].classList.add('is-active');
                serviceItems[activeServiceIndex].setAttribute('aria-hidden', 'false');
                serviceItems[activeServiceIndex].setAttribute('tabindex', '0');
            }, 5000);
        }
    });

    // Close the dropdown menu if the user clicks outside of it
    window.onclick = function(event) {
        if (!event.target.closest('#menuBtn')) {
            var dropdowns = document.getElementsByClassName("ytv-dropdown");
            for (var i = 0; i < dropdowns.length; i++) {
                var openDropdown = dropdowns[i];
                if (openDropdown.classList.contains('open')) {
                    openDropdown.classList.remove('open');
                }
            }
        }
    }

    const policyContent = {
        'baomat': {
            title: 'Chính sách bảo mật',
            content: '<p><strong>1. Mục đích và phạm vi thu thập</strong><br>Việc thu thập dữ liệu chủ yếu trên nền tảng bao gồm: email, điện thoại, tên đăng nhập, mật khẩu đăng nhập, địa chỉ khách hàng. Đây là các thông tin mà Y Tế Việt cần người dùng cung cấp bắt buộc khi đăng ký sử dụng dịch vụ và để liên hệ xác nhận khi khách hàng đăng ký sử dụng dịch vụ nhằm đảm bảo quyền lợi cho cho người tiêu dùng.</p><p><strong>2. Phạm vi sử dụng thông tin</strong><br>Nền tảng sử dụng thông tin thành viên cung cấp để: Cung cấp các dịch vụ đến thành viên; Gửi các thông báo về các hoạt động trao đổi thông tin; Ngừa các hoạt động phá hủy tài khoản người dùng của thành viên hoặc các hoạt động giả mạo thành viên; Liên lạc và giải quyết với thành viên trong những trường hợp đặc biệt.</p><p><strong>3. Thời gian lưu trữ thông tin</strong><br>Dữ liệu cá nhân của thành viên sẽ được lưu trữ cho đến khi có yêu cầu hủy bỏ hoặc tự thành viên đăng nhập và thực hiện hủy bỏ. Còn lại trong mọi trường hợp thông tin cá nhân thành viên sẽ được bảo mật trên máy chủ của Y Tế Việt.</p>'
        },
        'dieukhoan': {
            title: 'Điều khoản sử dụng',
            content: '<p><strong>1. Trách nhiệm của người sử dụng</strong><br>Bạn phải cung cấp thông tin chính xác, đầy đủ và cập nhật khi đăng ký tài khoản. Việc bảo mật mật khẩu và tài khoản là trách nhiệm của bạn. Bạn không được phép sử dụng tài khoản của mình cho bất kỳ hoạt động bất hợp pháp nào.</p><p><strong>2. Quyền lợi và trách nhiệm của Y Tế Việt</strong><br>Y Tế Việt cam kết cung cấp dịch vụ khám chữa bệnh trực tuyến ổn định, an toàn và bảo mật thông tin khách hàng. Chúng tôi có quyền tạm ngừng hoặc chấm dứt cung cấp dịch vụ nếu phát hiện người dùng vi phạm các điều khoản này.</p><p><strong>3. Bản quyền và sở hữu trí tuệ</strong><br>Mọi nội dung, hình ảnh, thông tin trên website đều thuộc bản quyền của Y Tế Việt và được bảo hộ bởi luật sở hữu trí tuệ.</p>'
        },
        'khieunai': {
            title: 'Giải quyết khiếu nại',
            content: '<p><strong>1. Quy trình tiếp nhận khiếu nại</strong><br>Người dùng có thể gửi khiếu nại liên quan đến dịch vụ qua hotline hoặc email hỗ trợ của chúng tôi. Chúng tôi sẽ tiếp nhận và phản hồi trong thời gian sớm nhất, tối đa không quá 48 giờ làm việc.</p><p><strong>2. Quy trình xử lý</strong><br>Sau khi tiếp nhận khiếu nại, bộ phận chăm sóc khách hàng sẽ tiến hành xác minh thông tin và liên hệ với khách hàng để đưa ra hướng giải quyết phù hợp, nhằm đảm bảo quyền lợi chính đáng của khách hàng.</p>'
        },
        'baohanh': {
            title: 'Chính sách bảo hành',
            content: '<p><strong>1. Điều kiện bảo hành</strong><br>Chúng tôi cung cấp chính sách hỗ trợ và xử lý các vấn đề phát sinh đối với các dịch vụ khám bệnh và tư vấn trực tuyến (nếu có lỗi từ phía hệ thống hoặc bác sĩ tư vấn không đúng chuyên môn). Các trường hợp liên quan tới mua thuốc hoặc vật tư y tế sẽ được xử lý theo quy định của nhà sản xuất hoặc nhà thuốc cung cấp.</p><p><strong>2. Từ chối hỗ trợ</strong><br>Từ chối hỗ trợ bồi hoàn đối với các trường hợp người bệnh không tuân thủ chỉ định của bác sĩ, tự ý thay đổi liều lượng thuốc hoặc cung cấp thông tin bệnh án sai lệch từ đầu.</p>'
        }
    };

    function showPolicy(type) {
        event.preventDefault();
        const data = policyContent[type];
        if (data) {
            document.getElementById('policyModalLabel').innerHTML = data.title;
            document.getElementById('policyModalBody').innerHTML = data.content;
            var policyModal = new bootstrap.Modal(document.getElementById('policyModal'));
            policyModal.show();
        }
    }
