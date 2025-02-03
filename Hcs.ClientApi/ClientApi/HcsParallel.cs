
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Hcs.ClientApi
{
    public class HcsParallel
    {
        /// <summary>
        /// Асинхронно обрабатывает все элементы @values типа @T методом @processor в параллельном режиме,
        /// используя максимум @maxThreads потоков.
        /// </summary>
        public static async Task ForEachAsync<T>(IEnumerable<T> values, Func<T, Task> processor, int maxThreads)
        {
            await Task.Run(() => ForEach(values, processor, maxThreads));
        }

        /// <summary>
        /// Обрабатывает все элементы @values типа @T методом @processor в параллельном режиме,
        /// используя максимум @maxThreads потоков.
        /// </summary>
        public static void ForEach<T>(IEnumerable<T> values, Func<T, Task> processor, int maxThreads)
        {
            var taskList = new List<Task>();
            var enumerator = values.GetEnumerator();

            int numTasksFinished = 0;
            while (true) {

                // наполняем массив ожидания следующими задачами
                while (taskList.Count < maxThreads) {
                    if (!enumerator.MoveNext()) break;

                    // запускаем новую задачу в отсоединенном потоке
                    Task newTask = Task.Run(() => processor(enumerator.Current));
                    taskList.Add(newTask);
                }

                // если массив ожидания пуст, работа окончена
                if (taskList.Count == 0) return;

                // ждем завершение любой задачи из массива ожидания
                int finishedIndex = Task.WaitAny(taskList.ToArray());
                var finishedTask = taskList[finishedIndex];
                numTasksFinished += 1;

                // удаляем задачу из массива ожидания чтобы более ее не ждать
                taskList.Remove(finishedTask);

                // если задача завершилась успешно уходим на добавление новой задачи
                if (!finishedTask.IsFaulted && 
                    !finishedTask.IsCanceled) continue;

                // задача завершилась аномально, ждем завершения других запущенных задач
                if (taskList.Count > 0) Task.WaitAll(taskList.ToArray());

                // составляем список всех возникших ошибок включая первую
                taskList.Insert(0, finishedTask);
                var errors = new List<Exception>();
                foreach (var task in taskList) {
                    if (task.IsFaulted) errors.Add(task.Exception);
                    if (task.IsCanceled) errors.Add(new Exception("Task was cancelled"));
                }

                // аномально завершаем обработку
                string message = 
                    $"Ошибка параллельной обработки №{numTasksFinished} из {values.Count()}" +
                    $" объектов типа {typeof(T).FullName}";
                throw new AggregateException(message, errors.ToArray());
            }
        }
    }
}
