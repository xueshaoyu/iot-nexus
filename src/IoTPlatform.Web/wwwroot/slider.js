// 滑块数值气泡：悬停时显示鼠标位置对应的预计值（附当前值），拖动/键盘调整时在滑块上方显示正在设置的值。
// 纯前端计算，不经服务端往返；数值提交仍由 Blazor 在 change 事件（松开/键盘调整）中完成。
(function () {
    const THUMB = 20; // 与 app.css 中滑块直径一致

    const info = (range) => {
        const slider = range.closest('.slider');
        if (!slider) return null;
        const d = slider.dataset;
        return {
            slider,
            bubble: slider.querySelector('.slider-bubble'),
            min: parseFloat(d.min), max: parseFloat(d.max), step: parseFloat(d.step) || 1,
            decimals: parseInt(d.decimals || '0', 10), unit: d.unit || '',
            preview: d.preview || '', current: d.current || '',
        };
    };

    const fmt = (s, v) => v.toFixed(s.decimals);
    const snap = (s, v) => Math.min(s.max, Math.max(s.min, s.min + Math.round((v - s.min) / s.step) * s.step));

    const place = (s, range, ratio) => {
        const sr = s.slider.getBoundingClientRect();
        const rr = range.getBoundingClientRect();
        const x = rr.left - sr.left + THUMB / 2 + (rr.width - THUMB) * ratio;
        s.bubble.style.left = x + 'px';
    };

    // 数值与单位紧贴（39%），整体不换行
    const text = (s, value) => `<span class="num">${fmt(s, value)}${s.unit ? `<small>${s.unit}</small>` : ''}</span>`;

    // 悬停：鼠标位置 → 预计值
    const hover = (range, clientX) => {
        const s = info(range);
        if (!s || !s.bubble || range.disabled || !(s.max > s.min)) return;
        const rr = range.getBoundingClientRect();
        const ratio = Math.min(1, Math.max(0, (clientX - rr.left - THUMB / 2) / (rr.width - THUMB)));
        const value = snap(s, s.min + ratio * (s.max - s.min));
        const current = parseFloat(range.value);
        s.bubble.innerHTML = `<span class="pv"><span class="lbl">${s.preview}</span>${text(s, value)}</span>` +
            (Math.abs(value - current) > s.step / 2 ? `<span class="sep">·</span><span class="cur"><span class="lbl">${s.current}</span>${text(s, current)}</span>` : '');
        place(s, range, (value - s.min) / (s.max - s.min));
        s.slider.classList.add('show-bubble');
        s.slider.classList.remove('live');
    };

    // 拖动/键盘：跟随滑块显示正在设置的值
    const live = (range) => {
        const s = info(range);
        if (!s || !s.bubble || !(s.max > s.min)) return;
        const value = parseFloat(range.value);
        s.bubble.innerHTML = text(s, value);
        place(s, range, (value - s.min) / (s.max - s.min));
        s.slider.classList.add('show-bubble', 'live');
    };

    const hide = (range) => {
        const slider = range && range.closest('.slider');
        if (slider) slider.classList.remove('show-bubble', 'live');
    };

    let dragging = null;
    let hovered = null;

    document.addEventListener('pointerdown', (e) => {
        const range = e.target.closest && e.target.closest('.slider .range');
        if (!range) return;
        dragging = range;
        requestAnimationFrame(() => live(range));
    });

    document.addEventListener('pointerup', () => {
        if (!dragging) return;
        const range = dragging;
        dragging = null;
        if (hovered !== range) hide(range);
    });

    document.addEventListener('pointermove', (e) => {
        const range = e.target.closest && e.target.closest('.slider .range');
        if (hovered && hovered !== range && hovered !== dragging) hide(hovered);
        hovered = range;
        if (range && range !== dragging) hover(range, e.clientX);
    });

    document.addEventListener('input', (e) => {
        if (e.target.matches && e.target.matches('.slider .range')) live(e.target);
    });

    document.addEventListener('focusout', (e) => {
        if (e.target.matches && e.target.matches('.slider .range') && e.target !== hovered) hide(e.target);
    });
})();
