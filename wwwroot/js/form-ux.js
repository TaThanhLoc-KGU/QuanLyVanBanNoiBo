/* Biểu mẫu thân thiện: thanh bước dính + thanh nút dính đáy — tự áp cho mọi form có .form-section.
   Không cần sửa từng view: đọc tiêu đề (.form-section-header), lời hướng dẫn (data-hint) và các ô bắt buộc (.form-required). */
(function () {
    function ready(fn) { document.readyState !== 'loading' ? fn() : document.addEventListener('DOMContentLoaded', fn); }
    ready(function () {
        var sections = Array.prototype.slice.call(document.querySelectorAll('form .form-section'));
        if (sections.length === 0) return;
        var form = sections[0].closest('form');

        function tieuDe(sec) {
            var h = sec.querySelector('.form-section-header');
            if (!h) return '';
            return Array.prototype.filter.call(h.childNodes, function (n) { return n.nodeType === 3; })
                .map(function (n) { return n.textContent.trim(); }).join(' ').trim();
        }

        // Lời hướng dẫn ngắn dưới tiêu đề (từ data-hint)
        sections.forEach(function (sec, i) {
            sec.id = sec.id || 'phan' + (i + 1);
            var h = sec.querySelector('.form-section-header');
            if (h && sec.dataset.hint && !h.querySelector('.fs-hint')) {
                var p = document.createElement('div');
                p.className = 'fs-hint';
                p.textContent = sec.dataset.hint;
                h.appendChild(p);
            }
        });

        // Thanh bước (chỉ khi có từ 2 phần trở lên)
        var nav = null;
        if (sections.length > 1) {
            nav = document.createElement('div');
            nav.className = 'form-steps';
            sections.forEach(function (sec, i) {
                var a = document.createElement('a');
                a.href = '#' + sec.id;
                a.dataset.for = sec.id;
                a.innerHTML = '<span class="n"><span>' + (i + 1) + '</span></span><span class="t"></span>';
                a.querySelector('.t').textContent = tieuDe(sec) || ('Phần ' + (i + 1));
                a.addEventListener('click', function (e) {
                    e.preventDefault();
                    sec.scrollIntoView({ behavior: 'smooth', block: 'start' });
                });
                nav.appendChild(a);
            });
            form.insertBefore(nav, sections[0]);
        }

        // Đánh dấu phần đã điền đủ các ô bắt buộc
        function kiemTra() {
            if (!nav) return;
            sections.forEach(function (sec) {
                var a = nav.querySelector('a[data-for="' + sec.id + '"]');
                var labels = sec.querySelectorAll('.form-required');
                var du = labels.length > 0;
                labels.forEach(function (lb) {
                    var wrap = lb.closest('[class*="col-"]') || lb.parentElement;
                    var ctl = wrap && wrap.querySelector('input:not([type=hidden]):not([type=checkbox]), select, textarea');
                    if (ctl && !String(ctl.value || '').trim()) du = false;
                });
                a.classList.toggle('done', du);
            });
        }
        form.addEventListener('input', kiemTra);
        form.addEventListener('change', kiemTra);
        setTimeout(kiemTra, 400);

        // Phần đang xem
        if (nav && 'IntersectionObserver' in window) {
            var io = new IntersectionObserver(function (es) {
                es.forEach(function (e) {
                    if (e.isIntersecting) {
                        nav.querySelectorAll('a').forEach(function (a) { a.classList.toggle('current', a.dataset.for === e.target.id); });
                    }
                });
            }, { rootMargin: '-25% 0px -60% 0px' });
            sections.forEach(function (s) { io.observe(s); });
            var first = nav.querySelector('a');
            if (first) first.classList.add('current');
        }

        // Thanh nút dính đáy: khối chứa nút submit ở cuối form
        var bar = null;
        Array.prototype.forEach.call(form.querySelectorAll('button[type=submit]'), function (b) {
            var c = b.parentElement;
            while (c && c.parentElement !== form) c = c.parentElement;
            if (c && !c.classList.contains('form-section') && !c.classList.contains('modal') && !c.classList.contains('form-steps')) bar = c;
        });
        if (bar) {
            bar.classList.add('form-actionbar');
            if (form.querySelector('.form-required') && !bar.querySelector('.form-req-note')) {
                var n = document.createElement('span');
                n.className = 'form-req-note';
                n.innerHTML = '<span class="form-required">*</span> Ô bắt buộc';
                bar.appendChild(n);
            }
        }
    });
})();
