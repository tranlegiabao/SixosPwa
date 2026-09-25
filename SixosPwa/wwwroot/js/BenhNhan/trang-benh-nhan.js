function toggleBnMenu(e) {
    e.stopPropagation();
    const menu = document.getElementById('bnMenuDropdown');
    if (menu) {
        menu.style.display = (menu.style.display === 'block') ? 'none' : 'block';
    }
}

document.addEventListener('click', function(e) {
    const menu = document.getElementById('bnMenuDropdown');
    if (menu && menu.style.display === 'block') {
        menu.style.display = 'none';
    }
});
// ═══════════════════════════════════════════════════════════════════
// O *Lich kham cua toi* — DU LIEU THAT (ADR 0025, dong no ADR 0023)
//
// 🔴 Khoi du lieu mau hardcode cu (7 muc, co ca LOI DAN Y TE BIA) da bi
// xoa han. No la mon no ma chinh ADR 0023 tu dat dieu kien: noi cua doc
// cua HIS truoc khi bat cho co so that nao.
//
// Nap bang AJAX KHI NGUOI DUNG BAM mo o, khong phai luc render trang:
// mot tai khoan N ho so thi mo trang mot lan se thanh N cuoc goi sang
// may khach.
// ═══════════════════════════════════════════════════════════════════

// Ba trang thai TACH BAC — xem chu thich o LichKhamController.
window.lichTrangThai = 'CHUA_TAI';
window.dsLichKham = [];
var daTaiLich = false;

async function taiLichKham(batBuocTaiLai) {
    if (daTaiLich && !batBuocTaiLai) return;

    try {
        const res = await fetch('/benh-nhan/lich', { credentials: 'same-origin' });
        if (!res.ok) throw new Error('HTTP ' + res.status);

        const data = await res.json();

        window.lichTrangThai = data.trangThai || 'CHUA_HOI_DUOC';
        window.dsLichKham = (data.the || []).map(function (x, idx) {
            return {
                id: idx + 1,
                ngayIso: x.ngayIso,
                ngayHienThi: x.ngayHienThi,
                nhom: x.nhom,
                noiDung: x.noiDung,
                chuyenKhoa: x.chuyenKhoa,
                moi: x.moi
            };
        });

        daTaiLich = true;
    } catch (e) {
        // Im lang bi dich thanh "ban khong co hen" la dung bay cua Dot 3.
        window.lichTrangThai = 'CHUA_HOI_DUOC';
        window.dsLichKham = [];
        daTaiLich = true;
    }
}

/// Doi *Moc xem lich* — goi khi nguoi dung MO o (ADR 0025).
async function doiMocXemLich() {
    try {
        const token = document.querySelector('#formDaXemLich input[name="__RequestVerificationToken"]');
        if (!token) return;

        await fetch('/benh-nhan/lich/da-xem', {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'RequestVerificationToken': token.value }
        });

        // Doi moc xong thi huy hieu tren MAN phai tat theo ngay, khong doi
        // lan mo sau — nguoi dung vua nhin thay chung.
        window.dsLichKham.forEach(function (x) { x.moi = false; });
    } catch (e) { /* Doi moc that bai chi lam huy hieu con lai, khong pha gi */ }
}

let cheDoXemTatCaThang = false;
let offsetThoiGian = 0;
// Dang o che do loc theo TUAN hay khong. Mac dinh FALSE = xem tat ca; chi bat
// len khi nguoi dung tu bam mui ten ‹ ›. Xem chu thich o thanh dieu huong.
let dangLocTuan = false;
let customKhoangChon = null;
let tuKhoaTimKiemVcb = '';
let soDongHienThiLich = 20;
let dangCuonTaiLich = false;

// Biến lịch tháng
let calNam = 2026;
let calThang = 9;
let calSelectedNgayIso = '2026-09-08';

(function initCalTime() {
    const h = new Date();
    calNam = h.getFullYear();
    calThang = h.getMonth() + 1;
    calSelectedNgayIso = formatIso(h);
})();

function layNgayAmLichMoPhong(nam, thang, ngay) {
    if (nam === 2026 && thang === 9) {
        if (ngay < 11) return `${ngay + 20}/7`;
        const am = ngay - 10;
        if (am === 15) return '15/8 🌕';
        return `${am}/8`;
    }
    if (nam === 2026 && thang === 10) {
        if (ngay < 11) return `${ngay + 20}/8`;
        const am = ngay - 10;
        if (ngay === 20) return '20/10 🌸';
        return `${am}/9`;
    }
    if (nam === 2026 && thang === 11) {
        if (ngay < 10) return `${ngay + 21}/9`;
        return `${ngay - 9}/10`;
    }
    return `${(ngay % 30) + 1}/AL`;
}

function formatIso(dt) {
    const y = dt.getFullYear();
    const m = String(dt.getMonth() + 1).padStart(2, '0');
    const d = String(dt.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
}

function formatVn(dt) {
    const d = String(dt.getDate()).padStart(2, '0');
    const m = String(dt.getMonth() + 1).padStart(2, '0');
    const y = dt.getFullYear();
    return `${d}/${m}/${y}`;
}

function timNgayCanKeIso() {
    const todayIso = formatIso(new Date());
    const ds = window.dsLichKham || [];
    
    const tuongLai = ds.filter(x => x.ngayIso >= todayIso).sort((a, b) => a.ngayIso.localeCompare(b.ngayIso));
    if (tuongLai.length > 0) {
        return tuongLai[0].ngayIso;
    }
    if (ds.length > 0) {
        return ds[0].ngayIso;
    }
    return todayIso;
}

// ==================== RENDER LỊCH THÁNG (CHUẨN THEO ẢNH USER) ====================
function renderLichThangUi(animDir) {
    const tieuDeEl = document.getElementById('vcbCalTieuDeThang');
    if (tieuDeEl) {
        tieuDeEl.innerText = `TH${String(calThang).padStart(2, '0')}`;
    }

    const badgeHomNay = document.getElementById('vcbCalNgayHienTaiBadge');
    if (badgeHomNay) {
        badgeHomNay.innerText = new Date().getDate();
    }

    const gridEl = document.getElementById('vcbCalNgayGrid');
    if (!gridEl) return;
    gridEl.innerHTML = '';

    // Thêm animation chuyển động mượt mà khi vuốt hoặc bấm nút
    if (animDir === 'left') {
        gridEl.classList.remove('vcb-slide-left', 'vcb-slide-right');
        void gridEl.offsetWidth; // Reflow
        gridEl.classList.add('vcb-slide-left');
    } else if (animDir === 'right') {
        gridEl.classList.remove('vcb-slide-left', 'vcb-slide-right');
        void gridEl.offsetWidth; // Reflow
        gridEl.classList.add('vcb-slide-right');
    }

    const todayIso = formatIso(new Date());
    const ngayCanKeIso = timNgayCanKeIso();
    const dsLich = window.dsLichKham || [];

    const firstDayOfMonth = new Date(calNam, calThang - 1, 1);
    const lastDayOfMonth = new Date(calNam, calThang, 0);

    const startGridDate = new Date(firstDayOfMonth);
    const dayOfWeek = startGridDate.getDay();
    const diffToMonday = (dayOfWeek === 0 ? -6 : 1) - dayOfWeek;
    startGridDate.setDate(startGridDate.getDate() + diffToMonday);

    // Xác định vẽ 35 hay 42 ô
    let testDate = new Date(startGridDate);
    testDate.setDate(testDate.getDate() + 34);
    const totalCells = (testDate < lastDayOfMonth) ? 42 : 35;

    let curDate = new Date(startGridDate);
    for (let i = 0; i < totalCells; i++) {
        const cellDate = new Date(curDate);
        const cellIso = formatIso(cellDate);
        const cellDay = cellDate.getDate();
        const isCurrentMonth = (cellDate.getMonth() === calThang - 1);

        const lichHenNgay = dsLich.filter(x => x.ngayIso === cellIso);
        const coHen = lichHenNgay.length > 0;

        let stateClass = '';
        let tagText = '';

        if (cellIso === ngayCanKeIso) {
            stateClass = 'cal-can-ke';
            tagText = lichHenNgay.length > 0 ? lichHenNgay[0].chuyenKhoa : 'Lịch hẹn';
        } else if (cellIso < todayIso) {
            stateClass = 'cal-da-qua';
            if (coHen) {
                tagText = 'Đã khám';
            }
        } else if (coHen) {
            stateClass = 'cal-co-hen';
            tagText = lichHenNgay[0].chuyenKhoa;
        }

        if (!isCurrentMonth) {
            stateClass += ' cal-ngoai-thang';
        }

        if (cellIso === calSelectedNgayIso) {
            stateClass += ' cal-selected';
        }

        const lunarText = layNgayAmLichMoPhong(cellDate.getFullYear(), cellDate.getMonth() + 1, cellDay);

        const cellDiv = document.createElement('div');
        cellDiv.className = `vcb-cal-cell ${stateClass}`;
        cellDiv.onclick = () => chonNgayLich(cellIso);

        cellDiv.innerHTML = `
            <span class="vcb-cal-solar">${cellDay}</span>
            <span class="vcb-cal-lunar">${lunarText}</span>
            ${tagText ? `<span class="vcb-cal-tag">${tagText}</span>` : ''}
        `;

        gridEl.appendChild(cellDiv);
        curDate.setDate(curDate.getDate() + 1);
    }

    // Không cần thanh nút bấm dưới cùng
}

function capNhatBottomBarLich() {
    const bar = document.getElementById('vcbCalBottomBar');
    if (!bar) return;

    // Hop nhat 09/09: giu ban cua Dot 4 (doc du lieu THAT) thay vi ban cua bk
    // (xoa trang thanh nay). Phan tu #vcbCalBottomBar hien KHONG co trong
    // markup ca hai ben nen ham nay dang la ma chet — giu ban con chay duoc de
    // thanh do quay lai luc nao thi no dung ngay.
    const dsLich = window.dsLichKham || [];
    const lichHen = dsLich.find(x => x.ngayIso === calSelectedNgayIso);
    const todayIso = formatIso(new Date());
    const ngayCanKeIso = timNgayCanKeIso();

    const dParts = calSelectedNgayIso.split('-');
    const ngayVn = `${dParts[2]}/${dParts[1]}/${dParts[0]}`;

    if (calSelectedNgayIso === ngayCanKeIso && lichHen) {
        bar.innerHTML = `
            <button type="button" class="vcb-cal-btn-action can-ke" onclick="xemTinTheoNgay('${calSelectedNgayIso}')">
                <span>🔴 Lịch cận kề ${ngayVn}: ${lichHen.chuyenKhoa || ''} ➔</span>
            </button>
        `;
    } else if (lichHen && calSelectedNgayIso >= todayIso) {
        bar.innerHTML = `
            <button type="button" class="vcb-cal-btn-action co-hen" onclick="xemTinTheoNgay('${calSelectedNgayIso}')">
                <span>🟢 Lịch hẹn ${ngayVn}: ${lichHen.chuyenKhoa || ''} ➔</span>
            </button>
        `;
    } else if (lichHen && calSelectedNgayIso < todayIso) {
        bar.innerHTML = `
            <button type="button" class="vcb-cal-btn-action da-qua" onclick="xemTinTheoNgay('${calSelectedNgayIso}')">
                <span>⚪ Đã qua (${ngayVn}): ${lichHen.chuyenKhoa || ''} ➔</span>
            </button>
        `;
    } else {
        bar.innerHTML = `
            <button type="button" class="vcb-cal-btn-action khong-hen" onclick="dongModalLichThang()">
                <span>Thêm / Xem lịch vào ngày ${dParts[2]} Th${dParts[1]} +</span>
            </button>
        `;
    }
}

function chonNgayLich(ngayIso) {
    calSelectedNgayIso = ngayIso;
    // 🔴 Hop nhat 09/09: bk con doc window.danhSachThongBaoLich — mang du lieu
    // BIA ma Dot 4 da xoa han (ADR 0025). De nguyen thi no luon undefined =>
    // bam vao ngay CO hen cung khong mo duoc gi, im lang.
    const dsLich = window.dsLichKham || [];
    const lichHen = dsLich.find(x => x.ngayIso === ngayIso);
    if (lichHen) {
        xemTinTheoNgay(ngayIso);
        return;
    }
    renderLichThangUi();
}

function chuyenThangLich(delta, animDir) {
    calThang += delta;
    if (calThang > 12) {
        calThang = 1;
        calNam++;
    } else if (calThang < 1) {
        calThang = 12;
        calNam--;
    }
    renderLichThangUi(animDir || (delta > 0 ? 'left' : 'right'));
}

function veHomNayLich() {
    const h = new Date();
    const prevThang = calThang;
    const prevNam = calNam;
    calNam = h.getFullYear();
    calThang = h.getMonth() + 1;
    calSelectedNgayIso = formatIso(h);
    
    let anim = 'left';
    if (calNam < prevNam || (calNam === prevNam && calThang < prevThang)) {
        anim = 'right';
    }
    renderLichThangUi(anim);
}

// ==================== CỬ CHỈ VUỐT TOÀN DIỆN (TOUCH & MOUSE SWIPE) ====================
(function initGlobalSwipe() {
    let startX = 0;
    let startY = 0;
    let curX = 0;
    let curY = 0;
    let isTouching = false;

    function isCalendarOpen() {
        const el = document.getElementById('vcbModalLichThang');
        return el && el.style.display !== 'none';
    }

    // 1. Cảm ứng trên điện thoại
    document.addEventListener('touchstart', function(e) {
        if (!isCalendarOpen()) return;
        if (e.touches.length !== 1) return;
        startX = e.touches[0].clientX;
        startY = e.touches[0].clientY;
        curX = startX;
        curY = startY;
        isTouching = true;
    }, { passive: true });

    document.addEventListener('touchmove', function(e) {
        if (!isTouching || !isCalendarOpen() || !e.touches || e.touches.length !== 1) return;
        curX = e.touches[0].clientX;
        curY = e.touches[0].clientY;
    }, { passive: true });

    function handleTouchEnd() {
        if (!isTouching || !isCalendarOpen()) return;
        isTouching = false;

        const deltaX = curX - startX;
        const deltaY = curY - startY;

        // Chỉ cần vuốt dứt khoát > 20px
        if (Math.abs(deltaX) > 20 && Math.abs(deltaX) > Math.abs(deltaY) * 0.6) {
            if (deltaX < 0) {
                chuyenThangLich(1, 'left'); // Vuốt sang trái 👈 -> Tháng 10
            } else {
                chuyenThangLich(-1, 'right'); // Vuốt sang phải 👉 -> Tháng 8
            }
        }
    }

    document.addEventListener('touchend', handleTouchEnd, { passive: true });
    document.addEventListener('touchcancel', handleTouchEnd, { passive: true });

    // 2. Kéo chuột trên máy tính
    let mouseStartX = 0;
    let mouseStartY = 0;
    let mouseCurX = 0;
    let mouseCurY = 0;
    let isMouseDown = false;

    document.addEventListener('mousedown', function(e) {
        if (!isCalendarOpen()) return;
        if (e.target.closest('button')) return;
        mouseStartX = e.clientX;
        mouseStartY = e.clientY;
        mouseCurX = mouseStartX;
        mouseCurY = mouseStartY;
        isMouseDown = true;
    });

    document.addEventListener('mousemove', function(e) {
        if (!isMouseDown || !isCalendarOpen()) return;
        mouseCurX = e.clientX;
        mouseCurY = e.clientY;
    });

    document.addEventListener('mouseup', function(e) {
        if (!isMouseDown || !isCalendarOpen()) return;
        isMouseDown = false;
        const dX = mouseCurX - mouseStartX;
        const dY = mouseCurY - mouseStartY;

        if (Math.abs(dX) > 25 && Math.abs(dX) > Math.abs(dY) * 0.6) {
            if (dX < 0) {
                chuyenThangLich(1, 'left');
            } else {
                chuyenThangLich(-1, 'right');
            }
        }
    });
})();

function moModalLichThang() {
    const modal = document.getElementById('vcbModalLichThang');
    if (modal) {
        modal.style.display = 'flex';
        renderLichThangUi();
    }
}

function dongModalLichThang() {
    const modal = document.getElementById('vcbModalLichThang');
    if (modal) {
        modal.style.display = 'none';
    }
}

function xemTinTheoNgay(ngayIso) {
    dongModalLichThang();
    customKhoangChon = {
        startIso: ngayIso,
        endIso: ngayIso,
        chuoiHienThi: `Ngày ${ngayIso.split('-')[2]}/${ngayIso.split('-')[1]}/${ngayIso.split('-')[0]}`
    };
    cheDoXemTatCaThang = false;

    const btn = document.getElementById('vcbBtnXemTatCa');
    const textEl = document.getElementById('vcbTextTatCa');
    if (btn) btn.classList.add('active');
    if (textEl) textEl.innerText = `Lọc: ${customKhoangChon.chuoiHienThi} (Bấm để xem lịch tháng)`;

    capNhatKhoangThoiGianUi();
    renderDanhSachVcb();
}

// ==================== CÁC HÀM XỬ LÝ TUẦN & MODAL THÔNG BÁO ====================
function layCacTuanTrongThang(nam, thang1Indexed) {
    const firstDayOfMonth = new Date(nam, thang1Indexed - 1, 1);
    const lastDayOfMonth = new Date(nam, thang1Indexed, 0);
    
    const weeks = [];
    let currentMonday = new Date(firstDayOfMonth);
    const dayOfWeek = currentMonday.getDay();
    const diffToMonday = (dayOfWeek === 0 ? -6 : 1) - dayOfWeek;
    currentMonday.setDate(currentMonday.getDate() + diffToMonday);
    currentMonday.setHours(0, 0, 0, 0);

    let weekIndex = 1;
    while (currentMonday <= lastDayOfMonth || (currentMonday.getMonth() === thang1Indexed - 1)) {
        const currentSunday = new Date(currentMonday);
        currentSunday.setDate(currentMonday.getDate() + 6);
        currentSunday.setHours(23, 59, 59, 999);

        if (currentSunday >= firstDayOfMonth && currentMonday <= lastDayOfMonth) {
            weeks.push({
                tenTuan: `Tuần ${weekIndex}`,
                monday: new Date(currentMonday),
                sunday: new Date(currentSunday),
                mondayIso: formatIso(currentMonday),
                sundayIso: formatIso(currentSunday),
                chuoiHienThi: `${formatVn(currentMonday)} đến ${formatVn(currentSunday)}`
            });
            weekIndex++;
        }

        currentMonday.setDate(currentMonday.getDate() + 7);
        if (currentMonday > lastDayOfMonth && currentMonday.getMonth() !== thang1Indexed - 1) {
            break;
        }
    }
    return weeks;
}

function layKhoangThoiGian(offset) {
    if (customKhoangChon) {
        return customKhoangChon;
    }

    const homNay = new Date();

    if (cheDoXemTatCaThang) {
        const targetMonth = new Date(homNay.getFullYear(), homNay.getMonth() + offset, 1);
        const startMonth = new Date(targetMonth.getFullYear(), targetMonth.getMonth(), 1);
        const endMonth = new Date(targetMonth.getFullYear(), targetMonth.getMonth() + 1, 0);
        endMonth.setHours(23, 59, 59, 999);

        const mStr = String(startMonth.getMonth() + 1).padStart(2, '0');
        const yStr = startMonth.getFullYear();

        return {
            startIso: formatIso(startMonth),
            endIso: formatIso(endMonth),
            chuoiHienThi: `Tháng ${mStr}/${yStr} (01/${mStr} đến ${String(endMonth.getDate()).padStart(2, '0')}/${mStr}/${yStr})`
        };
    } else {
        const dayOfWeek = homNay.getDay();
        const diffToMonday = (dayOfWeek === 0 ? -6 : 1) - dayOfWeek;
        
        const monday = new Date(homNay);
        monday.setDate(homNay.getDate() + diffToMonday + (offset * 7));
        monday.setHours(0, 0, 0, 0);

        const sunday = new Date(monday);
        sunday.setDate(monday.getDate() + 6);
        sunday.setHours(23, 59, 59, 999);

        return {
            startIso: formatIso(monday),
            endIso: formatIso(sunday),
            chuoiHienThi: `${formatVn(monday)} đến ${formatVn(sunday)}`
        };
    }
}

function capNhatKhoangThoiGianUi() {
    const el = document.getElementById('vcbChuoiKhoangNgay');
    if (!el) return;

    // Ba trang thai, va thanh nay phai NOI RA dang o trang thai nao. Khong
    // noi ra chinh la loi 09/09: thanh in mot tuan cu the trong khi nguoi
    // dung tuong minh dang xem tat ca.
    if (customKhoangChon) {
        el.innerText = customKhoangChon.chuoiHienThi;
    } else if (dangLocTuan) {
        el.innerText = layKhoangThoiGian(offsetThoiGian).chuoiHienThi;
    } else {
        el.innerText = 'Tất cả lịch khám';
    }
}

function chuyenKhoangThoiGian(direction) {
    customKhoangChon = null;
    soDongHienThiLich = 20;

    // Lan bam mui ten DAU TIEN la VAO che do loc tuan, dung tuan hien tai —
    // khong nhay thang sang tuan sau/truoc. Nguoi dung phai thay minh dang
    // dung o dau roi moi di tiep.
    if (!dangLocTuan) {
        dangLocTuan = true;
        offsetThoiGian = 0;
    } else {
        offsetThoiGian += direction;
    }

    const btn = document.getElementById('vcbBtnXemTatCa');
    const textEl = document.getElementById('vcbTextTatCa');
    if (btn) btn.classList.remove('active');
    if (textEl) textEl.innerText = 'Xem tất cả các tuần trong tháng';

    capNhatKhoangThoiGianUi();
    renderDanhSachVcb();
}

function capNhatBadgeChuaDoc() {
    const soChuaDoc = (window.dsLichKham || []).filter(x => x.moi).length;
    const badgeEl = document.getElementById('vcbBadgeChuaDoc');
    if (badgeEl) {
        badgeEl.innerText = soChuaDoc;
        badgeEl.style.display = soChuaDoc > 0 ? 'inline-block' : 'none';
    }

    const lhBadge = document.getElementById('bnLichHenBadge');
    if (lhBadge) {
        lhBadge.innerText = soChuaDoc;
        lhBadge.style.display = soChuaDoc > 0 ? 'inline-block' : 'none';
    }
}

function renderDanhSachVcb(giuViTriCuon = false) {
    const container = document.getElementById('vcbDanhSachThongBao');
    if (!container) return;

    let ds = window.dsLichKham || [];

    // 🔴 KHONG con loc NGAM theo tuan (09/09). May chu da tra ve dung thu can
    // hien: SAP TOI = hen tuong lai cua HIS, DA KHAM = 50 dot gan nhat cua
    // cong. Loc them mot lop TUAN chay san chi lam mat du lieu that: hen tai
    // kham cach 1-3 thang khong bao gio nam trong tuan hien tai.
    //
    // CHI loc khi NGUOI DUNG tu chon: bam mot ngay tren lich thang
    // (customKhoangChon), hoac bam mui ten ‹ › de vao che do tuan (dangLocTuan).
    // Ca hai deu keo theo bang *Dang xem … / Bo loc* — xem capNhatDangLoc.
    if (customKhoangChon) {
        ds = ds.filter(x => x.ngayIso >= customKhoangChon.startIso
                         && x.ngayIso <= customKhoangChon.endIso);
    } else if (dangLocTuan) {
        const khoang = layKhoangThoiGian(offsetThoiGian);
        ds = ds.filter(x => x.ngayIso >= khoang.startIso && x.ngayIso <= khoang.endIso);
    }

    capNhatKhoangThoiGianUi();
    capNhatDangLoc();

    // Loc theo tu khoa tim kiem neu co
    if (tuKhoaTimKiemVcb.trim() !== '') {
        const kw = tuKhoaTimKiemVcb.toLowerCase();
        ds = ds.filter(x => (x.noiDung || '').toLowerCase().includes(kw) ||
                            (x.ngayHienThi || '').toLowerCase().includes(kw) ||
                            (x.chuyenKhoa || '').toLowerCase().includes(kw));
    }

    window.dsLichKhamLoc = ds;
    const tongSoMuc = ds.length;

    // Phân trang hiển thị 20 dòng mỗi lần
    const dsHienThi = ds.slice(0, soDongHienThiLich);

    const sapToiGoc = ds.filter(x => x.nhom === 'SAP_TOI');
    const daKhamGoc = ds.filter(x => x.nhom === 'DA_KHAM');

    const sapToi = dsHienThi.filter(x => x.nhom === 'SAP_TOI');
    const daKham = dsHienThi.filter(x => x.nhom === 'DA_KHAM');

    let html = '';

    // ── SAP TOI ────────────────────────────────────────────────────
    html += '<div class="vcb-nhom">SẮP TỚI</div>';

    if (window.lichTrangThai === 'CHUA_HOI_DUOC') {
        html += '<div class="vcb-canh-bao">Chưa hỏi được cơ sở lúc này — chưa biết bạn có hẹn hay không. '
              + '<span class="vcb-thu-lai" onclick="thuLaiLich()">Thử lại</span></div>';
    } else if (sapToiGoc.length === 0) {
        html += '<div class="vcb-trang-rong">Bạn chưa có lịch hẹn tái khám nào sắp tới.</div>';
    } else {
        sapToi.forEach(item => { html += veTheLich(item); });
    }

    // ── DA KHAM ────────────────────────────────────────────────────
    if (daKhamGoc.length > 0) {
        html += '<div class="vcb-nhom">ĐÃ KHÁM</div>';
        daKham.forEach(item => { html += veTheLich(item); });
    }

    // Chỉ báo cuộn tải thêm / đã hiển thị toàn bộ
    if (soDongHienThiLich < tongSoMuc) {
        html += '<div id="vcbScrollSentinel" class="vcb-scroll-loading" style="text-align: center; padding: 14px 12px; color: #0284c7; font-size: 13px; font-weight: 500; display: flex; align-items: center; justify-content: center; gap: 8px;">'
              +   '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" style="animation: vcbSpin 0.8s linear infinite;">'
              +     '<circle cx="12" cy="12" r="10" stroke-dasharray="32" stroke-linecap="round" stroke="currentColor" opacity="0.25"></circle>'
              +     '<path d="M12 2a10 10 0 0 1 10 10" stroke="currentColor" stroke-linecap="round"></path>'
              +   '</svg>'
              +   '<span>Đang tải thêm... (hiển thị ' + dsHienThi.length + '/' + tongSoMuc + ')</span>'
              + '</div>';
    } else if (tongSoMuc > 20) {
        html += '<div class="vcb-scroll-end" style="text-align: center; padding: 14px 12px 20px; color: #94a3b8; font-size: 12.5px;">'
              +   'Đã hiển thị tất cả ' + tongSoMuc + ' lịch khám'
              + '</div>';
    }

    container.innerHTML = html;
    if (!giuViTriCuon) {
        container.scrollTop = 0;
    }
    capNhatBadgeChuaDoc();
}

// Lắng nghe sự kiện cuộn container để tự động nạp tiếp 20 dòng
(function initVcbInfiniteScroll() {
    const vcbScrollBox = document.getElementById('vcbDanhSachThongBao');
    if (vcbScrollBox) {
        vcbScrollBox.addEventListener('scroll', function () {
            if (dangCuonTaiLich) return;
            const tongSo = (window.dsLichKhamLoc || []).length;
            if (soDongHienThiLich >= tongSo) return;

            if (vcbScrollBox.scrollTop + vcbScrollBox.clientHeight >= vcbScrollBox.scrollHeight - 60) {
                dangCuonTaiLich = true;
                soDongHienThiLich += 20;
                renderDanhSachVcb(true);
                setTimeout(function () { dangCuonTaiLich = false; }, 150);
            }
        });
    }
})();

/// Mot the. 🔴 KHONG co GIO va KHONG co khoi *Lời dặn*: ca hai deu khong
/// co nguon that. NgayTaiKham ben HIS kieu `date` (khong gio), va
/// QL_ToaThuoc khong co cot LoiDan — bia ra la loi dan Y TE gia.
function veTheLich(item) {
    const badgeHtml = item.moi
        ? '<span class="vcb-badge-moi">MỚI</span>'
        : '<span class="vcb-tick-da-doc" title="Đã xem">✔✔</span>';

    const chan = item.chuyenKhoa
        ? '<span class="vcb-khoa-bac-si">🏥 ' + item.chuyenKhoa + '</span>'
        : '<span class="vcb-khoa-bac-si"></span>';

    return '<div class="vcb-the-thong-bao ' + (item.moi ? 'chua-doc' : 'da-doc') + '">'
         +   '<div class="vcb-the-dau">'
         +     '<span class="vcb-thoi-gian">' + item.ngayHienThi + '</span>'
         +     badgeHtml
         +   '</div>'
         +   '<div class="vcb-noi-dung-text">' + item.noiDung + '</div>'
         +   '<div class="vcb-the-chan">' + chan
         +     '<span>' + (item.moi ? 'Chưa xem' : 'Đã xem') + '</span>'
         +   '</div>'
         + '</div>';
}

async function thuLaiLich() {
    await taiLichKham(true);
    renderDanhSachVcb();
}

/// Bang "dang loc ngay ..." — cai gia phai tra de duoc phep loc.
function capNhatDangLoc() {
    const hop = document.getElementById('vcbDangLoc');
    const chu = document.getElementById('vcbDangLocChu');
    if (!hop || !chu) return;

    if (customKhoangChon) {
        chu.innerText = 'Đang xem ' + customKhoangChon.chuoiHienThi;
        hop.style.display = 'flex';
    } else if (dangLocTuan) {
        // 🔴 Dem CA danh sach chu khong chi tuan dang xem: cau dang gia nhat
        // la "ban con N muc o khoang khac" — no la thu chan nguoi dung ket
        // luan nham rang minh khong co hen.
        const tong = (window.dsLichKham || []).length;
        const khoang = layKhoangThoiGian(offsetThoiGian);
        const trong = (window.dsLichKham || []).filter(
            x => x.ngayIso >= khoang.startIso && x.ngayIso <= khoang.endIso).length;
        const ngoai = tong - trong;

        chu.innerText = 'Đang lọc tuần ' + khoang.chuoiHienThi
                      + (ngoai > 0 ? ' — còn ' + ngoai + ' mục ở tuần khác' : '');
        hop.style.display = 'flex';
    } else {
        hop.style.display = 'none';
    }
}

function boLocNgay() {
    customKhoangChon = null;
    cheDoXemTatCaThang = false;
    dangLocTuan = false;
    offsetThoiGian = 0;
    soDongHienThiLich = 20;
    renderDanhSachVcb();
}

// 🔴 Bam vao MOT the KHONG doi trang thai rieng cua no. Co che la MOT
// MOC THOI GIAN cho ca ho so, khong phai co "da doc" tung muc: lich hen la
// mot TRANG THAI xem di xem lai, khong phai su kien duoc day toi kieu thong
// bao ngan hang. Vi the nhan goc phai la *Chua xem*, khong phai
// "Cham de xem" — cham vao khong lam gi ca.
function danhDauTatCaDaDoc() {
    doiMocXemLich().then(renderDanhSachVcb);
}

function timKiemThongBaoVcb(value) {
    tuKhoaTimKiemVcb = value;
    soDongHienThiLich = 20;
    renderDanhSachVcb();
}

async function moModalLichHen() {
    document.getElementById('modalLichHen').style.display = 'flex';
    document.body.style.overflow = 'hidden';

    // Mo o LUON la xem TAT CA. Bo loc (tuan hay ngay) chi song trong mot lan
    // xem — giu lai qua lan mo sau la giau du lieu cua nguoi ta ma ho khong
    // nho vi sao.
    customKhoangChon = null;
    cheDoXemTatCaThang = false;
    dangLocTuan = false;
    offsetThoiGian = 0;
    soDongHienThiLich = 20;

    renderDanhSachVcb();          // ve khung truoc de man khong trong tron

    await taiLichKham(false);
    renderDanhSachVcb();

    // Doi moc SAU khi da ve: nguoi dung phai NHIN THAY huy hieu MOI mot
    // lan roi no moi tat. Doi truoc thi ho khong bao gio thay.
    if ((window.dsLichKham || []).some(x => x.moi)) {
        setTimeout(function () { doiMocXemLich(); }, 1200);
    }
}

function dongModalLichHen() {
    document.getElementById('modalLichHen').style.display = 'none';
    document.body.style.overflow = '';
}

function xuLyBamNgoaiModal(e) {
    if (e.target.id === 'modalLichHen') {
        dongModalLichHen();
    }
}

document.addEventListener('keydown', function(e) {
    if (e.key === 'Escape') {
        const calModal = document.getElementById('vcbModalLichThang');
        if (calModal && calModal.style.display === 'flex') {
            dongModalLichThang();
        } else {
            dongModalLichHen();
        }
    }
});

document.addEventListener('DOMContentLoaded', capNhatBadgeChuaDoc);
capNhatBadgeChuaDoc();
