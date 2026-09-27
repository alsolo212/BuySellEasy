namespace Api.Services;

public static class SupportKnowledgeBase
{
    public static string? TryGetAnswer(string question)
    {
        var normalized = Normalize(question);

        if (normalized.Length == 0)
        {
            return null;
        }

        if (ContainsAny(normalized, "как называется сайт", "название сайта", "имя сайта", "что это за сайт"))
        {
            return "Вы находитесь на торговой площадке Buy Sell Easy, сокращённо BSE.";
        }

        if (ContainsAny(normalized, "кнопка more", "где more", "есть ли more", "кнопка больше"))
        {
            return "В интерфейсе Buy Sell Easy нет кнопки More. Для удаления своего объявления используйте Profile -> Your products -> Delete, затем подтвердите действие кнопкой Delete.";
        }

        if (ContainsAny(normalized, "удалить товар", "удалить объявление", "удаление товара", "удаление объявления", "как удалить") &&
            !ContainsAny(normalized, "аккаунт", "учётную запись", "профиль"))
        {
            return "Чтобы удалить своё объявление, откройте Profile -> Your products. Найдите нужный товар и нажмите кнопку Delete справа от него. В окне подтверждения снова нажмите Delete. Кнопка Edit предназначена только для редактирования и для удаления не используется.";
        }

        if (ContainsAny(normalized, "изменить товар", "редактировать товар", "изменить объявление", "редактировать объявление", "как изменить товар"))
        {
            return "Чтобы изменить своё объявление, откройте Profile -> Your products и нажмите кнопку Edit у нужного товара. После изменения полей нажмите Edit на странице редактирования.";
        }

        if (ContainsAny(normalized, "добавить товар", "добавить объявление", "создать товар", "создать объявление", "как разместить товар"))
        {
            return "Чтобы добавить объявление, откройте Profile -> Your products и нажмите + Add Product. Заполните фото, Title, Category, Description, Condition, Price, Contact price, Quantity и Location, затем нажмите Create.";
        }

        if (ContainsAny(normalized, "изменить профиль", "редактировать профиль", "сменить аватар", "изменить аватар", "изменить пароль", "изменить телефон"))
        {
            return "Чтобы изменить данные профиля, откройте Profile и нажмите Edit. На странице Edit Profile можно изменить Name, Email, Phone, Password и Avatar, затем сохранить изменения.";
        }

        if (ContainsAny(normalized, "открыть поддержку", "чат поддержки", "написать в поддержку", "связаться с поддержкой"))
        {
            return "Чтобы открыть чат поддержки, перейдите в Profile -> Chats и выберите чат Support. Сначала отвечает виртуальный помощник. Администратор подключается после явной просьбы пользователя.";
        }

        if (ContainsAny(normalized, "написать продавцу", "связаться с продавцом", "чат с продавцом", "как связаться с продавцом"))
        {
            return "Откройте страницу нужного товара и нажмите кнопку Write в разделе Contact. После этого откроется чат с продавцом.";
        }

        if (ContainsAny(normalized, "добавить в корзину", "положить в корзину", "как пользоваться корзиной"))
        {
            return "Откройте страницу товара и нажмите Add to Cart. Выбранные товары появятся в разделе Cart, где можно изменить количество или удалить товар.";
        }

        if (ContainsAny(normalized, "как купить", "купить товар", "оформить заказ", "сделать заказ"))
        {
            return "Чтобы купить товар, откройте его страницу и нажмите Buy либо добавьте его в Cart. На странице Checkout заполните контактные данные и нажмите Buy.";
        }

        if (ContainsAny(normalized, "где мои заказы", "посмотреть заказ", "отследить заказ", "как отследить"))
        {
            return "Покупатель может открыть Profile -> Orders и нажать Track у нужного заказа. Там отображаются адрес доставки и текущий статус заказа.";
        }

        if (ContainsAny(normalized, "получить рейтинг", "как получить рейтинг", "поставить рейтинг", "оставить отзыв", "как оставить отзыв"))
        {
            return "Рейтинг продавца появляется после отзывов покупателей. После завершённой покупки откройте Profile -> Orders, найдите заказ со статусом Bought и нажмите Review. Выберите оценку от 1 до 5, напишите комментарий и подтвердите кнопкой Review. Отзывы продавца видны в его профиле во вкладке Reviews.";
        }

        if (ContainsAny(normalized, "пожаловаться на товар", "пожаловаться на объявление", "report товар", "report объявление"))
        {
            return "Откройте страницу товара и нажмите Report в блоке с информацией о товаре. Укажите причину в поле Reason и подтвердите кнопкой Report.";
        }

        if (ContainsAny(normalized, "найти товар", "поиск товара", "фильтр товаров", "сортировка товаров"))
        {
            return "На странице Products используйте поле Search. Кнопки сортировки и фильтра позволяют выбрать порядок, Category, Condition и диапазон Price. Для применения настроек нажмите Apply, для сброса — Reset.";
        }

        return null;
    }

    private static string Normalize(string value)
    {
        return value
            .Trim()
            .ToLowerInvariant()
            .Replace('ё', 'е');
    }

    private static bool ContainsAny(string value, params string[] phrases)
    {
        return phrases.Any(value.Contains);
    }
}
