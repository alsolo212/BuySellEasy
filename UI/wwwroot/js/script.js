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
/*..........      ...........     ........... */


document.addEventListener('DOMContentLoaded', function () {
    const thumbnails = document.querySelectorAll('.thumbnail-item');
    const mainImage = document.getElementById('mainProductImage');
    const prevBtn = document.querySelector('.prev');
    const nextBtn = document.querySelector('.next');

    let currentIndex = 0;
    const images = Array.from(thumbnails).map(t => t.dataset.target);

    thumbnails.forEach((thumb, index) => {
        thumb.addEventListener('click', () => {
            mainImage.src = thumb.dataset.target;
            currentIndex = index;
        });
    });

    prevBtn?.addEventListener('click', () => {
        currentIndex = (currentIndex - 1 + images.length) % images.length;
        mainImage.src = images[currentIndex];
    });

    nextBtn?.addEventListener('click', () => {
        currentIndex = (currentIndex + 1) % images.length;
        mainImage.src = images[currentIndex];
    });

    document.addEventListener('keydown', (e) => {
        if (e.key === 'ArrowLeft') prevBtn?.click();
        if (e.key === 'ArrowRight') nextBtn?.click();
    });
});

/*FILTERS*/
document.addEventListener("DOMContentLoaded", () => {
    // 1. Инициализация выпадающих меню
    const initDropdown = (toggleSelector, menuSelector) => {
        const toggle = document.querySelector(toggleSelector);
        const menu = document.querySelector(menuSelector);
        if (!toggle || !menu) return;

        toggle.addEventListener("click", (e) => {
            e.stopPropagation();
            const isVisible = menu.style.display === "flex";
            closeAllMenus();
            menu.style.display = isVisible ? "none" : "flex";
        });

        menu.addEventListener("click", e => e.stopPropagation());
    };

    const closeAllMenus = () => {
        document.querySelectorAll('.dropdown-menu').forEach(menu => menu.style.display = "none");
    };

    initDropdown('.filter-toggle', '.dropdown-menu:first-of-type');
    initDropdown('.sort-toggle', '.dropdown-menu:last-of-type');
    document.addEventListener("click", closeAllMenus);

    // 2. Работа с фильтрами
    const form = document.querySelector('.search-form');
    if (!form) return;

    const searchInput = document.querySelector('[name="SearchTerm"]');
    const conditionInput = document.querySelector('[name="Condition"]');
    const minPriceInput = document.querySelector('[name="MinPrice"]');
    const maxPriceInput = document.querySelector('[name="MaxPrice"]');
    const sortByRadios = document.querySelectorAll('[name="SortBy"]');
    const categoryIdInput = document.querySelector('[name="CategoryId"]');

    // Применять фильтры только при первом заходе в сессию
    const firstLoad = !sessionStorage.getItem('filtersApplied');
    sessionStorage.setItem('filtersApplied', 'true');

    const saveFilters = () => {
        sessionStorage.setItem('productFilters', JSON.stringify({
            categoryId: categoryIdInput?.value || '',
            searchTerm: searchInput?.value || '',
            condition: conditionInput?.value || '',
            minPrice: minPriceInput?.value || '',
            maxPrice: maxPriceInput?.value || '',
            sortBy: document.querySelector('[name="SortBy"]:checked')?.value || 'recommended'
        }));
    };

    const restoreFilters = () => {
        const savedFilters = sessionStorage.getItem('productFilters');
        if (!savedFilters) return;

        const filters = JSON.parse(savedFilters);
        categoryIdInput.value = filters.categoryId || '';
        searchInput.value = filters.searchTerm || '';
        conditionInput.value = filters.condition || '';
        minPriceInput.value = filters.minPrice || '';
        maxPriceInput.value = filters.maxPrice || '';

        sortByRadios.forEach(radio => {
            radio.checked = radio.value === (filters.sortBy || 'recommended');
        });

        // Применяем фильтры только при первом заходе
        if (firstLoad && (filters.categoryId || filters.condition || filters.minPrice || filters.maxPrice || filters.sortBy !== 'recommended')) {
            setTimeout(() => form.submit(), 100);
        }
    };

    // Обработчики событий
    [conditionInput, minPriceInput, maxPriceInput, categoryIdInput, ...sortByRadios]
        .forEach(input => input?.addEventListener('change', saveFilters));

    searchInput?.addEventListener('input', saveFilters);
    form.addEventListener('submit', saveFilters);

    // Кнопка сброса фильтров (сохраняем только поиск)
    document.getElementById('resetFilters')?.addEventListener('click', (e) => {
        e.preventDefault();
        const searchValue = searchInput.value;

        document.querySelector('[name="CategoryId"]').value = '';
        conditionInput.value = '';
        minPriceInput.value = '';
        maxPriceInput.value = '';
        document.querySelector('[name="SortBy"][value="recommended"]').checked = true;

        sessionStorage.setItem('productFilters', JSON.stringify({ searchTerm: searchValue }));
        form.submit();
    });

    // Восстанавливаем фильтры при загрузке
    restoreFilters();
});