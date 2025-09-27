# Интеграция с LM Studio

## Что добавлено

Этот проект теперь поддерживает подключение к локальному LM Studio в дополнение к Anthropic Claude API.

## Новые файлы

1. **AIProvider.cs** - Абстракция для работы с разными AI провайдерами
   - `IAIProvider` - интерфейс провайдера
   - `AnthropicProvider` - реализация для Anthropic Claude
   - `LMStudioProvider` - реализация для LM Studio

2. **CommandLineArgs.cs** - Парсер аргументов командной строки
   - Поддержка выбора провайдера
   - Настройка модели и базового URL
   - Обработка переменных окружения

## Измененные файлы

1. **Program.cs** - Обновлена справка с новыми опциями
2. **Chat.cs** - Рефакторинг для использования абстракции провайдеров
3. **CodingAgent.Core.csproj** - Добавлена зависимость на OpenAI SDK
4. **README.md** - Обновлена документация с информацией о LM Studio
5. **QUICKSTART.md** - Добавлены примеры использования LM Studio

## Новые возможности

### Командная строка
```bash
# Выбор провайдера
--provider anthropic|lmstudio

# Настройка модели
--model <название_модели>

# Кастомный URL для LM Studio
--base-url <url>

# Подробный режим
--verbose
```

### Переменные окружения
```bash
# Провайдер по умолчанию
export AI_PROVIDER=lmstudio

# URL LM Studio по умолчанию
export LM_STUDIO_URL=http://localhost:1234

# API ключ для Anthropic (если используется)
export ANTHROPIC_API_KEY=your-key-here
```

## Примеры использования

### С LM Studio
```bash
# Базовый чат
dotnet run --project CodingAgent.Core -- chat --provider lmstudio

# Агент с чтением файлов
dotnet run --project CodingAgent.Core -- read --provider lmstudio

# Кастомный URL
dotnet run --project CodingAgent.Core -- chat --provider lmstudio --base-url http://localhost:1234
```

### С Anthropic Claude
```bash
# Базовый чат (по умолчанию)
dotnet run --project CodingAgent.Core -- chat

# Явное указание провайдера
dotnet run --project CodingAgent.Core -- chat --provider anthropic
```

## Технические детали

### Зависимости
- **OpenAI SDK 2.1.0** - для работы с LM Studio API
- **System.ClientModel** - для аутентификации API ключей
- **Anthropic.SDK** - для работы с Claude API (существующая)

### Архитектура
- Использует паттерн Strategy для выбора провайдера
- Единый интерфейс `IAIProvider` для всех провайдеров
- Автоматическое определение провайдера из переменных окружения
- Graceful fallback на Anthropic при отсутствии настроек

### Ограничения
- LM Studio провайдер не поддерживает tools/function calling
- Требует запущенный локальный сервер LM Studio
- Некоторые модели могут работать медленнее чем Claude

## Преимущества LM Studio

✅ **Бесплатно** - никаких API ключей  
✅ **Приватность** - данные не покидают ваш компьютер  
✅ **Офлайн** - работает без интернета  
✅ **Кастомизация** - выбор из сотен моделей  
✅ **Контроль** - полный контроль над параметрами  

## Рекомендуемые модели

- **Llama 3.1 8B** - универсальная модель
- **Code Llama 7B** - специализирована для кода
- **Qwen2.5-Coder** - отличная для программирования
- **DeepSeek Coder** - сильная в алгоритмах
- **Mistral 7B** - быстрая и эффективная

## Статус

✅ **Готово к использованию**
- Проект успешно компилируется
- Все провайдеры работают
- Документация обновлена
- Примеры протестированы