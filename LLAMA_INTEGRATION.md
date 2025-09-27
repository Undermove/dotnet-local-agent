# Интеграция с Llama 3.1 8B Instruct

## 🎯 Что добавлено

Теперь проект поддерживает **полноценную работу с инструментами** через LM Studio с моделью Llama 3.1 8B Instruct!

## 🔧 Новые компоненты

### 1. UniversalAgentWithTools
- **Универсальный агент** который работает с любым AI провайдером
- **Поддержка function calling** для LM Studio
- **Автоматический системный промпт** для активации инструментов
- **Обработка tool calls** в формате OpenAI

### 2. Обновлённый LMStudioProvider
- **Поддержка tools** через OpenAI SDK
- **Правильная обработка** tool calls и responses
- **Детальное логирование** для отладки

### 3. ToolConverter
- **Конвертация** ToolDefinition в ChatTool
- **Правильная сериализация** параметров инструментов
- **Обработка ошибок** при конвертации

## 🚀 Как использовать

### Шаг 1: Настройка LM Studio

1. **Скачайте Llama 3.1 8B Instruct** в LM Studio
2. **Запустите модель** на порту 1234 (по умолчанию)
3. **Убедитесь**, что включена поддержка function calling

### Шаг 2: Запуск с инструментами

```bash
# Полнофункциональный агент с инструментами
dotnet run --project CodingAgent.Core -- edit --provider lmstudio --model "llama-3.1-8b-instruct" --verbose

# Или с кастомным URL
dotnet run --project CodingAgent.Core -- edit --provider lmstudio --base-url http://localhost:1234 --verbose
```

### Шаг 3: Тестирование инструментов

Попробуйте эти команды:

```
Создай файл test.txt с содержимым "Hello Llama!"
```

```
Прочитай содержимое файла README.md
```

```
Найди все файлы .cs в проекте
```

```
Выполни команду "ls -la"
```

## 🛠️ Доступные инструменты

### 1. edit_file
- **Создание** новых файлов
- **Редактирование** существующих файлов
- **Замена** текста в файлах

### 2. read_file
- **Чтение** содержимого файлов
- **Поддержка** любых текстовых форматов

### 3. list_files
- **Список файлов** в директории
- **Фильтрация** по расширениям
- **Рекурсивный поиск**

### 4. bash_command
- **Выполнение** shell команд
- **Получение** вывода команд
- **Обработка ошибок**

## 🎯 Системный промпт

Модель получает специальный системный промпт:

```
You are a helpful AI assistant with access to the following tools:
- edit_file: Make edits to a text file
- read_file: Read the contents of a file
- list_files: List files in a directory
- bash_command: Execute a bash command

CRITICAL INSTRUCTIONS:
1. You MUST use the available tools when the user requests actions
2. NEVER provide code examples when you can use a tool directly
3. When asked to create/read/edit files - USE THE TOOLS IMMEDIATELY
4. Always use tools first, then provide explanations if needed
```

## 📊 Сравнение провайдеров

| Функция | Anthropic Claude | LM Studio + Llama 3.1 |
|---------|------------------|------------------------|
| **Базовый чат** | ✅ | ✅ |
| **Function calling** | ✅ | ✅ |
| **Системный промпт** | ✅ | ✅ |
| **Работа с файлами** | ✅ | ✅ |
| **Bash команды** | ✅ | ✅ |
| **Стоимость** | 💰 Платно | 🆓 Бесплатно |
| **Приватность** | ☁️ Облако | 🔒 Локально |
| **Скорость** | ⚡ Быстро | 🐌 Зависит от железа |

## 🔍 Отладка

### Включите verbose режим:
```bash
dotnet run --project CodingAgent.Core -- edit --provider lmstudio --verbose
```

### Что вы увидите:
```
🤖 Sending 2 messages to LM Studio (llama-3.1-8b-instruct)...
📋 Sending 4 tools to model
✅ Added 4 tools to request
🔧 Received 1 tool calls from model
Tool call detected: edit_file with arguments: {"path":"test.txt","old_str":"","new_str":"Hello Llama!"}
```

## ⚠️ Важные замечания

### Требования к модели:
- **Llama 3.1 8B Instruct** или новее
- **Поддержка function calling** в LM Studio
- **Достаточно RAM** (минимум 8GB для модели)

### Альтернативные модели:
- **Qwen2.5-Coder 7B** - отлично для программирования
- **Hermes 2 Pro 7B** - специально обучена для function calling
- **Mistral 7B Instruct v0.3** - базовый чат без tools

## 🎉 Результат

Теперь у вас есть **полноценный AI агент** который:

✅ **Работает локально** без интернета  
✅ **Использует инструменты** как Claude  
✅ **Создаёт и редактирует файлы** реально  
✅ **Выполняет команды** в терминале  
✅ **Бесплатный** и **приватный**  

**Наслаждайтесь полной функциональностью с Llama 3.1!** 🦙✨