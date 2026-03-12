# Установка RVT PostgreSQL Exporter

## Шаг 1 — Создать переменную среды

Имя:
RVT_PG_ANIS_PASSWORD

Значение:
<ваш пароль PostgreSQL>

После создания переменной полностью перезапустить Revit.

---

## Шаг 2 — Настроить connections.json

Файл:
%APPDATA%\RvtPostgresExporter\connections.json

Убедиться, что имя базы совпадает с существующей в PostgreSQL.

---

## Шаг 3 — Настроить parameters.json

Файл:
%APPDATA%\RvtPostgresExporter\parameters.json

В нём задаются:
- таблица
- категории
- параметры Revit
- типы данных PostgreSQL

---

## Шаг 4 — Проверка

1. Запустить Revit
2. Открыть плагин
3. Нажать "Проверить подключение"
4. Статус должен быть OK
