document.addEventListener('DOMContentLoaded', function () {
    const dropArea = document.getElementById('dropArea');
    const fileInput = document.getElementById('photoUpload');
    const photoPreview = document.getElementById('photoPreview');

    // Проверяем существование элементов перед добавлением обработчиков
    if (!dropArea || !fileInput || !photoPreview) {
        console.error("One or more required elements not found");
        return;
    }

    const filesArray = []; // Хранит выбранные файлы

    // Функции для Drag & Drop
    function preventDefaults(e) {
        e.preventDefault();
        e.stopPropagation();
    }

    function highlight() {
        dropArea.classList.add('highlight');
    }

    function unhighlight() {
        dropArea.classList.remove('highlight');
    }

    function handleDrop(e) {
        const dt = e.dataTransfer;
        const files = dt.files;
        handleFiles(files);
    }

    function handleFiles(files) {
        if (!files || files.length === 0) return;

        // Добавляем новые файлы
        Array.from(files).forEach(file => {
            if (!file.type.match('image.*')) return;

            filesArray.push(file);
            const reader = new FileReader();

            reader.onload = function (e) {
                const previewItem = document.createElement('div');
                previewItem.className = 'preview-item';
                previewItem.innerHTML = `
                    <img src="${e.target.result}" alt="Preview" />
                    <button class="delete-photo-btn" type="button">🗙</button>
                `;
                photoPreview.appendChild(previewItem);

                // Обработчик удаления превью
                const removeBtn = previewItem.querySelector('.delete-photo-btn');
                removeBtn.addEventListener('click', (e) => {
                    e.stopPropagation();
                    const index = filesArray.indexOf(file);
                    if (index > -1) {
                        filesArray.splice(index, 1);
                    }
                    previewItem.remove();
                    updateFileInput();
                    toggleDropContentVisibility();
                });

                toggleDropContentVisibility();
                updateFileInput();
            };

            reader.readAsDataURL(file);
        });
    }

    function updateFileInput() {
        const dataTransfer = new DataTransfer();
        filesArray.forEach(file => dataTransfer.items.add(file));
        fileInput.files = dataTransfer.files;
    }

    function toggleDropContentVisibility() {
        if (filesArray.length > 0) {
            dropArea.classList.add('has-files');
        } else {
            dropArea.classList.remove('has-files');
        }
    }

    // Добавляем обработчики событий
    ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
        dropArea.addEventListener(eventName, preventDefaults, false);
    });

    ['dragenter', 'dragover'].forEach(eventName => {
        dropArea.addEventListener(eventName, highlight, false);
    });

    ['dragleave', 'drop'].forEach(eventName => {
        dropArea.addEventListener(eventName, unhighlight, false);
    });

    dropArea.addEventListener('drop', handleDrop, false);

    fileInput.addEventListener('change', function (e) {
        handleFiles(e.target.files);
    });

    // Обработчик для клика по всей drop-зоне (для открытия файлового диалога)
    dropArea.addEventListener('click', function (e) {
        if (e.target === dropArea || e.target.classList.contains('drop-content')) {
            fileInput.click();
        }
    });

    // Остальная логика для существующих фото (если нужно)
    const deleteImagesInput = document.getElementById('deleteImagesInput');
    const photoGrid = document.getElementById('photoGrid');

    if (photoGrid && deleteImagesInput) {
        const imagesToDelete = new Set();

        photoGrid.addEventListener('click', function (e) {
            if (e.target.classList.contains('delete-photo-btn')) {
                e.preventDefault();
                e.stopPropagation();

                const photoItem = e.target.closest('.photo-item');
                const imageId = photoItem.dataset.imageId;

                imagesToDelete.add(imageId);
                photoItem.remove();

                deleteImagesInput.value = Array.from(imagesToDelete).join(',');
            }
        });
    }
});