3) Настройка доступа (если плагин на другой машине)

Если PostgreSQL и Revit на одном ПК — можно пропустить.

3.1 postgresql.conf

Открой файл postgresql.conf и поставь:

listen_addresses = '*' (или конкретный IP)

3.2 pg_hba.conf

Добавь строку (пример для локалки):

host    revit_export    revit_exporter    192.168.0.0/16    md5

И оставь существующую для 127.0.0.1/32 (локально).

Перезапусти службу PostgreSQL.