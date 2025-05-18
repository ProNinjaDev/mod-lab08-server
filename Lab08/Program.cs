using System;
using System.Threading;

namespace TPProj
{
    class Program
    {
        public static double Factorial(int number)
        {
            if (number < 0) return -1;
            if (number == 0) return 1;
            double result = 1;
            for (int i = 1; i <= number; i++)
            {
                result *= i;
            }
            return result;
        }
        public static double CalculateP0(double rho, int n)
        {
            if (rho < 0 || n < 0) return -1;

            double sum = 0;
            for (int i = 0; i <= n; i++)
            {
                sum += Math.Pow(rho, i) / Factorial(i);
            }
            if (sum == 0) return -1;
            return 1.0 / sum;
        }
        public static double CalculateFailureProbability(double rho, int n, double p0)
        {
             if (rho < 0 || n < 0 || p0 < 0) return -1;
            return (Math.Pow(rho, n) / Factorial(n)) * p0;
        }

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

            double lambda = 1000.0 / clientSendInterval;
            double mu = 1000.0 / serverProcessingTime;

            Console.WriteLine($"Lambda: {lambda}");
            Console.WriteLine($"Mu: {mu}");

            Server server = new Server(numberOfChannels, serverProcessingTime);
            Client client = new Client(server);

            for (int id = 1; id <= totalRequests; id++)
            {
                client.send(id);
                if (clientSendInterval > 0)
                {
                    Thread.Sleep(clientSendInterval);
                }
            }

            int finalWaitMs = 1000;
            Thread.Sleep(finalWaitMs);

            Console.WriteLine("Всего заявок: {0}", server.requestCount);
            Console.WriteLine("Обработано заявок: {0}", server.processedCount);
            Console.WriteLine("Отклонено заявок: {0}", server.rejectedCount);

            if (server.requestCount > 0)
            {
                double failureProbExp = (double)server.rejectedCount / server.requestCount;
                double relativeThroughputExp = (double)server.processedCount / server.requestCount;
                double absoluteThroughputExp = lambda * relativeThroughputExp; 
                double avgNumOccupiedChannelsExp = relativeThroughputExp * (lambda / mu); 

                Console.WriteLine($"Вероятность отказа: {failureProbExp:P4}");
                Console.WriteLine($"Относительная пропускная способность: {relativeThroughputExp:P4}");
                Console.WriteLine($"Абсолютная пропускная способность: {absoluteThroughputExp:F4} заявок/сек");
                Console.WriteLine($"Среднее число занятых каналов: {avgNumOccupiedChannelsExp:F4}");
            }
            else
            {
                Console.WriteLine("Заявок не было подано");
            }

            Console.WriteLine("\nТеоретические расчеты");
            if (mu > 0)
            {
                double rho = lambda / mu;
                Console.WriteLine($"Приведенная интенсивность потока: {rho:F4}");

                if (rho >= 0)
                {
                    double p0_teor = CalculateP0(rho, numberOfChannels);
                    if (p0_teor >= 0)
                    {
                        Console.WriteLine($"Вероятность простоя системы: {p0_teor:P4}");

                        double failureProbTeor = CalculateFailureProbability(rho, numberOfChannels, p0_teor);
                        Console.WriteLine($"Вероятность отказа: {failureProbTeor:P4}");

                        double relativeThroughputTeor = 1.0 - failureProbTeor;
                        Console.WriteLine($"Относительная пропускная способность: {relativeThroughputTeor:P4}");

                        double absoluteThroughputTeor = lambda * relativeThroughputTeor;
                        Console.WriteLine($"Абсолютная пропускная способность: {absoluteThroughputTeor:F4} заявок/сек");

                        double avgNumOccupiedChannelsTeor = rho * relativeThroughputTeor;
                        if (avgNumOccupiedChannelsTeor > numberOfChannels) avgNumOccupiedChannelsTeor = numberOfChannels * relativeThroughputTeor;
                        Console.WriteLine($"Среднее число занятых каналов: {avgNumOccupiedChannelsTeor:F4}");
                    }
                }
                else
                {
                     Console.WriteLine("Rho < 0");
                }
            }
            else
            {
                Console.WriteLine("Интенсивность обслуживания mu <= 0");
            }
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
