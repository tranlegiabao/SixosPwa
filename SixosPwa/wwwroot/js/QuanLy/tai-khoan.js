// Hàm hiển thị cảnh báo dạng modal
window.showModal = window.showModal || function (noiDung, tieuDe = 'Cảnh báo') {
    return new Promise((resolve) => {
        const old = document.querySelector('.ytv-nen-mo-canh-bao');
        if (old) old.remove();

        const nen = document.createElement('div');
        nen.className = 'ytv-nen-mo ytv-nen-mo-canh-bao';
        nen.style.zIndex = '1100';
        nen.innerHTML = `
            <div class="ytv-hop-xac-nhan" role="alertdialog" aria-modal="true" style="max-width: 440px;">
                <div class="ytv-hop-dau">
                    <span class="ytv-hop-bieu-tuong" style="color: #d97706; background: #fef3c7;">
                        <svg viewBox="0 0 24 24" aria-hidden="true">
                            <path d="M12 9v4"></path><path d="M12 17h.01"></path>
                            <path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0Z"></path>
                        </svg>
                    </span>
                    <h2 class="ytv-hop-tieu-de" id="ytvCanhBaoTieuDe"></h2>
                </div>
                <p class="ytv-hop-noi-dung" id="ytvCanhBaoNoiDung" style="white-space: pre-line; word-break: break-word; font-size: 13.5px;"></p>
                <div class="ytv-hop-nut">
                    <button type="button" class="btn btn-warning text-white fw-bold px-4" style="background: #f59e0b; border-color: #f59e0b;" data-ytv-dong>Đồng ý</button>
                </div>
            </div>`;

        nen.querySelector('#ytvCanhBaoTieuDe').textContent = tieuDe || 'Cảnh báo';
        nen.querySelector('#ytvCanhBaoNoiDung').textContent = noiDung || '';

        const dong = () => {
            document.removeEventListener('keydown', khiGoPhim);
            nen.remove();
            document.body.classList.remove('ytv-khoa-cuon');
            resolve();
        };
        const khiGoPhim = (e) => { if (e.key === 'Escape' || e.key === 'Enter') dong(); };

        nen.querySelector('[data-ytv-dong]').addEventListener('click', dong);
        nen.addEventListener('click', (e) => { if (e.target === nen) dong(); });
        document.addEventListener('keydown', khiGoPhim);

        document.body.classList.add('ytv-khoa-cuon');
        document.body.appendChild(nen);
        requestAnimationFrame(() => nen.classList.add('hien'));
        nen.querySelector('[data-ytv-dong]').focus();
    });
};

// Hàm hiển thị xác nhận dạng modal (thay thế window.confirm của trình duyệt)
window.showConfirm = window.showConfirm || function (noiDung, tieuDe = 'Xác nhận', nutXacNhan = 'Đồng ý', nguyHiem = true) {
    return new Promise((resolve) => {
        const old = document.querySelector('.ytv-nen-mo-xac-nhan');
        if (old) old.remove();

        const nen = document.createElement('div');
        nen.className = 'ytv-nen-mo ytv-nen-mo-xac-nhan';
        nen.style.zIndex = '1100';
        nen.innerHTML = `
            <div class="ytv-hop-xac-nhan" role="alertdialog" aria-modal="true" style="max-width: 440px;">
                <div class="ytv-hop-dau">
                    <span class="ytv-hop-bieu-tuong" style="${nguyHiem ? 'color: #d53f52; background: #fff1f3;' : 'color: #d97706; background: #fef3c7;'}">
                        <svg viewBox="0 0 24 24" aria-hidden="true">
                            <path d="M12 9v4"></path><path d="M12 17h.01"></path>
                            <path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0Z"></path>
                        </svg>
                    </span>
                    <h2 class="ytv-hop-tieu-de" id="ytvXacNhanTieuDe"></h2>
                </div>
                <p class="ytv-hop-noi-dung" id="ytvXacNhanNoiDung" style="white-space: pre-line; word-break: break-word; font-size: 13.5px;"></p>
                <div class="ytv-hop-nut">
                    <button type="button" class="btn admin-btn-secondary" data-ytv-huy>Hủy</button>
                    <button type="button" class="btn ${nguyHiem ? 'ytv-nut-nguy-hiem' : 'btn-warning text-white fw-bold'}" data-ytv-dong-y></button>
                </div>
            </div>`;

        nen.querySelector('#ytvXacNhanTieuDe').textContent = tieuDe || 'Xác nhận';
        nen.querySelector('#ytvXacNhanNoiDung').textContent = noiDung || '';
        nen.querySelector('[data-ytv-huy]').textContent = 'Hủy';
        nen.querySelector('[data-ytv-dong-y]').textContent = nutXacNhan || 'Đồng ý';

        const dong = (ketQua) => {
            document.removeEventListener('keydown', khiGoPhim);
            nen.remove();
            document.body.classList.remove('ytv-khoa-cuon');
            resolve(ketQua);
        };
        const khiGoPhim = (e) => { if (e.key === 'Escape') dong(false); };

        nen.querySelector('[data-ytv-huy]').addEventListener('click', () => dong(false));
        nen.querySelector('[data-ytv-dong-y]').addEventListener('click', () => dong(true));
        nen.addEventListener('click', (e) => { if (e.target === nen) dong(false); });
        document.addEventListener('keydown', khiGoPhim);

        document.body.classList.add('ytv-khoa-cuon');
        document.body.appendChild(nen);
        requestAnimationFrame(() => nen.classList.add('hien'));
        nen.querySelector('[data-ytv-huy]').focus();
    });
};

document.addEventListener("DOMContentLoaded", function() {
    khoiTaoDatePicker();

    // Kích hoạt nạp AJAX khi bấm Lọc
    const formFilter = document.getElementById('taiKhoanFilterForm');
    if (formFilter) {
        formFilter.addEventListener('submit', function (e) {
            e.preventDefault();
            taiDanhSachTaiKhoan(1, true);
        });
    }
});

function khoiTaoDatePicker(scopeSelector) {
    const $targets = scopeSelector ? $(scopeSelector) : $('.datepicker-input');
    $targets.each(function() {
        const $this = $(this);
        if (!$this.data('DateTimePicker')) {
            $this.datetimepicker({
                locale: "vi",
                format: "DD/MM/YYYY",
                useStrict: false,
                widgetPositioning: {
                    horizontal: "auto",
                    vertical: "bottom",
                },
                extraFormats: ["DD/MM/YYYY", "DD-MM-YYYY", "YYYY-MM-DD", "YYYY"],
                icons: {
                    date: "ti ti-calendar",
                    up: "ti ti-chevron-up",
                    down: "ti ti-chevron-down",
                    previous: "ti ti-chevron-left",
                    next: "ti ti-chevron-right",
                    time: "ti ti-alarm",
                    close: 'ti ti-x'
                },
                keyBinds: {
                    left: null,
                    right: null,
                },
                showClose: true
            });
        }
    });
}

// Bật/tắt mở rộng Drawer danh sách hồ sơ
function toggleAccountDrawer(accountId, evt) {
    if (evt) {
        const isToggleArrow = evt.target.closest('.btn-toggle-arrow');
        if (!isToggleArrow && (evt.target.closest('a') || evt.target.closest('input') || evt.target.closest('select') || evt.target.closest('button'))) {
            return;
        }
    }

    const row = document.getElementById('account-row-' + accountId);
    const drawer = document.getElementById('drawer-' + accountId);
    if (!drawer || !row) return;

    if (drawer.style.display === 'none' || drawer.style.display === '') {
        drawer.style.display = 'table-row';
        row.classList.add('is-open');
        khoiTaoDatePicker('#drawer-' + accountId + ' .datepicker-input');
    } else {
        drawer.style.display = 'none';
        row.classList.remove('is-open');
    }
}

// Bật/tắt form thêm hồ sơ mới
function toggleFormThem(accountId) {
    const f = document.getElementById('form-them-' + accountId);
    if (!f) return;
    const isShow = (f.style.display === 'none' || f.style.display === '');
    f.style.display = isShow ? 'block' : 'none';
    if (isShow) {
        khoiTaoDatePicker('#form-them-' + accountId + ' .datepicker-input');
    }
}

function capNhatTrangThaiKhoaModal(coMa, maBN) {
    const inpMa = document.getElementById('modalSuaMaBN');
    const btnGoNoi = document.getElementById('modalSuaBtnGoNoi');
    const cccd = document.getElementById('modalSuaCCCD');
    const ten = document.getElementById('modalSuaTen');
    const sdt = document.getElementById('modalSuaSDT');
    const gt = document.getElementById('modalSuaGioiTinh');
    const ns = document.getElementById('modalSuaNgaySinh');
    const btnSave = document.getElementById('modalSuaBtnSave');
    const note = document.getElementById('modalSuaDaNoiNote');

    if (coMa) {
        if (inpMa) inpMa.readOnly = true;
        if (btnGoNoi) {
            btnGoNoi.style.display = 'inline-flex';
            btnGoNoi.disabled = false;
            btnGoNoi.style.opacity = '1';
            btnGoNoi.style.cursor = 'pointer';
        }
        if (cccd) cccd.readOnly = true;
        if (ten) ten.readOnly = true;
        if (sdt) sdt.readOnly = true;
        if (gt) gt.disabled = true;
        if (ns) ns.disabled = true;
        if (btnSave) btnSave.style.display = 'none';
        if (note) {
            note.style.display = 'block';
            note.innerHTML = 'Hồ sơ đang liên kết mã bệnh nhân <b>' + (maBN || '') + '</b> tại cơ sở. Vui lòng bấm <b>Gỡ đồng bộ</b> trước khi chỉnh sửa thông tin.';
        }
    } else {
        if (inpMa) inpMa.readOnly = false;
        if (btnGoNoi) {
            btnGoNoi.style.display = 'none';
            btnGoNoi.disabled = true;
            btnGoNoi.style.opacity = '0.45';
            btnGoNoi.style.cursor = 'not-allowed';
        }
        if (cccd) cccd.readOnly = false;
        if (ten) ten.readOnly = false;
        if (sdt) sdt.readOnly = false;
        if (gt) gt.disabled = false;
        if (ns) ns.disabled = false;
        if (btnSave) btnSave.style.display = 'block';
        if (note) note.style.display = 'none';
    }
}

// ==================== MỞ MODAL SỬA HỒ SƠ THEO MẪU USER ====================
function moModalSuaHoSo(id, accountId) {
    const tr = document.getElementById('hoso-row-' + id);
    if (!tr) return;

    const tenBN = tr.getAttribute('data-ten') || tr.querySelector('.val-ten')?.innerText?.trim();
    const cccd = tr.getAttribute('data-cccd') || '';
    const sdt = tr.getAttribute('data-sdt') || '';
    const ngaySinhIso = tr.getAttribute('data-ngaysinh') || '';
    const gioiTinh = tr.getAttribute('data-gioitinh') || '1';
    const maBN = tr.getAttribute('data-mabn') || '';
    const tenCoSo = tr.getAttribute('data-tencoso') || '';
    const idHoSoCoSo = tr.getAttribute('data-idhosocoso') || '';
    const idCoSo = tr.getAttribute('data-idcoso') || '';

    document.getElementById('modalSuaId').value = id;
    document.getElementById('modalSuaAccountId').value = accountId;
    document.getElementById('modalSuaIdHoSoCoSo').value = idHoSoCoSo;
    
    const inpCoSo = document.getElementById('modalSuaIdCoSo');
    if (inpCoSo) {
        inpCoSo.value = idCoSo || locCoSoDangChon() || '';
    }

    document.getElementById('modalSuaTenSubtitle').innerText = tenBN;

    const inpMaBN = document.getElementById('modalSuaMaBN');
    inpMaBN.value = maBN || '';

    document.getElementById('modalSuaCCCD').value = cccd;
    document.getElementById('modalSuaTen').value = tenBN;
    document.getElementById('modalSuaSDT').value = sdt;
    document.getElementById('modalSuaGioiTinh').value = gioiTinh;

    // Cập nhật trạng thái khóa/mở khóa theo việc hồ sơ có mã hay không
    capNhatTrangThaiKhoaModal(!!maBN, maBN);

    // Khởi tạo DateTimePicker cho Modal Sửa hồ sơ
    const $inpNgaySinh = $('#modalSuaNgaySinh');
    if ($inpNgaySinh.data('DateTimePicker')) {
        $inpNgaySinh.data('DateTimePicker').destroy();
    }

    let ngaySinhDisplay = '';
    if (ngaySinhIso) {
        const m = moment(ngaySinhIso, ['YYYY-MM-DD', 'YYYY/MM/DD', 'DD/MM/YYYY']);
        if (m.isValid()) {
            ngaySinhDisplay = m.format('DD/MM/YYYY');
        }
    }
    $inpNgaySinh.val(ngaySinhDisplay);
    khoiTaoDatePicker('#modalSuaNgaySinh');
    if (ngaySinhDisplay) {
        $inpNgaySinh.data('DateTimePicker').date(moment(ngaySinhDisplay, 'DD/MM/YYYY'));
    }

    document.getElementById('modalSuaHoSoBackdrop').style.display = 'flex';
}

function dongModalSuaHoSo(evt) {
    if (evt && evt.target !== document.getElementById('modalSuaHoSoBackdrop')) {
        return;
    }
    document.getElementById('modalSuaHoSoBackdrop').style.display = 'none';
}

// Lưu thông tin từ Modal
async function luuThongTinModal() {
    const id = document.getElementById('modalSuaId').value;
    const accountId = document.getElementById('modalSuaAccountId').value;

    const tenBN = document.getElementById('modalSuaTen')?.value?.trim();
    const cccd = document.getElementById('modalSuaCCCD')?.value?.trim();
    const sdt = document.getElementById('modalSuaSDT')?.value?.trim();
    const ngaySinhRaw = document.getElementById('modalSuaNgaySinh')?.value?.trim();
    const gioiTinh = document.getElementById('modalSuaGioiTinh')?.value;
    const maBN = document.getElementById('modalSuaMaBN')?.value?.trim();
    const idCoSo = parseInt(document.getElementById('modalSuaIdCoSo')?.value || '0') || null;

    if (maBN && !idCoSo) {
        showModal('Vui lòng chọn cơ sở ở bộ lọc đầu trang trước khi gán mã bệnh nhân.', 'Cảnh báo');
        return;
    }

    if (!tenBN) {
        showModal('Vui lòng nhập họ và tên bệnh nhân.', 'Cảnh báo');
        return;
    }

    if (!cccd) {
        showModal('Vui lòng nhập số căn cước công dân.', 'Cảnh báo');
        return;
    }

    let ngaySinhIso = null;
    if (ngaySinhRaw) {
        const m = moment(ngaySinhRaw, ['DD/MM/YYYY', 'DD-MM-YYYY', 'YYYY-MM-DD']);
        if (m.isValid()) {
            if (m.isAfter(moment(), 'day')) {
                showModal('Ngày sinh không được lớn hơn ngày hiện tại.', 'Cảnh báo');
                return;
            }
            ngaySinhIso = m.format('YYYY-MM-DD');
        } else {
            showModal('Ngày sinh không hợp lệ (định dạng dd/mm/yyyy).', 'Cảnh báo');
            return;
        }
    }

    const btnSave = document.getElementById('modalSuaBtnSave');
    btnSave.disabled = true;
    btnSave.innerText = 'Đang lưu...';

    try {
        const resp = await fetch('/Admin/TaiKhoan/CapNhatHoSo', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                id: parseInt(id),
                idTaiKhoan: parseInt(accountId),
                tenBN: tenBN,
                cccd: cccd,
                sdt: sdt,
                ngaySinh: ngaySinhIso,
                gioiTinh: gioiTinh,
                maBN: maBN || null,
                idCoSo: idCoSo
            })
        });

        const res = await resp.json();
        if (res.success) {
            const d = res.data;
            const tr = document.getElementById('hoso-row-' + id);
            if (tr) {
                tr.setAttribute('data-ten', d.tenBN);
                tr.setAttribute('data-cccd', d.cccd);
                tr.setAttribute('data-sdt', d.sdt);
                tr.setAttribute('data-ngaysinh', d.ngaySinh ? moment(d.ngaySinh).format('YYYY-MM-DD') : '');
                tr.setAttribute('data-gioitinh', d.gioiTinh);
                tr.setAttribute('data-mabn', d.maBN || '');
                tr.setAttribute('data-idhosocoso', d.idHoSoCoSo || '');
                tr.setAttribute('data-idcoso', d.idCoSo || '');
                tr.setAttribute('data-tencoso', d.tenCoSo || '');

                tr.querySelector('.val-ten').innerText = d.tenBN;
                tr.querySelector('.val-cccd').innerText = d.cccd || '—';
                tr.querySelector('.val-sdt').innerText = d.sdt || '—';
                tr.querySelector('.val-ngaysinh').innerText = d.ngaySinhVn || (d.ngaySinh ? moment(d.ngaySinh).format('DD/MM/YYYY') : '—');
                tr.querySelector('.val-gioitinh').innerText = d.gioiTinhVn || 'Khác';
                const badge = tr.querySelector('.val-mabn');
                if (badge) {
                    badge.innerText = d.maBN ? d.maBN : (d.tenCoSo ? d.tenCoSo : 'Chưa nối');
                }
            }

            // Cập nhật thông tin dòng cha nếu là hồ sơ chính
            if (d.isPrimary && d.idTaiKhoan) {
                const parentRow = document.getElementById('account-row-' + d.idTaiKhoan);
                if (parentRow) {
                    const phoneStrong = parentRow.querySelector('.table-person strong');
                    if (phoneStrong && d.sdt) phoneStrong.innerText = d.sdt;
                }
            }

            document.getElementById('modalSuaHoSoBackdrop').style.display = 'none';

            if (res.canhBao) {
                showModal(res.canhBao, 'Cảnh báo');
                showToast('Đã lưu thông tin hồ sơ thành công.', 'success');
            } else {
                showToast(res.message || 'Đã lưu thông tin hồ sơ thành công.', 'success');
            }
        } else {
            if (res.isWarning) {
                showModal(res.message || 'Dữ liệu không hợp lệ.', 'Cảnh báo');
            } else {
                showToast(res.message || 'Không thể lưu hồ sơ.', 'error');
            }
        }
    } catch (e) {
        console.error(e);
        showToast('Lỗi kết nối máy chủ khi lưu hồ sơ.', 'error');
    } finally {
        btnSave.disabled = false;
        btnSave.innerText = 'Lưu';
    }
}

// Co so dang chon o bo loc, tra ve dang so. Dung lam co so DICH khi ho so chua
// gan voi co so nao — con hon de may chu doan.
function locCoSoDangChon() {
    const v = document.getElementById('filterLoaiCS')?.value || '';
    return v.startsWith('cs:') ? v.slice(3) : '';
}

// 🔴 *Go noi* THAT: goi `dbo.DM_BenhNhanCoSo_GoNoi` qua may chu.
// Ban cu chi xoa trang o input roi bao nguoi ta bam Luu — ma duong Luu do lai
// `Remove()` thang dong bang EF: BO QUA sao luu `bak.GoNoi_*_V001`, BO QUA viec de
// lai mot dong tu khai, va dam vao khoa ngoai NO_ACTION khi ho so con tai lieu.
async function goNoiModal() {
    const idHoSoCoSo = document.getElementById('modalSuaIdHoSoCoSo')?.value;
    const inpMa = document.getElementById('modalSuaMaBN');
    const maHienTai = inpMa?.value?.trim();

    if (!idHoSoCoSo || !maHienTai) {
        showModal('Hồ sơ này chưa nối mã nào nên không có gì để gỡ.', 'Cảnh báo');
        return;
    }

    const nhac = 'Gỡ mã ' + maHienTai + ' khỏi hồ sơ này?\n\n'
        + 'Tài liệu và đợt khám đi kèm mã này sẽ bị gỡ theo (có sao lưu để khôi phục được). '
        + 'Hồ sơ vẫn còn, chỉ trở về dạng tự khai.';
    const dongY = await showConfirm(nhac, 'Gỡ mã bệnh nhân?', 'Gỡ đồng bộ', true);
    if (!dongY) return;

    const btn = document.getElementById('modalSuaBtnGoNoi');
    if (btn) { btn.disabled = true; btn.innerText = 'Đang gỡ...'; }

    try {
        const resp = await fetch('/Admin/TaiKhoan/GoNoiHoSo', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ idHoSoCoSo: parseInt(idHoSoCoSo) })
        });
        const res = await resp.json();

        if (res.success) {
            inpMa.value = '';
            document.getElementById('modalSuaIdHoSoCoSo').value = '';
            capNhatTrangThaiKhoaModal(false);

            const id = document.getElementById('modalSuaId').value;
            const tr = document.getElementById('hoso-row-' + id);
            if (tr) {
                tr.setAttribute('data-mabn', '');
                tr.setAttribute('data-idhosocoso', '');
                const badge = tr.querySelector('.val-mabn');
                if (badge) badge.innerText = tr.getAttribute('data-tencoso') || 'Chưa nối';
            }

            showToast(res.message || 'Đã gỡ đồng bộ.', 'success');
        } else {
            showToast(res.message || 'Không gỡ đồng bộ được.', 'error');
        }
    } catch (e) {
        console.error(e);
        showToast('Lỗi kết nối máy chủ khi gỡ đồng bộ.', 'error');
    } finally {
        if (btn) { btn.disabled = false; btn.innerText = 'Gỡ đồng bộ'; }
    }
}

// Xóa hồ sơ (AJAX)
async function xoaThongTinHoSo(id, tenBN, accountId) {
    const dongY = await showConfirm(`Bạn có chắc chắn muốn xóa hồ sơ "${tenBN}" này không?`, 'Xóa hồ sơ bệnh nhân?', 'Xóa hồ sơ', true);
    if (!dongY) {
        return;
    }

    try {
        const resp = await fetch('/Admin/TaiKhoan/XoaHoSo', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ id: id, idTaiKhoan: accountId })
        });

        const res = await resp.json();
        if (res.success) {
            const tr = document.getElementById('hoso-row-' + id);
            if (tr) {
                tr.remove();
            }

            // Cập nhật số đếm hồ sơ trên badge
            capNhatSoLuongHoSo(accountId);

            showToast(res.message || 'Đã xóa hồ sơ thành công.', 'success');
        } else {
            showToast(res.message || 'Không thể xóa hồ sơ.', 'error');
        }
    } catch (e) {
        console.error(e);
        showToast('Lỗi kết nối khi xóa hồ sơ.', 'error');
    }
}

// Tạo hồ sơ mới (AJAX)
async function taoHoSoMoi(accountId) {
    const tenEl = document.getElementById('new-ten-' + accountId);
    const cccdEl = document.getElementById('new-cccd-' + accountId);
    const sdtEl = document.getElementById('new-sdt-' + accountId);
    const nsEl = document.getElementById('new-ngaysinh-' + accountId);
    const gtEl = document.getElementById('new-gioitinh-' + accountId);

    const tenBN = tenEl?.value?.trim();
    const cccd = cccdEl?.value?.trim();
    const idCoSo = parseInt(locCoSoDangChon() || '0') || null;
    const sdt = sdtEl?.value?.trim();
    const ngaySinhRaw = nsEl?.value?.trim();
    const gioiTinh = gtEl?.value;

    if (!tenBN) {
        showModal('Vui lòng nhập họ và tên hồ sơ.', 'Cảnh báo');
        return;
    }

    if (!cccd) {
        showModal('Vui lòng nhập số căn cước công dân.', 'Cảnh báo');
        return;
    }

    let ngaySinhIso = null;
    if (ngaySinhRaw) {
        const m = moment(ngaySinhRaw, ['DD/MM/YYYY', 'DD-MM-YYYY', 'YYYY-MM-DD']);
        if (m.isValid()) {
            if (m.isAfter(moment(), 'day')) {
                showModal('Ngày sinh không được lớn hơn ngày hiện tại.', 'Cảnh báo');
                return;
            }
            ngaySinhIso = m.format('YYYY-MM-DD');
        } else {
            showModal('Ngày sinh không hợp lệ (định dạng dd/mm/yyyy).', 'Cảnh báo');
            return;
        }
    }

    try {
        const resp = await fetch('/Admin/TaiKhoan/TaoHoSo', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                idTaiKhoan: accountId,
                tenBN: tenBN,
                cccd: cccd,
                idCoSo: idCoSo,
                sdt: sdt,
                ngaySinh: ngaySinhIso,
                gioiTinh: gioiTinh
            })
        });

        const res = await resp.json();
        if (res.success) {
            const d = res.data;
            const tbody = document.getElementById('tbody-hoso-' + accountId);
            const emptyRow = document.getElementById('empty-row-' + accountId);
            if (emptyRow) emptyRow.remove();

            const newRow = document.createElement('tr');
            newRow.id = 'hoso-row-' + d.id;
            newRow.setAttribute('data-id', d.id);
            newRow.setAttribute('data-account-id', accountId);
            newRow.setAttribute('data-ten', d.tenBN);
            newRow.setAttribute('data-cccd', d.cccd);
            newRow.setAttribute('data-sdt', d.sdt);
            newRow.setAttribute('data-ngaysinh', d.ngaySinh ? moment(d.ngaySinh).format('YYYY-MM-DD') : '');
            newRow.setAttribute('data-gioitinh', d.gioiTinh);
            newRow.setAttribute('data-mabn', '');
            newRow.setAttribute('data-idhosocoso', d.idHoSoCoSo || '');
            newRow.setAttribute('data-idcoso', d.idCoSo || '');
            newRow.setAttribute('data-tencoso', d.tenCoSo || '');

            const ngaySinhHienThi = d.ngaySinhVn || (d.ngaySinh ? moment(d.ngaySinh).format('DD/MM/YYYY') : '—');
            const badgeCoSoText = d.tenCoSo ? d.tenCoSo : 'Chưa nối';

            newRow.innerHTML = `
                <td>
                    <span class="val-ten fw-bold text-dark">${d.tenBN}</span>
                </td>
                <td class="text-center">
                    <span class="val-cccd font-monospace">${d.cccd || '—'}</span>
                </td>
                <td class="text-center">
                    <span class="val-sdt">${d.sdt || '—'}</span>
                </td>
                <td class="text-center">
                    <span class="val-ngaysinh">${ngaySinhHienThi}</span>
                </td>
                <td class="text-center">
                    <span class="val-gioitinh">${d.gioiTinhVn || 'Khác'}</span>
                </td>
                <td class="text-center">
                    <span class="val-mabn badge bg-light text-secondary border">${badgeCoSoText}</span>
                </td>
                <td class="text-center">
                    <div class="hoso-action-group">
                        <button type="button" class="btn-hoso-icon btn-hoso-edit" onclick="moModalSuaHoSo(${d.id}, ${accountId})" title="Sửa hồ sơ">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path></svg>
                        </button>
                        <button type="button" class="btn-hoso-icon btn-hoso-delete" onclick="xoaThongTinHoSo(${d.id}, '${d.tenBN.replace(/'/g, "\\'")}', ${accountId})" title="Xóa hồ sơ">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path><line x1="10" y1="11" x2="10" y2="17"></line><line x1="14" y1="11" x2="14" y2="17"></line></svg>
                        </button>
                    </div>
                </td>
            `;

            tbody.appendChild(newRow);

            // Reset form
            if (tenEl) tenEl.value = '';
            if (cccdEl) cccdEl.value = '';
            if (nsEl) {
                nsEl.value = '';
                if ($(nsEl).data('DateTimePicker')) {
                    $(nsEl).data('DateTimePicker').clear();
                }
            }
            toggleFormThem(accountId);

            capNhatSoLuongHoSo(accountId);

            showToast(res.message || 'Đã tạo hồ sơ mới thành công.', 'success');
        } else {
            if (res.isWarning) {
                showModal(res.message || 'Dữ liệu không hợp lệ.', 'Cảnh báo');
            } else {
                showToast(res.message || 'Không thể tạo hồ sơ.', 'error');
            }
        }
    } catch (e) {
        console.error(e);
        showToast('Lỗi kết nối khi tạo hồ sơ.', 'error');
    }
}

function capNhatSoLuongHoSo(accountId) {
    const tbody = document.getElementById('tbody-hoso-' + accountId);
    const rows = tbody ? tbody.querySelectorAll('tr[data-id]') : [];
    const count = rows.length;

    const countSpan = document.getElementById('badge-count-' + accountId);
    if (countSpan) {
        countSpan.innerText = count;
    }

    const countLabel = document.getElementById('count-label-' + accountId);
    if (countLabel) {
        countLabel.innerText = `${count} hồ sơ`;
    }

    if (count === 0 && tbody) {
        tbody.innerHTML = `
            <tr class="empty-hoso-tr" id="empty-row-${accountId}">
                <td colspan="7" class="text-center py-4 text-muted">
                    <div>📋</div>
                    <div>Chưa có hồ sơ bệnh nhân nào liên kết với tài khoản này.</div>
                </td>
            </tr>
        `;
    }
}

// ==================== PHÂN TRANG AJAX CHO TÀI KHOẢN ====================
let dangTai = false;

function doiSoLuongDong(val) {
    const ps = parseInt(val, 10);
    if ([20, 50, 100, 500].includes(ps)) {
        soLuongMoiTrang = ps;
        const filterPs = document.getElementById('filterPageSize');
        if (filterPs) filterPs.value = ps;
        const curLabel = document.getElementById('currentSelectedPageSize');
        if (curLabel) curLabel.innerText = ps;
        const selPs = document.getElementById('selectPageSize');
        if (selPs) selPs.value = ps;
        document.querySelectorAll('.page-size-dropdown-menu .dropdown-item').forEach(btn => {
            const btnVal = parseInt(btn.getAttribute('data-value'), 10);
            btn.classList.toggle('active', btnVal === ps);
        });
        taiDanhSachTaiKhoan(1, true);
    }
}

function capNhatThanhPhanTrang(page, maxPages, totalItems, daLoc, pageSize) {
    trangHienTai = page;
    tongSoTrang = maxPages;
    if (pageSize && [20, 50, 100, 500].includes(pageSize)) {
        soLuongMoiTrang = pageSize;
        const curLabel = document.getElementById('currentSelectedPageSize');
        if (curLabel) curLabel.innerText = pageSize;
        const selPs = document.getElementById('selectPageSize');
        if (selPs) selPs.value = pageSize;
        const filterPs = document.getElementById('filterPageSize');
        if (filterPs) filterPs.value = pageSize;
        document.querySelectorAll('.page-size-dropdown-menu .dropdown-item').forEach(btn => {
            const btnVal = parseInt(btn.getAttribute('data-value'), 10);
            btn.classList.toggle('active', btnVal === pageSize);
        });
    }

    const paginationContainer = document.getElementById('taiKhoanPaginationContainer');
    const paginationNav = document.getElementById('taiKhoanPaginationNav');
    const countLabel = document.getElementById('taiKhoanCountLabel');
    const hintLabel = document.getElementById('taiKhoanHintLabel');
    const currentText = document.getElementById('paginationCurrentText');
    const btnFirst = document.getElementById('btnPageFirst');
    const btnPrev = document.getElementById('btnPagePrev');
    const btnNext = document.getElementById('btnPageNext');
    const btnLast = document.getElementById('btnPageLast');

    if (!daLoc) {
        if (paginationContainer) paginationContainer.style.display = 'none';
        if (countLabel) countLabel.innerHTML = 'Vui lòng chọn cơ sở (hoặc nhập tiêu chí tìm kiếm) và bấm <strong>Lọc</strong> để xem danh sách tài khoản';
        if (hintLabel) hintLabel.innerText = '';
        return;
    }

    if (countLabel) {
        countLabel.innerHTML = totalItems > 0 ? `<strong>${totalItems.toLocaleString('vi-VN')}</strong> tài khoản` : 'Không tìm thấy tài khoản nào phù hợp';
    }
    if (hintLabel) {
        hintLabel.innerText = totalItems > 0 ? 'Bấm vào từng dòng để mở/đóng danh sách hồ sơ' : '';
    }

    if (totalItems > 0) {
        if (paginationContainer) paginationContainer.style.display = 'block';
        if (paginationNav) {
            paginationNav.style.display = maxPages > 1 ? 'flex' : 'none';
        }
        if (currentText) {
            currentText.innerHTML = `Trang <strong>${page}</strong>/<strong>${maxPages}</strong>`;
        }
        if (btnFirst) btnFirst.disabled = (page <= 1);
        if (btnPrev) btnPrev.disabled = (page <= 1);
        if (btnNext) btnNext.disabled = (page >= maxPages);
        if (btnLast) btnLast.disabled = (page >= maxPages);
    } else {
        if (paginationContainer) paginationContainer.style.display = 'none';
    }
}

async function taiDanhSachTaiKhoan(page, updateUrl = true) {
    if (dangTai) return;
    dangTai = true;

    const overlay = document.getElementById('tableLoadingOverlay');
    if (overlay) overlay.style.display = 'flex';

    // Bật loadingpage chuẩn HisSoft
    if (typeof showPageloader === 'function') {
        showPageloader();
    }

    const form = document.getElementById('taiKhoanFilterForm');
    const params = new URLSearchParams(form ? new FormData(form) : '');
    params.set('page', page);
    params.set('pageSize', soLuongMoiTrang);

    const startTime = Date.now();

    try {
        const url = `/Admin/TaiKhoan?${params.toString()}`;
        const resp = await fetch(url, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        if (resp.ok) {
            const html = await resp.text();
            const tbody = document.getElementById('accountTableBody');
            if (tbody) {
                tbody.innerHTML = html;
                khoiTaoDatePicker('#accountTableBody');
            }

            const totalPagesHeader = resp.headers.get('X-Total-Pages');
            const totalItemsHeader = resp.headers.get('X-Total-Items');
            const currentPageHeader = resp.headers.get('X-Current-Page');
            const pageSizeHeader = resp.headers.get('X-Page-Size');
            const daLocHeader = resp.headers.get('X-Da-Loc');

            const maxPages = totalPagesHeader ? parseInt(totalPagesHeader, 10) : 1;
            const totalItems = totalItemsHeader ? parseInt(totalItemsHeader, 10) : 0;
            const curPage = currentPageHeader ? parseInt(currentPageHeader, 10) : page;
            const curPageSize = pageSizeHeader ? parseInt(pageSizeHeader, 10) : soLuongMoiTrang;
            const daLoc = daLocHeader === '1';

            capNhatThanhPhanTrang(curPage, maxPages, totalItems, daLoc, curPageSize);

            if (updateUrl && window.history && window.history.pushState) {
                window.history.pushState({ page: curPage, pageSize: curPageSize }, '', url);
            }

            // Cuộn khung bảng lên đầu danh sách
            const scrollBox = document.getElementById('taiKhoanTableScrollContainer');
            if (scrollBox) {
                scrollBox.scrollTop = 0;
            }

            // Cuộn mượt lên đầu bảng nếu màn hình đang ở xa
            const tableMain = document.getElementById('taiKhoanMainTable');
            if (tableMain) {
                const rect = tableMain.getBoundingClientRect();
                if (rect.top < 60) {
                    window.scrollTo({ top: window.scrollY + rect.top - 80, behavior: 'smooth' });
                }
            }
        } else {
            showToast('Lỗi tải danh sách tài khoản từ máy chủ.', 'error');
        }
    } catch (err) {
        console.error('Lỗi tải danh sách tài khoản:', err);
        showToast('Lỗi kết nối khi tải danh sách tài khoản.', 'error');
    } finally {
        const elapsed = Date.now() - startTime;
        const remaining = Math.max(0, 200 - elapsed);
        setTimeout(() => {
            if (typeof hidePageloader === 'function') {
                hidePageloader();
            }
            if (overlay) overlay.style.display = 'none';
            dangTai = false;
        }, remaining);
    }
}

function chuyenTrang(action) {
    let targetPage = trangHienTai;
    if (action === 'first') {
        targetPage = 1;
    } else if (action === 'prev') {
        targetPage = trangHienTai - 1;
    } else if (action === 'next') {
        targetPage = trangHienTai + 1;
    } else if (action === 'last') {
        targetPage = tongSoTrang;
    } else if (typeof action === 'number') {
        targetPage = action;
    } else {
        targetPage = parseInt(action, 10);
    }

    if (isNaN(targetPage) || targetPage < 1 || targetPage > tongSoTrang || targetPage === trangHienTai) {
        return;
    }
    taiDanhSachTaiKhoan(targetPage, true);
}

// Xử lý nút Back / Forward của trình duyệt
window.addEventListener('popstate', function (event) {
    const urlParams = new URLSearchParams(window.location.search);
    const p = parseInt(urlParams.get('page') || '1', 10);
    const ps = parseInt(urlParams.get('pageSize') || '50', 10);
    if ([20, 50, 100, 500].includes(ps)) {
        soLuongMoiTrang = ps;
        const curLabel = document.getElementById('currentSelectedPageSize');
        if (curLabel) curLabel.innerText = ps;
        const selPs = document.getElementById('selectPageSize');
        if (selPs) selPs.value = ps;
        const filterPs = document.getElementById('filterPageSize');
        if (filterPs) filterPs.value = ps;
        document.querySelectorAll('.page-size-dropdown-menu .dropdown-item').forEach(btn => {
            const btnVal = parseInt(btn.getAttribute('data-value'), 10);
            btn.classList.toggle('active', btnVal === ps);
        });
    }
    taiDanhSachTaiKhoan(p, false);
});
