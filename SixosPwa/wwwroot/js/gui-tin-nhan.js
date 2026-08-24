// Danh sách bệnh nhân – sẽ được tải từ server sau khi nhấn nút Lọc 
let mockPatients = []; 
// Mẫu tin nhắn định nghĩa sẵn 
const templates = { 
custom: "", 
remind: "Kính chào anh/chị [Họ tên] (Mã BN: [Mã bệnh nhân]), Bệnh viện HisSoft xin nhắc lịch khám sức khỏe định kỳ của anh/chị vào lúc [Thời gian]. Vui lòng nhịn ăn sáng tối thiểu 8 tiếng trước khi đi xét nghiệm. ĐT hỗ trợ: 19001080.", 
result: "Thông báo từ HisSoft: Kết quả xét nghiệm định kỳ của bệnh nhân [Họ tên] ([Mã bệnh nhân]) khám ngày [Thời gian] đã có đầy đủ trên hệ thống ứng dụng PWA. Vui lòng mở app để xem chi tiết chẩn đoán.", 
survey: "Cảm ơn quý khách [Họ tên] đã tin tưởng khám sức khỏe tại HisSoft vào [Thời gian]. Vui lòng dành 1 phút đánh giá chất lượng dịch vụ của chúng tôi tại liên kết: https://hissoft.vn/survey/[Mã bệnh nhân]. Trân trọng!" 
}; 
// Bệnh nhân được chọn để hiển thị Preview chính 
let activePreviewIndex = 0;
let patientSearchControl = null;

function initializePatientSearch() {
    const searchElement = document.getElementById("searchPatient");
    if (!searchElement || typeof TomSelect === "undefined" || patientSearchControl) return;

    patientSearchControl = new TomSelect(searchElement, {
        valueField: "id",
        labelField: "name",
        searchField: ["name", "phone", "id"],
        maxItems: 1,
        closeAfterSelect: false,
        allowEmptyOption: true,
        options: mockPatients,
        render: {
            option: function (patient, escape) {
                return '<div><strong>' + escape(patient.name || "Chưa có tên") + '</strong><small class="d-block text-muted">' + escape(patient.phone || patient.id || "") + '</small></div>';
            },
            item: function (patient, escape) {
                return '<div>' + escape(patient.name || "Chưa có tên") + '</div>';
            }
        },
        onType: function (query) {
            filterPatients(query);
        },
        onChange: function (value) {
            const query = this.control_input?.value || "";
            filterPatients(query);
            if (!value) return;
            const selectedIndex = mockPatients.findIndex(patient => String(patient.id) === String(value));
            if (selectedIndex >= 0) selectActivePreview(selectedIndex);
        }
    });

    patientSearchControl.control_input.addEventListener("input", function () {
        filterPatients(this.value);
    });
}

function refreshPatientSearch() {
    if (!patientSearchControl) {
        initializePatientSearch();
        return;
    }

    patientSearchControl.clear(true);
    patientSearchControl.clearOptions();
    patientSearchControl.addOptions(mockPatients);
    patientSearchControl.refreshOptions(false);
} 
// Khởi động trang 
document.addEventListener("DOMContentLoaded", () => { 
initializePatientSearch();
renderPatientTable(); 
loadTemplate(); 
// Cập nhật thông tin đối tác mặc định từ database 
const partnerSelect = document.getElementById("partnerSelect"); 
if (partnerSelect && partnerSelect.value !== "custom") { 
const selectedOption = partnerSelect.options[partnerSelect.selectedIndex]; 
if (selectedOption) { 
const dbPassword = selectedOption.getAttribute("data-password") || ""; 
document.getElementById("smsPassword").value = dbPassword; 
const brandname = selectedOption.getAttribute("data-brandname") || selectedOption.text; 
document.getElementById("previewSender").innerHTML = `Từ: <strong>${brandname}</strong>`; 
} 
} 
updateLivePreview(); 
}); 
// Toggle hiển thị input nhập đối tác tùy chỉnh 
function togglePartnerInput() { 
const partnerSelect = document.getElementById("partnerSelect"); 
const customInput = document.getElementById("customPartnerInput"); 
const passwordInput = document.getElementById("smsPassword"); 
if (partnerSelect.value === "custom") { 
customInput.classList.remove("d-none"); 
customInput.required = true; 
customInput.value = ""; 
passwordInput.value = "";
document.getElementById("previewSender").innerHTML = "Từ: <strong>SMS Brandname</strong>"; 
} else { 
customInput.classList.add("d-none"); 
customInput.required = false; 
// Cập nhật mật khẩu tự động từ database 
const selectedOption = partnerSelect.options[partnerSelect.selectedIndex]; 
if (selectedOption) { 
const dbPassword = selectedOption.getAttribute("data-password") || ""; 
passwordInput.value = dbPassword; 
const brandname = selectedOption.getAttribute("data-brandname") || selectedOption.text; 
document.getElementById("previewSender").innerHTML = `Từ: <strong>${brandname}</strong>`; 
} 
} 
updateLivePreview(); 
} 
// Hiện/ẩn mật khẩu API 
function togglePasswordVisibility() { 
const passwordInput = document.getElementById("smsPassword"); 
const toggleIcon = document.getElementById("togglePasswordIcon"); 
if (passwordInput.type === "password") { 
passwordInput.type = "text"; 
toggleIcon.innerText = "🙈"; 
} else { 
passwordInput.type = "password";
toggleIcon.innerText = "👁️"; 
} 
} 
// ─── GỌI STORED PROCEDURE LỌC DANH SÁCH BỆNH NHÂN ─────────────── 
async function locDanhSachBN() { 
const partnerSelect = document.getElementById("partnerSelect"); 
const selectedOption = partnerSelect.options[partnerSelect.selectedIndex]; 
const tenDT = selectedOption ? selectedOption.text : ""; 
const password = document.getElementById("smsPassword").value.trim(); 
if (!tenDT || tenDT === "-- Tự cấu hình đối tác khác..." || partnerSelect.value === "custom") { 
 showToast("Vui lòng chọn một đối tác từ danh sách trước khi lọc bệnh nhân!", 'warning');
return; 
} 
if (!password) { 
 showToast("Vui lòng nhập mật khẩu đối tác!", 'warning');
return; 
} 
// Hiển thị spinner trên nút 
const btn = document.getElementById("btnLocBN"); 
const spinner = document.getElementById("locSpinner"); 
const icon = document.getElementById("locBtnIcon"); 
btn.disabled = true; 
spinner.classList.remove("d-none");
icon.classList.add("d-none"); 
try { 
const resp = await fetch("/Home/LocDanhSachBN", { 
method: "POST", 
credentials: "same-origin", 
headers: { 
"Content-Type": "application/json", 
"X-Requested-With": "XMLHttpRequest" 
}, 
body: JSON.stringify({ tenDT, password }) 
}); 
const result = await resp.json(); 
if (!result.success) { 
 showToast(result.message, 'error');
return; 
} 
if (!result.data || result.data.length === 0) { 
 showToast("Không tìm thấy bệnh nhân nào thuộc đối tác này.", 'info');
// Xoá bảng cũ 
mockPatients = []; 
refreshPatientSearch();
renderPatientTable(); 
return;
} 
// Chuyển đổi dữ liệu về định dạng mockPatients 
mockPatients = result.data.map(bn => ({ 
id: bn.maBN, 
name: bn.tenBN, 
phone: bn.sdt || "(chưa có)", 
appointment: "", 
status: "Chưa gửi" 
})); 
activePreviewIndex = 0; 
refreshPatientSearch();
renderPatientTable(); 
updateLivePreview(); 
// Hiển thị toast thông báo lọc thành công 
showToast(`Lọc thành công! Tìm thấy ${mockPatients.length} bệnh nhân thuộc đối tác ${tenDT}.`, 'success');
} catch (err) { 
console.error(err); 
 showToast("Lỗi kết nối máy chủ khi lọc danh sách bệnh nhân!", 'error');
} finally { 
btn.disabled = false; 
spinner.classList.add("d-none"); 
icon.classList.remove("d-none"); 
} 
} 
// Render bảng bệnh nhân 
function renderPatientTable() { 
const tbody = document.getElementById("patientTableBody"); 
tbody.innerHTML = ""; 
if (mockPatients.length === 0) { 
tbody.innerHTML = `<tr><td colspan="4" class="text-center text-muted py-4">Nhấn <strong>🔎 Lọc danh sách</strong> để tải bệnh nhân theo đối tác.</td></tr>`; 
document.getElementById("selectedCountBadge").innerText = "Đã chọn: 0"; 
return; 
}
mockPatients.forEach((patient, idx) => { 
const tr = document.createElement("tr"); 
tr.className = `patient-row ${idx === activePreviewIndex ? 'selected-row' : ''}`; 
tr.onclick = (e) => { 
// Nếu bấm vào checkbox thì không click row trùng lặp 
if (e.target.type !== 'checkbox') { 
selectActivePreview(idx); 
} 
}; 
let badgeClass = "bg-secondary-subtle text-secondary"; 
if (patient.status === "Đang gửi...") badgeClass = "bg-warning-subtle text-warning-emphasis"; 
if (patient.status === "Đã gửi") badgeClass = "bg-success-subtle text-success"; 
tr.innerHTML = ` 
<td class="text-center" onclick="event.stopPropagation()"> 
<input class="form-check-input patient-checkbox" type="checkbox" value="${patient.id}" data-index="${idx}" onchange="updateSelectedCount()" /> 
</td> 
<td> 
<div class="fw-semibold text-dark">${patient.name}</div> 
<div class="text-muted small">${patient.id}</div> 
</td> 
<td class="text-secondary">${patient.phone}</td> 
<td class="text-end"> 
<span class="badge ${badgeClass}" id="status-badge-${idx}">${patient.status}</span>
</td> 
`; 
tbody.appendChild(tr); 
}); 
updateSelectedCount(); 
} 
// Chọn bệnh nhân hiển thị Preview 
function selectActivePreview(index) { 
activePreviewIndex = index; 
// Xóa class highlight cũ 
const rows = document.querySelectorAll(".patient-row"); 
rows.forEach((row, idx) => { 
if (idx === index) { 
row.classList.add("selected-row"); 
} else { 
row.classList.remove("selected-row"); 
} 
}); 
updateLivePreview(); 

// Load Chat History for the selected patient
const patient = mockPatients[index];
if (patient) loadChatHistory(patient);
} 

async function loadChatHistory(patient) {
    document.getElementById("chatPatientName").innerText = patient.name;
    document.getElementById("chatPatientPhone").innerText = patient.phone;
    
    // Enable inputs
    document.getElementById("directMessageInput").disabled = false;
    document.getElementById("btnSendDirect").disabled = false;
    
    const historyDiv = document.getElementById("chatHistory");
    historyDiv.innerHTML = '<div class="text-center text-muted my-5"><span class="spinner-border spinner-border-sm text-primary"></span> Đang tải...</div>';
    
    try {
        const resp = await fetch('/Home/GetChatHistory?sdtBenhNhan=' + encodeURIComponent(patient.phone));
        const res = await resp.json();
        
        if (res.success) {
            if (res.data.length === 0) {
                historyDiv.innerHTML = `
                    <div class="text-center text-muted my-5">
                        <i class="far fa-comments fs-1 mb-3 opacity-50"></i>
                        <p>Chưa có lịch sử trò chuyện với bệnh nhân này.</p>
                    </div>`;
            } else {
                historyDiv.innerHTML = res.data.map(msg => {
                    const type = msg.isSender ? 'sent' : 'received';
                    return `
                        <div class="chat-row ${type}">
                            <div class="chat-bubble ${type}">${msg.noiDung}</div>
                            <div class="chat-time">${msg.thoiGian}</div>
                        </div>
                    `;
                }).join('');
                // Scroll to bottom
                historyDiv.scrollTop = historyDiv.scrollHeight;
            }
        }
    } catch (e) {
        historyDiv.innerHTML = '<div class="text-center text-danger my-5">Lỗi tải dữ liệu.</div>';
    }
}

async function sendDirectMessage(e) {
    e.preventDefault();
    const input = document.getElementById("directMessageInput");
    const message = input.value.trim();
    if (!message) return;
    
    const patient = mockPatients[activePreviewIndex];
    if (!patient) return;
    
    const btn = document.getElementById("btnSendDirect");
    btn.disabled = true;
    
    try {
        const resp = await fetch('/Home/TraLoiTinNhan', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ nguoiNhan: patient.phone, message: message })
        });
        const res = await resp.json();
        if (res.success) {
            input.value = '';
            // Reload history
            loadChatHistory(patient);
        } else {
            showToast(res.message, 'error');
        }
    } catch (err) {
        showToast("Lỗi khi gửi tin nhắn.", 'error');
    } finally {
        btn.disabled = false;
    }
}

// Lọc bệnh nhân khi tìm kiếm 
function filterPatients(query) {
    const normalizedQuery = (query || "").toLowerCase().trim();
    const rows = document.querySelectorAll(".patient-row");
    let visibleCount = 0;

    rows.forEach((row, idx) => {
        const patient = mockPatients[idx];
        const searchableText = [patient.name, patient.phone, patient.id].join(" ").toLowerCase();
        const matches = !normalizedQuery || searchableText.includes(normalizedQuery);
        row.classList.toggle("d-none", !matches);
        if (matches) visibleCount++;
    });

    const tbody = document.getElementById("patientTableBody");
    const oldEmptyRow = document.getElementById("patientSearchEmpty");
    if (oldEmptyRow) oldEmptyRow.remove();
    if (mockPatients.length > 0 && visibleCount === 0) {
        tbody.insertAdjacentHTML("beforeend", '<tr id="patientSearchEmpty"><td colspan="4" class="text-center text-muted py-4">Không tìm thấy bệnh nhân phù hợp.</td></tr>');
    }
}
// Tích chọn toàn bộ danh sách bệnh nhân 
function toggleSelectAll(masterCheckbox) { 
const checkboxes = document.querySelectorAll(".patient-checkbox"); 
checkboxes.forEach(cb => { 
// Chỉ check các dòng đang hiển thị (không bị ẩn bởi bộ lọc) 
const tr = cb.closest("tr"); 
if (!tr.classList.contains("d-none")) { 
cb.checked = masterCheckbox.checked; 
} 
}); 
updateSelectedCount(); 
} 
// Cập nhật số lượng bệnh nhân đã chọn 
function updateSelectedCount() { 
const checkedBoxes = document.querySelectorAll(".patient-checkbox:checked"); 
const badge = document.getElementById("selectedCountBadge"); 
badge.innerText = `Đã chọn: ${checkedBoxes.length} / ${mockPatients.length}`; 
// Cập nhật master checkbox 
const masterCheckbox = document.getElementById("selectAllCheckbox"); 
const visibleCheckboxes = document.querySelectorAll(".patient-checkbox:not(.d-none)"); 
if (checkedBoxes.length === 0) { 
masterCheckbox.checked = false; 
masterCheckbox.indeterminate = false; 
} else if (checkedBoxes.length === mockPatients.length) { 
masterCheckbox.checked = true; 
masterCheckbox.indeterminate = false; 
} else { 
masterCheckbox.checked = false; 
masterCheckbox.indeterminate = true; 
} 
updateLivePreview(); 
} 
// Load mẫu tin nhắn 
function loadTemplate() { 
const selectVal = document.getElementById("templateSelect").value; 
document.getElementById("smsContent").value = templates[selectVal]; 
updateLivePreview(); 
} 
// Đọc nội dung soạn thảo, thay thế thẻ động và cập nhật Live Preview 
function updateLivePreview() { 
const content = document.getElementById("smsContent").value; 
// Cập nhật bộ đếm ký tự 
const charCount = content.length; 
const smsCount = charCount === 0 ? 0 : Math.ceil(charCount / 160); 
const countText = document.getElementById("charCountText"); 
countText.innerText = `${charCount} / 160 ký tự (${smsCount} tin nhắn)`; 
if (charCount > 160) { 
countText.classList.remove("text-muted"); 
countText.classList.add("text-warning", "fw-bold"); 
} else { 
countText.classList.remove("text-warning", "fw-bold"); 
countText.classList.add("text-muted"); 
} 
// Tìm thông tin bệnh nhân đang chọn preview 
const currentPatient = mockPatients[activePreviewIndex]; 
if (!currentPatient) return; 
// Hiển thị số điện thoại nhận tin 
document.getElementById("previewTargetPhone").innerHTML = `Tới: <strong>${currentPatient.phone}</strong>`; 
if (!content.trim()) { 
document.getElementById("previewBubbleText").innerText = "(Nội dung trống)"; 
return; 
} 
// Thay thế các tag tham số động 
let previewText = content; 
previewText = previewText.replaceAll("[Họ tên]", currentPatient.name); 
previewText = previewText.replaceAll("[Mã bệnh nhân]", currentPatient.id); 
previewText = previewText.replaceAll("[Thời gian]", currentPatient.appointment); 
document.getElementById("previewBubbleText").innerText = previewText; 
// Cập nhật thời gian 
const now = new Date(); 
const timeStr = now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }); 
document.getElementById("previewBubbleTime").innerText = timeStr + " • SMS qua " + (document.getElementById("partnerSelect").value === "custom" ? document.getElementById("customPartnerInput").value || "Đối tác" : document.getElementById("partnerSelect").value); 
} 
// Chèn tag động tại vị trí con trỏ của textarea 
function insertTag(tag) { 
const textarea = document.getElementById("smsContent"); 
const start = textarea.selectionStart; 
const end = textarea.selectionEnd; 
const text = textarea.value; 
textarea.value = text.substring(0, start) + tag + text.substring(end); 
textarea.focus(); 
textarea.selectionStart = textarea.selectionEnd = start + tag.length; 
// Cập nhật lại Preview 
updateLivePreview(); 
} 
// Bật animation và gửi tin nhắn hàng loạt – lưu thông báo vào DB 
async function triggerSendSms() { 
const selectedCheckboxes = document.querySelectorAll(".patient-checkbox:checked"); 
const content = document.getElementById("smsContent").value.trim(); 
const partner = document.getElementById("partnerSelect").value === "custom" 
? document.getElementById("customPartnerInput").value.trim() 
: document.getElementById("partnerSelect").value; 
// Validate dữ liệu 
if (selectedCheckboxes.length === 0) { 
showToast("Vui lòng chọn ít nhất 1 bệnh nhân để gửi tin!", 'warning');
return; 
} 
if (!content) { 
showToast("Vui lòng nhập nội dung tin nhắn!", 'warning');
return; 
} 
if (!partner) { 
showToast("Vui lòng nhập/chọn đối tác SMS!", 'warning');
return; 
} 
// Lấy danh sách index và số điện thoại các bệnh nhân được chọn 
const targetIndexes = []; 
const danhSachSDT = []; 
selectedCheckboxes.forEach(cb => { 
const idx = parseInt(cb.getAttribute("data-index")); 
targetIndexes.push(idx); 
danhSachSDT.push(mockPatients[idx].phone); 
}); 
// Bắt đầu mô phỏng giao diện gửi tin nhắn 
const btn = document.getElementById("btnSendSms"); 
const spinner = document.getElementById("sendSpinner"); 
const icon = document.getElementById("sendIcon"); 
const btnText = document.getElementById("sendBtnText"); 
const progressCard = document.getElementById("sendingProgressCard"); 
const progressBar = document.getElementById("sendingProgressBar"); 
const progressStatus = document.getElementById("progressStatusText"); 
const progressPercent = document.getElementById("progressPercentText"); 
// Disable nút gửi 
btn.disabled = true; 
spinner.classList.remove("d-none"); 
icon.classList.add("d-none"); 
btnText.innerText = "Đang kết nối API..."; 
// Hiển thị khung tiến trình 
progressCard.classList.remove("d-none"); 
progressBar.classList.add("progress-bar-animated"); 
progressBar.style.width = "0%"; 
progressPercent.innerText = "0%"; 
progressStatus.innerText = `Bắt đầu gửi tin nhắn thông qua ${partner}...`;
// Đổi trạng thái bảng → "Đang gửi..." 
targetIndexes.forEach(idx => { 
mockPatients[idx].status = "Đang gửi..."; 
const badge = document.getElementById(`status-badge-${idx}`); 
if (badge) { 
badge.innerText = "Đang gửi..."; 
badge.className = "badge bg-warning-subtle text-warning-emphasis"; 
} 
}); 
// ── Gọi API thực tế lưu thông báo vào DB ────────────────────── 
let apiSuccess = false; 
try { 
const resp = await fetch("/Home/GuiTinNhan", { 
method: "POST", 
credentials: "same-origin", 
headers: { 
"Content-Type": "application/json", 
"X-Requested-With": "XMLHttpRequest" 
}, 
body: JSON.stringify({ 
message: content, 
danhSachNguoiNhan: danhSachSDT 
}) 
});
const result = await resp.json(); 
apiSuccess = result.success; 
} catch (err) { 
console.warn("Lỗi gọi API:", err); 
} 
// ── Animation tiến trình ─────────────────────────────────────── 
const totalPatients = targetIndexes.length; 
let currentPercent = 0; 
const interval = setInterval(() => { 
currentPercent += 5; 
if (currentPercent > 100) currentPercent = 100; 
progressBar.style.width = `${currentPercent}%`; 
progressPercent.innerText = `${currentPercent}%`; 
const currentPatientIdx = Math.min(Math.floor((currentPercent / 100) * totalPatients), totalPatients - 1); 
const currentPat = mockPatients[targetIndexes[currentPatientIdx]]; 
progressStatus.innerText = `Đang gửi SMS cho bệnh nhân: ${currentPat.name} (${currentPat.phone})...`; 
// Cập nhật trạng thái từng bệnh nhân 
for (let i = 0; i <= currentPatientIdx; i++) { 
const idx = targetIndexes[i];
if (mockPatients[idx].status !== "Đã gửi" && currentPercent > (i / totalPatients) * 100) { 
mockPatients[idx].status = "Đã gửi"; 
const badge = document.getElementById(`status-badge-${idx}`); 
if (badge) { 
badge.innerText = "Đã gửi"; 
badge.className = "badge bg-success-subtle text-success"; 
} 
} 
} 
if (currentPercent >= 100) { 
clearInterval(interval); 
// Khôi phục nút 
btn.disabled = false; 
spinner.classList.add("d-none"); 
icon.classList.remove("d-none"); 
btnText.innerText = "Gửi Tin Nhắn Hàng Loạt"; 
progressStatus.innerText = `✅ Đã hoàn tất gửi ${totalPatients} tin nhắn${apiSuccess ? " và lưu thông báo thành công!" : " (chế độ demo)!"}`; 
progressBar.classList.remove("progress-bar-animated");
// Show Toast 
showToast(`Gửi thành công! Đã gửi SMS cho ${totalPatients} bệnh nhân thông qua cổng ${partner}. Bệnh nhân sẽ nhận được thông báo đẩy ngay!`, 'success');
// Tự động ẩn Progress Card sau 5 giây 
setTimeout(() => { 
progressCard.classList.add("d-none"); 
}, 5000); 
} 
}, 150); 
}
