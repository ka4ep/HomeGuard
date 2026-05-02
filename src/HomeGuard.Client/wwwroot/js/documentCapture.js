// wwwroot/js/documentCapture.js
// ES module — подключается через JS.InvokeAsync("import", ...)

const MAX_BYTES = 20 * 1024 * 1024; // 20 MB
const ALLOWED   = new Set(['application/pdf', 'image/jpeg', 'image/png']);
const EXT_MAP   = { 'image/jpg': 'image/jpeg' }; // нормализация

let currentStream = null;

// ── Drop zone ────────────────────────────────────────────────────────────────

export function initDropZone(dropEl, inputEl, dotNet) {
    dropEl.addEventListener('dragover', e => {
        e.preventDefault();
        dropEl.classList.add('is-dragging');
    });

    dropEl.addEventListener('dragleave', e => {
        // Игнорируем события от дочерних элементов
        if (!dropEl.contains(e.relatedTarget))
            dropEl.classList.remove('is-dragging');
    });

    dropEl.addEventListener('drop', async e => {
        e.preventDefault();
        dropEl.classList.remove('is-dragging');
        const file = e.dataTransfer?.files?.[0];
        if (file) await processFile(file, dotNet);
    });

    dropEl.addEventListener('click', () => inputEl.click());

    inputEl.addEventListener('change', async () => {
        const file = inputEl.files?.[0];
        if (file) await processFile(file, dotNet);
        inputEl.value = ''; // сбросить, чтобы тот же файл можно было выбрать повторно
    });
}

async function processFile(file, dotNet) {
    const mime = EXT_MAP[file.type] ?? file.type;

    if (!ALLOWED.has(mime)) {
        await dotNet.invokeMethodAsync('OnFileError',
            `Неподдерживаемый формат: ${file.name}. Допускаются PDF, JPG, PNG.`);
        return;
    }

    if (file.size > MAX_BYTES) {
        await dotNet.invokeMethodAsync('OnFileError',
            `Файл слишком большой (${(file.size / 1024 / 1024).toFixed(1)} МБ). Максимум 20 МБ.`);
        return;
    }

    try {
        const base64 = await readBase64(file);
        await dotNet.invokeMethodAsync('OnFileReady', base64, mime, file.name);
    } catch {
        await dotNet.invokeMethodAsync('OnFileError', 'Не удалось прочитать файл.');
    }
}

function readBase64(file) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload  = () => resolve(reader.result.split(',')[1]);
        reader.onerror = () => reject();
        reader.readAsDataURL(file);
    });
}

// ── Camera ───────────────────────────────────────────────────────────────────

export async function startCamera(videoEl) {
    try {
        // Сначала пробуем заднюю камеру (на телефонах/ноутбуке без задней — упадёт в fallback)
        let stream;
        try {
            stream = await navigator.mediaDevices.getUserMedia({
                video: {
                    facingMode: { ideal: 'environment' },
                    width:  { ideal: 1920 },
                    height: { ideal: 1080 }
                }
            });
        } catch {
            // Fallback: любая доступная камера
            stream = await navigator.mediaDevices.getUserMedia({ video: true });
        }

        videoEl.srcObject = stream;
        currentStream = stream;

        // Ждём первого кадра — но не дольше 3 сек.
        // Некоторые виртуальные камеры (OBS и др.) не стреляют onloadedmetadata.
        await Promise.race([
            new Promise(resolve => { videoEl.onloadedmetadata = resolve; }),
            new Promise(resolve => setTimeout(resolve, 3000))
        ]);
        // play() может быть уже вызван браузером (autoplay), игнорируем ошибку
        try { await videoEl.play(); } catch { /* autoplay уже идёт */ }

        return true;
    } catch {
        return false;
    }
}

export function captureFrame(videoEl, canvasEl) {
    canvasEl.width  = videoEl.videoWidth;
    canvasEl.height = videoEl.videoHeight;
    canvasEl.getContext('2d').drawImage(videoEl, 0, 0);
    // Возвращаем полный data URL — компонент сам вырежет base64
    return canvasEl.toDataURL('image/jpeg', 0.85);
}

export function stopCamera() {
    currentStream?.getTracks().forEach(t => t.stop());
    currentStream = null;
}
