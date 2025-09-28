# Тестирование Task Completion Agent

## Простой тест

Давайте протестируем агента на простой задаче создания калькулятора.

### Запуск теста

```bash
# Запуск с подробным логированием
dotnet run --project CodingAgent.Core -- complete --verbose

# Или с LM Studio (если настроен)
dotnet run --project CodingAgent.Core -- complete --provider lmstudio --verbose
```

### Тестовая задача

**Задача:** "Создай простой класс Calculator с методами Add, Subtract, Multiply и Divide. Добавь базовые unit тесты."

**Ограничения:** "C#, .NET 9.0, следуй стандартам кодирования Microsoft"

### Ожидаемый результат

Агент должен:

1. **PLAN** - Создать план с подзадачами:
   - Создать класс Calculator
   - Реализовать методы
   - Создать тестовый проект
   - Написать unit тесты
   - Проверить компиляцию и тесты

2. **ACT** - Выполнить каждую подзадачу:
   - Создать файл Calculator.cs
   - Добавить методы с проверкой деления на ноль
   - Создать тестовый проект
   - Написать тесты для всех методов

3. **OBSERVE** - Проверить результаты:
   - `dotnet build` - успешная компиляция
   - `dotnet test` - все тесты проходят
   - `dotnet format` - код соответствует стилю

4. **CRITIQUE/REFLECT** - Анализировать и корректировать при необходимости

### Структура файлов после выполнения

```
/Users/dmitryafonchenko/repos/dotnet-local-agent/
├── Calculator.cs                    # Основной класс
├── Calculator.Tests/
│   ├── Calculator.Tests.csproj     # Тестовый проект
│   └── CalculatorTests.cs          # Unit тесты
└── ...
```

### Пример ожидаемого кода

**Calculator.cs:**
```csharp
namespace CodingAgent.Core;

public class Calculator
{
    public double Add(double a, double b) => a + b;
    
    public double Subtract(double a, double b) => a - b;
    
    public double Multiply(double a, double b) => a * b;
    
    public double Divide(double a, double b)
    {
        if (b == 0)
            throw new DivideByZeroException("Cannot divide by zero");
        return a / b;
    }
}
```

**CalculatorTests.cs:**
```csharp
using Xunit;

namespace Calculator.Tests;

public class CalculatorTests
{
    private readonly CodingAgent.Core.Calculator _calculator = new();

    [Fact]
    public void Add_ShouldReturnCorrectSum()
    {
        var result = _calculator.Add(2, 3);
        Assert.Equal(5, result);
    }

    [Fact]
    public void Divide_ByZero_ShouldThrowException()
    {
        Assert.Throws<DivideByZeroException>(() => _calculator.Divide(10, 0));
    }
    
    // ... другие тесты
}
```

## Проверка результата

После выполнения задачи проверьте:

```bash
# Компиляция
dotnet build

# Запуск тестов
dotnet test

# Проверка стиля
dotnet format --verify-no-changes
```

## Метрики успеха

- ✅ Задача выполнена полностью
- ✅ Код компилируется без ошибок
- ✅ Все тесты проходят
- ✅ Код соответствует стилю
- ✅ Агент завершил работу автоматически
- ✅ Количество итераций разумное (< 20)

## Возможные проблемы

1. **Ошибки компиляции** - Агент должен их исправить автоматически
2. **Падающие тесты** - Агент должен исправить логику
3. **Проблемы стиля** - Агент должен отформатировать код
4. **Зависимости** - Агент должен добавить нужные пакеты

## Расширенные тесты

После успешного базового теста можно попробовать более сложные задачи:

1. **Рефакторинг** - "Отрефактори существующий код в Program.cs"
2. **Добавление функций** - "Добавь логирование во все методы"
3. **Интеграция** - "Создай REST API для калькулятора"
4. **Оптимизация** - "Оптимизируй производительность кода"

## Отладка

Если что-то пошло не так:

1. Проверьте переменные окружения (ANTHROPIC_API_KEY)
2. Используйте `--verbose` для подробного лога
3. Проверьте доступность AI провайдера
4. Убедитесь, что .NET SDK установлен корректно