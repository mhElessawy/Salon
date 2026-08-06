/* ══════════════════════════════════════════════════════════════════
 * اتفاقية الباقات الإلكترونية — Modal مشترك + لوحة توقيع إلكتروني (Canvas)
 * يُستخدم مع الجزء الجزئي Views/Shared/_PackageAgreementModal.cshtml من:
 * Views/Packages/Index.cshtml, Views/Sales/CreateBarber.cshtml, Views/Sales/CreateMassage.cshtml
 * ══════════════════════════════════════════════════════════════════ */
window.PkgAgreement = (function () {
    var canvas, ctx, drawing = false, hasSignature = false;
    var currentOpts = null;

    function getPos(e) {
        var rect = canvas.getBoundingClientRect();
        var t = (e.touches && e.touches.length) ? e.touches[0] : e;
        return {
            x: (t.clientX - rect.left) * (canvas.width / rect.width),
            y: (t.clientY - rect.top) * (canvas.height / rect.height)
        };
    }

    function startDraw(e) {
        drawing = true;
        hasSignature = true;
        var p = getPos(e);
        ctx.beginPath();
        ctx.moveTo(p.x, p.y);
        e.preventDefault();
    }

    function moveDraw(e) {
        if (!drawing) return;
        var p = getPos(e);
        ctx.strokeStyle = '#1a1a2e';
        ctx.lineWidth = 2.2;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        ctx.lineTo(p.x, p.y);
        ctx.stroke();
        e.preventDefault();
    }

    function endDraw() {
        drawing = false;
    }

    function clearSignature() {
        if (ctx) ctx.clearRect(0, 0, canvas.width, canvas.height);
        hasSignature = false;
    }

    function showError(msg) {
        var el = document.getElementById('pkgAgError');
        if (!el) return;
        el.textContent = msg;
        el.classList.remove('d-none');
    }

    function hideError() {
        var el = document.getElementById('pkgAgError');
        if (el) el.classList.add('d-none');
    }

    function init() {
        canvas = document.getElementById('pkgAgSignatureCanvas');
        if (!canvas) return; // الجزء الجزئي غير مضمّن في هذه الصفحة
        ctx = canvas.getContext('2d');

        canvas.addEventListener('mousedown', startDraw);
        canvas.addEventListener('mousemove', moveDraw);
        window.addEventListener('mouseup', endDraw);
        canvas.addEventListener('touchstart', startDraw, { passive: false });
        canvas.addEventListener('touchmove', moveDraw, { passive: false });
        canvas.addEventListener('touchend', endDraw);

        var clearBtn = document.getElementById('pkgAgClearSignature');
        if (clearBtn) clearBtn.addEventListener('click', clearSignature);

        var submitBtn = document.getElementById('pkgAgSubmitBtn');
        if (submitBtn) submitBtn.addEventListener('click', submit);
    }

    function fillModal(opts) {
        hideError();
        clearSignature();
        var agreeBox = document.getElementById('pkgAgAgree');
        if (agreeBox) agreeBox.checked = false;

        document.getElementById('pkgAgCustomerName').textContent = opts.customerName || '—';
        document.getElementById('pkgAgCustomerPhone').textContent = opts.customerPhone || '';

        var isEn = localStorage.getItem('lang') === 'en';
        var priceStr = parseFloat(opts.price || 0).toFixed(3);
        document.getElementById('pkgAgPackageName').textContent =
            (isEn && opts.packageNameEn) ? opts.packageNameEn : (opts.packageNameAr || '');
        document.getElementById('pkgAgPackageMeta').textContent =
            (opts.sessionCount || 0) + ' جلسة | صلاحية ' + (opts.validityDays || 0) + ' يوم | ' + priceStr + ' د.ك';

        document.getElementById('pkgAgAmount').value = priceStr;
        document.getElementById('pkgAgPaymentMethod').value = 'كاش';

        document.getElementById('pkgAgTermsAr').textContent = opts.termsAr || '';
        document.getElementById('pkgAgTermsEn').textContent = opts.termsEn || '';

        new bootstrap.Modal(document.getElementById('pkgAgreementModal')).show();
    }

    // baseOpts: { customerId, servicePackageId, notes, antiForgeryToken, assignUrl, onSuccess(data), onError(msg) }
    function openForCustomerAndPackage(baseOpts) {
        currentOpts = baseOpts;
        var url = '/Packages/GetAgreementData?customerId=' + encodeURIComponent(baseOpts.customerId) +
            '&servicePackageId=' + encodeURIComponent(baseOpts.servicePackageId);
        fetch(url)
            .then(function (r) { if (!r.ok) throw new Error('تعذر جلب بيانات الاتفاقية'); return r.json(); })
            .then(function (data) {
                currentOpts = Object.assign({}, baseOpts, data);
                fillModal(currentOpts);
            })
            .catch(function (e) {
                if (typeof baseOpts.onError === 'function') baseOpts.onError(e.message);
                else alert(e.message || 'حدث خطأ أثناء جلب بيانات الاتفاقية');
            });
    }

    function submit() {
        hideError();
        if (!currentOpts) return;

        var agree = document.getElementById('pkgAgAgree').checked;
        var amount = parseFloat(document.getElementById('pkgAgAmount').value) || 0;
        var paymentMethod = document.getElementById('pkgAgPaymentMethod').value;

        if (amount <= 0) { showError('الرجاء إدخال مبلغ صحيح'); return; }
        if (!agree) { showError('يجب الموافقة على شروط وأحكام الباقة'); return; }
        if (!hasSignature) { showError('الرجاء التوقيع إلكترونياً قبل إتمام البيع'); return; }

        var signatureData = canvas.toDataURL('image/png');
        var submitBtn = document.getElementById('pkgAgSubmitBtn');
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i> جاري الحفظ...';

        var body = new URLSearchParams({
            customerId: currentOpts.customerId,
            servicePackageId: currentOpts.servicePackageId,
            pricePaid: amount,
            notes: currentOpts.notes || '',
            paymentMethod: paymentMethod,
            agreedToTerms: 'true',
            signatureData: signatureData,
            __RequestVerificationToken: currentOpts.antiForgeryToken
        });

        fetch(currentOpts.assignUrl || '/Packages/AssignPackage', {
            method: 'POST',
            body: body,
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'X-Requested-With': 'XMLHttpRequest'
            }
        })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                if (data && data.success) {
                    data.amountPaid = amount;
                    data.paymentMethod = paymentMethod;
                    bootstrap.Modal.getInstance(document.getElementById('pkgAgreementModal')).hide();
                    if (typeof currentOpts.onSuccess === 'function') currentOpts.onSuccess(data);
                } else {
                    showError((data && data.message) || 'حدث خطأ أثناء الحفظ');
                }
            })
            .catch(function (e) {
                showError('خطأ: ' + e.message);
            })
            .finally(function () {
                submitBtn.disabled = false;
                submitBtn.innerHTML = '<i class="fas fa-check-circle me-1"></i> الموافقة وإتمام البيع';
            });
    }

    document.addEventListener('DOMContentLoaded', init);

    return {
        openForCustomerAndPackage: openForCustomerAndPackage
    };
})();