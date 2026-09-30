namespace TaskManager.App.Services;

internal static partial class Translations
{
    /// <summary>Сообщения ядра и базы, которые доходят до пользователя (ошибки проверки, импорт, резервные копии).</summary>
    public static readonly string[][] Messages =
    [
        // Задача
        ["Дедлайн не может быть в прошлом.", "The deadline cannot be in the past.", "Die Frist darf nicht in der Vergangenheit liegen.", "Дедлайн не може бути в минулому."],
        ["Нельзя переоткрыть подзадачу, пока выполнена родительская задача. Сначала переоткройте родителя.", "A subtask cannot be reopened while its parent task is done. Reopen the parent first.", "Eine Unteraufgabe kann nicht wieder geöffnet werden, solange die übergeordnete Aufgabe erledigt ist. Öffnen Sie zuerst die übergeordnete Aufgabe.", "Не можна відкрити підзавдання знову, поки виконано батьківське завдання. Спершу відкрийте батьківське."],
        ["Задача не может быть подзадачей самой себя.", "A task cannot be a subtask of itself.", "Eine Aufgabe kann nicht ihre eigene Unteraufgabe sein.", "Завдання не може бути підзавданням самого себе."],
        ["Нельзя добавить подзадачу к удалённой задаче.", "A subtask cannot be added to a deleted task.", "Zu einer gelöschten Aufgabe kann keine Unteraufgabe hinzugefügt werden.", "Не можна додати підзавдання до видаленого завдання."],
        ["Нельзя добавить невыполненную подзадачу к выполненной задаче. Сначала переоткройте родителя.", "An unfinished subtask cannot be added to a done task. Reopen the parent first.", "Zu einer erledigten Aufgabe kann keine unerledigte Unteraufgabe hinzugefügt werden. Öffnen Sie zuerst die übergeordnete Aufgabe.", "Не можна додати невиконане підзавдання до виконаного завдання. Спершу відкрийте батьківське."],
        ["Неизвестный статус задачи.", "Unknown task status.", "Unbekannter Aufgabenstatus.", "Невідомий статус завдання."],
        ["У выполненной задачи нет даты выполнения.", "A done task has no completion date.", "Eine erledigte Aufgabe hat kein Erledigungsdatum.", "Виконане завдання не має дати виконання."],
        ["Задача уже удалена.", "The task is already deleted.", "Die Aufgabe ist bereits gelöscht.", "Завдання вже видалено."],
        ["Задача не удалена, восстанавливать нечего.", "The task is not deleted, there is nothing to restore.", "Die Aufgabe ist nicht gelöscht, es gibt nichts wiederherzustellen.", "Завдання не видалено, відновлювати нічого."],
        ["Для повторения по календарю нужен дедлайн.", "Repeating by calendar needs a deadline.", "Für die Wiederholung nach Kalender ist eine Frist nötig.", "Для повторення за календарем потрібен дедлайн."],
        ["Нельзя завершить задачу: не завершены подзадачи ({0}).", "The task cannot be completed: subtasks are not finished ({0}).", "Die Aufgabe kann nicht abgeschlossen werden: Unteraufgaben sind nicht erledigt ({0}).", "Не можна завершити завдання: не завершено підзавдання ({0})."],
        ["Удалённую задачу нельзя изменять. Сначала восстановите её.", "A deleted task cannot be changed. Restore it first.", "Eine gelöschte Aufgabe kann nicht geändert werden. Stellen Sie sie zuerst wieder her.", "Видалене завдання не можна змінювати. Спершу відновіть його."],
        ["Название задачи не может быть пустым.", "The task title cannot be empty.", "Der Aufgabentitel darf nicht leer sein.", "Назва завдання не може бути порожньою."],
        ["Название задачи не может быть длиннее {0} символов.", "The task title cannot be longer than {0} characters.", "Der Aufgabentitel darf nicht länger als {0} Zeichen sein.", "Назва завдання не може бути довшою за {0} символів."],
        ["Неизвестный приоритет.", "Unknown priority.", "Unbekannte Priorität.", "Невідомий пріоритет."],

        // Статусы
        ["Нельзя перевести задачу из «{0}» в «{1}»: {2}", "A task cannot go from “{0}” to “{1}”: {2}", "Eine Aufgabe kann nicht von „{0}“ auf „{1}“ wechseln: {2}", "Не можна перевести завдання з «{0}» у «{1}»: {2}"],
        ["задача уже в этом статусе.", "the task is already in this status.", "die Aufgabe hat diesen Status bereits.", "завдання вже в цьому статусі."],
        ["для этого используйте «переоткрыть».", "use “reopen” for that.", "verwenden Sie dafür „Wieder öffnen“.", "для цього використайте «відкрити знову»."],
        ["завершённую или отменённую задачу можно только переоткрыть.", "a done or cancelled task can only be reopened.", "eine erledigte oder abgebrochene Aufgabe kann nur wieder geöffnet werden.", "завершене або скасоване завдання можна лише відкрити знову."],
        ["такой переход не разрешён.", "this transition is not allowed.", "dieser Wechsel ist nicht erlaubt.", "такий перехід не дозволено."],
        ["переоткрыть можно только выполненную (Done) или отменённую (Cancelled) задачу.", "only a done or cancelled task can be reopened.", "nur eine erledigte oder abgebrochene Aufgabe kann wieder geöffnet werden.", "відкрити знову можна лише виконане або скасоване завдання."],

        // Повторение, теги
        ["Шаг повторения должен быть не меньше 1.", "The repeat step must be at least 1.", "Der Wiederholungsschritt muss mindestens 1 sein.", "Крок повторення має бути не менше 1."],
        ["Для еженедельного повторения выберите хотя бы один день недели.", "For a weekly repeat choose at least one weekday.", "Wählen Sie für die wöchentliche Wiederholung mindestens einen Wochentag.", "Для щотижневого повторення виберіть принаймні один день тижня."],
        ["Дни недели можно выбирать только для еженедельного повторения.", "Weekdays can only be chosen for a weekly repeat.", "Wochentage können nur bei wöchentlicher Wiederholung gewählt werden.", "Дні тижня можна вибирати лише для щотижневого повторення."],
        ["Неизвестная частота повторения.", "Unknown repeat frequency.", "Unbekannte Wiederholungshäufigkeit.", "Невідома частота повторення."],
        ["Для еженедельного повторения не выбран ни один день недели.", "No weekday is chosen for the weekly repeat.", "Für die wöchentliche Wiederholung ist kein Wochentag gewählt.", "Для щотижневого повторення не вибрано жодного дня тижня."],
        ["Название тега не может быть пустым.", "The tag name cannot be empty.", "Der Tag-Name darf nicht leer sein.", "Назва тегу не може бути порожньою."],
        ["Название тега не может быть длиннее {0} символов.", "The tag name cannot be longer than {0} characters.", "Der Tag-Name darf nicht länger als {0} Zeichen sein.", "Назва тегу не може бути довшою за {0} символів."],
        ["Тег не найден.", "Tag not found.", "Tag nicht gefunden.", "Тег не знайдено."],
        ["Тег «{0}» уже есть. Выберите другое название.", "The tag “{0}” already exists. Choose another name.", "Der Tag „{0}“ existiert bereits. Wählen Sie einen anderen Namen.", "Тег «{0}» уже є. Виберіть іншу назву."],
        ["Тег «{0}» используется в задачах, удалить его нельзя.", "The tag “{0}” is used by tasks and cannot be deleted.", "Der Tag „{0}“ wird in Aufgaben verwendet und kann nicht gelöscht werden.", "Тег «{0}» використовується в завданнях, видалити його не можна."],

        // Подзадачи и зависимости
        ["Нельзя сделать задачу подзадачей самой себя или своей подзадачи.", "A task cannot become a subtask of itself or of its own subtask.", "Eine Aufgabe kann nicht zur Unteraufgabe ihrer selbst oder ihrer eigenen Unteraufgabe werden.", "Завдання не може стати підзавданням самого себе чи власного підзавдання."],
        ["Нельзя восстановить подзадачу, пока удалена родительская задача. Сначала восстановите родителя.", "A subtask cannot be restored while its parent is deleted. Restore the parent first.", "Eine Unteraufgabe kann nicht wiederhergestellt werden, solange die übergeordnete Aufgabe gelöscht ist. Stellen Sie zuerst die übergeordnete Aufgabe wieder her.", "Не можна відновити підзавдання, поки видалено батьківське завдання. Спершу відновіть батьківське."],
        ["Нельзя восстановить невыполненную подзадачу выполненной задачи. Сначала переоткройте родителя.", "An unfinished subtask of a done task cannot be restored. Reopen the parent first.", "Eine unerledigte Unteraufgabe einer erledigten Aufgabe kann nicht wiederhergestellt werden. Öffnen Sie zuerst die übergeordnete Aufgabe.", "Не можна відновити невиконане підзавдання виконаного завдання. Спершу відкрийте батьківське знову."],
        ["Нельзя добавить зависимость выполненной или отменённой задаче.", "A dependency cannot be added to a done or cancelled task.", "Einer erledigten oder abgebrochenen Aufgabe kann keine Abhängigkeit hinzugefügt werden.", "Не можна додати залежність виконаному чи скасованому завданню."],
        ["Нельзя зависеть от отменённой задачи: она никогда не будет выполнена.", "A task cannot depend on a cancelled task: it will never be completed.", "Eine Aufgabe kann nicht von einer abgebrochenen Aufgabe abhängen: Sie wird nie erledigt.", "Не можна залежати від скасованого завдання: його ніколи не буде виконано."],
        ["Такая зависимость уже есть.", "This dependency already exists.", "Diese Abhängigkeit besteht bereits.", "Така залежність уже є."],
        ["Нельзя добавить зависимость: она создаёт цикл ({0}).", "The dependency cannot be added: it creates a cycle ({0}).", "Die Abhängigkeit kann nicht hinzugefügt werden: Sie erzeugt einen Zyklus ({0}).", "Не можна додати залежність: вона створює цикл ({0})."],
        ["Такой зависимости нет.", "There is no such dependency.", "Diese Abhängigkeit gibt es nicht.", "Такої залежності немає."],
        ["Нельзя перевести задачу в «{0}»: не выполнены зависимости: {1}", "The task cannot move to “{0}”: dependencies are not done: {1}", "Die Aufgabe kann nicht auf „{0}“ wechseln: Abhängigkeiten sind nicht erledigt: {1}", "Не можна перевести завдання в «{0}»: не виконано залежності: {1}"],
        ["Вложенность подзадач не может быть больше {0} уровней.", "Subtasks cannot be nested deeper than {0} levels.", "Unteraufgaben dürfen nicht tiefer als {0} Ebenen verschachtelt sein.", "Вкладеність підзавдань не може бути більшою за {0} рівнів."],

        // Запросы и параллельные изменения
        ["Номер страницы должен быть не меньше 1.", "The page number must be at least 1.", "Die Seitennummer muss mindestens 1 sein.", "Номер сторінки має бути не менше 1."],
        ["Размер страницы должен быть от 1 до {0}.", "The page size must be from 1 to {0}.", "Die Seitengröße muss zwischen 1 und {0} liegen.", "Розмір сторінки має бути від 1 до {0}."],
        ["Начало диапазона дедлайнов не может быть позже конца.", "The start of the deadline range cannot be after its end.", "Der Beginn des Fristbereichs darf nicht nach dessen Ende liegen.", "Початок діапазону дедлайнів не може бути пізніше за кінець."],
        ["Данные были изменены в другом месте. Обновите их и повторите действие.", "The data was changed elsewhere. Refresh it and try again.", "Die Daten wurden an anderer Stelle geändert. Aktualisieren Sie sie und versuchen Sie es erneut.", "Дані було змінено в іншому місці. Оновіть їх і повторіть дію."],

        // Импорт
        ["Файл не похож на экспорт задач: {0}", "The file does not look like a task export: {0}", "Die Datei sieht nicht wie ein Aufgabenexport aus: {0}", "Файл не схожий на експорт завдань: {0}"],
        ["В файле нет списка задач.", "The file has no task list.", "Die Datei enthält keine Aufgabenliste.", "У файлі немає списку завдань."],
        ["Версия формата {0} не поддерживается (поддерживается до {1}).", "Format version {0} is not supported (up to {1} is supported).", "Formatversion {0} wird nicht unterstützt (unterstützt bis {1}).", "Версія формату {0} не підтримується (підтримується до {1})."],
        ["Id {0} повторяется в файле.", "Id {0} appears more than once in the file.", "Die ID {0} kommt in der Datei mehrfach vor.", "Id {0} повторюється у файлі."],
        ["Удалённая задача не импортируется.", "A deleted task is not imported.", "Eine gelöschte Aufgabe wird nicht importiert.", "Видалене завдання не імпортується."],
        ["Вложенность больше {0} уровней.", "Nesting is deeper than {0} levels.", "Die Verschachtelung ist tiefer als {0} Ebenen.", "Вкладеність більша за {0} рівнів."],
        ["Родительская задача не импортирована из-за ошибки.", "The parent task was not imported because of an error.", "Die übergeordnete Aufgabe wurde wegen eines Fehlers nicht importiert.", "Батьківське завдання не імпортовано через помилку."],
        ["Родительская задача уже есть, подзадачи пропущены.", "The parent task already exists, subtasks were skipped.", "Die übergeordnete Aufgabe existiert bereits, Unteraufgaben wurden übersprungen.", "Батьківське завдання вже є, підзавдання пропущено."],
        ["Родительская задача пропущена.", "The parent task was skipped.", "Die übergeordnete Aufgabe wurde übersprungen.", "Батьківське завдання пропущено."],
        ["Такая задача уже есть (то же название и дедлайн).", "This task already exists (same title and deadline).", "Diese Aufgabe existiert bereits (gleicher Titel und Frist).", "Таке завдання вже є (та сама назва й дедлайн)."],
        ["Зависимость от задачи #{0} пропущена: её нет среди импортированных.", "The dependency on task #{0} was skipped: it is not among the imported ones.", "Die Abhängigkeit von Aufgabe #{0} wurde übersprungen: Sie ist nicht unter den importierten.", "Залежність від завдання #{0} пропущено: його немає серед імпортованих."],
        ["Зависимость от задачи #{0} пропущена: она создаёт цикл.", "The dependency on task #{0} was skipped: it creates a cycle.", "Die Abhängigkeit von Aufgabe #{0} wurde übersprungen: Sie erzeugt einen Zyklus.", "Залежність від завдання #{0} пропущено: вона створює цикл."],
        ["Родительская задача #{0} не найдена в файле.", "Parent task #{0} was not found in the file.", "Die übergeordnete Aufgabe #{0} wurde in der Datei nicht gefunden.", "Батьківське завдання #{0} не знайдено у файлі."],
        ["Иерархия подзадач зациклена.", "The subtask hierarchy loops.", "Die Hierarchie der Unteraufgaben ist zyklisch.", "Ієрархія підзавдань зациклена."],

        // Резервные копии
        ["Файл не найден.", "File not found.", "Datei nicht gefunden.", "Файл не знайдено."],
        ["Файл повреждён: проверка целостности базы не пройдена.", "The file is damaged: the database integrity check failed.", "Die Datei ist beschädigt: Die Integritätsprüfung der Datenbank ist fehlgeschlagen.", "Файл пошкоджено: перевірку цілісності бази не пройдено."],
        ["Это не резервная копия TaskManager.", "This is not a TaskManager backup.", "Dies ist keine TaskManager-Sicherung.", "Це не резервна копія TaskManager."],
        ["Копия создана более новой версией приложения и не может быть открыта этой версией.", "The backup was made by a newer version of the app and cannot be opened by this one.", "Die Sicherung wurde von einer neueren App-Version erstellt und kann mit dieser Version nicht geöffnet werden.", "Копію створено новішою версією застосунку, і цією версією її не можна відкрити."],
        ["Файл не является базой данных SQLite или повреждён.", "The file is not an SQLite database or is damaged.", "Die Datei ist keine SQLite-Datenbank oder ist beschädigt.", "Файл не є базою даних SQLite або пошкоджений."],
        ["Файл базы данных остался занят другим процессом.", "The database file is still in use by another process.", "Die Datenbankdatei wird noch von einem anderen Prozess verwendet.", "Файл бази даних залишився зайнятим іншим процесом."],
        ["Данные восстановлены из резервной копии.", "Data restored from the backup.", "Daten aus der Sicherung wiederhergestellt.", "Дані відновлено з резервної копії."],
        ["Данные восстановлены из резервной копии. Прежняя база сохранена: {0}", "Data restored from the backup. The previous database is saved: {0}", "Daten aus der Sicherung wiederhergestellt. Die bisherige Datenbank wurde gesichert: {0}", "Дані відновлено з резервної копії. Попередню базу збережено: {0}"],
        ["Не удалось восстановить данные из копии, приложение работает со старой базой. Файл копии оставлен: {0}", "The data could not be restored from the backup, the app keeps using the old database. The backup file was left in place: {0}", "Die Daten konnten nicht aus der Sicherung wiederhergestellt werden, die App arbeitet mit der alten Datenbank. Die Sicherungsdatei blieb erhalten: {0}", "Не вдалося відновити дані з копії, застосунок працює зі старою базою. Файл копії залишено: {0}"],

        // Экспорт в Markdown
        ["# Задачи\n\n", "# Tasks\n\n", "# Aufgaben\n\n", "# Завдання\n\n"],
        ["в работе", "in progress", "in Arbeit", "в роботі"],
        ["заблокирована", "blocked", "blockiert", "заблоковано"],
        ["отменена", "cancelled", "abgebrochen", "скасовано"],
    ];
}
