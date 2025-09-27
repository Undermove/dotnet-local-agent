# Быстрый старт - AI Coding Agent

## 1. Установка
```bash
cd csharp-version
dotnet restore
```

## 2. Настройка провайдера AI

### Вариант A: Anthropic Claude (облачный)
```bash
export ANTHROPIC_API_KEY="your-api-key-here"
```

### Вариант B: LM Studio (локальный)
1. Скачайте и установите [LM Studio](https://lmstudio.ai/)
2. Загрузите модель (например, Llama 3.1 или Code Llama)
3. Запустите локальный сервер в LM Studio
4. Никаких API ключей не требуется!

```bash
# Опционально: установить URL по умолчанию
export LM_STUDIO_URL="http://localhost:1234"
export AI_PROVIDER="lmstudio"
```

## 3. Запуск

### С Anthropic Claude
```bash
# Базовый чат
dotnet run --project CodingAgent.Core -- chat

# Агент с чтением файлов
dotnet run --project CodingAgent.Core -- read --provider anthropic

# Агент с редактированием
dotnet run --project CodingAgent.Core -- edit --verbose
```

### С LM Studio
```bash
# Базовый чат с LM Studio
dotnet run --project CodingAgent.Core -- chat --provider lmstudio

# Агент с чтением файлов
dotnet run --project CodingAgent.Core -- read --provider lmstudio

# Кастомный URL LM Studio
dotnet run --project CodingAgent.Core -- chat --provider lmstudio --base-url http://localhost:1234

# Полный список команд
dotnet run --project CodingAgent.Core
```

## 4. Открытие в IDE
Откройте `CodingAgent.sln` в Visual Studio, VS Code или Rider.

## Примеры использования

### Чтение файла
```
Пользователь: Прочитай файл README.md
Агент: [покажет содержимое файла]
```

### Редактирование
```
Пользователь: Добавь комментарий в начало файла Program.cs
Агент: [отредактирует файл]
```

### Поиск по коду
```
Пользователь: Найди все методы с названием "Main"
Агент: [найдет и покажет все вхождения]
```

## Преимущества LM Studio

✅ **Бесплатно** - никаких API ключей и оплаты  
✅ **Приватность** - все данные остаются локально  
✅ **Офлайн работа** - не требует интернета  
✅ **Кастомизация** - выбор из сотен моделей  
✅ **Скорость** - работает на вашем железе  

## Рекомендуемые модели для LM Studio

- **Llama 3.1 8B** - хороший баланс качества и скорости
- **Code Llama 7B** - специализирован для кода
- **Mistral 7B** - быстрый и эффективный
- **Qwen2.5-Coder** - отличный для программирования

Подробная документация в [README.md](README.md)