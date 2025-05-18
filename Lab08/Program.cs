using System;
using System.Threading;

namespace TPProj
{
    class Program
    {
        static void Main()
        {
            int numberOfChannels = 5;
            int totalRequests = 100;
            int clientSendInterval = 50;
            int serverProcessingTime = 500;

            Console.WriteLine($"Количество каналов: {numberOfChannels}");
            Console.WriteLine($"Всего заявок для симуляции: {totalRequests}");
            Console.WriteLine($"Интервал поступления заявок: {clientSendInterval}");
            Console.WriteLine($"Время обработки заявки: {serverProcessingTime}");

            Server server = new Server(numberOfChannels, serverProcessingTime);
            Client client = new Client(server);

            for (int id = 1; id <= 100; id++)
            {
                client.send(id);
                if (clientSendInterval > 0)
                {
                    Thread.Sleep(clientSendInterval);
                }
            }

            Thread.Sleep(1000);

            Console.WriteLine("Всего заявок: {0}", server.requestCount);
            Console.WriteLine("Обработано заявок: {0}", server.processedCount);
            Console.WriteLine("Отклонено заявок: {0}", server.rejectedCount);
        }
    }

    struct PoolRecord
    {
        public Thread thread;
        public bool in_use;
    }

    class Server
    {
        private PoolRecord[] pool;
        private object threadLock = new object();
        public int requestCount = 0;
        public int processedCount = 0;
        public int rejectedCount = 0;
        private int poolSize;
        private int processingTime;

        public Server(int n, int procTime)
        {
            poolSize = n;
            processingTime = procTime;
            pool = new PoolRecord[poolSize];
        }
        public void proc(object sender, procEventArgs e)
        {
            lock (threadLock)
            {
                // Console.WriteLine("Заявка с номером: {0}", e.id);
                requestCount++;
                for (int i = 0; i < poolSize; i++)
                {
                    if (!pool[i].in_use)
                    {
                        pool[i].in_use = true;
                        pool[i].thread = new Thread(new ParameterizedThreadStart(Answer));
                        pool[i].thread.Start(new Tuple<int, int, int>(e.id, i, processingTime));
                        processedCount++;
                        Console.WriteLine("Заявка {0} принята на обработку потоком {1}", e.id, i);
                        return;
                    }
                }
                rejectedCount++;
                Console.WriteLine("Заявка {0} отклонена, нет свободных потоков", e.id);
            }
        }

        public void Answer(object arg)
        {
            Tuple<int, int, int> data = (Tuple<int, int, int>)arg;
            int requestId = data.Item1;
            int poolIndex = data.Item2;
            int currentProcessingTime = data.Item3;

            Console.WriteLine("Поток {0} начал обработку заявки: {1} (время: {2} мс)", poolIndex, requestId, currentProcessingTime);
            if (currentProcessingTime > 0)
            {
                Thread.Sleep(currentProcessingTime);
            }
            Console.WriteLine("Поток {0} завершил обработку заявки: {1}", poolIndex, requestId);

            lock(threadLock)
            {
                pool[poolIndex].in_use = false;
                // Console.WriteLine("Поток {0} освободился", poolIndex);
            }
        }
    }

    class Client
    {
        private Server server;

        public Client(Server server)
        {
            this.server = server;
            this.request += server.proc;
        }

        public void send(int id)
        {
            procEventArgs args = new procEventArgs();
            args.id = id;
            OnProc(args);
        }

        protected virtual void OnProc(procEventArgs e)
        {
            EventHandler<procEventArgs> handler = request;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public event EventHandler<procEventArgs> request;
    }

    public class procEventArgs : EventArgs
    {
        public int id { get; set; }
    }
}
